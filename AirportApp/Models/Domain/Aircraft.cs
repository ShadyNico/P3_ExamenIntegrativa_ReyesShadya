using System.ComponentModel.DataAnnotations;

namespace AirportApp.Models.Domain;

public sealed class Airline
{
    public short AirlineId { get; set; }

    [Required, StringLength(2, MinimumLength = 2)]
    public string Iata { get; set; } = string.Empty;

    [StringLength(30)]
    public string? AirlineName { get; set; }

    [Range(1, short.MaxValue)]
    public short BaseAirportId { get; set; }

    public Airport BaseAirport { get; set; } = null!;
    public ICollection<Airplane> Airplanes { get; } = [];
    public ICollection<FlightSchedule> FlightSchedules { get; } = [];
    public ICollection<Flight> Flights { get; } = [];
}

public sealed class AirplaneType
{
    public int TypeId { get; set; }
    public string? Identifier { get; set; }
    public string? Description { get; set; }
    public ICollection<Airplane> Airplanes { get; } = [];
}

public sealed class Airplane
{
    public int AirplaneId { get; set; }
    public int Capacity { get; set; }
    public int TypeId { get; set; }
    public short AirlineId { get; set; }

    public AirplaneType Type { get; set; } = null!;
    public Airline Airline { get; set; } = null!;
    public ICollection<Flight> Flights { get; } = [];
}
