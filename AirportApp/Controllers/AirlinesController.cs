using AirportApp.Data;
using AirportApp.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Controllers;

[Authorize(Roles = "Administrador,Supervisor,Consulta")]
public sealed class AirlinesController(DomainDbContext context) : Controller
{
    public async Task<IActionResult> Index(int page = 1)
    {
        var query = context.Airlines.AsNoTracking()
            .Include(x => x.BaseAirport)
            .OrderBy(x => x.AirlineName)
            .ThenBy(x => x.AirlineId);
        return View(await this.PaginateAsync(query, page));
    }

    public async Task<IActionResult> Details(short id)
    {
        var airline = await context.Airlines.AsNoTracking()
            .Include(x => x.BaseAirport)
            .FirstOrDefaultAsync(x => x.AirlineId == id);
        return airline is null ? NotFound() : View(airline);
    }

    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Create()
    {
        await PopulateAirportsAsync();
        return View(new Airline());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Create(
        [Bind("Iata,AirlineName,BaseAirportId")] Airline airline)
    {
        airline.Iata = (airline.Iata ?? string.Empty).Trim().ToUpperInvariant();
        if (!ModelState.IsValid)
        {
            await PopulateAirportsAsync(airline.BaseAirportId);
            return View(airline);
        }
        context.Airlines.Add(airline);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Edit(short id)
    {
        var airline = await context.Airlines.FindAsync(id);
        if (airline is null)
        {
            return NotFound();
        }
        await PopulateAirportsAsync(airline.BaseAirportId);
        return View(airline);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Edit(
        short id,
        [Bind("AirlineId,Iata,AirlineName,BaseAirportId")] Airline input)
    {
        if (id != input.AirlineId)
        {
            return NotFound();
        }
        var airline = await context.Airlines.FindAsync(id);
        if (airline is null)
        {
            return NotFound();
        }
        input.Iata = (input.Iata ?? string.Empty).Trim().ToUpperInvariant();
        if (!ModelState.IsValid)
        {
            await PopulateAirportsAsync(input.BaseAirportId);
            return View(input);
        }
        airline.Iata = input.Iata;
        airline.AirlineName = input.AirlineName?.Trim();
        airline.BaseAirportId = input.BaseAirportId;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Delete(short id)
    {
        var airline = await context.Airlines.AsNoTracking()
            .Include(x => x.BaseAirport)
            .FirstOrDefaultAsync(x => x.AirlineId == id);
        return airline is null ? NotFound() : View(airline);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> DeleteConfirmed(short id)
    {
        var airline = await context.Airlines.FindAsync(id);
        if (airline is null)
        {
            return NotFound();
        }
        context.Airlines.Remove(airline);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "La aerolínea tiene vuelos o aviones relacionados.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateAirportsAsync(short? selected = null)
    {
        var airports = await context.Airports.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.AirportId,
                Label = (x.Iata ?? x.Icao).Trim() + " · " + x.Name
            })
            .ToListAsync();
        ViewData["BaseAirportId"] = new SelectList(airports, "AirportId", "Label", selected);
    }
}
