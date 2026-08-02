using AirportApp.Data;
using AirportApp.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Controllers;

[Authorize]
public sealed class PassengersController(DomainDbContext context) : Controller
{
    public async Task<IActionResult> Index(string? buscar, int page = 1)
    {
        var query = context.Passengers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = $"%{buscar.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.FirstName, term) ||
                EF.Functions.ILike(x.LastName, term) ||
                EF.Functions.ILike(x.PassportNo, term));
        }

        ViewData["Buscar"] = buscar;
        query = query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.PassengerId);
        return View(await this.PaginateAsync(query, page));
    }

    public async Task<IActionResult> Ejercicio11()
    {
        var passengers = await context.Passengers.AsNoTracking()
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ThenBy(x => x.PassengerId)
            .Skip(1)
            .Take(5)
            .ToListAsync();
        return View("Index", passengers);
    }

    public async Task<IActionResult> Details(int id)
    {
        var passenger = await context.Passengers.AsNoTracking()
            .Include(x => x.Details)
            .FirstOrDefaultAsync(x => x.PassengerId == id);
        return passenger is null ? NotFound() : View(passenger);
    }

    [Authorize(Roles = "Administrador,Operador")]
    public IActionResult Create() => View(new Passenger());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Operador")]
    public async Task<IActionResult> Create(
        [Bind("PassportNo,FirstName,LastName")] Passenger passenger)
    {
        Normalize(passenger);
        if (!ModelState.IsValid)
        {
            return View(passenger);
        }
        context.Passengers.Add(passenger);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Edit(int id)
    {
        var passenger = await context.Passengers.FindAsync(id);
        return passenger is null ? NotFound() : View(passenger);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("PassengerId,PassportNo,FirstName,LastName")] Passenger input)
    {
        if (id != input.PassengerId)
        {
            return NotFound();
        }
        var passenger = await context.Passengers.FindAsync(id);
        if (passenger is null)
        {
            return NotFound();
        }

        Normalize(input);
        if (!ModelState.IsValid)
        {
            return View(input);
        }
        passenger.PassportNo = input.PassportNo;
        passenger.FirstName = input.FirstName;
        passenger.LastName = input.LastName;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Delete(int id)
    {
        var passenger = await context.Passengers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PassengerId == id);
        return passenger is null ? NotFound() : View(passenger);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var passenger = await context.Passengers.FindAsync(id);
        if (passenger is null)
        {
            return NotFound();
        }
        context.Passengers.Remove(passenger);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "El pasajero tiene reservas y no puede eliminarse.";
        }
        return RedirectToAction(nameof(Index));
    }

    private static void Normalize(Passenger passenger)
    {
        passenger.PassportNo = (passenger.PassportNo ?? string.Empty).Trim().ToUpperInvariant();
        passenger.FirstName = (passenger.FirstName ?? string.Empty).Trim();
        passenger.LastName = (passenger.LastName ?? string.Empty).Trim();
    }
}
