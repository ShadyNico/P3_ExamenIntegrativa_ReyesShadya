using AirportApp.Data;
using AirportApp.Models.AirportServices;
using AirportApp.Models.ViewModels;
using AirportApp.Services;
using AirportApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Controllers;

[Authorize]
public sealed class ReservationsController(
    ApplicationDbContext context,
    IReservationService reservationService,
    IAvailabilityService availabilityService,
    IPricingService pricingService,
    ServiceBookingQueryService queryService,
    UserManager<IdentityUser> userManager,
    ILogger<ReservationsController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User)!;
        var isAdministrator = User.IsInRole("Administrador");
        var query = context.ServiceReservations
            .AsNoTracking()
            .Include(item => item.AirportService)
            .ThenInclude(item => item.Airport)
            .Include(item => item.Order)
            .Where(item => isAdministrator || item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt);

        var reservations = await query.ToListAsync(cancellationToken);
        var models = reservations.Select(item => new ReservationSummaryViewModel
        {
            ServiceReservationId = item.ServiceReservationId,
            OrderId = item.Order?.OrderId ?? 0,
            ReservationCode = item.ReservationCode,
            OrderNumber = item.Order?.OrderNumber ?? string.Empty,
            AirportName = AirportServiceLabels.Airport(
                item.AirportService.Airport.Iata,
                item.AirportService.Airport.Name),
            ServiceName = item.AirportService.Name,
            CustomerName = item.CustomerName,
            CustomerEmail = item.CustomerEmail,
            CustomerPhone = item.CustomerPhone,
            ReservationDate = item.ReservationDate,
            StartTime = item.StartTime,
            EndTime = item.EndTime,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            Subtotal = item.Subtotal,
            Tax = item.Tax,
            Total = item.Total,
            ReservationStatus = item.ReservationStatus,
            OrderStatus = item.Order?.OrderStatus ?? ServiceOrderStatus.Pending
        }).ToList();

        return View(models);
    }

    public async Task<IActionResult> Create(
        int serviceId,
        int? availabilityId,
        CancellationToken cancellationToken)
    {
        var model = new ReservationCreateViewModel
        {
            AirportServiceId = serviceId,
            ServiceAvailabilityId = availabilityId ?? 0,
            CustomerEmail = userManager.GetUserName(User) ?? string.Empty
        };

        if (!await PrepareCreateModelAsync(model, cancellationToken))
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        ReservationCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PrepareCreateModelAsync(model, cancellationToken);
            return View(model);
        }

        try
        {
            var reservationId = await reservationService.CreatePendingAsync(
                model,
                userManager.GetUserId(User)!,
                cancellationToken);
            TempData["Success"] = "Reserva creada. Confirma el resumen antes de pagar.";
            return RedirectToAction(nameof(Confirm), new { id = reservationId });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception,
                "No se pudo crear la reserva del servicio {ServiceId}.",
                model.AirportServiceId);
            ModelState.AddModelError(string.Empty, exception.Message);
            await PrepareCreateModelAsync(model, cancellationToken);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> CheckAvailability(
        int serviceAvailabilityId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var available = await availabilityService.HasCapacityAsync(
            serviceAvailabilityId,
            quantity,
            cancellationToken);
        return Json(new
        {
            available,
            message = available
                ? "Existe capacidad para la cantidad solicitada."
                : "La cantidad supera la capacidad disponible."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CalculatePrice(
        int airportServiceId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var service = await context.AirportServices.AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.AirportServiceId == airportServiceId && item.IsActive,
                cancellationToken);
        if (service is null || quantity <= 0)
        {
            return BadRequest(new { message = "Servicio o cantidad no válidos." });
        }

        var price = pricingService.Calculate(service.BasePrice, quantity);
        return Json(new
        {
            unitPrice = price.UnitPrice,
            subtotal = price.Subtotal,
            tax = price.Tax,
            total = price.Total
        });
    }

    public async Task<IActionResult> Confirm(int id, CancellationToken cancellationToken)
    {
        var model = await FindSummaryAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await FindSummaryAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        try
        {
            await reservationService.CancelAsync(
                id,
                userManager.GetUserId(User)!,
                User.IsInRole("Administrador"),
                cancellationToken);
            TempData["Success"] = "Reserva cancelada.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Receipt(int paymentId, CancellationToken cancellationToken)
    {
        var model = await queryService.GetReceiptAsync(
            paymentId,
            userManager.GetUserId(User)!,
            User.IsInRole("Administrador"),
            cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    private Task<ReservationSummaryViewModel?> FindSummaryAsync(
        int id,
        CancellationToken cancellationToken) =>
        queryService.GetReservationSummaryAsync(
            id,
            userManager.GetUserId(User)!,
            User.IsInRole("Administrador"),
            cancellationToken);

    private async Task<bool> PrepareCreateModelAsync(
        ReservationCreateViewModel model,
        CancellationToken cancellationToken)
    {
        var service = await context.AirportServices
            .AsNoTracking()
            .Include(item => item.Airport)
            .SingleOrDefaultAsync(
                item => item.AirportServiceId == model.AirportServiceId && item.IsActive,
                cancellationToken);
        if (service is null)
        {
            return false;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var slots = await context.ServiceAvailabilities
            .AsNoTracking()
            .Where(slot => slot.AirportServiceId == service.AirportServiceId &&
                           slot.AvailableDate >= today &&
                           slot.IsAvailable &&
                           slot.ReservedCapacity < slot.MaximumCapacity)
            .OrderBy(slot => slot.AvailableDate)
            .ThenBy(slot => slot.StartTime)
            .Take(120)
            .ToListAsync(cancellationToken);

        model.AirportName = AirportServiceLabels.Airport(service.Airport.Iata, service.Airport.Name);
        model.ServiceName = service.Name;
        model.PriceTypeLabel = AirportServiceLabels.PriceType(service.PriceType);
        model.UnitPrice = service.BasePrice;
        model.AvailableSlots = slots.Select(slot => new SelectListItem
        {
            Value = slot.ServiceAvailabilityId.ToString(),
            Text = $"{slot.AvailableDate:dd/MM/yyyy} · {slot.StartTime:HH:mm}–{slot.EndTime:HH:mm} · {slot.AvailableCapacity} disponibles",
            Selected = slot.ServiceAvailabilityId == model.ServiceAvailabilityId
        }).ToList();
        return true;
    }
}
