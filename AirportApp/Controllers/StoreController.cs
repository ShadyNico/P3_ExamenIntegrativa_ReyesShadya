using AirportApp.Data;
using AirportApp.Models.Commerce;
using AirportApp.Models.ViewModels;
using AirportApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Controllers;

[Authorize]
public sealed class StoreController(
    ApplicationDbContext context,
    DomainDbContext domainContext,
    UserManager<IdentityUser> userManager) : Controller
{
    public async Task<IActionResult> Index(int page = 1)
    {
        await EnsureCatalogInitializedAsync();

        var query = context.FlightStocks
            .AsNoTracking()
            .Where(item => item.IsActive && item.Stock > 0)
            .OrderBy(item => item.Title)
            .ThenBy(item => item.FlightStockId);

        return View(await this.PaginateAsync(query, page));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int flightStockId, int quantity = 1)
    {
        if (!CommerceCalculations.IsValidQuantity(quantity, availableStock: int.MaxValue))
        {
            ModelState.AddModelError(nameof(quantity), "La cantidad debe estar entre 1 y 20.");
        }

        var stock = await context.FlightStocks
            .SingleOrDefaultAsync(item => item.FlightStockId == flightStockId && item.IsActive);

        if (stock is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid || !CommerceCalculations.IsValidQuantity(quantity, stock.Stock))
        {
            TempData["Error"] = "La cantidad solicitada no está disponible.";
            return RedirectToAction(nameof(Index));
        }

        var userId = userManager.GetUserId(User)!;
        var item = await context.ShoppingCartItems
            .SingleOrDefaultAsync(cart => cart.UserId == userId && cart.FlightStockId == flightStockId);

        if (item is null)
        {
            context.ShoppingCartItems.Add(new ShoppingCartItem
            {
                UserId = userId,
                FlightStockId = flightStockId,
                Quantity = quantity
            });
        }
        else if (CommerceCalculations.IsValidQuantity(item.Quantity + quantity, stock.Stock))
        {
            item.Quantity += quantity;
        }
        else
        {
            TempData["Error"] = "La cantidad acumulada supera el stock o el máximo permitido.";
            return RedirectToAction(nameof(Index));
        }

        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Cart));
    }

    public async Task<IActionResult> Cart()
    {
        var userId = userManager.GetUserId(User)!;
        var items = await context.ShoppingCartItems
            .AsNoTracking()
            .Include(item => item.FlightStock)
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.AddedAt)
            .ToListAsync();

        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFromCart(int id)
    {
        var userId = userManager.GetUserId(User)!;
        var item = await context.ShoppingCartItems
            .SingleOrDefaultAsync(cart => cart.ShoppingCartItemId == id && cart.UserId == userId);

        if (item is not null)
        {
            context.ShoppingCartItems.Remove(item);
            await context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Cart));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(string provider = "PayPhone")
    {
        var normalizedProvider = provider.ToUpperInvariant() switch
        {
            "PAYPHONE" => "PayPhone",
            "PAYPAL" => "PayPal",
            "PAYPALBUTTON" => "PayPalButton",
            _ => string.Empty
        };

        if (normalizedProvider.Length == 0)
        {
            return BadRequest("Proveedor de pago no admitido.");
        }

        var userId = userManager.GetUserId(User)!;
        var email = userManager.GetUserName(User) ?? string.Empty;
        var items = await context.ShoppingCartItems
            .Include(item => item.FlightStock)
            .Where(item => item.UserId == userId)
            .ToListAsync();

        if (items.Count == 0)
        {
            TempData["Error"] = "El carrito está vacío.";
            return RedirectToAction(nameof(Cart));
        }

        if (items.Any(item => !item.FlightStock.IsActive ||
                              item.Quantity < 1 ||
                              item.Quantity > item.FlightStock.Stock))
        {
            TempData["Error"] = "El stock cambió. Revisa el carrito antes de continuar.";
            return RedirectToAction(nameof(Cart));
        }

        var order = new PurchaseOrder
        {
            UserId = userId,
            UserEmailSnapshot = email,
            Status = "Pending"
        };

        foreach (var item in items)
        {
            var subtotal = CommerceCalculations.Subtotal(item.Quantity, item.FlightStock.UnitPrice);
            order.Details.Add(new PurchaseOrderDetail
            {
                FlightStockId = item.FlightStockId,
                ItemTitleSnapshot = item.FlightStock.Title,
                Quantity = item.Quantity,
                UnitPrice = item.FlightStock.UnitPrice,
                Subtotal = subtotal
            });
            order.Total += subtotal;
        }

        context.PurchaseOrders.Add(order);
        context.ShoppingCartItems.RemoveRange(items);
        await context.SaveChangesAsync();

        if (normalizedProvider == "PayPalButton")
        {
            return RedirectToAction(
                "PayPalButton",
                "Payment",
                new { orderId = order.PurchaseOrderId });
        }

        return View("StartPayment", new PaymentStartViewModel(
            order.PurchaseOrderId,
            normalizedProvider));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CheckoutPayPalButton() => Checkout("PayPalButton");

    private async Task EnsureCatalogInitializedAsync()
    {
        if (await context.FlightStocks.AnyAsync())
        {
            return;
        }

        var flights = await domainContext.Flights
            .AsNoTracking()
            .Include(flight => flight.FromAirport)
            .Include(flight => flight.ToAirport)
            .OrderByDescending(flight => flight.Departure)
            .Take(50)
            .Select(flight => new
            {
                flight.FlightId,
                flight.FlightNo,
                From = flight.FromAirport.Iata,
                To = flight.ToAirport.Iata
            })
            .ToListAsync();

        var flightIds = flights.Select(flight => flight.FlightId).ToArray();
        var prices = await domainContext.Bookings
            .AsNoTracking()
            .Where(booking => flightIds.Contains(booking.FlightId))
            .GroupBy(booking => booking.FlightId)
            .Select(group => new { FlightId = group.Key, Price = group.Average(item => item.Price) })
            .ToDictionaryAsync(item => item.FlightId, item => decimal.Round(item.Price, 2));

        foreach (var flight in flights)
        {
            context.FlightStocks.Add(new FlightStock
            {
                DomainEntityId = flight.FlightId,
                Title = $"{flight.FlightNo}: {flight.From} → {flight.To}",
                UnitPrice = prices.GetValueOrDefault(flight.FlightId, 100m),
                Stock = 20,
                IsActive = true
            });
        }

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            if (!await context.FlightStocks.AnyAsync())
            {
                throw;
            }
        }
    }
}
