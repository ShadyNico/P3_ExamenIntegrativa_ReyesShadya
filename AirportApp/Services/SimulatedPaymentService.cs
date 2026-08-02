using System.Data;
using System.Security.Cryptography;
using AirportApp.Data;
using AirportApp.Models.AirportServices;
using AirportApp.Models.ViewModels;
using AirportApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Services;

public sealed class SimulatedPaymentService(
    ApplicationDbContext context,
    ILogger<SimulatedPaymentService> logger) : IPaymentService
{
    private const string SimulatedDeclineCard = "4000000000000002";

    public async Task<PaymentProcessingResult> ProcessAsync(
        PaymentViewModel model,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        if (model.PaymentMethod is SimulatedPaymentMethod.PayPalSandbox or
            SimulatedPaymentMethod.PayPhone)
        {
            throw new InvalidOperationException(
                "La pasarela seleccionada debe iniciarse desde su botón de pago seguro.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var order = await context.ServiceOrders
            .Include(item => item.Reservation)
            .ThenInclude(item => item.ServiceAvailability)
            .Include(item => item.Payments)
            .SingleOrDefaultAsync(item => item.OrderId == model.OrderId, cancellationToken);

        if (order is null ||
            (!isAdministrator && !string.Equals(order.Reservation.UserId, userId, StringComparison.Ordinal)))
        {
            throw new KeyNotFoundException("Orden no encontrada.");
        }

        PaymentRules.EnsureCanProcess(order);

        var validation = ValidatePayment(model);
        var payment = new ServicePayment
        {
            OrderId = order.OrderId,
            PaymentReference = NewReference(),
            PaymentMethod = model.PaymentMethod,
            Amount = order.Total,
            PaymentStatus = SimulatedPaymentStatus.Pending,
            CardLastFourDigits = validation.LastFourDigits
        };

        if (!validation.IsValid || validation.ShouldDecline)
        {
            payment.PaymentStatus = SimulatedPaymentStatus.Rejected;
            payment.RejectionReason = validation.Message;
            context.ServicePayments.Add(payment);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogWarning(
                "Pago simulado rechazado para la orden {OrderNumber}: {Reason}",
                order.OrderNumber,
                validation.Message);

            return new PaymentProcessingResult(payment.PaymentId, false, validation.Message);
        }

        var availability = order.Reservation.ServiceAvailability;
        if (!AvailabilityService.HasCapacity(
                availability.MaximumCapacity,
                availability.ReservedCapacity,
                order.Reservation.Quantity))
        {
            payment.PaymentStatus = SimulatedPaymentStatus.Rejected;
            payment.RejectionReason = "La capacidad se agotó antes de confirmar el pago.";
            context.ServicePayments.Add(payment);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new PaymentProcessingResult(payment.PaymentId, false, payment.RejectionReason);
        }

        payment.AuthorizationCode = Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
        PaymentRules.ApplyApprovedPayment(order, payment);
        context.ServicePayments.Add(payment);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Pago simulado {PaymentReference} aprobado para {OrderNumber}.",
            payment.PaymentReference,
            order.OrderNumber);

        return new PaymentProcessingResult(
            payment.PaymentId,
            true,
            "Pago aprobado y reserva confirmada.");
    }

    private static PaymentValidation ValidatePayment(PaymentViewModel model)
    {
        if (model.PaymentMethod == SimulatedPaymentMethod.SimulatedPayPal)
        {
            var email = model.PayPalEmail?.Trim() ?? string.Empty;
            var paypalValid = email.Contains('@') && email.Contains('.', StringComparison.Ordinal);
            var decline = email.Contains("rechazar", StringComparison.OrdinalIgnoreCase) ||
                          email.Contains("reject", StringComparison.OrdinalIgnoreCase);
            return new PaymentValidation(
                paypalValid,
                decline,
                null,
                paypalValid ? (decline ? "PayPal simulado rechazó el pago." : "Pago válido.")
                    : "El correo de PayPal simulado no es válido.");
        }

        var digits = new string((model.CardNumber ?? string.Empty).Where(char.IsDigit).ToArray());
        var expirationValid = model.ExpirationMonth is >= 1 and <= 12 &&
                              model.ExpirationYear is not null &&
                              new DateOnly(model.ExpirationYear.Value, model.ExpirationMonth.Value, 1)
                                  .AddMonths(1) > DateOnly.FromDateTime(DateTime.Today);
        var cvvValid = model.Cvv is { Length: >= 3 and <= 4 } && model.Cvv.All(char.IsDigit);
        var cardValid = !string.IsNullOrWhiteSpace(model.CardholderName) &&
                    IsValidCardNumber(digits) &&
                    cvvValid &&
                    expirationValid;

        return new PaymentValidation(
            cardValid,
            cardValid && digits == SimulatedDeclineCard,
            digits.Length >= 4 ? digits[^4..] : null,
            !cardValid ? "Los datos de la tarjeta no son válidos."
                : digits == SimulatedDeclineCard ? "El emisor simulado rechazó la tarjeta."
                : "Pago válido.");
    }

    public static bool IsValidCardNumber(string digits)
    {
        if (digits.Length is < 12 or > 19 || !digits.All(char.IsDigit))
        {
            return false;
        }

        var sum = 0;
        var doubleDigit = false;
        for (var index = digits.Length - 1; index >= 0; index--)
        {
            var value = digits[index] - '0';
            if (doubleDigit)
            {
                value *= 2;
                if (value > 9)
                {
                    value -= 9;
                }
            }

            sum += value;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }

    private static string NewReference() =>
        $"PAY-{DateTime.UtcNow:yyMMddHHmmss}-{Guid.NewGuid():N}"[..32].ToUpperInvariant();

    private sealed record PaymentValidation(
        bool IsValid,
        bool ShouldDecline,
        string? LastFourDigits,
        string Message);
}

public static class PaymentRules
{
    public static void EnsureCanProcess(ServiceOrder order)
    {
        if (order.OrderStatus == ServiceOrderStatus.Paid ||
            order.Payments.Any(payment => payment.PaymentStatus == SimulatedPaymentStatus.Approved))
        {
            throw new InvalidOperationException("La orden ya fue pagada.");
        }

        if (order.OrderStatus != ServiceOrderStatus.Pending ||
            order.Reservation.ReservationStatus != ServiceReservationStatus.Pending)
        {
            throw new InvalidOperationException("La orden no admite pagos en su estado actual.");
        }
    }

    public static void ApplyApprovedPayment(ServiceOrder order, ServicePayment payment)
    {
        EnsureCanProcess(order);

        var availability = order.Reservation.ServiceAvailability;
        if (!AvailabilityService.HasCapacity(
                availability.MaximumCapacity,
                availability.ReservedCapacity,
                order.Reservation.Quantity))
        {
            throw new InvalidOperationException("No existe capacidad suficiente.");
        }

        availability.ReservedCapacity += order.Reservation.Quantity;
        payment.PaymentStatus = SimulatedPaymentStatus.Approved;
        order.OrderStatus = ServiceOrderStatus.Paid;
        order.Reservation.ReservationStatus = ServiceReservationStatus.Confirmed;
    }
}
