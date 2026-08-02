using AirportApp.Data;
using AirportApp.Models.AirportServices;
using AirportApp.Models.ViewModels;
using AirportApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Controllers;

public sealed class AirportServicesController(
    ApplicationDbContext context,
    ILogger<AirportServicesController> logger) : Controller
{
    public async Task<IActionResult> Index(short? airportId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = context.AirportServices
            .AsNoTracking()
            .Include(service => service.Airport)
            .Where(service => service.IsActive);
        if (airportId.HasValue)
        {
            query = query.Where(service => service.AirportId == airportId.Value);
        }

        var services = await query.OrderBy(service => service.Airport.Name)
            .ThenBy(service => service.Name)
            .Select(service => new
            {
                Service = service,
                Slots = service.Availabilities.Count(slot =>
                    slot.AvailableDate >= today && slot.IsAvailable &&
                    slot.ReservedCapacity < slot.MaximumCapacity)
            })
            .ToListAsync(cancellationToken);

        ViewBag.Airports = await AirportOptionsAsync(airportId, cancellationToken);
        ViewBag.SelectedAirportId = airportId;
        return View(services.Select(item => ToSelection(item.Service, item.Slots)).ToList());
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var service = await context.AirportServices
            .AsNoTracking()
            .Include(item => item.Airport)
            .SingleOrDefaultAsync(item => item.AirportServiceId == id, cancellationToken);
        if (service is null)
        {
            return NotFound();
        }

        var slots = await context.ServiceAvailabilities.CountAsync(
            slot => slot.AirportServiceId == id && slot.AvailableDate >= today &&
                    slot.IsAvailable && slot.ReservedCapacity < slot.MaximumCapacity,
            cancellationToken);
        return View(ToSelection(service, slots));
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken) =>
        View(await PrepareFormAsync(new AirportServiceFormViewModel(), cancellationToken));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Create(
        AirportServiceFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (await context.AirportServices.AnyAsync(
                service => service.AirportId == model.AirportId &&
                           service.ServiceType == model.ServiceType,
                cancellationToken))
        {
            ModelState.AddModelError(nameof(model.ServiceType),
                "El aeropuerto ya tiene este tipo de servicio.");
        }

        if (!ModelState.IsValid)
        {
            return View(await PrepareFormAsync(model, cancellationToken));
        }

        context.AirportServices.Add(new AirportService
        {
            AirportId = model.AirportId,
            Name = model.Name.Trim(),
            Description = model.Description.Trim(),
            ServiceType = model.ServiceType,
            BasePrice = model.BasePrice,
            PriceType = model.PriceType,
            IsActive = model.IsActive
        });
        await context.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Servicio aeroportuario creado.";
        return RedirectToAction(nameof(Index), new { airportId = model.AirportId });
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var service = await context.AirportServices.FindAsync([id], cancellationToken);
        if (service is null)
        {
            return NotFound();
        }

        return View(await PrepareFormAsync(new AirportServiceFormViewModel
        {
            AirportServiceId = service.AirportServiceId,
            AirportId = service.AirportId,
            Name = service.Name,
            Description = service.Description,
            ServiceType = service.ServiceType,
            BasePrice = service.BasePrice,
            PriceType = service.PriceType,
            IsActive = service.IsActive
        }, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Edit(
        int id,
        AirportServiceFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.AirportServiceId)
        {
            return BadRequest();
        }

        var service = await context.AirportServices.FindAsync([id], cancellationToken);
        if (service is null)
        {
            return NotFound();
        }

        if (await context.AirportServices.AnyAsync(
                item => item.AirportServiceId != id &&
                        item.AirportId == model.AirportId &&
                        item.ServiceType == model.ServiceType,
                cancellationToken))
        {
            ModelState.AddModelError(nameof(model.ServiceType),
                "El aeropuerto ya tiene este tipo de servicio.");
        }

        if (!ModelState.IsValid)
        {
            return View(await PrepareFormAsync(model, cancellationToken));
        }

        service.AirportId = model.AirportId;
        service.Name = model.Name.Trim();
        service.Description = model.Description.Trim();
        service.ServiceType = model.ServiceType;
        service.BasePrice = model.BasePrice;
        service.PriceType = model.PriceType;
        service.IsActive = model.IsActive;
        await context.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "Servicio actualizado.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var service = await context.AirportServices.FindAsync([id], cancellationToken);
        if (service is null)
        {
            return NotFound();
        }

        service.IsActive = false;
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Servicio aeroportuario {ServiceId} desactivado.", id);
        TempData["Success"] = "Servicio desactivado sin borrar su historial.";
        return RedirectToAction(nameof(Index), new { airportId = service.AirportId });
    }

    private async Task<AirportServiceFormViewModel> PrepareFormAsync(
        AirportServiceFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.Airports = await AirportOptionsAsync(model.AirportId, cancellationToken);
        return model;
    }

    private async Task<IReadOnlyList<SelectListItem>> AirportOptionsAsync(
        short? selected,
        CancellationToken cancellationToken) =>
        await context.AirportReferences.AsNoTracking()
            .Where(airport => airport.Services.Any() ||
                              new[] { "UIO", "GYE", "MEC", "ESM" }.Contains(airport.Iata!.Trim()))
            .OrderBy(airport => airport.Name)
            .Select(airport => new SelectListItem
            {
                Value = airport.AirportId.ToString(),
                Text = (airport.Iata ?? "").Trim() + " - " + airport.Name.Trim(),
                Selected = selected.HasValue && airport.AirportId == selected.Value
            })
            .ToListAsync(cancellationToken);

    private static ServiceSelectionViewModel ToSelection(AirportService service, int slots) => new()
    {
        AirportServiceId = service.AirportServiceId,
        AirportId = service.AirportId,
        AirportName = AirportServiceLabels.Airport(service.Airport.Iata, service.Airport.Name),
        Name = service.Name,
        Description = service.Description,
        ServiceType = service.ServiceType,
        PriceType = service.PriceType,
        BasePrice = service.BasePrice,
        AvailableSlotCount = slots
    };
}
