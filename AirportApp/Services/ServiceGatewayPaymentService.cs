using System.Data;
using System.Security.Cryptography;
using AirportApp.Data;
using AirportApp.Models.AirportServices;
using AirportApp.Services.Interfaces;
using AirportApp.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Services;

public sealed class ServiceGatewayPaymentService(
    ApplicationDbContext context,
    PayPalService payPalService,
    PayPhoneApiLinkService payPhoneService,
    ILogger<ServiceGatewayPaymentService> logger) : IServiceGatewayPaymentService
{
    public async Task<GatewayPaymentStartResult> StartPayPalAsync(
        int orderId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var order = await FindOwnedOrderAsync(orderId, userId, isAdministrator, cancellationToken);
        PaymentRules.EnsureCanProcess(order);

        var existing = order.Payments.FirstOrDefault(payment =>
            payment.PaymentMethod == SimulatedPaymentMethod.PayPalSandbox &&
            payment.PaymentStatus == SimulatedPaymentStatus.Pending &&
            !string.IsNullOrWhiteSpace(payment.GatewayOrderId));
        if (existing is not null)
        {
            return new GatewayPaymentStartResult(
                existing.PaymentId,
                existing.GatewayOrderId!,
                existing.GatewayPaymentUrl ?? string.Empty);
        }

        var result = await payPalService.CreateOrderAsync(
            order.Total,
            $"Servicio {order.OrderNumber}",
            includeRedirectUrls: false,
            cancellationToken);

        var payment = NewPayment(order, SimulatedPaymentMethod.PayPalSandbox, "PPS");
        payment.GatewayOrderId = result.OrderId;
        payment.GatewayPaymentUrl = result.ApprovalUrl;
        payment.GatewayResponseSanitized = $"PayPal creó la orden con estado {result.Status}.";
        context.ServicePayments.Add(payment);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PayPal Sandbox inició el pago {PaymentId} de la orden de servicio {OrderNumber}.",
            payment.PaymentId,
            order.OrderNumber);

        return new GatewayPaymentStartResult(
            payment.PaymentId,
            result.OrderId,
            result.ApprovalUrl);
    }

    public async Task<GatewayPaymentStartResult> StartPayPhoneAsync(
        int orderId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var order = await FindOwnedOrderAsync(orderId, userId, isAdministrator, cancellationToken);
        PaymentRules.EnsureCanProcess(order);

        var existing = order.Payments.FirstOrDefault(payment =>
            payment.PaymentMethod == SimulatedPaymentMethod.PayPhone &&
            payment.PaymentStatus == SimulatedPaymentStatus.Pending &&
            !string.IsNullOrWhiteSpace(payment.GatewayPaymentUrl));
        if (existing is not null)
        {
            return new GatewayPaymentStartResult(
                existing.PaymentId,
                existing.GatewayOrderId ?? existing.PaymentReference,
                existing.GatewayPaymentUrl!);
        }

        var clientTransactionId = NewPayPhoneReference();
        var paymentUrl = await payPhoneService.CreatePaymentLinkAsync(
            order.Subtotal,
            order.Tax,
            order.Total,
            clientTransactionId,
            $"Servicio {order.OrderNumber}",
            cancellationToken);

        var payment = NewPayment(order, SimulatedPaymentMethod.PayPhone, "PPH");
        payment.PaymentReference = clientTransactionId;
        payment.GatewayOrderId = clientTransactionId;
        payment.GatewayPaymentUrl = paymentUrl;
        payment.GatewayResponseSanitized = "PayPhone API Links creó el enlace de pago.";
        context.ServicePayments.Add(payment);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "PayPhone API Links creó el pago {PaymentId} de la orden de servicio {OrderNumber}.",
            payment.PaymentId,
            order.OrderNumber);

        return new GatewayPaymentStartResult(
            payment.PaymentId,
            clientTransactionId,
            paymentUrl);
    }

    public async Task<PaymentProcessingResult> CapturePayPalAsync(
        string paypalOrderId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var payment = await GatewayPaymentQuery()
            .SingleOrDefaultAsync(
                item => item.PaymentMethod == SimulatedPaymentMethod.PayPalSandbox &&
                        item.GatewayOrderId == paypalOrderId,
                cancellationToken);
        if (payment is null || !CanAccess(payment.Order.Reservation.UserId, userId, isAdministrator))
        {
            throw new KeyNotFoundException("Pago no encontrado.");
        }

        if (payment.PaymentStatus == SimulatedPaymentStatus.Approved)
        {
            return new PaymentProcessingResult(payment.PaymentId, true, "El pago ya estaba confirmado.");
        }

        var capture = await payPalService.CaptureOrderAsync(paypalOrderId, cancellationToken);
        if (!capture.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase))
        {
            var terminal = capture.Status.Equals("DECLINED", StringComparison.OrdinalIgnoreCase) ||
                           capture.Status.Equals("DENIED", StringComparison.OrdinalIgnoreCase) ||
                           capture.Status.Equals("VOIDED", StringComparison.OrdinalIgnoreCase);
            return await SaveNonApprovedStatusAsync(
                payment,
                terminal ? SimulatedPaymentStatus.Rejected : SimulatedPaymentStatus.Pending,
                $"PayPal devolvió el estado {capture.Status}.",
                capture.CaptureId,
                cancellationToken);
        }

        return await FinalizeApprovedAsync(
            payment.PaymentId,
            capture.CaptureId,
            authorizationCode: null,
            lastFourDigits: null,
            expectedAmountInCents: null,
            "PayPal confirmó y capturó el pago.",
            cancellationToken);
    }

    private async Task<ServiceOrder> FindOwnedOrderAsync(
        int orderId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken)
    {
        var order = await context.ServiceOrders
            .Include(item => item.Reservation)
            .ThenInclude(item => item.ServiceAvailability)
            .Include(item => item.Payments)
            .SingleOrDefaultAsync(item => item.OrderId == orderId, cancellationToken);
        if (order is null || !CanAccess(order.Reservation.UserId, userId, isAdministrator))
        {
            throw new KeyNotFoundException("Orden no encontrada.");
        }

        return order;
    }

    private IQueryable<ServicePayment> GatewayPaymentQuery() =>
        context.ServicePayments
            .Include(item => item.Order)
            .ThenInclude(item => item.Payments)
            .Include(item => item.Order)
            .ThenInclude(item => item.Reservation)
            .ThenInclude(item => item.ServiceAvailability);

    private async Task<PaymentProcessingResult> FinalizeApprovedAsync(
        int paymentId,
        string? gatewayTransactionId,
        string? authorizationCode,
        string? lastFourDigits,
        int? expectedAmountInCents,
        string gatewayMessage,
        CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var payment = await GatewayPaymentQuery()
            .SingleAsync(item => item.PaymentId == paymentId, cancellationToken);
        if (payment.PaymentStatus == SimulatedPaymentStatus.Approved)
        {
            await transaction.CommitAsync(cancellationToken);
            return new PaymentProcessingResult(payment.PaymentId, true, "El pago ya estaba confirmado.");
        }

        payment.GatewayTransactionId = NullIfEmpty(gatewayTransactionId);
        payment.AuthorizationCode = Truncate(authorizationCode, 20);
        payment.CardLastFourDigits = lastFourDigits;
        payment.GatewayResponseSanitized = gatewayMessage;

        if (expectedAmountInCents is not null &&
            expectedAmountInCents != CommerceCalculations.ToCents(payment.Amount))
        {
            payment.PaymentStatus = SimulatedPaymentStatus.ReviewRequired;
            payment.RejectionReason = "El valor confirmado por la pasarela no coincide con la orden.";
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PaymentProcessingResult(payment.PaymentId, false, payment.RejectionReason);
        }

        try
        {
            PaymentRules.ApplyApprovedPayment(payment.Order, payment);
        }
        catch (InvalidOperationException exception)
        {
            payment.PaymentStatus = SimulatedPaymentStatus.ReviewRequired;
            payment.RejectionReason =
                $"La pasarela aprobó el pago, pero la reserva requiere revisión: {exception.Message}";
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogError(
                exception,
                "El pago externo {PaymentId} fue aprobado, pero no pudo confirmar la reserva.",
                payment.PaymentId);
            return new PaymentProcessingResult(payment.PaymentId, false, payment.RejectionReason);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PaymentProcessingResult(
            payment.PaymentId,
            true,
            "Pago aprobado y reserva confirmada.");
    }

    private async Task<PaymentProcessingResult> SaveNonApprovedStatusAsync(
        ServicePayment payment,
        SimulatedPaymentStatus status,
        string message,
        string? gatewayTransactionId,
        CancellationToken cancellationToken)
    {
        payment.PaymentStatus = status;
        payment.RejectionReason = Truncate(message, 500);
        payment.GatewayTransactionId = NullIfEmpty(gatewayTransactionId);
        payment.GatewayResponseSanitized = Truncate(message, 4000);
        await context.SaveChangesAsync(cancellationToken);
        return new PaymentProcessingResult(payment.PaymentId, false, message);
    }

    private static ServicePayment NewPayment(
        ServiceOrder order,
        SimulatedPaymentMethod method,
        string prefix) => new()
    {
        OrderId = order.OrderId,
        PaymentReference = NewReference(prefix),
        PaymentMethod = method,
        Amount = order.Total,
        PaymentStatus = SimulatedPaymentStatus.Pending
    };

    private static string NewReference(string prefix) =>
        $"{prefix}-{DateTime.UtcNow:yyMMddHHmmss}-{Guid.NewGuid():N}"[..36].ToUpperInvariant();

    private static string NewPayPhoneReference() =>
        $"SP{Convert.ToHexString(RandomNumberGenerator.GetBytes(7))}"[..15];

    private static bool CanAccess(string ownerId, string userId, bool isAdministrator) =>
        isAdministrator || string.Equals(ownerId, userId, StringComparison.Ordinal);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maximumLength ? value : value[..maximumLength];
}
