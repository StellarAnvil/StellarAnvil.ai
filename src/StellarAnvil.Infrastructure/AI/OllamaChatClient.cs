using Microsoft.Extensions.AI;
using System.Text;
using System.Text.Json;

namespace StellarAnvil.Infrastructure.AI;

/// <summary>
/// Ollama chat client for local AI models
/// </summary>
public class OllamaChatClient : IChatClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaChatClient(HttpClient httpClient, string baseUrl, string model)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
    }

    public ChatClientMetadata Metadata => new($"ollama-{_model}");

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert to Ollama format
            var ollamaRequest = new
            {
                model = _model,
                prompt = ConvertMessagesToPrompt(chatMessages.ToList()),
                stream = false
            };

            var json = JsonSerializer.Serialize(ollamaRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                // Fallback to helpful message if Ollama is not available
                return new ChatResponse([new ChatMessage(ChatRole.Assistant, 
                    "I'm trying to use Ollama (local AI) but it seems to be unavailable. Please ensure Ollama is running on localhost:11434 with the DeepSeek R1 model.")]);
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var ollamaResponse = JsonSerializer.Deserialize<JsonElement>(responseJson);
            
            var responseText = ollamaResponse.GetProperty("response").GetString() ?? 
                "I'm a local AI assistant powered by Ollama.";

            return new ChatResponse([new ChatMessage(ChatRole.Assistant, responseText)]);
        }
        catch (Exception ex)
        {
            // Fallback if Ollama is not available
            return new ChatResponse([new ChatMessage(ChatRole.Assistant, 
                $"I'm trying to use Ollama (local AI) but encountered an error: {ex.Message}. Please install and run Ollama with DeepSeek R1 model.")]);
        }
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> chatMessages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Streaming not implemented for Ollama client");
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    public TService? GetService<TService>(object? key = null) where TService : class
    {
        return null;
    }

    public object? GetService(Type serviceType, object? key = null)
    {
        return null;
    }

    private static string ConvertMessagesToPrompt(IList<ChatMessage> messages)
    {
        var prompt = new StringBuilder();
        
        foreach (var message in messages)
        {
            var role = message.Role.Value switch
            {
                "system" => "System",
                "assistant" => "Assistant",
                _ => "User"
            };
            
            prompt.AppendLine($"{role}: {message.Text}");
        }
        
        prompt.AppendLine("Assistant:");
        return prompt.ToString();
    }
}
