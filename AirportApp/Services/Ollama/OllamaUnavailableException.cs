namespace AirportApp.Services.Ollama;

public class OllamaUnavailableException : Exception
{
    public OllamaUnavailableException(string message)
        : base(message)
    {
    }

    public OllamaUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
