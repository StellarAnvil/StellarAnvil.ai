using System.Runtime.CompilerServices;
using StellarAnvil.Api.Application.DTOs;
using StellarAnvil.Api.Application.Results;

namespace StellarAnvil.Api.Application.Streaming;

/// <summary>
/// Writes OpenAI-compatible streaming responses.
/// </summary>
public static class OpenAiStreamWriter
{
    /// <summary>
    /// Streams content first, then sends task ID marker as the final chunk.
    /// This ensures task ID appears once at the end, not scattered throughout.
    /// </summary>
    public static async IAsyncEnumerable<ChatCompletionChunk> StreamResponseWithTaskIdAsync(
        string content,
        string taskId,
        string model,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var completionId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        // Stream the main content
        const int chunkSize = 10;
        for (var i = 0; i < content.Length; i += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var chunk = content.Substring(i, Math.Min(chunkSize, content.Length - i));
            
            yield return new ChatCompletionChunk
            {
                Id = completionId,
                Created = created,
                Model = model,
                Choices =
                [
                    new ChunkChoice
                    {
                        Index = 0,
                        Delta = new ChatMessageDelta
                        {
                            Content = chunk
                        },
                        FinishReason = null
                    }
                ]
            };
            
            await Task.Delay(5, cancellationToken);
        }
        
        // Send task ID marker as a single final content chunk (before finish)
        var taskIdMarker = $"\n\n<!-- task:{taskId} -->";
        yield return new ChatCompletionChunk
        {
            Id = completionId,
            Created = created,
            Model = model,
            Choices =
            [
                new ChunkChoice
                {
                    Index = 0,
                    Delta = new ChatMessageDelta
                    {
                        Content = taskIdMarker
                    },
                    FinishReason = null
                }
            ]
        };
        
        // Final chunk with finish reason
        yield return new ChatCompletionChunk
        {
            Id = completionId,
            Created = created,
            Model = model,
            Choices =
            [
                new ChunkChoice
                {
                    Index = 0,
                    Delta = new ChatMessageDelta(),
                    FinishReason = "stop"
                }
            ]
        };
    }
    
    /// <summary>
    /// Streams tool calls to the client in OpenAI-compatible format.
    /// Emits tool_calls deltas and ends with finish_reason="tool_calls".
    /// Task ID is embedded in the first chunk's content for continuity tracking.
    /// </summary>
    public static async IAsyncEnumerable<ChatCompletionChunk> StreamToolCallsAsync(
        List<RequestedToolCall> toolCalls,
        string taskId,
        string model,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var completionId = $"chatcmpl-{Guid.NewGuid():N}";
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        // First chunk: role + task ID marker as content (so client can track continuity)
        // The task ID is in content so it persists in conversation history
        yield return new ChatCompletionChunk
        {
            Id = completionId,
            Created = created,
            Model = model,
            Choices =
            [
                new ChunkChoice
                {
                    Index = 0,
                    Delta = new ChatMessageDelta
                    {
                        Role = "assistant",
                        Content = $"<!-- task:{taskId} -->"
                    },
                    FinishReason = null
                }
            ]
        };
        
        // Stream each tool call - OpenAI format sends each tool call's parts
        for (var i = 0; i < toolCalls.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var tc = toolCalls[i];
            
            // First delta for this tool call: id, type, function name
            yield return new ChatCompletionChunk
            {
                Id = completionId,
                Created = created,
                Model = model,
                Choices =
                [
                    new ChunkChoice
                    {
                        Index = 0,
                        Delta = new ChatMessageDelta
                        {
                            ToolCalls =
                            [
                                new ToolCallDelta
                                {
                                    Index = i,
                                    Id = tc.CallId,
                                    Type = "function",
                                    Function = new FunctionCallDelta
                                    {
                                        Name = tc.FunctionName,
                                        Arguments = ""
                                    }
                                }
                            ]
                        },
                        FinishReason = null
                    }
                ]
            };
            
            // Stream arguments in chunks (like OpenAI does)
            var args = tc.Arguments;
            const int argChunkSize = 50;
            for (var j = 0; j < args.Length; j += argChunkSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var argChunk = args.Substring(j, Math.Min(argChunkSize, args.Length - j));
                
                yield return new ChatCompletionChunk
                {
                    Id = completionId,
                    Created = created,
                    Model = model,
                    Choices =
                    [
                        new ChunkChoice
                        {
                            Index = 0,
                            Delta = new ChatMessageDelta
                            {
                                ToolCalls =
                                [
                                    new ToolCallDelta
                                    {
                                        Index = i,
                                        Function = new FunctionCallDelta
                                        {
                                            Arguments = argChunk
                                        }
                                    }
                                ]
                            },
                            FinishReason = null
                        }
                    ]
                };
                
                await Task.Delay(1, cancellationToken);
            }
        }
        
        // Final chunk with finish_reason="tool_calls" - this is critical!
        // It tells the client "stop here and execute the tools"
        yield return new ChatCompletionChunk
        {
            Id = completionId,
            Created = created,
            Model = model,
            Choices =
            [
                new ChunkChoice
                {
                    Index = 0,
                    Delta = new ChatMessageDelta(),
                    FinishReason = "tool_calls"
                }
            ]
        };
    }
    
    /// <summary>
    /// Converts Application tool calls to transport DTOs.
    /// Used when storing tool calls in conversation history.
    /// </summary>
    public static List<ToolCall> ToToolCallDtos(List<RequestedToolCall> toolCalls)
    {
        return toolCalls.Select(tc => new ToolCall
        {
            Id = tc.CallId,
            Type = "function",
            Function = new FunctionCall
            {
                Name = tc.FunctionName,
                Arguments = tc.Arguments
            }
        }).ToList();
    }
}
