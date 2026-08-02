using System.Net;
using System.Text;
using AirportApp.Services.Ollama;
using AirportApp.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AirportApp.Tests;

public sealed class OllamaServiceTests
{
    [Fact]
    public async Task ValidResponse_IsTrimmedAndReturned()
    {
        var service = CreateService((_, _) => Task.FromResult(
            Json(HttpStatusCode.OK, """{"response":"  Ruta recomendada.  "}""")));

        var result = await service.GenerateRecommendationAsync("vuelo corto");

        Assert.Equal("Ruta recomendada.", result.Response);
        Assert.Equal("test-model", result.Model);
    }

    [Fact]
    public async Task HttpError_BecomesUnavailable()
    {
        var service = CreateService((_, _) => Task.FromResult(
            Json(HttpStatusCode.BadGateway, """{"error":"details"}""")));

        await Assert.ThrowsAsync<OllamaUnavailableException>(
            () => service.GenerateRecommendationAsync("consulta"));
    }

    [Theory]
    [InlineData("{not-json")]
    [InlineData("""{"response":"  "}""")]
    public async Task InvalidOrEmptyJson_BecomesUnavailable(string content)
    {
        var service = CreateService((_, _) => Task.FromResult(Json(HttpStatusCode.OK, content)));

        await Assert.ThrowsAsync<OllamaUnavailableException>(
            () => service.GenerateRecommendationAsync("consulta"));
    }

    [Fact]
    public async Task InternalTimeout_BecomesTypedTimeout()
    {
        var service = CreateService((_, _) => throw new TaskCanceledException("timeout"));

        await Assert.ThrowsAsync<OllamaTimeoutException>(
            () => service.GenerateRecommendationAsync("consulta"));
    }

    private static OllamaService CreateService(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
    {
        var client = new HttpClient(new StubHandler(responseFactory))
        {
            BaseAddress = new Uri("http://localhost:11434/")
        };
        var settings = Options.Create(new OllamaSettings
        {
            BaseUrl = "http://localhost:11434",
            Model = "test-model",
            TimeoutSeconds = 1
        });
        return new OllamaService(client, settings, NullLogger<OllamaService>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string content) => new(status)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            responseFactory(request, cancellationToken);
    }
}
