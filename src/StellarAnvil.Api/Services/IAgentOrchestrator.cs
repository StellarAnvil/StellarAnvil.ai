using StellarAnvil.Api.Models.OpenAI;

namespace StellarAnvil.Api.Services;

public interface IAgentOrchestrator
{
    /// <summary>
    /// Processes a chat completion request through the multi-agent workflow.
    /// Handles fresh chats (creates new task) and continuations (resumes existing task).
    /// Returns a streaming response with the task ID appended.
    /// </summary>
    IAsyncEnumerable<ChatCompletionChunk> ProcessAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}

