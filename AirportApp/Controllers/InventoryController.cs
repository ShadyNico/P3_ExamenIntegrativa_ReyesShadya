using AirportApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Controllers;

[Authorize(Roles = "Administrador")]
public sealed class InventoryController(ApplicationDbContext context) : Controller
{
    public async Task<IActionResult> Index(int page = 1)
    {
        var query = context.FlightStocks
            .AsNoTracking()
            .OrderBy(item => item.Title);
        return View(await this.PaginateAsync(query, page));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Inicializar() =>
        RedirectToAction("Index", "Store");

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("AumentarStock")]
    public async Task<IActionResult> IncreaseStock(int id, int quantity)
    {
        if (quantity is < 1 or > 1000)
        {
            TempData["Error"] = "La cantidad debe estar entre 1 y 1000.";
            return RedirectToAction(nameof(Index));
        }

        var item = await context.FlightStocks.SingleOrDefaultAsync(stock => stock.FlightStockId == id);
        if (item is null)
        {
            return NotFound();
        }

        item.Stock = checked(item.Stock + quantity);
        await context.SaveChangesAsync();
        TempData["Message"] = "Inventario actualizado.";
        return RedirectToAction(nameof(Index));
    }
}
