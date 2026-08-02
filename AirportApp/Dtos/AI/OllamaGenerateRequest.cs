using System.Text.Json.Serialization;

namespace AirportApp.Dtos.AI;

internal sealed class OllamaGenerateRequest
{
    public OllamaGenerateRequest(string model, string prompt)
    {
        Model = model;
        Prompt = prompt;
    }

    [JsonPropertyName("model")]
    public string Model { get; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; }

    [JsonPropertyName("stream")]
    public bool Stream => false;
}
