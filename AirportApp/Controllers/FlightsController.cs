using AirportApp.Data;
using AirportApp.Models.Domain;
using AirportApp.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Controllers;

[Authorize]
public sealed class FlightsController(DomainDbContext context) : Controller
{
    public async Task<IActionResult> Index(
        string? buscar,
        int? duracionMinima,
        int page = 1)
    {
        var query = context.Flights
            .AsNoTracking()
            .Include(x => x.FromAirport)
            .Include(x => x.ToAirport)
            .Include(x => x.Airline)
            .Include(x => x.Airplane)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = $"%{buscar.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.FlightNo, term) ||
                EF.Functions.ILike(x.FromAirport.Name, term) ||
                EF.Functions.ILike(x.ToAirport.Name, term) ||
                (x.Airline.AirlineName != null &&
                 EF.Functions.ILike(x.Airline.AirlineName, term)));
        }

        if (duracionMinima is > 0)
        {
            var minimum = TimeSpan.FromMinutes(duracionMinima.Value);
            query = query.Where(x => x.Arrival - x.Departure >= minimum);
        }

        ViewData["Buscar"] = buscar;
        ViewData["DuracionMinima"] = duracionMinima;

        query = query.OrderBy(x => x.FlightNo).ThenBy(x => x.FlightId);
        return View(await this.PaginateAsync(query, page));
    }

    public async Task<IActionResult> Details(int id)
    {
        var flight = await DetailedQuery().FirstOrDefaultAsync(x => x.FlightId == id);
        return flight is null ? NotFound() : View(flight);
    }

    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Create()
    {
        await PopulateSelectionsAsync();
        return View(new FlightFormViewModel
        {
            Departure = DateTime.Today.AddHours(8),
            Arrival = DateTime.Today.AddHours(10)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Create(FlightFormViewModel model)
    {
        var schedule = await context.FlightSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.FlightNo == model.FlightNo);
        var airplane = await context.Airplanes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AirplaneId == model.AirplaneId);

        if (schedule is null)
        {
            ModelState.AddModelError(nameof(model.FlightNo), "El vuelo programado no existe.");
        }
        if (airplane is null)
        {
            ModelState.AddModelError(nameof(model.AirplaneId), "El avión no existe.");
        }
        else if (schedule is not null && airplane.AirlineId != schedule.AirlineId)
        {
            ModelState.AddModelError(
                nameof(model.AirplaneId),
                "El avión debe pertenecer a la aerolínea del vuelo programado.");
        }

        if (!ModelState.IsValid || schedule is null)
        {
            await PopulateSelectionsAsync(model.FlightNo, model.AirplaneId);
            return View(model);
        }

        context.Flights.Add(new Flight
        {
            FlightNo = schedule.FlightNo,
            FromAirportId = schedule.FromAirportId,
            ToAirportId = schedule.ToAirportId,
            AirlineId = schedule.AirlineId,
            AirplaneId = model.AirplaneId,
            Departure = model.Departure,
            Arrival = model.Arrival
        });
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Edit(int id)
    {
        var flight = await context.Flights.AsNoTracking()
            .FirstOrDefaultAsync(x => x.FlightId == id);
        if (flight is null)
        {
            return NotFound();
        }

        await PopulateSelectionsAsync(flight.FlightNo, flight.AirplaneId);
        return View(new FlightFormViewModel
        {
            FlightId = flight.FlightId,
            FlightNo = flight.FlightNo.Trim(),
            Departure = flight.Departure,
            Arrival = flight.Arrival,
            AirplaneId = flight.AirplaneId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Supervisor")]
    public async Task<IActionResult> Edit(int id, FlightFormViewModel model)
    {
        if (id != model.FlightId)
        {
            return NotFound();
        }

        var flight = await context.Flights.FirstOrDefaultAsync(x => x.FlightId == id);
        var schedule = await context.FlightSchedules.AsNoTracking()
            .FirstOrDefaultAsync(x => x.FlightNo == model.FlightNo);
        var airplane = await context.Airplanes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.AirplaneId == model.AirplaneId);

        if (flight is null)
        {
            return NotFound();
        }
        if (schedule is null)
        {
            ModelState.AddModelError(nameof(model.FlightNo), "El vuelo programado no existe.");
        }
        if (airplane is null || (schedule is not null && airplane.AirlineId != schedule.AirlineId))
        {
            ModelState.AddModelError(nameof(model.AirplaneId), "El avión no corresponde a la aerolínea.");
        }

        if (!ModelState.IsValid || schedule is null)
        {
            await PopulateSelectionsAsync(model.FlightNo, model.AirplaneId);
            return View(model);
        }

        flight.FlightNo = schedule.FlightNo;
        flight.FromAirportId = schedule.FromAirportId;
        flight.ToAirportId = schedule.ToAirportId;
        flight.AirlineId = schedule.AirlineId;
        flight.AirplaneId = model.AirplaneId;
        flight.Departure = model.Departure;
        flight.Arrival = model.Arrival;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Delete(int id)
    {
        var flight = await DetailedQuery().FirstOrDefaultAsync(x => x.FlightId == id);
        return flight is null ? NotFound() : View(flight);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var flight = await context.Flights.FindAsync(id);
        if (flight is null)
        {
            return NotFound();
        }

        context.Flights.Remove(flight);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "El vuelo tiene reservas o registros relacionados y no puede eliminarse.";
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Ejercicio35()
    {
        var flights = await DetailedQuery()
            .Where(x =>
                x.Airline.AirlineName != null &&
                EF.Functions.ILike(x.Airline.AirlineName, "%Air%") &&
                x.Bookings.Any(b => EF.Functions.ILike(b.Passenger.LastName, "B%")))
            .OrderBy(x => x.FlightId)
            .Take(5)
            .ToListAsync();
        return View("Index", flights);
    }

    public async Task<IActionResult> Ejercicio40()
    {
        var minimum = TimeSpan.FromMinutes(100);
        var flights = await DetailedQuery()
            .Where(x =>
                x.Airline.AirlineName != null &&
                EF.Functions.ILike(x.Airline.AirlineName, "%Air%") &&
                x.Bookings.Any(b => EF.Functions.ILike(b.Passenger.LastName, "%R%")) &&
                x.Arrival - x.Departure > minimum)
            .OrderBy(x => x.FlightId)
            .Take(10)
            .ToListAsync();
        return View("Index", flights);
    }

    private IQueryable<Flight> DetailedQuery() =>
        context.Flights
            .AsNoTracking()
            .Include(x => x.FromAirport)
            .Include(x => x.ToAirport)
            .Include(x => x.Airline)
            .Include(x => x.Airplane)
            .ThenInclude(x => x.Type);

    private async Task PopulateSelectionsAsync(string? flightNo = null, int? airplaneId = null)
    {
        var schedules = await context.FlightSchedules
            .AsNoTracking()
            .Include(x => x.FromAirport)
            .Include(x => x.ToAirport)
            .OrderBy(x => x.FlightNo)
            .Select(x => new
            {
                x.FlightNo,
                Label = x.FlightNo.Trim() + " · " + x.FromAirport.Icao.Trim() +
                        " → " + x.ToAirport.Icao.Trim()
            })
            .ToListAsync();

        var airplanes = await context.Airplanes
            .AsNoTracking()
            .Include(x => x.Type)
            .Include(x => x.Airline)
            .OrderBy(x => x.AirplaneId)
            .Select(x => new
            {
                x.AirplaneId,
                Label = "#" + x.AirplaneId + " · " +
                        (x.Type.Identifier ?? "Sin tipo") + " · " +
                        (x.Airline.AirlineName ?? x.Airline.Iata)
            })
            .ToListAsync();

        ViewData["FlightNo"] = new SelectList(schedules, "FlightNo", "Label", flightNo);
        ViewData["AirplaneId"] = new SelectList(airplanes, "AirplaneId", "Label", airplaneId);
    }
}
