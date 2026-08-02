using System.ComponentModel.DataAnnotations;

namespace AirportApp.Models.Domain;

public sealed class Passenger
{
    public int PassengerId { get; set; }

    [Required, StringLength(9, MinimumLength = 5)]
    public string PassportNo { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    public PassengerDetails? Details { get; set; }
    public ICollection<Booking> Bookings { get; } = [];
}

public sealed class PassengerDetails
{
    public int PassengerId { get; set; }
    public DateOnly BirthDate { get; set; }
    public string? Sex { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public short Zip { get; set; }
    public string Country { get; set; } = string.Empty;
    public string? EmailAddress { get; set; }
    public string? TelephoneNo { get; set; }

    public Passenger Passenger { get; set; } = null!;
}

public sealed class Booking
{
    public int BookingId { get; set; }
    public int FlightId { get; set; }
    public string? Seat { get; set; }
    public int PassengerId { get; set; }
    public decimal Price { get; set; }

    public Flight Flight { get; set; } = null!;
    public Passenger Passenger { get; set; } = null!;
}
