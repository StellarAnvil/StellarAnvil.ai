using StellarAnvil.Api.Models.OpenAI;

namespace StellarAnvil.Api.Services;

public interface IOpenAIAgentService
{
    /// <summary>
    /// Streams a chat completion response (for user-facing responses)
    /// </summary>
    IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Streams a chat completion with a specific system prompt injected
    /// </summary>
    IAsyncEnumerable<ChatCompletionChunk> StreamWithSystemPromptAsync(
        ChatCompletionRequest request,
        string systemPrompt,
        CancellationToken cancellationToken = default);
}
