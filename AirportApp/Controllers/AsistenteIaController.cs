using Microsoft.AspNetCore.Mvc;

namespace AirportApp.Controllers;

[Route("asistente-ia")]
public sealed class AsistenteIaController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }
}
