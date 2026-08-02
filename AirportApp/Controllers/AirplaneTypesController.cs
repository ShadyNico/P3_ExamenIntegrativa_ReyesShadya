using AirportApp.Data;
using AirportApp.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Controllers;

[Authorize(Roles = "Administrador")]
public sealed class AirplaneTypesController(DomainDbContext context) : Controller
{
    public async Task<IActionResult> Index(int page = 1)
    {
        var query = context.AirplaneTypes.AsNoTracking()
            .OrderBy(x => x.Identifier)
            .ThenBy(x => x.TypeId);
        return View(await this.PaginateAsync(query, page));
    }

    public async Task<IActionResult> Details(int id)
    {
        var item = await context.AirplaneTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TypeId == id);
        return item is null ? NotFound() : View(item);
    }

    public IActionResult Create() => View(new AirplaneType());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Identifier,Description")] AirplaneType item)
    {
        if (!ModelState.IsValid)
        {
            return View(item);
        }
        context.Add(item);
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await context.AirplaneTypes.FindAsync(id);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("TypeId,Identifier,Description")] AirplaneType input)
    {
        if (id != input.TypeId)
        {
            return NotFound();
        }
        var item = await context.AirplaneTypes.FindAsync(id);
        if (item is null)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return View(input);
        }
        item.Identifier = input.Identifier?.Trim();
        item.Description = input.Description?.Trim();
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var item = await context.AirplaneTypes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TypeId == id);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await context.AirplaneTypes.FindAsync(id);
        if (item is null)
        {
            return NotFound();
        }
        context.Remove(item);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "El tipo tiene aviones relacionados.";
        }
        return RedirectToAction(nameof(Index));
    }
}
