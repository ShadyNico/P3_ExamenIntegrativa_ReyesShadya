using NpgsqlTypes;

namespace AirportApp.Models.Domain;

public sealed class Airport
{
    public short AirportId { get; set; }
    public string? Iata { get; set; }
    public string Icao { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public AirportGeo? Geo { get; set; }
    public AirportReachable? Reachability { get; set; }
    public ICollection<Airline> BasedAirlines { get; } = [];
    public ICollection<Flight> DepartingFlights { get; } = [];
    public ICollection<Flight> ArrivingFlights { get; } = [];
    public ICollection<FlightSchedule> DepartingSchedules { get; } = [];
    public ICollection<FlightSchedule> ArrivingSchedules { get; } = [];
}

public sealed class AirportGeo
{
    public short AirportId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Country { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public byte[] Geolocation { get; set; } = [];
    public NpgsqlPoint Location { get; private set; }

    public Airport Airport { get; set; } = null!;
}

public sealed class AirportReachable
{
    public short AirportId { get; set; }
    public int? Hops { get; set; }
    public Airport Airport { get; set; } = null!;
}
