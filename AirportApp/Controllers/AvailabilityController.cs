using AirportApp.Data;
using AirportApp.Models.ViewModels;
using AirportApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Controllers;

public sealed class AvailabilityController(
    ApplicationDbContext context,
    IAvailabilityService availabilityService) : Controller
{
    public async Task<IActionResult> Index(
        AvailabilitySearchViewModel model,
        CancellationToken cancellationToken)
    {
        model.Date ??= DateOnly.FromDateTime(DateTime.Today);
        model.Airports = await context.AirportReferences
            .AsNoTracking()
            .Where(airport => airport.Services.Any(service => service.IsActive))
            .OrderBy(airport => airport.Name)
            .Select(airport => new SelectListItem
            {
                Value = airport.AirportId.ToString(),
                Text = (airport.Iata ?? "").Trim() + " - " + airport.Name.Trim()
            })
            .ToListAsync(cancellationToken);

        model.Services = await context.AirportServices
            .AsNoTracking()
            .Where(service => service.IsActive &&
                              (!model.AirportId.HasValue || service.AirportId == model.AirportId.Value))
            .OrderBy(service => service.Name)
            .Select(service => new SelectListItem
            {
                Value = service.AirportServiceId.ToString(),
                Text = service.Name
            })
            .ToListAsync(cancellationToken);

        if (model.AirportId.HasValue && model.AirportServiceId.HasValue && model.Date.HasValue)
        {
            model.Results = await availabilityService.SearchAsync(
                model.AirportId.Value,
                model.AirportServiceId.Value,
                model.Date.Value,
                cancellationToken);
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Services(short airportId, CancellationToken cancellationToken)
    {
        var services = await context.AirportServices
            .AsNoTracking()
            .Where(service => service.AirportId == airportId && service.IsActive)
            .OrderBy(service => service.Name)
            .Select(service => new { id = service.AirportServiceId, name = service.Name })
            .ToListAsync(cancellationToken);
        return Json(services);
    }
}
