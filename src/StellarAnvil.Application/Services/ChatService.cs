using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using StellarAnvil.Application.DTOs;
using StellarAnvil.Application.DTOs.OpenAI;
using StellarAnvil.Domain.Services;
using StellarAnvil.Domain.Enums;
using StellarAnvil.Domain.Entities;
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

    public async IAsyncEnumerable<ChatCompletionChunk> ProcessChatCompletionAsync(ChatCompletionRequest request)
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

        // UNIFIED PROCESSING: Extract user, detect task, use SK + AutoGen
        await foreach (var chunk in ProcessUnifiedWorkflowAsync(request, chatId, created))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// UNIFIED workflow processing: SK function calling + AutoGen collaboration + streaming
    /// </summary>
    private async IAsyncEnumerable<ChatCompletionChunk> ProcessUnifiedWorkflowAsync(
        ChatCompletionRequest request, string chatId, long created)
    {
        IAsyncEnumerable<ChatCompletionChunk>? workflow = null;
        
        try
        {
            // Step 1: Extract user name from messages
            var userName = await ExtractUserNameFromMessages(request.Messages);
            if (string.IsNullOrEmpty(userName))
            {
                workflow = StreamMessage("I need to know who you are to help you. Please introduce yourself by saying 'I am [your name]'.", chatId, created, request.Model);
            }
            else
            {
                // Step 2: Get user from database
                var user = await _teamMemberService.GetTeamMemberByNameAsync(userName);
                if (user == null)
                {
                    workflow = StreamMessage($"Hey {userName}, I did not find you in my system, can you please request admin to add you?", chatId, created, request.Model);
                }
                else
                {
                    // Step 3: Check for existing task context in chat history
                    var existingTaskId = ExtractTaskNumberFromMessages(request.Messages);
                    
                    if (existingTaskId.HasValue)
                    {
                        // Continue existing task
                        workflow = ContinueExistingTaskWorkflowAsync(existingTaskId.Value, request, user, chatId, created);
                    }
                    else
                    {
                        // New task creation workflow
                        workflow = CreateNewTaskWorkflowAsync(request, user, chatId, created);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            workflow = StreamMessage($"Error processing request: {ex.Message}", chatId, created, request.Model);
        }

        if (workflow != null)
        {
            await foreach (var chunk in workflow)
            {
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Create new task using SK function calling + AutoGen collaboration
    /// </summary>
    private async IAsyncEnumerable<ChatCompletionChunk> CreateNewTaskWorkflowAsync(
        ChatCompletionRequest request, TeamMember user, string chatId, long created)
    {
        // Stream acknowledgment
        await foreach (var chunk in StreamMessage($"Hi {user.Name}! I'll help you with that task. Let me analyze your request and get our team working on it...", chatId, created, request.Model))
        {
            yield return chunk;
        }

        // Stream task creation progress
        await foreach (var chunk in StreamMessage("🔍 Analyzing your request and creating task...", chatId, created, request.Model))
        {
            yield return chunk;
        }

        IAsyncEnumerable<ChatCompletionChunk>? resultWorkflow = null;

        try
        {
            // Use Semantic Kernel with TaskManagementSkills to create task
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            
            // Add system message for task creation
            chatHistory.AddSystemMessage("You are an AI assistant that helps create and manage SDLC tasks. Use the CreateTaskAsync function when users request help with tasks.");
            
            // Add user messages to chat history
            foreach (var message in request.Messages)
            {
                var role = message.Role switch
                {
                    "system" => Microsoft.SemanticKernel.ChatCompletion.AuthorRole.System,
                    "user" => Microsoft.SemanticKernel.ChatCompletion.AuthorRole.User,
                    "assistant" => Microsoft.SemanticKernel.ChatCompletion.AuthorRole.Assistant,
                    _ => Microsoft.SemanticKernel.ChatCompletion.AuthorRole.User
                };
                chatHistory.AddMessage(role, message.Content ?? "");
            }

            // Enable automatic function calling for task creation
            var executionSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = Microsoft.SemanticKernel.Connectors.OpenAI.ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 4000,
                Temperature = 0.7
            };

            // Get chat completion service from kernel and process with SK
            var chatCompletionService = _kernel.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
            var result = await chatCompletionService.GetChatMessageContentsAsync(
                chatHistory, 
                executionSettings, 
                _kernel);
            
            var skResponse = result.LastOrDefault()?.Content ?? "";
            
            // Parse the task creation result from SK
            var taskCreationResult = ParseTaskCreationResult(skResponse);
            
            if (taskCreationResult.Success && taskCreationResult.TaskId.HasValue)
            {
                resultWorkflow = StreamTaskSuccessAndCollaboration(taskCreationResult, chatId, created, request.Model);
            }
            else
            {
                resultWorkflow = StreamMessage(taskCreationResult.Message ?? "I couldn't create a task from your request. Could you please be more specific about what you need help with?", chatId, created, request.Model);
            }
        }
        catch (Exception ex)
        {
            resultWorkflow = StreamMessage($"Error during task creation: {ex.Message}", chatId, created, request.Model);
        }

        if (resultWorkflow != null)
        {
            await foreach (var chunk in resultWorkflow)
            {
                yield return chunk;
            }
        }
    }

    private async IAsyncEnumerable<ChatCompletionChunk> StreamTaskSuccessAndCollaboration(
        TaskCreationResult taskResult, string chatId, long created, string model)
    {
        // Stream task creation success
        await foreach (var chunk in StreamMessage($"✅ Task #{taskResult.TaskId} created successfully!", chatId, created, model))
        {
            yield return chunk;
        }

        // Start AutoGen collaboration
        await foreach (var chunk in StreamAutoGenCollaboration(taskResult.TaskId!.Value, taskResult.Description, chatId, created, model))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Continue existing task workflow
    /// </summary>
    private async IAsyncEnumerable<ChatCompletionChunk> ContinueExistingTaskWorkflowAsync(
        int taskId, ChatCompletionRequest request, TeamMember user, string chatId, long created)
    {
        var task = await _taskApplicationService.GetByTaskNumberAsync(taskId);
        if (task == null)
        {
            await foreach (var chunk in StreamMessage($"Sorry, I couldn't find Task #{taskId}. It might have been completed or doesn't exist.", chatId, created, request.Model))
            {
                yield return chunk;
            }
            yield break;
        }

        await foreach (var chunk in StreamMessage($"Continuing work on Task #{taskId}: {task.Description}", chatId, created, request.Model))
        {
            yield return chunk;
        }

        // Continue with AutoGen collaboration for existing task
        await foreach (var chunk in StreamAutoGenCollaboration(task.Id, task.Description, chatId, created, request.Model))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Stream AutoGen multi-agent collaboration
    /// </summary>
    private async IAsyncEnumerable<ChatCompletionChunk> StreamAutoGenCollaboration(
        Guid taskId, string taskDescription, string chatId, long created, string model)
    {
        await foreach (var chunk in StreamMessage("👥 Starting team collaboration...", chatId, created, model))
        {
            yield return chunk;
        }

        IAsyncEnumerable<ChatCompletionChunk>? collaborationWorkflow = null;

        try
        {
            // Start AutoGen collaboration
            var collaborationResult = await _collaborationService.CollaborateAsync(
                taskId,
                TeamMemberRole.BusinessAnalyst, // Start with BA
                taskDescription,
                "Starting requirements analysis for this task...");

            if (collaborationResult.Success)
            {
                collaborationWorkflow = StreamCollaborationSuccess(collaborationResult, taskId, chatId, created, model);
            }
            else
            {
                collaborationWorkflow = StreamMessage($"❌ {collaborationResult.Message}", chatId, created, model);
            }
        }
        catch (Exception ex)
        {
            collaborationWorkflow = StreamMessage($"Error during collaboration: {ex.Message}", chatId, created, model);
        }

        if (collaborationWorkflow != null)
        {
            await foreach (var chunk in collaborationWorkflow)
            {
                yield return chunk;
            }
        }
    }

    private async IAsyncEnumerable<ChatCompletionChunk> StreamCollaborationSuccess(
        CollaborationResult collaborationResult, Guid taskId, string chatId, long created, string model)
    {
        // Stream the collaboration process
        await foreach (var chunk in StreamMessage($"👨‍💼 {collaborationResult.AssignedMember?.Name} (Junior BA): Starting analysis...", chatId, created, model))
        {
            yield return chunk;
        }

        // Stream collaboration messages
        foreach (var message in collaborationResult.CollaborationHistory)
        {
            var roleIcon = message.Role switch
            {
                "Junior" => "👨‍💼",
                "Senior Reviewer" => "👨‍💻",
                "Junior (Revision)" => "👨‍💼",
                _ => "💬"
            };
            
            await foreach (var chunk in StreamMessage($"{roleIcon} {message.Sender}: {message.Content}", chatId, created, model))
            {
                yield return chunk;
            }
        }

        // Stream final result
        await foreach (var chunk in StreamMessage($"✅ Task completed! Final result:\n\n{collaborationResult.FinalOutput}\n\n📋 Task #{taskId} - Ready for next phase.", chatId, created, model))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Stream a message word by word
    /// </summary>
    private async IAsyncEnumerable<ChatCompletionChunk> StreamMessage(string message, string chatId, long created, string model)
    {
        var words = message.Split(' ');
        
        foreach (var word in words)
        {
            yield return new ChatCompletionChunk
            {
                Id = chatId,
                Created = created,
                Model = model,
                Choices = new List<ChoiceDelta>
                {
                    new()
                    {
                        Index = 0,
                        Delta = new MessageDelta { Content = word + " " }
                    }
                }
            };
            
            await System.Threading.Tasks.Task.Delay(50); // Simulate real-time typing
        }
    }

    /// <summary>
    /// Parse task creation result from SK response
    /// </summary>
    private TaskCreationResult ParseTaskCreationResult(string skResponse)
    {
        try
        {
            // Look for JSON in the SK response
            var jsonMatch = Regex.Match(skResponse, @"\{.*\}", RegexOptions.Singleline);
            if (jsonMatch.Success)
            {
                var json = JsonSerializer.Deserialize<JsonElement>(jsonMatch.Value);
                
                var success = json.TryGetProperty("success", out var successProp) && successProp.GetBoolean();
                var message = json.TryGetProperty("message", out var messageProp) ? messageProp.GetString() : "";
                var taskIdStr = json.TryGetProperty("taskId", out var taskIdProp) ? taskIdProp.GetString() : null;
                
                Guid? taskId = null;
                if (Guid.TryParse(taskIdStr, out var parsedId))
                {
                    taskId = parsedId;
                }

                var description = json.TryGetProperty("description", out var descProp) ? descProp.GetString() : "";

                return new TaskCreationResult
                {
                    Success = success,
                    Message = message,
                    TaskId = taskId,
                    Description = description ?? ""
                };
            }
        }
        catch
        {
            // Fall through to default
        }

        return new TaskCreationResult
        {
            Success = false,
            Message = "Could not parse task creation result",
            TaskId = null,
            Description = ""
        };
    }

    /// <summary>
    /// Extract user name from chat messages
    /// </summary>
    private System.Threading.Tasks.Task<string?> ExtractUserNameFromMessages(IEnumerable<DTOs.OpenAI.ChatMessage> messages)
    {
        var userMessages = messages.Where(m => m.Role == "user").Select(m => m.Content).ToList();
        
        foreach (var message in userMessages)
        {
            if (string.IsNullOrEmpty(message)) continue;
            
            // Look for "I am [name]" pattern
            var match = Regex.Match(message, @"I\s+am\s+(\w+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return System.Threading.Tasks.Task.FromResult<string?>(match.Groups[1].Value);
            }
        }
        
        return System.Threading.Tasks.Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Extract task number from chat messages (for context tracking)
    /// </summary>
    private int? ExtractTaskNumberFromMessages(IEnumerable<DTOs.OpenAI.ChatMessage> messages)
    {
        var assistantMessages = messages.Where(m => m.Role == "assistant").Select(m => m.Content).ToList();
        
        foreach (var message in assistantMessages)
        {
            if (string.IsNullOrEmpty(message)) continue;
            
            // Look for "Task #123" pattern
            var match = Regex.Match(message, @"Task\s+#(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var taskNumber))
            {
                return taskNumber;
            }
        }
        
        return null;
    }

    // LEGACY METHODS - TO BE REMOVED AFTER VERIFICATION
    private async Task<ChatCompletionResponse> ProcessChatCompletionWithSemanticKernelAsync(ChatCompletionRequest request)
    {
        try
        {
            // Convert OpenAI messages to SK ChatHistory
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            
            foreach (var message in request.Messages)
            {
                var role = message.Role switch
                {
                    "system" => Microsoft.SemanticKernel.ChatCompletion.AuthorRole.System,
                    "user" => Microsoft.SemanticKernel.ChatCompletion.AuthorRole.User,
                    "assistant" => Microsoft.SemanticKernel.ChatCompletion.AuthorRole.Assistant,
                    _ => Microsoft.SemanticKernel.ChatCompletion.AuthorRole.User
                };
                chatHistory.AddMessage(role, message.Content ?? "");
            }

            // Enable automatic function calling
            var executionSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = Microsoft.SemanticKernel.Connectors.OpenAI.ToolCallBehavior.AutoInvokeKernelFunctions,
                MaxTokens = 4000,
                Temperature = 0.7
            };

            // Get chat completion service from kernel
            var chatCompletionService = _kernel.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
            
            // Process with SK - this will automatically call functions if needed
            var result = await chatCompletionService.GetChatMessageContentsAsync(
                chatHistory, 
                executionSettings, 
                _kernel);
            
            var content = result.LastOrDefault()?.Content ?? "";

            // Convert SK result back to OpenAI format
            return new ChatCompletionResponse
            {
                Id = $"chatcmpl-{Guid.NewGuid():N}",
                Object = "chat.completion",
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = request.Model,
                Choices = new List<Choice>
                {
                    new()
                    {
                        Index = 0,
                        Message = new DTOs.OpenAI.ChatMessage
                        {
                            Role = "assistant",
                            Content = content
                        },
                        FinishReason = "stop"
                    }
                },
                Usage = new Usage
                {
                    PromptTokens = request.Messages.Sum(m => m.Content?.Length ?? 0) / 4,
                    CompletionTokens = content.Length / 4,
                    TotalTokens = (request.Messages.Sum(m => m.Content?.Length ?? 0) + content.Length) / 4
                }
            };
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Error processing request: {ex.Message}");
        }
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
        var modelToUse = request.Model ?? (assignedMember.Type == Domain.Enums.TeamMemberType.AI ? assignedMember.Model : "Llama3.1:8B");
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
        var plannerModel = request.Model ?? "Llama3.1:8B";
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

/// <summary>
/// Result of task creation from Semantic Kernel
/// </summary>
public class TaskCreationResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Guid? TaskId { get; set; }
    public string Description { get; set; } = string.Empty;
}
