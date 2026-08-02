using System.Text.Json.Serialization;

namespace AirportApp.Dtos.AI;

public sealed class GenerateRecommendationRequest
{
    [JsonPropertyName("consulta")]
    public string? Consulta { get; init; }
}
