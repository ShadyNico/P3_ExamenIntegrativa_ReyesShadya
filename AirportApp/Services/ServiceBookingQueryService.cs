using AirportApp.Data;
using AirportApp.Models.AirportServices;
using AirportApp.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Services;

public sealed class ServiceBookingQueryService(ApplicationDbContext context)
{
    public async Task<ReservationSummaryViewModel?> GetReservationSummaryAsync(
        int reservationId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var reservation = await ReservationQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ServiceReservationId == reservationId,
                cancellationToken);

        return reservation is null || !CanAccess(reservation.UserId, userId, isAdministrator)
            ? null
            : ToSummary(reservation);
    }

    public async Task<CheckoutViewModel?> GetCheckoutAsync(
        int orderId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var reservation = await ReservationQuery()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Order!.OrderId == orderId, cancellationToken);

        return reservation is null || !CanAccess(reservation.UserId, userId, isAdministrator)
            ? null
            : new CheckoutViewModel { Summary = ToSummary(reservation) };
    }

    public async Task<ReceiptViewModel?> GetReceiptAsync(
        int paymentId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var payment = await context.ServicePayments
            .AsNoTracking()
            .Include(item => item.Order)
            .ThenInclude(item => item.Reservation)
            .ThenInclude(item => item.AirportService)
            .ThenInclude(item => item.Airport)
            .SingleOrDefaultAsync(item => item.PaymentId == paymentId, cancellationToken);

        if (payment is null ||
            !CanAccess(payment.Order.Reservation.UserId, userId, isAdministrator))
        {
            return null;
        }

        var reservation = payment.Order.Reservation;
        return new ReceiptViewModel
        {
            PaymentId = payment.PaymentId,
            OrderId = payment.OrderId,
            ReservationCode = reservation.ReservationCode,
            OrderNumber = payment.Order.OrderNumber,
            PaymentReference = payment.PaymentReference,
            CustomerName = reservation.CustomerName,
            CustomerEmail = reservation.CustomerEmail,
            CustomerPhone = reservation.CustomerPhone,
            AirportName = AirportServiceLabels.Airport(
                reservation.AirportService.Airport.Iata,
                reservation.AirportService.Airport.Name),
            ServiceName = reservation.AirportService.Name,
            ReservationDate = reservation.ReservationDate,
            StartTime = reservation.StartTime,
            EndTime = reservation.EndTime,
            Quantity = reservation.Quantity,
            UnitPrice = reservation.UnitPrice,
            Subtotal = reservation.Subtotal,
            Tax = reservation.Tax,
            Total = reservation.Total,
            PaymentMethod = payment.PaymentMethod,
            PaymentStatus = payment.PaymentStatus,
            ReservationStatus = reservation.ReservationStatus,
            CreatedAt = payment.TransactionDate,
            CardLastFourDigits = payment.CardLastFourDigits,
            AuthorizationCode = payment.AuthorizationCode,
            GatewayTransactionId = payment.GatewayTransactionId
        };
    }

    private IQueryable<ServiceReservation> ReservationQuery() =>
        context.ServiceReservations
            .Include(item => item.AirportService)
            .ThenInclude(item => item.Airport)
            .Include(item => item.Order);

    private static bool CanAccess(string ownerId, string userId, bool isAdministrator) =>
        isAdministrator || string.Equals(ownerId, userId, StringComparison.Ordinal);

    private static ReservationSummaryViewModel ToSummary(ServiceReservation reservation) => new()
    {
        ServiceReservationId = reservation.ServiceReservationId,
        OrderId = reservation.Order?.OrderId ?? 0,
        ReservationCode = reservation.ReservationCode,
        OrderNumber = reservation.Order?.OrderNumber ?? string.Empty,
        AirportName = AirportServiceLabels.Airport(
            reservation.AirportService.Airport.Iata,
            reservation.AirportService.Airport.Name),
        ServiceName = reservation.AirportService.Name,
        CustomerName = reservation.CustomerName,
        CustomerEmail = reservation.CustomerEmail,
        CustomerPhone = reservation.CustomerPhone,
        ReservationDate = reservation.ReservationDate,
        StartTime = reservation.StartTime,
        EndTime = reservation.EndTime,
        Quantity = reservation.Quantity,
        UnitPrice = reservation.UnitPrice,
        Subtotal = reservation.Subtotal,
        Tax = reservation.Tax,
        Total = reservation.Total,
        ReservationStatus = reservation.ReservationStatus,
        OrderStatus = reservation.Order?.OrderStatus ?? ServiceOrderStatus.Pending
    };
}
