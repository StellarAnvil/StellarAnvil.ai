using System.Text.Json.Serialization;

namespace StellarAnvil.Application.DTOs.OpenAI;

public class ChatCompletionChunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = "chat.completion.chunk";

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<ChoiceDelta> Choices { get; set; } = new();

    [JsonPropertyName("usage")]
    public Usage? Usage { get; set; }
}

public class ChoiceDelta
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public MessageDelta Delta { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class MessageDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<ToolCallDelta>? ToolCalls { get; set; }
}

public class ToolCallDelta
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("function")]
    public FunctionCallDelta? Function { get; set; }
}

public class FunctionCallDelta
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}
