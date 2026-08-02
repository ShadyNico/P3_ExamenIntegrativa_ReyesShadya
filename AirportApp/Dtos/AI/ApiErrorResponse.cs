using System.Text.Json.Serialization;

namespace AirportApp.Dtos.AI;

public sealed class ApiErrorResponse
{
    public ApiErrorResponse(string mensaje)
    {
        Mensaje = mensaje;
    }

    [JsonPropertyName("mensaje")]
    public string Mensaje { get; }
}
