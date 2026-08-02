using System.Data;
using System.Globalization;
using AirportApp.Data;
using AirportApp.Models.Commerce;
using AirportApp.Services.Payments;
using AirportApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Controllers;

[Authorize]
public sealed class PaymentController(
    ApplicationDbContext context,
    PayPhoneApiLinkService payPhoneService,
    PayPalService payPalService,
    UserManager<IdentityUser> userManager,
    IConfiguration configuration,
    ILogger<PaymentController> logger) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLink(int orderId, CancellationToken cancellationToken)
    {
        var order = await FindOwnedOrderAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var existing = await context.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                payment => payment.PurchaseOrderId == orderId &&
                           payment.Provider == "PayPhone" &&
                           payment.Status == "Pending",
                cancellationToken);
        if (existing is not null)
        {
            return RedirectToAction(nameof(Details), new { id = existing.PaymentTransactionId });
        }

        var payment = NewPayment(order, "PayPhone");
        try
        {
            payment.PayphonePaymentUrl = await payPhoneService.CreatePaymentLinkAsync(
                order.Total,
                payment.ClientTransactionId,
                $"Orden AirportApp #{order.PurchaseOrderId}",
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "No fue posible iniciar PayPhone para la orden {OrderId}.", orderId);
            payment.Status = "Failed";
            payment.GatewayResponseSanitized = exception.Message;
            TempData["Error"] = "No se pudo iniciar el pago con PayPhone.";
        }

        context.PaymentTransactions.Add(payment);
        await context.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { id = payment.PaymentTransactionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePayPalOrder(
        int orderId,
        CancellationToken cancellationToken)
    {
        var order = await FindOwnedOrderAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var existing = await context.PaymentTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                payment => payment.PurchaseOrderId == orderId &&
                           payment.Provider == "PayPal" &&
                           payment.Status == "Pending" &&
                           payment.PayPalApprovalUrl != null,
                cancellationToken);
        if (existing is not null)
        {
            return RedirectToAction(nameof(Details), new { id = existing.PaymentTransactionId });
        }

        var payment = NewPayment(order, "PayPal");
        try
        {
            var result = await payPalService.CreateOrderAsync(
                order.Total,
                $"Orden AirportApp #{order.PurchaseOrderId}",
                includeRedirectUrls: true,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(result.ApprovalUrl))
            {
                throw new InvalidOperationException("PayPal devolvió una respuesta incompleta.");
            }

            payment.PayPalOrderId = result.OrderId;
            payment.PayPalApprovalUrl = result.ApprovalUrl;
            payment.GatewayResponseSanitized = $"Estado de creación: {result.Status}";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "No fue posible iniciar PayPal para la orden {OrderId}.", orderId);
            payment.Status = "Failed";
            payment.GatewayResponseSanitized = exception.Message;
            TempData["Error"] = "No se pudo iniciar el pago con PayPal.";
        }

        context.PaymentTransactions.Add(payment);
        await context.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Details), new { id = payment.PaymentTransactionId });
    }

    public async Task<IActionResult> PayPalButton(int orderId, CancellationToken cancellationToken)
    {
        var order = await FindOwnedOrderAsync(orderId, cancellationToken);
        return order is null ? NotFound() : View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePayPalButtonOrderJson(
        int orderId,
        CancellationToken cancellationToken)
    {
        var order = await FindOwnedOrderAsync(orderId, cancellationToken);
        if (order is null)
        {
            return NotFound(new { success = false, message = "Orden no encontrada." });
        }

        if (!string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "La orden no admite otro pago." });
        }

        try
        {
            var result = await payPalService.CreateOrderAsync(
                order.Total,
                $"Orden AirportApp #{order.PurchaseOrderId}",
                includeRedirectUrls: false,
                cancellationToken);
            var payment = NewPayment(order, "PayPal");
            payment.PayPalOrderId = result.OrderId;
            payment.GatewayResponseSanitized = $"Estado de creación: {result.Status}";
            context.PaymentTransactions.Add(payment);
            await context.SaveChangesAsync(cancellationToken);
            return Json(new { success = true, orderId = result.OrderId });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "No fue posible crear la orden PayPal {OrderId}.", orderId);
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { success = false, message = "No se pudo iniciar el pago con PayPal." });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapturePayPalButtonOrderJson(
        string paypalOrderId,
        CancellationToken cancellationToken)
    {
        var payment = await FindOwnedPayPalPaymentAsync(paypalOrderId, cancellationToken);
        if (payment is null)
        {
            return NotFound(new { success = false, message = "Transacción no encontrada." });
        }

        try
        {
            if (payment.Status != "Paid")
            {
                var capture = await payPalService.CaptureOrderAsync(paypalOrderId, cancellationToken);
                payment.PayPalCaptureId = capture.CaptureId;
                payment.GatewayResponseSanitized = $"Estado de captura: {capture.Status}";
                if (capture.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
                {
                    await MarkPaymentAsPaidAsync(payment, cancellationToken);
                }
                else
                {
                    payment.Status = NormalizeGatewayStatus(capture.Status);
                    await context.SaveChangesAsync(cancellationToken);
                }
            }

            return Json(new
            {
                success = payment.Status == "Paid",
                status = payment.Status,
                redirectUrl = Url.Action(nameof(Details), new { id = payment.PaymentTransactionId })
            });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "No fue posible capturar la orden PayPal.");
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { success = false, message = "No se pudo confirmar el pago con PayPal." });
        }
    }

    public async Task<IActionResult> Success(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction("Index", "Store");
        }

        var payment = await FindOwnedPayPalPaymentAsync(token, cancellationToken);
        if (payment is null)
        {
            return NotFound();
        }

        if (payment.Status != "Paid")
        {
            try
            {
                var capture = await payPalService.CaptureOrderAsync(token, cancellationToken);
                payment.PayPalCaptureId = capture.CaptureId;
                payment.GatewayResponseSanitized = $"Estado de captura: {capture.Status}";
                if (capture.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
                {
                    await MarkPaymentAsPaidAsync(payment, cancellationToken);
                }
                else
                {
                    payment.Status = NormalizeGatewayStatus(capture.Status);
                    await context.SaveChangesAsync(cancellationToken);
                }
            }
            catch (InvalidOperationException exception)
            {
                logger.LogWarning(exception, "Falló el retorno de PayPal.");
                TempData["Error"] = "No se pudo confirmar el pago con PayPal.";
            }
        }

        return RedirectToAction(nameof(Details), new { id = payment.PaymentTransactionId });
    }

    public async Task<IActionResult> Cancel(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction("Index", "Store");
        }

        var payment = await FindOwnedPayPalPaymentAsync(token, cancellationToken);
        if (payment is null)
        {
            return NotFound();
        }

        if (payment.Status == "Pending")
        {
            payment.Status = "Cancelled";
            payment.GatewayResponseSanitized = "Pago cancelado por el usuario.";
            await context.SaveChangesAsync(cancellationToken);
        }

        return RedirectToAction(nameof(Details), new { id = payment.PaymentTransactionId });
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var payment = await context.PaymentTransactions
            .AsNoTracking()
            .Include(item => item.PurchaseOrder)
            .ThenInclude(order => order.Details)
            .FirstOrDefaultAsync(item => item.PaymentTransactionId == id, cancellationToken);

        if (payment is null || !CanAccess(payment.PurchaseOrder.UserId))
        {
            return NotFound();
        }

        return View(payment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> MarkAsPaid(int id, CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Payments:AllowManualConfirmation"))
        {
            return NotFound();
        }

        var payment = await context.PaymentTransactions
            .Include(item => item.PurchaseOrder)
            .ThenInclude(order => order.Details)
            .FirstOrDefaultAsync(item => item.PaymentTransactionId == id, cancellationToken);
        if (payment is null)
        {
            return NotFound();
        }

        await MarkPaymentAsPaidAsync(payment, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<PurchaseOrder?> FindOwnedOrderAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var order = await context.PurchaseOrders
            .Include(item => item.Details)
            .FirstOrDefaultAsync(item => item.PurchaseOrderId == id, cancellationToken);
        return order is not null && CanAccess(order.UserId) ? order : null;
    }

    private async Task<PaymentTransaction?> FindOwnedPayPalPaymentAsync(
        string orderId,
        CancellationToken cancellationToken)
    {
        var payment = await context.PaymentTransactions
            .Include(item => item.PurchaseOrder)
            .ThenInclude(order => order.Details)
            .FirstOrDefaultAsync(item => item.PayPalOrderId == orderId, cancellationToken);
        return payment is not null && CanAccess(payment.PurchaseOrder.UserId) ? payment : null;
    }

    private bool CanAccess(string ownerId) =>
        User.IsInRole("Administrador") ||
        string.Equals(ownerId, userManager.GetUserId(User), StringComparison.Ordinal);

    private async Task MarkPaymentAsPaidAsync(
        PaymentTransaction payment,
        CancellationToken cancellationToken)
    {
        if (payment.Status == "Paid")
        {
            return;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        foreach (var detail in payment.PurchaseOrder.Details)
        {
            var stock = await context.FlightStocks
                .SingleOrDefaultAsync(item => item.FlightStockId == detail.FlightStockId, cancellationToken);
            if (stock is null || stock.Stock < detail.Quantity)
            {
                payment.Status = "Failed";
                payment.PurchaseOrder.Status = "StockUnavailable";
                payment.GatewayResponseSanitized = "Pago recibido, pero el stock requiere revisión manual.";
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            stock.Stock -= detail.Quantity;
        }

        payment.Status = "Paid";
        payment.ConfirmedAt = DateTime.UtcNow;
        payment.PurchaseOrder.Status = "Paid";
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static PaymentTransaction NewPayment(PurchaseOrder order, string provider) => new()
    {
        PurchaseOrderId = order.PurchaseOrderId,
        Provider = provider,
        ClientTransactionId =
            $"{DateTime.UtcNow.ToString("yyMMddHHmmssfff", CultureInfo.InvariantCulture)}-{Guid.NewGuid():N}"[..32],
        AmountInCents = CommerceCalculations.ToCents(order.Total),
        Status = "Pending"
    };

    private static string NormalizeGatewayStatus(string status) =>
        status.ToUpperInvariant() switch
        {
            "CREATED" or "SAVED" or "APPROVED" or "VOIDED" => status.ToUpperInvariant(),
            _ => "Pending"
        };
}
