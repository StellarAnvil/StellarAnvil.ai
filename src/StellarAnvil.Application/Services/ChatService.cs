using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using StellarAnvil.Application.DTOs.OpenAI;
using StellarAnvil.Domain.Services;
using StellarAnvil.Domain.Enums;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StellarAnvil.Application.Services;

public class ChatService : IChatService
{
    private readonly Kernel _kernel;
    private readonly IChatClient _chatClient;
    private readonly ITeamMemberService _teamMemberService;

    public ChatService(Kernel kernel, IChatClient chatClient, ITeamMemberService teamMemberService)
    {
        _kernel = kernel;
        _chatClient = chatClient;
        _teamMemberService = teamMemberService;
    }

    public async Task<ChatCompletionResponse> ProcessChatCompletionAsync(ChatCompletionRequest request)
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

        // Process the chat completion
        
        // Add system prompt based on team member role
        var systemPrompt = !string.IsNullOrEmpty(teamMember.SystemPrompt)
            ? teamMember.SystemPrompt
            : GetDefaultSystemPrompt(teamMember.Role);
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

        var response = await _chatClient.CompleteAsync(aiMessages);

        return new ChatCompletionResponse
        {
            Id = $"chatcmpl-{Guid.NewGuid():N}",
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
                        Content = response.Message.Text ?? ""
                    },
                    FinishReason = "stop"
                }
            },
            Usage = new Usage
            {
                PromptTokens = EstimateTokens(string.Join(" ", aiMessages.Select(m => m.Text))),
                CompletionTokens = EstimateTokens(response.Message.Text ?? ""),
                TotalTokens = 0
            }
        };
    }

    public async Task<ModelResponse> GetModelsAsync()
    {
        return new ModelResponse
        {
            Data = new List<Model>
            {
                new() { Id = "gpt-4", Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                new() { Id = "gpt-3.5-turbo", Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                new() { Id = "claude-3-sonnet", Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                new() { Id = "claude-3-haiku", Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                new() { Id = "gemini-pro", Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                new() { Id = "gemini-pro-vision", Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
            }
        };
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
        return role switch
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
