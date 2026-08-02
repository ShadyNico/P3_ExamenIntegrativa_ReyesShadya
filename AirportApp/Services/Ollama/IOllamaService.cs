namespace AirportApp.Services.Ollama;

public interface IOllamaService
{
    Task<OllamaRecommendation> GenerateRecommendationAsync(
        string consulta,
        CancellationToken cancellationToken = default);
}
