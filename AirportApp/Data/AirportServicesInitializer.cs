using AirportApp.Models.AirportServices;
using AirportApp.Services;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Data;

public static class AirportServicesInitializer
{
    private const long AdvisoryLockId = 2_026_073_100_1;

    private static readonly string[] AirportCodes = ["UIO", "GYE", "MEC", "ESM"];

    private static readonly ServiceSeed[] ServiceSeeds =
    [
        new(AirportServiceType.VipLounge, "Sala VIP",
            "Acceso a sala exclusiva con zona de descanso, refrigerios y conectividad.",
            ServicePriceType.PerPerson, 35m, 30),
        new(AirportServiceType.Parking, "Estacionamiento",
            "Reserva anticipada de estacionamiento dentro del recinto aeroportuario.",
            ServicePriceType.PerDay, 12m, 120),
        new(AirportServiceType.InternalTransport, "Transporte interno",
            "Traslado interno asistido entre áreas operativas del aeropuerto.",
            ServicePriceType.PerPerson, 8m, 20),
        new(AirportServiceType.PriorityAssistance, "Asistencia prioritaria",
            "Atención prioritaria durante controles, orientación y embarque.",
            ServicePriceType.PerPerson, 18m, 15),
        new(AirportServiceType.Companion, "Acompañamiento",
            "Acompañamiento personalizado para pasajeros que requieren apoyo adicional.",
            ServicePriceType.PerPerson, 25m, 10),
        new(AirportServiceType.TerminalTransfer, "Traslado entre terminales",
            "Traslado coordinado de pasajeros y equipaje entre terminales.",
            ServicePriceType.PerPerson, 10m, 25)
    ];

    private static readonly (TimeOnly Start, TimeOnly End)[] Slots =
    [
        (new TimeOnly(6, 0), new TimeOnly(10, 0)),
        (new TimeOnly(10, 30), new TimeOnly(14, 30)),
        (new TimeOnly(15, 0), new TimeOnly(20, 0))
    ];

    public static async Task InitializeAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue("AirportServices:SeedEnabled", true))
        {
            return;
        }

        var context = services.GetRequiredService<ApplicationDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AirportServicesInitializer");

        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync(
                $"SELECT pg_advisory_lock({AdvisoryLockId})",
                cancellationToken);

            var airports = await context.AirportReferences
                .Where(airport => airport.Iata != null && AirportCodes.Contains(airport.Iata.Trim()))
                .OrderBy(airport => airport.AirportId)
                .ToListAsync(cancellationToken);

            if (airports.Count < AirportCodes.Length)
            {
                logger.LogWarning(
                    "Solo se encontraron {Count} de los {Expected} aeropuertos ecuatorianos solicitados.",
                    airports.Count,
                    AirportCodes.Length);
            }

            var existingServices = await context.AirportServices
                .Where(service => airports.Select(airport => airport.AirportId).Contains(service.AirportId))
                .ToListAsync(cancellationToken);

            foreach (var (airport, airportIndex) in airports.Select((value, index) => (value, index)))
            {
                foreach (var seed in ServiceSeeds)
                {
                    if (existingServices.Any(service =>
                            service.AirportId == airport.AirportId &&
                            service.ServiceType == seed.ServiceType))
                    {
                        continue;
                    }

                    var service = new AirportService
                    {
                        AirportId = airport.AirportId,
                        Name = seed.Name,
                        Description = seed.Description,
                        ServiceType = seed.ServiceType,
                        PriceType = seed.PriceType,
                        BasePrice = seed.BasePrice + airportIndex * 1.50m,
                        IsActive = true
                    };
                    context.AirportServices.Add(service);
                    existingServices.Add(service);
                }
            }

            await context.SaveChangesAsync(cancellationToken);

            var firstDate = DateOnly.FromDateTime(DateTime.Today);
            var finalDate = firstDate.AddDays(29);
            var serviceIds = existingServices.Select(service => service.AirportServiceId).ToArray();
            var existingSlots = await context.ServiceAvailabilities
                .Where(slot =>
                    serviceIds.Contains(slot.AirportServiceId) &&
                    slot.AvailableDate >= firstDate &&
                    slot.AvailableDate <= finalDate)
                .Select(slot => new
                {
                    slot.AirportServiceId,
                    slot.AvailableDate,
                    slot.StartTime,
                    slot.EndTime
                })
                .ToListAsync(cancellationToken);

            var existingKeys = existingSlots
                .Select(slot => (slot.AirportServiceId, slot.AvailableDate, slot.StartTime, slot.EndTime))
                .ToHashSet();

            foreach (var service in existingServices)
            {
                var seed = ServiceSeeds.Single(item => item.ServiceType == service.ServiceType);
                for (var day = 0; day < 30; day++)
                {
                    var date = firstDate.AddDays(day);
                    foreach (var slot in Slots)
                    {
                        if (!existingKeys.Add((service.AirportServiceId, date, slot.Start, slot.End)))
                        {
                            continue;
                        }

                        context.ServiceAvailabilities.Add(new ServiceAvailability
                        {
                            AirportServiceId = service.AirportServiceId,
                            AvailableDate = date,
                            StartTime = slot.Start,
                            EndTime = slot.End,
                            MaximumCapacity = seed.Capacity,
                            ReservedCapacity = 0,
                            IsAvailable = true
                        });
                    }
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Servicios aeroportuarios inicializados para {AirportCount} aeropuertos hasta {FinalDate}.",
                airports.Count,
                finalDate);
        }
        finally
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync(
                    $"SELECT pg_advisory_unlock({AdvisoryLockId})",
                    cancellationToken);
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
        }
    }

    private sealed record ServiceSeed(
        AirportServiceType ServiceType,
        string Name,
        string Description,
        ServicePriceType PriceType,
        decimal BasePrice,
        int Capacity);
}
