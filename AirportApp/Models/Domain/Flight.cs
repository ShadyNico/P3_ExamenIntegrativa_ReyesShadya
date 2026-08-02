namespace AirportApp.Models.Domain;

public sealed class FlightSchedule
{
    public string FlightNo { get; set; } = string.Empty;
    public short FromAirportId { get; set; }
    public short ToAirportId { get; set; }
    public TimeOnly Departure { get; set; }
    public TimeOnly Arrival { get; set; }
    public short AirlineId { get; set; }
    public bool? Monday { get; set; }
    public bool? Tuesday { get; set; }
    public bool? Wednesday { get; set; }
    public bool? Thursday { get; set; }
    public bool? Friday { get; set; }
    public bool? Saturday { get; set; }
    public bool? Sunday { get; set; }

    public Airport FromAirport { get; set; } = null!;
    public Airport ToAirport { get; set; } = null!;
    public Airline Airline { get; set; } = null!;
    public ICollection<Flight> Flights { get; } = [];
}

public sealed class Flight
{
    public int FlightId { get; set; }
    public string FlightNo { get; set; } = string.Empty;
    public short FromAirportId { get; set; }
    public short ToAirportId { get; set; }
    public DateTime Departure { get; set; }
    public DateTime Arrival { get; set; }
    public short AirlineId { get; set; }
    public int AirplaneId { get; set; }

    public FlightSchedule Schedule { get; set; } = null!;
    public Airport FromAirport { get; set; } = null!;
    public Airport ToAirport { get; set; } = null!;
    public Airline Airline { get; set; } = null!;
    public Airplane Airplane { get; set; } = null!;
    public ICollection<Booking> Bookings { get; } = [];
    public ICollection<FlightLog> LogEntries { get; } = [];

    public TimeSpan Duration => Arrival - Departure;
}

public sealed class FlightLog
{
    public long FlightLogId { get; set; }
    public DateTime LogDate { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int FlightId { get; set; }
    public string FlightNoOld { get; set; } = string.Empty;
    public string FlightNoNew { get; set; } = string.Empty;
    public short FromOld { get; set; }
    public short ToOld { get; set; }
    public short FromNew { get; set; }
    public short ToNew { get; set; }
    public DateTime DepartureOld { get; set; }
    public DateTime ArrivalOld { get; set; }
    public DateTime DepartureNew { get; set; }
    public DateTime ArrivalNew { get; set; }
    public int AirplaneIdOld { get; set; }
    public int AirplaneIdNew { get; set; }
    public short AirlineIdOld { get; set; }
    public short AirlineIdNew { get; set; }
    public string? Comment { get; set; }

    public Flight Flight { get; set; } = null!;
}
