using System.Text.Json.Serialization;

namespace AirportApp.Dtos.AI;

public sealed class GenerateRecommendationResponse
{
    public GenerateRecommendationResponse(string respuesta, string modelo)
    {
        Respuesta = respuesta;
        Modelo = modelo;
    }

    [JsonPropertyName("respuesta")]
    public string Respuesta { get; }

    [JsonPropertyName("modelo")]
    public string Modelo { get; }
}
