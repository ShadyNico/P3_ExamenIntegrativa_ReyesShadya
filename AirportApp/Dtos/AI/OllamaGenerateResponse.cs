using System.Text.Json.Serialization;

namespace AirportApp.Dtos.AI;

internal sealed class OllamaGenerateResponse
{
    [JsonPropertyName("response")]
    public string? Response { get; init; }
}
