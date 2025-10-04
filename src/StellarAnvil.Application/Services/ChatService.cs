using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using AutoGen.Core;
using Google.Cloud.AIPlatform.V1;
using StellarAnvil.Application.DTOs;
using StellarAnvil.Application.DTOs.OpenAI;
using StellarAnvil.Domain.Services;
using StellarAnvil.Domain.Enums;
using StellarAnvil.Domain.Entities;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;

namespace StellarAnvil.Application.Services;

public class ChatService : IChatService
{
    private readonly IKernelFactoryService _kernelFactory;
    private readonly IAutoGenGeminiService _autoGenGeminiService;
    private readonly ITeamMemberService _teamMemberService;
    private readonly IAIClientService _aiClientService;
    private readonly ITaskApplicationService _taskApplicationService;
    private readonly AutoGenCollaborationService _collaborationService;

    public ChatService(IKernelFactoryService kernelFactory, IAutoGenGeminiService autoGenGeminiService, ITeamMemberService teamMemberService, IAIClientService aiClientService, ITaskApplicationService taskApplicationService, AutoGenCollaborationService collaborationService)
    {
        _kernelFactory = kernelFactory;
        _autoGenGeminiService = autoGenGeminiService;
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
            var collaborationResult = await _collaborationService.CollaborateAsync(
                Guid.NewGuid(),
                TeamMemberRole.BusinessAnalyst,
                "Test Task",
                "This is a test collaboration for debugging purposes.");
            // Step 1: Extract user name from messages
            // var userName = await ExtractUserNameFromMessages(request.Messages);
            // if (string.IsNullOrEmpty(userName))
            // {
            //     workflow = StreamMessage("I need to know who you are to help you. Please introduce yourself by saying 'I am [your name]'.", chatId, created, request.Model);
            // }
            // else
            // {
            //     // Step 2: Get user from database
            //     var user = await _teamMemberService.GetTeamMemberByNameAsync(userName);
            //     if (user == null)
            //     {
            //         workflow = StreamMessage($"Hey {userName}, I did not find you in my system, can you please request admin to add you?", chatId, created, request.Model);
            //     }
            //     else
            //     {
            //         // Step 3: Check for existing task context in chat history
            //         var existingTaskId = ExtractTaskNumberFromMessages(request.Messages);
                    
            //         if (existingTaskId.HasValue)
            //         {
            //             // Continue existing task
            //             workflow = ContinueExistingTaskWorkflowAsync(existingTaskId.Value, request, user, chatId, created);
            //         }
            //         else
            //         {
            //             // New task creation workflow
            //             workflow = CreateNewTaskWorkflowAsync(request, user, chatId, created);
            //         }
            //     }
            // }
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
            // Extract user name from the request
            var userName = await ExtractUserNameFromMessages(request.Messages);
            if (string.IsNullOrEmpty(userName))
            {
                userName = "Unknown User";
            }

            // Step 1: Use default Semantic Kernel model for task creation and assignment, but with AutoGen for the specific Gemini call
            var defaultKernel = await _kernelFactory.CreateKernelForModelAsync("gemini-2.5-pro");
            
            var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            
            // Add system message for task creation
            chatHistory.AddSystemMessage($@"You are an AI assistant that helps create and manage SDLC tasks.

CRITICAL: For ANY user request that involves work, tasks, or projects, you MUST create task FIRST using CreateTaskAsync function.

User: {userName}

When the user says things like:
- ""I need help with X""
- ""Can you help me with Y""
- ""I want to create Z""
- ""Help me build something""

Do NOT provide direct solutions. Always create a task first using CreateTaskAsync function.");
            
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

            // Use AutoGen Gemini instead of Semantic Kernel for the execution
            // Convert OpenAI tools to Google Cloud AI Platform tools if available
            Google.Cloud.AIPlatform.V1.Tool[]? geminiTools = null;
            if (request.Tools != null && request.Tools.Any())
            {
                var toolList = new List<Google.Cloud.AIPlatform.V1.Tool>();
                foreach (var tool in request.Tools)
                {
                    if (tool.Type == "function" && tool.Function != null)
                    {
                        var functionDeclaration = new Google.Cloud.AIPlatform.V1.FunctionDeclaration
                        {
                            Name = tool.Function.Name,
                            Description = tool.Function.Description ?? "",
                        };

                        // Convert OpenAI parameters to Google Cloud AI Platform schema
                        if (tool.Function.Parameters != null)
                        {
                            try
                            {
                                var parametersJson = JsonSerializer.Serialize(tool.Function.Parameters);
                                var openApiSchema = new Google.Cloud.AIPlatform.V1.OpenApiSchema();
                                
                                // Parse the JSON schema and convert to OpenApiSchema
                                using var document = JsonDocument.Parse(parametersJson);
                                if (document.RootElement.TryGetProperty("type", out var typeElement))
                                {
                                    openApiSchema.Type = Google.Cloud.AIPlatform.V1.Type.Object;
                                }
                                
                                // For now, set a basic schema - this could be enhanced to fully parse the JSON schema
                                functionDeclaration.Parameters = openApiSchema;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to convert parameters for function {tool.Function.Name}: {ex.Message}");
                            }
                        }

                        var geminiTool = new Google.Cloud.AIPlatform.V1.Tool
                        {
                            FunctionDeclarations = { functionDeclaration }
                        };
                        toolList.Add(geminiTool);
                    }
                }
                geminiTools = toolList.ToArray();
            }
            
            var geminiAgent = await _autoGenGeminiService.CreateGeminiAgentAsync("gemini-2.0-flash-exp", null, geminiTools);
            var autoGenMessages = new List<IMessage<Content>>();
            foreach (var msg in chatHistory)
            {
                string roleStr;
                if (msg.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.System)
                    roleStr = "system";
                else if (msg.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.Assistant)
                    roleStr = "model"; // Gemini uses "model" for assistant role
                else
                    roleStr = "user";
                
                // Create Google Cloud Content for Gemini
                var content = new Content
                {
                    Role = roleStr,
                    Parts = { new Part { Text = msg.Content ?? "" } }
                };
                
                // Create IMessage<Content> using MessageEnvelope
                var message = MessageEnvelope.Create(content, from: roleStr);
                autoGenMessages.Add(message);
            }
            
            var response = await _autoGenGeminiService.SendConversationAsync(geminiAgent, autoGenMessages);
            var responseContent = response.GetContent() ?? "";
            
            Console.WriteLine($"AutoGen Gemini Response: {responseContent}");
            
            // Check if function was called successfully by looking at the response content
            // This works for all providers (OpenAI, Gemini, Claude, Ollama)
            bool functionWasCalled = responseContent.Contains("task number is") || 
                                     responseContent.Contains("Task #") || 
                                     responseContent.Contains("assigned to") ||
                                     System.Text.RegularExpressions.Regex.IsMatch(responseContent, @"[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}");
            
            if (functionWasCalled)
            {
                // Function successfully called - extract task info from the natural response
                // Try multiple GUID extraction patterns to handle different response formats
                
                Guid taskGuid = Guid.Empty;
                bool guidFound = false;
                
                // Pattern 1: "Task #guid" format
                var taskGuidMatch = System.Text.RegularExpressions.Regex.Match(responseContent, @"Task #([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");
                if (taskGuidMatch.Success && Guid.TryParse(taskGuidMatch.Groups[1].Value, out taskGuid))
                {
                    guidFound = true;
                }
                
                // Pattern 2: "task number is guid" format (for Gemini responses)
                if (!guidFound)
                {
                    var taskNumberMatch = System.Text.RegularExpressions.Regex.Match(responseContent, @"task number is ([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");
                    if (taskNumberMatch.Success && Guid.TryParse(taskNumberMatch.Groups[1].Value, out taskGuid))
                    {
                        guidFound = true;
                    }
                }
                
                // Pattern 3: Any GUID in the response
                if (!guidFound)
                {
                    var anyGuidMatch = System.Text.RegularExpressions.Regex.Match(responseContent, @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");
                    if (anyGuidMatch.Success && Guid.TryParse(anyGuidMatch.Groups[1].Value, out taskGuid))
                    {
                        guidFound = true;
                    }
                }
                
                if (guidFound)
                {
                    // Stream the SK response first, then continue with collaboration
                    // Convert GUID to int for the existing AutoGen workflow (use GetHashCode for consistency)
                    var taskNumber = Math.Abs(taskGuid.GetHashCode());
                    Console.WriteLine($"Extracted task GUID: {taskGuid}, converted to task number: {taskNumber}");
                    resultWorkflow = StreamTaskSuccessAndAutogenCollaboration(taskNumber, responseContent, chatId, created, request.Model);
                }
                else
                {
                    // Try to extract task ID from JSON response if present in the AutoGen response
                    try
                    {
                        var jsonMatch = System.Text.RegularExpressions.Regex.Match(responseContent, @"\{.*""taskId"".*\}");
                        if (jsonMatch.Success)
                        {
                            using var document = JsonDocument.Parse(jsonMatch.Value);
                            if (document.RootElement.TryGetProperty("taskId", out var taskIdElement))
                            {
                                var taskIdString = taskIdElement.GetString();
                                if (Guid.TryParse(taskIdString, out Guid taskId))
                                {
                                    var taskNumber = Math.Abs(taskId.GetHashCode());
                                    Console.WriteLine($"Extracted task GUID from JSON: {taskId}, converted to task number: {taskNumber}");
                                    resultWorkflow = StreamTaskSuccessAndAutogenCollaboration(taskNumber, responseContent, chatId, created, request.Model);
                                }
                                else
                                {
                                    Console.WriteLine("Failed to parse GUID from JSON taskId");
                                    resultWorkflow = StreamMessage(responseContent, chatId, created, request.Model);
                                }
                            }
                            else
                            {
                                Console.WriteLine("No taskId property found in JSON");
                                resultWorkflow = StreamMessage(responseContent, chatId, created, request.Model);
                            }
                        }
                        else
                        {
                            // AutoGen called function but we couldn't extract task ID - stream the response anyway
                            Console.WriteLine("No JSON found in response, streaming original message");
                            resultWorkflow = StreamMessage(responseContent, chatId, created, request.Model);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing task ID from JSON: {ex.Message}");
                        resultWorkflow = StreamMessage(responseContent, chatId, created, request.Model);
                    }
                }
            }
            else
            {
                // No function was called - just stream the AutoGen response
                resultWorkflow = StreamMessage(responseContent, chatId, created, request.Model);
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

    private async IAsyncEnumerable<ChatCompletionChunk> StreamTaskSuccessAndAutogenCollaboration(
        int taskNumber, string responseContent, string chatId, long created, string model)
    {
        // Stream the SK response (which includes task creation confirmation)
        await foreach (var chunk in StreamMessage(responseContent, chatId, created, model))
        {
            yield return chunk;
        }

        // Get the task details to start AutoGen collaboration
        var task = await _taskApplicationService.GetByTaskNumberAsync(taskNumber);
        if (task != null)
        {
            // Start AutoGen collaboration
            await foreach (var chunk in StreamAutoGenCollaboration(task.Id, task.Description, chatId, created, model))
            {
                yield return chunk;
            }
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
        if (task.AssigneeId == null)
        {
            return CreateErrorResponse($"Task #{taskNumber} doesn't have an assigned team member. Please contact admin.");
        }
        
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
            Model = modelToUse ?? "unknown",
            Choices = new List<Choice>
            {
                new()
                {
                    Index = 0,
                    Delta = new DTOs.OpenAI.ChatMessage
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
                        Delta = new DTOs.OpenAI.ChatMessage
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
                        Delta = new DTOs.OpenAI.ChatMessage
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
            Data = supportedModels.Select(modelId => new DTOs.OpenAI.Model 
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
                    Delta = new DTOs.OpenAI.ChatMessage
                    {
                        Role = "assistant",
                        Content = message
                    },
                    FinishReason = "stop"
                }
            }
        };
    }

    public async IAsyncEnumerable<ChatCompletionChunk> ProcessChatWithFunctionCallsAsync(ChatCompletionRequest request)
    {
        // Convert request messages to AutoGen format
        var autoGenMessages = new List<IMessage<Content>>();
        foreach (var msg in request.Messages)
        {
            string roleStr = msg.Role switch
            {
                "system" => "system",
                "assistant" => "model", // Gemini uses "model" for assistant role
                _ => "user"
            };

            // Create Google Cloud Content for Gemini
            var geminiContent = new Content
            {
                Role = roleStr,
                Parts = { new Part { Text = msg.Content ?? "" } }
            };

            // Create IMessage<Content> using MessageEnvelope
            var message = MessageEnvelope.Create(geminiContent, from: roleStr);
            autoGenMessages.Add(message);
        }
        
        autoGenMessages = autoGenMessages.SkipWhile(m => m.From == "system").ToList(); // Skip initial system messages for Gemini

        // Convert OpenAI tools to Google Cloud AI Platform tools if available
        Google.Cloud.AIPlatform.V1.Tool[]? geminiTools = null;
        if (request.Tools != null && request.Tools.Any())
        {
            var toolList = new List<Google.Cloud.AIPlatform.V1.Tool>();
            foreach (var tool in request.Tools)
            {
                if (tool.Type == "function" && tool.Function != null)
                {
                    var functionDeclaration = new Google.Cloud.AIPlatform.V1.FunctionDeclaration
                    {
                        Name = tool.Function.Name,
                        Description = tool.Function.Description ?? "",
                    };

                    // Convert OpenAI parameters to Google Cloud AI Platform schema
                    if (tool.Function.Parameters != null)
                    {
                        try
                        {
                            var parametersJson = JsonSerializer.Serialize(tool.Function.Parameters);
                            Console.WriteLine($"Original parameters JSON for {tool.Function.Name}: {parametersJson}");
                            
                            using var document = JsonDocument.Parse(parametersJson);
                            
                            var openApiSchema = new Google.Cloud.AIPlatform.V1.OpenApiSchema
                            {
                                Type = Google.Cloud.AIPlatform.V1.Type.Object
                            };
                            
                            // Parse properties if they exist
                            if (document.RootElement.TryGetProperty("properties", out var propertiesElement))
                            {
                                foreach (var property in propertiesElement.EnumerateObject())
                                {
                                    var propertySchema = new Google.Cloud.AIPlatform.V1.OpenApiSchema
                                    {
                                        Type = Google.Cloud.AIPlatform.V1.Type.String // Default to string, Gemini seems to want this
                                    };
                                    
                                    if (property.Value.TryGetProperty("description", out var descElement))
                                    {
                                        propertySchema.Description = descElement.GetString() ?? "";
                                    }
                                    
                                    openApiSchema.Properties.Add(property.Name, propertySchema);
                                }
                            }
                            
                            // Parse required fields if they exist
                            if (document.RootElement.TryGetProperty("required", out var requiredElement))
                            {
                                foreach (var requiredField in requiredElement.EnumerateArray())
                                {
                                    var fieldName = requiredField.GetString();
                                    if (!string.IsNullOrEmpty(fieldName))
                                    {
                                        openApiSchema.Required.Add(fieldName);
                                    }
                                }
                            }
                            
                            Console.WriteLine($"Converted OpenApiSchema - Properties: {openApiSchema.Properties.Count}, Required: {openApiSchema.Required.Count}");
                            functionDeclaration.Parameters = openApiSchema;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to convert parameters for function {tool.Function.Name}: {ex.Message}");
                        }
                    }

                    var geminiTool = new Google.Cloud.AIPlatform.V1.Tool
                    {
                        FunctionDeclarations = { functionDeclaration }
                    };
                    toolList.Add(geminiTool);
                }
            }
            geminiTools = toolList.ToArray();
        }

        // Create AutoGen Gemini agent with tools
        var geminiAgent = await _autoGenGeminiService.CreateGeminiAgentAsync(request.Model ?? "gemini-2.0-flash-exp", null, geminiTools);

        // Get response from AutoGen
        var result = await _autoGenGeminiService.SendConversationAsync(geminiAgent, autoGenMessages);
        var getToolCalls = result.GetToolCalls();
        ChoiceDelta? choiceDelta = null;
        bool isToolCall = getToolCalls != null && getToolCalls.Any();
        if (getToolCalls != null && getToolCalls.Any())
        {
            choiceDelta = new ChoiceDelta
            {
                Index = 0,
                Delta = new DTOs.OpenAI.MessageDelta
                {
                    Role = "assistant",
                    ToolCalls = getToolCalls.Select(tc => new DTOs.OpenAI.ToolCallDelta
                    {
                        Id = tc.ToolCallId ?? Guid.NewGuid().ToString(),
                        Type = "function",
                        Function = new DTOs.OpenAI.FunctionCallDelta
                        {
                            Name = tc.FunctionName,
                            Arguments = tc.FunctionArguments
                        }
                    }).ToList(),
                    Content = null
                }
            };
        }
        else
        {
            choiceDelta = new ChoiceDelta
            {
                Index = 0,
                Delta = new DTOs.OpenAI.MessageDelta
                {
                    Role = "assistant",
                    Content = result.GetContent() ?? ""
                }
            };
        }

        var chatId = $"chatcmpl-{Guid.NewGuid():N}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var modelName = request.Model ?? "gemini-2.0-pro";

        yield return new ChatCompletionChunk
        {
            Id = $"chatcmpl-{Guid.NewGuid():N}",
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = request.Model ?? "gemini-2.0-pro",
            Choices = new List<ChoiceDelta>
            {
                choiceDelta
            },
        };

        // Send the final chunk with finish_reason
        yield return new ChatCompletionChunk
        {
            Id = chatId,
            Object = "chat.completion.chunk",
            Created = timestamp,
            Model = modelName,
            Choices =
            [
                new ChoiceDelta
                {
                    Index = 0,
                    Delta = new MessageDelta
                    {
                        Content = "" // Empty content
                    },
                    FinishReason = isToolCall ? "tool_calls" : "stop"
                }
            ]
        };

        // await foreach (var message in result)
        // {
        //     var content = (message as IMessage)?.GetContent() ?? "";
        //     yield return new ChatCompletionChunk
        //     {
        //         Id = $"chatcmpl-{Guid.NewGuid():N}",
        //         Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        //         Model = request.Model ?? "gemini-2.0-flash-exp",
        //         Choices = new List<ChoiceDelta>
        //         {
        //             new()
        //             {
        //                 Index = 0,
        //                 Delta = new MessageDelta { Content = content + " " }
        //             }
        //         },

        //     };
        //     await System.Threading.Tasks.Task.Delay(50); // Simulate real-time typing
        // }
    }

    private static int EstimateTokens(string text)
    {
        // Simple token estimation (roughly 4 characters per token)
        return (text?.Length ?? 0) / 4;
    }
}

