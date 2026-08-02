using System.Net.Http.Json;
using System.Text.Json;
using AirportApp.Dtos.AI;
using AirportApp.Settings;
using Microsoft.Extensions.Options;

namespace AirportApp.Services.Ollama;

public sealed class OllamaService(
    HttpClient httpClient,
    IOptions<OllamaSettings> options,
    ILogger<OllamaService> logger) : IOllamaService
{
    private const string AssistantInstruction =
        """
        Actúa como asistente de una plataforma aeroportuaria.
        Recomienda una ruta o un tipo de vuelo según las preferencias del usuario.
        Responde en español de forma clara y concisa, en un máximo de tres oraciones.
        No inventes horarios, precios ni disponibilidad del catálogo.

        Preferencias del usuario:
        """;

    private readonly OllamaSettings settings = options.Value;

    public async Task<OllamaRecommendation> GenerateRecommendationAsync(
        string consulta,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"{AssistantInstruction}\n{consulta.Trim()}";
        var request = new OllamaGenerateRequest(settings.Model, prompt);

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "api/generate",
                request,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Ollama respondió con HTTP {StatusCode}.", (int)response.StatusCode);
                throw new OllamaUnavailableException(
                    "Ollama no pudo generar la recomendación solicitada.");
            }

            var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(
                cancellationToken: cancellationToken);
            if (string.IsNullOrWhiteSpace(payload?.Response))
            {
                logger.LogWarning("Ollama devolvió una respuesta vacía.");
                throw new OllamaUnavailableException("Ollama devolvió una respuesta vacía.");
            }

            return new OllamaRecommendation(payload.Response.Trim(), settings.Model);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Ollama superó el tiempo de espera de {TimeoutSeconds} segundos.",
                settings.TimeoutSeconds);
            throw new OllamaTimeoutException("Ollama superó el tiempo de espera configurado.", exception);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("No fue posible comunicar con Ollama: {Reason}", exception.Message);
            throw new OllamaUnavailableException(
                "No fue posible establecer comunicación con Ollama.",
                exception);
        }
        catch (JsonException exception)
        {
            logger.LogWarning("Ollama devolvió JSON no válido: {Reason}", exception.Message);
            throw new OllamaUnavailableException("Ollama devolvió una respuesta no válida.", exception);
        }
    }
}
