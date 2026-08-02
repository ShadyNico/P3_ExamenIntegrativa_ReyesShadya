using AirportApp.Data;
using AirportApp.Models.ViewModels;
using AirportApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Services;

public sealed class AvailabilityService(ApplicationDbContext context)
    : IAvailabilityService
{
    public async Task<IReadOnlyList<AvailabilitySlotViewModel>> SearchAsync(
        short airportId,
        int airportServiceId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        if (date < DateOnly.FromDateTime(DateTime.Today))
        {
            return [];
        }

        return await context.ServiceAvailabilities
            .AsNoTracking()
            .Where(slot =>
                slot.AirportServiceId == airportServiceId &&
                slot.AirportService.AirportId == airportId &&
                slot.AirportService.IsActive &&
                slot.AvailableDate == date &&
                slot.IsAvailable &&
                slot.ReservedCapacity < slot.MaximumCapacity)
            .OrderBy(slot => slot.StartTime)
            .Select(slot => new AvailabilitySlotViewModel
            {
                ServiceAvailabilityId = slot.ServiceAvailabilityId,
                AvailableDate = slot.AvailableDate,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                MaximumCapacity = slot.MaximumCapacity,
                ReservedCapacity = slot.ReservedCapacity,
                AvailableCapacity = slot.MaximumCapacity - slot.ReservedCapacity
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasCapacityAsync(
        int serviceAvailabilityId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            return false;
        }

        return await context.ServiceAvailabilities
            .AsNoTracking()
            .AnyAsync(slot =>
                slot.ServiceAvailabilityId == serviceAvailabilityId &&
                slot.IsAvailable &&
                slot.AirportService.IsActive &&
                slot.AvailableDate >= DateOnly.FromDateTime(DateTime.Today) &&
                slot.MaximumCapacity - slot.ReservedCapacity >= quantity,
                cancellationToken);
    }

    public static bool HasCapacity(int maximumCapacity, int reservedCapacity, int quantity) =>
        maximumCapacity > 0 &&
        reservedCapacity >= 0 &&
        quantity > 0 &&
        reservedCapacity + quantity <= maximumCapacity;
}
