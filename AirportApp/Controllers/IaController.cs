using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AirportApp.Dtos.AI;
using AirportApp.Services.Ollama;

namespace AirportApp.Controllers;

[ApiController]
[Route("api/ia")]
[EnableRateLimiting("ollama")]
public sealed class IaController : ControllerBase
{
    private readonly IOllamaService _ollamaService;

    public IaController(IOllamaService ollamaService)
    {
        _ollamaService = ollamaService;
    }

    [HttpPost("generar")]
    [ProducesResponseType<GenerateRecommendationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GenerateRecommendationResponse>> Generate(
        [FromBody] GenerateRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Consulta))
        {
            return BadRequest(new ApiErrorResponse(
                "Escribe tus preferencias antes de generar una recomendación."));
        }

        if (request.Consulta.Length > 1000)
        {
            return BadRequest(new ApiErrorResponse(
                "La consulta no puede superar 1000 caracteres."));
        }

        try
        {
            var recommendation = await _ollamaService.GenerateRecommendationAsync(
                request.Consulta,
                cancellationToken);

            return Ok(new GenerateRecommendationResponse(
                recommendation.Response,
                recommendation.Model));
        }
        catch (OllamaTimeoutException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ApiErrorResponse(
                    "Ollama tardó demasiado en responder. Inténtalo nuevamente."));
        }
        catch (OllamaUnavailableException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new ApiErrorResponse(
                    "Ollama no está disponible. Verifica que esté instalado y en ejecución."));
        }
    }
}
