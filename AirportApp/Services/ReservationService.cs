using System.Data;
using AirportApp.Data;
using AirportApp.Models.AirportServices;
using AirportApp.Models.ViewModels;
using AirportApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Services;

public sealed class ReservationService(
    ApplicationDbContext context,
    IPricingService pricingService,
    ILogger<ReservationService> logger) : IReservationService
{
    public async Task<int> CreatePendingAsync(
        ReservationCreateViewModel model,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var slot = await context.ServiceAvailabilities
            .Include(item => item.AirportService)
            .SingleOrDefaultAsync(
                item => item.ServiceAvailabilityId == model.ServiceAvailabilityId,
                cancellationToken);

        if (slot is null)
        {
            throw new InvalidOperationException("El horario seleccionado no existe para el servicio.");
        }

        ReservationRules.EnsureCanReserve(
            slot,
            model.AirportServiceId,
            model.Quantity,
            DateOnly.FromDateTime(DateTime.Today));

        var price = pricingService.Calculate(slot.AirportService.BasePrice, model.Quantity);
        var reservation = new ServiceReservation
        {
            ReservationCode = NewCode("RSV"),
            UserId = userId,
            AirportServiceId = slot.AirportServiceId,
            ServiceAvailabilityId = slot.ServiceAvailabilityId,
            CustomerName = model.CustomerName.Trim(),
            CustomerEmail = model.CustomerEmail.Trim(),
            CustomerPhone = model.CustomerPhone.Trim(),
            ReservationDate = slot.AvailableDate,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            Quantity = model.Quantity,
            UnitPrice = price.UnitPrice,
            Subtotal = price.Subtotal,
            Tax = price.Tax,
            Total = price.Total,
            ReservationStatus = ServiceReservationStatus.Pending,
            Order = new ServiceOrder
            {
                OrderNumber = NewCode("ORD"),
                Subtotal = price.Subtotal,
                Tax = price.Tax,
                Total = price.Total,
                OrderStatus = ServiceOrderStatus.Pending
            }
        };

        context.ServiceReservations.Add(reservation);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Reserva de servicio {ReservationCode} creada para el usuario {UserId}.",
            reservation.ReservationCode,
            userId);

        return reservation.ServiceReservationId;
    }

    public async Task CancelAsync(
        int reservationId,
        string userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var reservation = await context.ServiceReservations
            .Include(item => item.Order)
            .SingleOrDefaultAsync(
                item => item.ServiceReservationId == reservationId,
                cancellationToken);

        if (reservation is null ||
            (!isAdministrator && !string.Equals(reservation.UserId, userId, StringComparison.Ordinal)))
        {
            throw new KeyNotFoundException("Reserva no encontrada.");
        }

        if (reservation.ReservationStatus is ServiceReservationStatus.Completed or
            ServiceReservationStatus.Confirmed)
        {
            throw new InvalidOperationException(
                "Una reserva confirmada o completada requiere un proceso de reembolso.");
        }

        reservation.ReservationStatus = ServiceReservationStatus.Cancelled;
        if (reservation.Order is not null)
        {
            reservation.Order.OrderStatus = ServiceOrderStatus.Cancelled;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string NewCode(string prefix) =>
        $"{prefix}-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
}

public static class ReservationRules
{
    public static void EnsureCanReserve(
        ServiceAvailability slot,
        int airportServiceId,
        int quantity,
        DateOnly today)
    {
        if (slot.AirportServiceId != airportServiceId)
        {
            throw new InvalidOperationException("El horario seleccionado no existe para el servicio.");
        }

        if (!slot.IsAvailable || !slot.AirportService.IsActive)
        {
            throw new InvalidOperationException("El servicio o el horario ya no está disponible.");
        }

        if (slot.AvailableDate < today)
        {
            throw new InvalidOperationException("No se permiten reservas en fechas pasadas.");
        }

        if (!AvailabilityService.HasCapacity(
                slot.MaximumCapacity,
                slot.ReservedCapacity,
                quantity))
        {
            throw new InvalidOperationException("La cantidad solicitada supera la capacidad disponible.");
        }
    }
}
