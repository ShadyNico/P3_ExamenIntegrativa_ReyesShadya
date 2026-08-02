using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AirportApp.Models;
using Microsoft.AspNetCore.Authorization;


namespace AirportApp.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [Authorize(Roles = "Administrador")]
    public IActionResult PanelAdministrador()
    {
        return View();
    }
}

