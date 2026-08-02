using AirportApp.Data;
using AirportApp.Models.AirportServices;
using AirportApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Repositories;

public sealed class AirportServiceRepository(ApplicationDbContext context)
    : IAirportServiceRepository
{
    public async Task<IReadOnlyList<AirportService>> GetActiveByAirportAsync(
        short airportId,
        CancellationToken cancellationToken = default) =>
        await context.AirportServices
            .AsNoTracking()
            .Include(service => service.Airport)
            .Where(service => service.AirportId == airportId && service.IsActive)
            .OrderBy(service => service.Name)
            .ToListAsync(cancellationToken);

    public async Task<AirportService?> GetByIdAsync(
        int airportServiceId,
        bool tracking = false,
        CancellationToken cancellationToken = default)
    {
        var query = context.AirportServices
            .Include(service => service.Airport)
            .Where(service => service.AirportServiceId == airportServiceId);

        return await (tracking ? query : query.AsNoTracking())
            .SingleOrDefaultAsync(cancellationToken);
    }
}
