using AirportApp.Data;
using AirportApp.Models.Domain;
using AirportApp.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Controllers;

[Authorize]
public sealed class BookingsController(DomainDbContext context) : Controller
{
    public async Task<IActionResult> Index(string? buscar, int page = 1)
    {
        var query = context.Bookings.AsNoTracking()
            .Include(x => x.Flight)
            .Include(x => x.Passenger)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = $"%{buscar.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Flight.FlightNo, term) ||
                EF.Functions.ILike(x.Passenger.PassportNo, term) ||
                EF.Functions.ILike(x.Passenger.LastName, term) ||
                (x.Seat != null && EF.Functions.ILike(x.Seat, term)));
        }
        ViewData["Buscar"] = buscar;
        query = query.OrderBy(x => x.BookingId);
        return View(await this.PaginateAsync(query, page));
    }

    public async Task<IActionResult> Details(int id)
    {
        var booking = await DetailedQuery().FirstOrDefaultAsync(x => x.BookingId == id);
        return booking is null ? NotFound() : View(booking);
    }

    [Authorize(Roles = "Administrador,Supervisor")]
    public IActionResult Create() => View(new BookingFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Create(BookingFormViewModel model)
    {
        await ValidateReferencesAsync(model);
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        context.Bookings.Add(new Booking
        {
            FlightId = model.FlightId,
            PassengerId = model.PassengerId,
            Seat = NormalizeSeat(model.Seat),
            Price = model.Price
        });
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Edit(int id)
    {
        var booking = await context.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BookingId == id);
        return booking is null
            ? NotFound()
            : View(new BookingFormViewModel
            {
                BookingId = booking.BookingId,
                FlightId = booking.FlightId,
                PassengerId = booking.PassengerId,
                Seat = booking.Seat?.Trim(),
                Price = booking.Price
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Edit(int id, BookingFormViewModel model)
    {
        if (id != model.BookingId)
        {
            return NotFound();
        }
        var booking = await context.Bookings.FindAsync(id);
        if (booking is null)
        {
            return NotFound();
        }
        await ValidateReferencesAsync(model);
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        booking.FlightId = model.FlightId;
        booking.PassengerId = model.PassengerId;
        booking.Seat = NormalizeSeat(model.Seat);
        booking.Price = model.Price;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Delete(int id)
    {
        var booking = await DetailedQuery().FirstOrDefaultAsync(x => x.BookingId == id);
        return booking is null ? NotFound() : View(booking);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var booking = await context.Bookings.FindAsync(id);
        if (booking is null)
        {
            return NotFound();
        }
        context.Bookings.Remove(booking);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private IQueryable<Booking> DetailedQuery() =>
        context.Bookings.AsNoTracking()
            .Include(x => x.Flight).ThenInclude(x => x.FromAirport)
            .Include(x => x.Flight).ThenInclude(x => x.ToAirport)
            .Include(x => x.Passenger);

    private async Task ValidateReferencesAsync(BookingFormViewModel model)
    {
        if (!await context.Flights.AnyAsync(x => x.FlightId == model.FlightId))
        {
            ModelState.AddModelError(nameof(model.FlightId), "El vuelo no existe.");
        }
        if (!await context.Passengers.AnyAsync(x => x.PassengerId == model.PassengerId))
        {
            ModelState.AddModelError(nameof(model.PassengerId), "El pasajero no existe.");
        }

        var normalizedSeat = NormalizeSeat(model.Seat);
        if (normalizedSeat is not null &&
            await context.Bookings.AnyAsync(x =>
                x.FlightId == model.FlightId &&
                x.Seat == normalizedSeat &&
                x.BookingId != model.BookingId))
        {
            ModelState.AddModelError(nameof(model.Seat), "El asiento ya está reservado en este vuelo.");
        }
    }

    private static string? NormalizeSeat(string? seat) =>
        string.IsNullOrWhiteSpace(seat) ? null : seat.Trim().ToUpperInvariant();
}
