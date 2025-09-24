using StellarAnvil.Application.DTOs.OpenAI;

namespace StellarAnvil.Application.Services;

public interface IChatService
{
    Task<ChatCompletionResponse> ProcessChatCompletionAsync(ChatCompletionRequest request);
    IAsyncEnumerable<ChatCompletionChunk> ProcessChatCompletionStreamAsync(ChatCompletionRequest request);
    Task<ModelResponse> GetModelsAsync();
    Task<string?> ExtractUserNameFromPrompt(string prompt);
}
