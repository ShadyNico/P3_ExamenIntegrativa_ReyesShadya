using AirportApp.Data;
using AirportApp.Models.AirportServices;
using AirportApp.Models.ViewModels;
using AirportApp.Services;
using AirportApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Controllers;

[Authorize]
public sealed class PaymentsController(
    ApplicationDbContext context,
    IPaymentService paymentService,
    IServiceGatewayPaymentService gatewayPaymentService,
    ServiceBookingQueryService queryService,
    UserManager<IdentityUser> userManager,
    ILogger<PaymentsController> logger) : Controller
{
    public async Task<IActionResult> Process(int orderId, CancellationToken cancellationToken)
    {
        var checkout = await FindCheckoutAsync(orderId, cancellationToken);
        if (checkout is null)
        {
            return NotFound();
        }

        if (checkout.Summary.OrderStatus == ServiceOrderStatus.Paid)
        {
            var paymentId = await context.ServicePayments.AsNoTracking()
                .Where(payment => payment.OrderId == orderId &&
                                  payment.PaymentStatus == SimulatedPaymentStatus.Approved)
                .Select(payment => payment.PaymentId)
                .FirstOrDefaultAsync(cancellationToken);
            return RedirectToAction("Receipt", "Reservations", new { paymentId });
        }

        return View(new PaymentViewModel
        {
            OrderId = orderId,
            PaymentMethod = SimulatedPaymentMethod.CreditCard,
            ExpirationMonth = DateTime.Today.Month,
            ExpirationYear = DateTime.Today.Year + 1,
            Checkout = checkout
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Process(
        PaymentViewModel model,
        CancellationToken cancellationToken)
    {
        var checkout = await FindCheckoutAsync(model.OrderId, cancellationToken);
        if (checkout is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            model.Checkout = checkout;
            return View(model);
        }

        try
        {
            var result = await paymentService.ProcessAsync(
                model,
                userManager.GetUserId(User)!,
                User.IsInRole("Administrador"),
                cancellationToken);
            TempData[result.Approved ? "Success" : "Error"] = result.Message;
            return RedirectToAction("Receipt", "Reservations", new { paymentId = result.PaymentId });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Pago simulado bloqueado para la orden {OrderId}.", model.OrderId);
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Process), new { orderId = model.OrderId });
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception,
                "La capacidad cambió mientras se procesaba la orden {OrderId}.",
                model.OrderId);
            TempData["Error"] = "La disponibilidad cambió. Revisa la capacidad e intenta nuevamente.";
            return RedirectToAction(nameof(Process), new { orderId = model.OrderId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePayPalOrderJson(
        int orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await gatewayPaymentService.StartPayPalAsync(
                orderId,
                userManager.GetUserId(User)!,
                User.IsInRole("Administrador"),
                cancellationToken);
            return Json(new { success = true, orderId = result.GatewayOrderId });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Orden no encontrada." });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "No fue posible iniciar PayPal para la orden {OrderId}.", orderId);
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { success = false, message = exception.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapturePayPalOrderJson(
        string paypalOrderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await gatewayPaymentService.CapturePayPalAsync(
                paypalOrderId,
                userManager.GetUserId(User)!,
                User.IsInRole("Administrador"),
                cancellationToken);
            return Json(new
            {
                success = result.Approved,
                message = result.Message,
                redirectUrl = Url.Action(
                    "Receipt",
                    "Reservations",
                    new { paymentId = result.PaymentId })
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Pago no encontrado." });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "No fue posible capturar la orden {PayPalOrderId}.", paypalOrderId);
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { success = false, message = "No se pudo confirmar el pago con PayPal." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartPayPhoneJson(
        int orderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await gatewayPaymentService.StartPayPhoneAsync(
                orderId,
                userManager.GetUserId(User)!,
                User.IsInRole("Administrador"),
                cancellationToken);
            return Json(new
            {
                success = true,
                paymentId = result.PaymentId,
                transactionId = result.GatewayOrderId,
                redirectUrl = result.RedirectUrl,
                message = "Enlace creado. Redirigiendo al formulario seguro de PayPhone."
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { success = false, message = "Orden no encontrada." });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "No fue posible iniciar PayPhone para la orden {OrderId}.", orderId);
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { success = false, message = exception.Message });
        }
    }

    private Task<CheckoutViewModel?> FindCheckoutAsync(
        int orderId,
        CancellationToken cancellationToken) =>
        queryService.GetCheckoutAsync(
            orderId,
            userManager.GetUserId(User)!,
            User.IsInRole("Administrador"),
            cancellationToken);
}
