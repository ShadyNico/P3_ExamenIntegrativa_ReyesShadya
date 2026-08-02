namespace AirportApp.Services.Ollama;

public sealed class OllamaTimeoutException : OllamaUnavailableException
{
    public OllamaTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
