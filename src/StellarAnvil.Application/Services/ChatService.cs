using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using StellarAnvil.Application.DTOs;
using StellarAnvil.Application.DTOs.OpenAI;
using StellarAnvil.Domain.Services;
using StellarAnvil.Domain.Enums;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StellarAnvil.Application.Services;

public class ChatService : IChatService
{
    private readonly Kernel _kernel;
    private readonly ITeamMemberService _teamMemberService;
    private readonly IAIClientService _aiClientService;
    private readonly ITaskApplicationService _taskApplicationService;
    private readonly AutoGenCollaborationService _collaborationService;

    public ChatService(Kernel kernel, ITeamMemberService teamMemberService, IAIClientService aiClientService, ITaskApplicationService taskApplicationService, AutoGenCollaborationService collaborationService)
    {
        _kernel = kernel;
        _teamMemberService = teamMemberService;
        _aiClientService = aiClientService;
        _taskApplicationService = taskApplicationService;
        _collaborationService = collaborationService;
    }

    public async Task<ChatCompletionResponse> ProcessChatCompletionAsync(ChatCompletionRequest request)
    {
        // If streaming is requested, we shouldn't be here - but handle it gracefully
        if (request.Stream)
        {
            // Convert streaming to non-streaming response
            var chunks = new List<ChatCompletionChunk>();
            await foreach (var chunk in ProcessChatCompletionStreamAsync(request))
            {
                chunks.Add(chunk);
            }
            
            // Combine all chunks into a single response
            var combinedContent = string.Join("", chunks
                .SelectMany(c => c.Choices)
                .Where(c => c.Delta.Content != null)
                .Select(c => c.Delta.Content));
            
            var lastChunk = chunks.LastOrDefault();
            return new ChatCompletionResponse
            {
                Id = lastChunk?.Id ?? $"chatcmpl-{Guid.NewGuid():N}",
                Created = lastChunk?.Created ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = new List<Choice>
                {
                    new()
                    {
                        Index = 0,
                        Message = new DTOs.OpenAI.ChatMessage
                        {
                            Role = "assistant",
                            Content = combinedContent
                        },
                        FinishReason = "stop"
                    }
                },
                Usage = lastChunk?.Usage ?? new Usage()
            };
        }
        
        return await ProcessChatCompletionInternalAsync(request);
    }
    
    private async Task<ChatCompletionResponse> ProcessChatCompletionInternalAsync(ChatCompletionRequest request)
    {
        // Extract user name from the first user message
        var userName = await ExtractUserNameFromMessages(request.Messages);
        
        if (string.IsNullOrEmpty(userName))
        {
            return CreateErrorResponse("Hey, before I start, can you provide your name? Please say 'I am [your name]'");
        }

        // Get team member from database
        var teamMember = await _teamMemberService.GetTeamMemberByNameAsync(userName);
        if (teamMember == null)
        {
            return CreateErrorResponse($"Hey {userName}, I did not find you in my system. Can you please request admin to add you?");
        }

        // Check if this is continuing an existing task or creating a new one
        var taskNumber = ExtractTaskNumberFromHistory(request.Messages);
        
        if (taskNumber.HasValue)
        {
            // Continue existing task
            return await ContinueExistingTaskAsync(taskNumber.Value, request, teamMember);
        }
        else
        {
            // Create new task
            return await CreateNewTaskAsync(request, teamMember);
        }
    }

    private int? ExtractTaskNumberFromHistory(List<DTOs.OpenAI.ChatMessage> messages)
    {
        // Look for task numbers in assistant messages (e.g., "Task #123", "Working on Task #456")
        var assistantMessages = messages.Where(m => m.Role == "assistant").ToList();
        
        foreach (var message in assistantMessages)
        {
            var patterns = new[]
            {
                @"Task #(\d+)",
                @"task #(\d+)", 
                @"Task (\d+)",
                @"task (\d+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(message.Content, pattern, RegexOptions.IgnoreCase);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var taskNumber))
                {
                    return taskNumber;
                }
            }
        }

        return null;
    }

    private async Task<ChatCompletionResponse> ContinueExistingTaskAsync(int taskNumber, ChatCompletionRequest request, Domain.Entities.TeamMember teamMember)
    {
        // Find the existing task
        var task = await _taskApplicationService.GetByTaskNumberAsync(taskNumber);
        if (task == null)
        {
            return CreateErrorResponse($"Sorry, I couldn't find Task #{taskNumber}. It might have been completed or doesn't exist.");
        }

        // Get the assigned team member for this task
        var assignedMember = await _teamMemberService.GetByIdAsync(task.AssigneeId.Value);
        if (assignedMember == null)
        {
            return CreateErrorResponse($"Task #{taskNumber} doesn't have an assigned team member. Please contact admin.");
        }

        // Get AI client for the requested model (or use assigned member's model if AI)
        var modelToUse = request.Model ?? (assignedMember.Type == Domain.Enums.TeamMemberType.AI ? assignedMember.Model : "deepseek-r1");
        var chatClient = await _aiClientService.GetClientForModelAsync(modelToUse);

        // Get assigned member's system prompt with task context
        var systemPrompt = GetSystemPromptWithTaskContext(assignedMember, task);
        var aiMessages = new List<Microsoft.Extensions.AI.ChatMessage>();
        
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            aiMessages.Add(new Microsoft.Extensions.AI.ChatMessage(Microsoft.Extensions.AI.ChatRole.System, systemPrompt));
        }
        
        // Convert OpenAI messages to AI messages
        foreach (var msg in request.Messages)
        {
            var role = msg.Role switch
            {
                "system" => Microsoft.Extensions.AI.ChatRole.System,
                "assistant" => Microsoft.Extensions.AI.ChatRole.Assistant,
                _ => Microsoft.Extensions.AI.ChatRole.User
            };
            aiMessages.Add(new Microsoft.Extensions.AI.ChatMessage(role, msg.Content));
        }

        var response = await chatClient.GetResponseAsync(aiMessages);

        return new ChatCompletionResponse
        {
            Id = $"chatcmpl-{Guid.NewGuid():N}",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = modelToUse,
            Choices = new List<Choice>
            {
                new()
                {
                    Index = 0,
                    Message = new DTOs.OpenAI.ChatMessage
                    {
                        Role = "assistant",
                        Content = response.Messages?.FirstOrDefault()?.Text ?? ""
                    },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage
            {
                PromptTokens = EstimateTokens(string.Join(" ", aiMessages.Select(m => m.Text))),
                CompletionTokens = EstimateTokens(response.Messages?.FirstOrDefault()?.Text ?? ""),
                TotalTokens = 0
            }
        };
    }

    private async Task<ChatCompletionResponse> CreateNewTaskAsync(ChatCompletionRequest request, Domain.Entities.TeamMember teamMember)
    {
        // Use the requested model (or default) as the planner
        var plannerModel = request.Model ?? "deepseek-r1";
        var plannerClient = await _aiClientService.GetClientForModelAsync(plannerModel);

        // Get the latest user message for task analysis
        var userMessage = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? "";
        
        // Planner analyzes the request and creates task breakdown
        var plannerPrompt = $@"You are an AI task planner for a software development team. 

User {teamMember.Name} ({teamMember.Role}) has requested: ""{userMessage}""

Analyze this request and:
1. Determine if this is a task request or just conversation
2. If it's a task, create a task breakdown and select appropriate workflow
3. If it's conversation, respond helpfully

Available workflows:
- Simple SDLC: Small changes, bug fixes, minor features (PO -> BA -> Dev -> QA)
- Standard SDLC: Medium features without UI (PO -> BA -> Architect -> Dev -> QA)  
- Full SDLC: Complex features with UI/UX (PO -> BA -> Architect -> UX -> Dev -> QA)

If creating a task, respond in this format:
TASK_CREATION: {{
  ""description"": ""Clear task description"",
  ""workflow"": ""Simple SDLC"" or ""Standard SDLC"" or ""Full SDLC"",
  ""reasoning"": ""Why this workflow was chosen""
}}

If just conversation, respond normally and be helpful.";

        var plannerMessages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(Microsoft.Extensions.AI.ChatRole.System, plannerPrompt),
            new(Microsoft.Extensions.AI.ChatRole.User, userMessage)
        };

        var plannerResponse = await plannerClient.GetResponseAsync(plannerMessages);
        var plannerContent = plannerResponse.Messages?.FirstOrDefault()?.Text ?? "";

        // Check if planner decided to create a task
        if (plannerContent.Contains("TASK_CREATION:"))
        {
            return await ProcessTaskCreation(plannerContent, teamMember, plannerModel);
        }
        else
        {
            // Just a conversation - respond with planner's response
            return new ChatCompletionResponse
            {
                Id = $"chatcmpl-{Guid.NewGuid():N}",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = plannerModel,
                Choices = new List<Choice>
                {
                    new()
                    {
                        Index = 0,
                        Message = new DTOs.OpenAI.ChatMessage
                        {
                            Role = "assistant",
                            Content = plannerContent
                        },
                        FinishReason = "stop"
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = EstimateTokens(string.Join(" ", plannerMessages.Select(m => m.Text))),
                    CompletionTokens = EstimateTokens(plannerContent),
                    TotalTokens = 0
                }
            };
        }
    }

    private async Task<ChatCompletionResponse> ProcessTaskCreation(string plannerContent, Domain.Entities.TeamMember teamMember, string plannerModel)
    {
        try
        {
            // Parse the task creation response
            var jsonStart = plannerContent.IndexOf("{");
            var jsonEnd = plannerContent.LastIndexOf("}") + 1;
            var jsonContent = plannerContent.Substring(jsonStart, jsonEnd - jsonStart);
            
            var taskInfo = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            var description = taskInfo.GetProperty("description").GetString() ?? "";
            var workflowName = taskInfo.GetProperty("workflow").GetString() ?? "Simple SDLC";
            var reasoning = taskInfo.GetProperty("reasoning").GetString() ?? "";

            // Create the task
            var createTaskDto = new CreateTaskDto { Description = description };
            var createdTask = await _taskApplicationService.CreateAsync(createTaskDto);

            // Start AutoGen collaboration for the task
            var collaborationResult = await _collaborationService.CollaborateAsync(
                createdTask.Id, 
                TeamMemberRole.BusinessAnalyst, // Always start with BA
                description, 
                "Starting requirements analysis for this task...");

            if (!collaborationResult.Success)
            {
                return CreateErrorResponse(collaborationResult.Message);
            }

            // Build the complete streaming response showing the full collaboration
            var responseMessage = BuildCollaborationResponse(
                teamMember, createdTask, workflowName, reasoning, collaborationResult);

            return new ChatCompletionResponse
            {
                Id = $"chatcmpl-{Guid.NewGuid():N}",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = plannerModel,
                Choices = new List<Choice>
                {
                    new()
                    {
                        Index = 0,
                        Message = new DTOs.OpenAI.ChatMessage
                        {
                            Role = "assistant",
                            Content = responseMessage
                        },
                        FinishReason = "stop"
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = EstimateTokens(plannerContent),
                    CompletionTokens = EstimateTokens(responseMessage),
                    TotalTokens = 0
                }
            };
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Sorry, I had trouble processing your task request: {ex.Message}");
        }
    }

    private string BuildCollaborationResponse(
        Domain.Entities.TeamMember requester, 
        TaskDto task, 
        string workflowName, 
        string reasoning, 
        CollaborationResult collaboration)
    {
        var response = new StringBuilder();
        
        // Initial acknowledgment
        response.AppendLine($"Hey {requester.Name}! I got your task and here's the breakdown:");
        response.AppendLine();
        response.AppendLine($"**Task #{task.Id}**: {task.Description}");
        response.AppendLine($"**Workflow**: {workflowName}");
        response.AppendLine($"**Reasoning**: {reasoning}");
        response.AppendLine();
        response.AppendLine($"I've assigned **{collaboration.AssignedMember?.Name}** ({collaboration.AssignedMember?.Role} - {collaboration.AssignedMember?.Grade}) to work on this task.");
        
        if (collaboration.ReviewingMember != null)
        {
            response.AppendLine($"**{collaboration.ReviewingMember.Name}** ({collaboration.ReviewingMember.Role} - {collaboration.ReviewingMember.Grade}) will review the work.");
        }
        
        response.AppendLine();
        response.AppendLine("---");
        response.AppendLine();

        // Show the collaboration process
        if (collaboration.CollaborationHistory.Any())
        {
            response.AppendLine("**Team Collaboration:**");
            response.AppendLine();
            
            foreach (var message in collaboration.CollaborationHistory)
            {
                response.AppendLine($"**{message.Sender} ({message.Role})**:");
                response.AppendLine(message.Content);
                response.AppendLine();
            }
            
            response.AppendLine("---");
            response.AppendLine();
        }

        // Final deliverable
        response.AppendLine("**Final Requirements Analysis:**");
        response.AppendLine(collaboration.FinalOutput);
        response.AppendLine();
        response.AppendLine("Is this what you were looking for? If you need any changes or have questions, just let me know!");

        return response.ToString();
    }

    public async IAsyncEnumerable<ChatCompletionChunk> ProcessChatCompletionStreamAsync(ChatCompletionRequest request)
    {
        var chatId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        // First chunk - role
        yield return new ChatCompletionChunk
        {
            Id = chatId,
            Created = created,
            Model = request.Model,
            Choices = new List<ChoiceDelta>
            {
                new()
                {
                    Index = 0,
                    Delta = new MessageDelta { Role = "assistant" }
                }
            }
        };

        // Get the non-streaming response
        var fullResponse = await ProcessChatCompletionInternalAsync(request);
        var content = fullResponse.Choices.FirstOrDefault()?.Message?.Content ?? "";
        
        // Stream the content in chunks (simulating real-time typing)
        var words = content.Split(' ');
        var currentText = "";
        
        foreach (var word in words)
        {
            currentText += (currentText.Length > 0 ? " " : "") + word;
            
            yield return new ChatCompletionChunk
            {
                Id = chatId,
                Created = created,
                Model = request.Model,
                Choices = new List<ChoiceDelta>
                {
                    new()
                    {
                        Index = 0,
                        Delta = new MessageDelta { Content = (currentText.Length > 0 ? " " : "") + word }
                    }
                }
            };
            
            // Small delay to simulate real-time streaming
            await Task.Delay(50);
        }
        
        // Final chunk with finish reason
        yield return new ChatCompletionChunk
        {
            Id = chatId,
            Created = created,
            Model = request.Model,
            Choices = new List<ChoiceDelta>
            {
                new()
                {
                    Index = 0,
                    Delta = new MessageDelta(),
                    FinishReason = "stop"
                }
            },
            Usage = fullResponse.Usage
        };
    }

    private async Task<Domain.Entities.TeamMember?> AssignTaskToTeamMember(Guid taskId, string workflowName)
    {
        // Determine the first role based on workflow (always starts with requirements analysis)
        var firstRole = Domain.Enums.TeamMemberRole.BusinessAnalyst;

        // Find available team member: Junior -> Senior (never Lead)
        var availableMembers = await _teamMemberService.GetAvailableByRoleAsync(firstRole);
        
        var juniorMember = availableMembers.FirstOrDefault(m => m.Grade == Domain.Enums.TeamMemberGrade.Junior);
        if (juniorMember != null)
        {
            await _taskApplicationService.AssignTaskAsync(taskId, juniorMember.Id);
            return juniorMember;
        }

        var seniorMember = availableMembers.FirstOrDefault(m => m.Grade == Domain.Enums.TeamMemberGrade.Senior);
        if (seniorMember != null)
        {
            await _taskApplicationService.AssignTaskAsync(taskId, seniorMember.Id);
            return seniorMember;
        }

        return null; // No available team members
    }

    private string GetSystemPromptWithTaskContext(Domain.Entities.TeamMember teamMember, TaskDto task)
    {
        var basePrompt = !string.IsNullOrEmpty(teamMember.SystemPrompt)
            ? teamMember.SystemPrompt
            : GetDefaultSystemPrompt(teamMember.Role);

        // Add task context and task number requirement
        return $@"{basePrompt}

CURRENT TASK CONTEXT:
You are currently working on Task #{task.Id}: {task.Description}
Current State: {task.CurrentState}

IMPORTANT: Always mention ""Task #{task.Id}"" in your responses so the system can track the conversation context.

If you need clarification or have questions about this task, ask the user directly. When you complete your work on this task, clearly indicate completion and ask for approval to move to the next phase.";
    }

    public async Task<ModelResponse> GetModelsAsync()
    {
        var supportedModels = await _aiClientService.GetSupportedModelsAsync();
        
        return new ModelResponse
        {
            Data = supportedModels.Select(modelId => new Model 
            { 
                Id = modelId, 
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                OwnedBy = GetProviderFromModel(modelId)
            }).ToList()
        };
    }

    private static string GetProviderFromModel(string model)
    {
        if (model.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase))
            return "openai";
        if (model.StartsWith("claude-", StringComparison.OrdinalIgnoreCase))
            return "anthropic";
        if (model.StartsWith("gemini-", StringComparison.OrdinalIgnoreCase))
            return "google";
        return "ollama";
    }

    public async Task<string?> ExtractUserNameFromPrompt(string prompt)
    {
        return await ExtractUserNameFromMessages(new List<DTOs.OpenAI.ChatMessage>
        {
            new() { Role = "user", Content = prompt }
        });
    }

    private async Task<string?> ExtractUserNameFromMessages(List<DTOs.OpenAI.ChatMessage> messages)
    {
        var userMessages = messages.Where(m => m.Role == "user").ToList();
        
        foreach (var message in userMessages)
        {
            // Try regex patterns first
            var patterns = new[]
            {
                @"I am (\w+(?:\s+\w+)*)",
                @"My name is (\w+(?:\s+\w+)*)",
                @"This is (\w+(?:\s+\w+)*)",
                @"I'm (\w+(?:\s+\w+)*)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(message.Content, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }
        }

        return null;
    }


    private static string GetDefaultSystemPrompt(TeamMemberRole role)
    {
        var basePrompt = role switch
        {
            TeamMemberRole.ProductOwner => "You are a Product Owner AI. Prioritize features based on business value. Follow higher grade priority in conflicts. Only one assignment at a time.",
            TeamMemberRole.BusinessAnalyst => "You are a Business Analyst AI. Analyze requirements and work exclusively with Jira for task management. Follow higher grade priority in conflicts. Only one assignment at a time.",
            TeamMemberRole.Architect => "You are an Architect AI. Design system architecture and technical solutions. Follow higher grade priority in conflicts. Only one assignment at a time.",
            TeamMemberRole.UXDesigner => "You are a UX Designer AI. Focus on user experience and interface design. Follow higher grade priority in conflicts. Only one assignment at a time.",
            TeamMemberRole.Developer => "You are a Developer AI. Implement features and write code according to specifications. Follow higher grade priority in conflicts. Only one assignment at a time.",
            TeamMemberRole.QualityAssurance => "You are a Quality Assurance AI. Test applications and ensure quality standards. Follow higher grade priority in conflicts. Only one assignment at a time.",
            TeamMemberRole.SecurityReviewer => "You are a Security Reviewer AI. Analyze code and systems for security vulnerabilities. Follow higher grade priority in conflicts. Only one assignment at a time.",
            _ => "You are an AI assistant helping with software development tasks."
        };

        return $@"{basePrompt}

IMPORTANT: When working on tasks, always mention the task number in your responses (e.g., 'Working on Task #123...'). This helps track conversation context.";
    }

    private static ChatCompletionResponse CreateErrorResponse(string message)
    {
        return new ChatCompletionResponse
        {
            Id = $"chatcmpl-{Guid.NewGuid():N}",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = "stellar-anvil",
            Choices = new List<Choice>
            {
                new()
                {
                    Index = 0,
                    Message = new DTOs.OpenAI.ChatMessage
                    {
                        Role = "assistant",
                        Content = message
                    },
                    FinishReason = "stop"
                }
            }
        };
    }

    private static int EstimateTokens(string text)
    {
        // Simple token estimation (roughly 4 characters per token)
        return (text?.Length ?? 0) / 4;
    }
}
