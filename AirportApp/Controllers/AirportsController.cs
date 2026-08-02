using AirportApp.Data;
using AirportApp.Models.ViewModels;
using AirportApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Controllers;

public sealed class AirportsController(ApplicationDbContext context) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var airports = await context.AirportReferences
            .AsNoTracking()
            .Where(airport => airport.Services.Any(service => service.IsActive))
            .OrderBy(airport => airport.Name)
            .Select(airport => new
            {
                airport.AirportId,
                airport.Iata,
                airport.Name,
                Count = airport.Services.Count(service => service.IsActive)
            })
            .ToListAsync(cancellationToken);

        return View(airports.Select(airport => new AirportSelectionViewModel
        {
            AirportId = airport.AirportId,
            Iata = airport.Iata?.Trim() ?? string.Empty,
            Name = AirportServiceLabels.Airport(airport.Iata, airport.Name),
            ActiveServiceCount = airport.Count
        }).ToList());
    }

    public async Task<IActionResult> Details(short id, CancellationToken cancellationToken)
    {
        var airport = await context.AirportReferences
            .AsNoTracking()
            .Include(item => item.Services.Where(service => service.IsActive))
            .SingleOrDefaultAsync(item => item.AirportId == id, cancellationToken);
        if (airport is null)
        {
            return NotFound();
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var services = new List<ServiceSelectionViewModel>();
        foreach (var service in airport.Services.OrderBy(item => item.Name))
        {
            var slots = await context.ServiceAvailabilities.CountAsync(
                slot => slot.AirportServiceId == service.AirportServiceId &&
                        slot.AvailableDate >= today &&
                        slot.IsAvailable &&
                        slot.ReservedCapacity < slot.MaximumCapacity,
                cancellationToken);
            services.Add(new ServiceSelectionViewModel
            {
                AirportServiceId = service.AirportServiceId,
                AirportId = airport.AirportId,
                AirportName = AirportServiceLabels.Airport(airport.Iata, airport.Name),
                Name = service.Name,
                Description = service.Description,
                ServiceType = service.ServiceType,
                PriceType = service.PriceType,
                BasePrice = service.BasePrice,
                AvailableSlotCount = slots
            });
        }

        return View(new AirportDetailsViewModel
        {
            AirportId = airport.AirportId,
            Iata = airport.Iata?.Trim() ?? string.Empty,
            Icao = airport.Icao.Trim(),
            Name = AirportServiceLabels.Airport(airport.Iata, airport.Name),
            Services = services
        });
    }
}
