using System.Text.Json.Serialization;

namespace StellarAnvil.Api.Application.DTOs;

public record ModelsResponse
{
    [JsonPropertyName("object")]
    public string Object { get; init; } = "list";

    [JsonPropertyName("data")]
    public required List<ModelInfo> Data { get; init; }
}

public record ModelInfo
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("object")]
    public string Object { get; init; } = "model";

    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; init; } = "openai";
}

