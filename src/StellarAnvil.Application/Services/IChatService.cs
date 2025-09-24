using StellarAnvil.Application.DTOs.OpenAI;

namespace StellarAnvil.Application.Services;

public interface IChatService
{
    IAsyncEnumerable<ChatCompletionChunk> ProcessChatCompletionAsync(ChatCompletionRequest request);
    Task<ModelResponse> GetModelsAsync();
    Task<string?> ExtractUserNameFromPrompt(string prompt);
}
