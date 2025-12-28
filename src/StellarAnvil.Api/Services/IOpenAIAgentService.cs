using StellarAnvil.Api.Models.OpenAI;

namespace StellarAnvil.Api.Services;

public interface IOpenAIAgentService
{
    IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default);
}

