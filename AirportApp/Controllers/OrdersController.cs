using AirportApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AirportApp.Controllers;

[Authorize]
public sealed class OrdersController(
    ServiceBookingQueryService queryService,
    UserManager<IdentityUser> userManager) : Controller
{
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await queryService.GetCheckoutAsync(
            id,
            userManager.GetUserId(User)!,
            User.IsInRole("Administrador"),
            cancellationToken);
        return model is null ? NotFound() : View(model);
    }
}
