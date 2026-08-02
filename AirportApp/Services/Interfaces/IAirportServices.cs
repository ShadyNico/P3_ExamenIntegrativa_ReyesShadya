using AirportApp.Models.ViewModels;

namespace AirportApp.Services.Interfaces;

public interface IPricingService
{
    PricingBreakdown Calculate(decimal unitPrice, int quantity);
}

public interface IAvailabilityService
{
    Task<IReadOnlyList<AvailabilitySlotViewModel>> SearchAsync(
        short airportId,
        int airportServiceId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task<bool> HasCapacityAsync(
        int serviceAvailabilityId,
        int quantity,
        CancellationToken cancellationToken = default);
}

public interface IReservationService
{
    Task<int> CreatePendingAsync(
        ReservationCreateViewModel model,
        string userId,
        CancellationToken cancellationToken = default);

    Task CancelAsync(
        int reservationId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);
}

public interface IPaymentService
{
    Task<PaymentProcessingResult> ProcessAsync(
        PaymentViewModel model,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);
}

public interface IServiceGatewayPaymentService
{
    Task<GatewayPaymentStartResult> StartPayPalAsync(
        int orderId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<GatewayPaymentStartResult> StartPayPhoneAsync(
        int orderId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

    Task<PaymentProcessingResult> CapturePayPalAsync(
        string paypalOrderId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default);

}

public sealed record PricingBreakdown(
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal,
    decimal Tax,
    decimal Total);

public sealed record PaymentProcessingResult(
    int PaymentId,
    bool Approved,
    string Message,
    bool IsTerminal = true);

public sealed record GatewayPaymentStartResult(
    int PaymentId,
    string GatewayOrderId,
    string RedirectUrl);
