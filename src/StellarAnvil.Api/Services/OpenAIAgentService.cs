using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using StellarAnvil.Api.Models.OpenAI;
using ChatMessage = StellarAnvil.Api.Models.OpenAI.ChatMessage;

namespace StellarAnvil.Api.Services;

public class OpenAIAgentService : IOpenAIAgentService
{
    private readonly ChatClient _chatClient;
    private readonly string _model;

    public OpenAIAgentService(IConfiguration configuration)
    {
        var apiKey = configuration["AI:OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("AI:OpenAI:ApiKey configuration is required");
        
        _model = configuration["AI:OpenAI:Model"] ?? "gpt-5-nano";
        
        var openAIClient = new OpenAIClient(apiKey);
        _chatClient = openAIClient.GetChatClient(_model);
    }

    public async IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = ConvertMessages(request.Messages);
        var options = CreateChatOptions(request);
        
        await foreach (var chunk in StreamCoreAsync(messages, options, request.Model ?? _model, cancellationToken))
        {
            yield return chunk;
        }
    }

    public async IAsyncEnumerable<ChatCompletionChunk> StreamWithSystemPromptAsync(
        ChatCompletionRequest request,
        string systemPrompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Inject system prompt at the beginning
        var messagesWithSystem = new List<ChatMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };
        messagesWithSystem.AddRange(request.Messages);
        
        var messages = ConvertMessages(messagesWithSystem);
        var options = CreateChatOptions(request);
        
        await foreach (var chunk in StreamCoreAsync(messages, options, request.Model ?? _model, cancellationToken))
        {
            yield return chunk;
        }
    }

    private async IAsyncEnumerable<ChatCompletionChunk> StreamCoreAsync(
        List<OpenAI.Chat.ChatMessage> messages,
        ChatCompletionOptions options,
        string model,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {

        var completionId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages, options, cancellationToken).WithCancellation(cancellationToken))
        {
            var chunk = new ChatCompletionChunk
            {
                Id = completionId,
                Created = created,
                Model = model,
                Choices = update.ContentUpdate.Select((content, index) => new ChunkChoice
                {
                    Index = index,
                    Delta = new ChatMessageDelta
                    {
                        Content = content.Text
                    },
                    FinishReason = MapFinishReason(update.FinishReason)
                }).ToList()
            };

            // If there's no content update but there's a finish reason, still emit a chunk
            if (chunk.Choices.Count == 0 && update.FinishReason != null)
            {
                chunk = chunk with
                {
                    Choices =
                    [
                        new ChunkChoice
                        {
                            Index = 0,
                            Delta = new ChatMessageDelta(),
                            FinishReason = MapFinishReason(update.FinishReason)
                        }
                    ]
                };
            }

            // Handle tool calls in streaming
            if (update.ToolCallUpdates?.Count > 0)
            {
                var toolCallDeltas = update.ToolCallUpdates.Select(tc => new ToolCallDelta
                {
                    Index = tc.Index,
                    Id = tc.ToolCallId,
                    Type = "function",
                    Function = new FunctionCallDelta
                    {
                        Name = tc.FunctionName,
                        Arguments = tc.FunctionArgumentsUpdate?.ToString()
                    }
                }).ToList();

                if (chunk.Choices.Count > 0)
                {
                    chunk = chunk with
                    {
                        Choices =
                        [
                            chunk.Choices[0] with
                            {
                                Delta = chunk.Choices[0].Delta with
                                {
                                    ToolCalls = toolCallDeltas
                                }
                            }
                        ]
                    };
                }
            }

            if (chunk.Choices.Count > 0)
            {
                yield return chunk;
            }
        }
    }

    private static List<OpenAI.Chat.ChatMessage> ConvertMessages(List<ChatMessage> messages)
    {
        var result = new List<OpenAI.Chat.ChatMessage>();

        foreach (var msg in messages)
        {
            OpenAI.Chat.ChatMessage chatMessage = msg.Role.ToLowerInvariant() switch
            {
                "system" => new SystemChatMessage(msg.Content ?? string.Empty),
                "user" => new UserChatMessage(msg.Content ?? string.Empty),
                "assistant" => CreateAssistantMessage(msg),
                "tool" => new ToolChatMessage(msg.ToolCallId ?? string.Empty, msg.Content ?? string.Empty),
                _ => new UserChatMessage(msg.Content ?? string.Empty)
            };

            result.Add(chatMessage);
        }

        return result;
    }

    private static AssistantChatMessage CreateAssistantMessage(ChatMessage msg)
    {
        var assistantMessage = new AssistantChatMessage(msg.Content ?? string.Empty);

        if (msg.ToolCalls != null)
        {
            foreach (var toolCall in msg.ToolCalls)
            {
                assistantMessage.ToolCalls.Add(
                    ChatToolCall.CreateFunctionToolCall(
                        toolCall.Id,
                        toolCall.Function.Name,
                        BinaryData.FromString(toolCall.Function.Arguments)));
            }
        }

        return assistantMessage;
    }

    private static ChatCompletionOptions CreateChatOptions(ChatCompletionRequest request)
    {
        var options = new ChatCompletionOptions();

        if (request.Temperature.HasValue)
            options.Temperature = (float)request.Temperature.Value;

        if (request.TopP.HasValue)
            options.TopP = (float)request.TopP.Value;

        if (request.MaxTokens.HasValue)
            options.MaxOutputTokenCount = request.MaxTokens.Value;

        if (request.User != null)
            options.EndUserId = request.User;

        if (request.Tools != null)
        {
            foreach (var tool in request.Tools)
            {
                var functionParams = tool.Function.Parameters != null
                    ? BinaryData.FromObjectAsJson(tool.Function.Parameters)
                    : null;

                options.Tools.Add(ChatTool.CreateFunctionTool(
                    tool.Function.Name,
                    tool.Function.Description,
                    functionParams));
            }
        }

        return options;
    }

    private static string? MapFinishReason(ChatFinishReason? finishReason)
    {
        if (finishReason == null) return null;

        return finishReason switch
        {
            ChatFinishReason.Stop => "stop",
            ChatFinishReason.Length => "length",
            ChatFinishReason.ToolCalls => "tool_calls",
            ChatFinishReason.ContentFilter => "content_filter",
            _ => "stop"
        };
    }
}

