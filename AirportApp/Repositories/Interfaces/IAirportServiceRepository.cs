using AirportApp.Models.AirportServices;

namespace AirportApp.Repositories.Interfaces;

public interface IAirportServiceRepository
{
    Task<IReadOnlyList<AirportService>> GetActiveByAirportAsync(
        short airportId,
        CancellationToken cancellationToken = default);

    Task<AirportService?> GetByIdAsync(
        int airportServiceId,
        bool tracking = false,
        CancellationToken cancellationToken = default);
}
