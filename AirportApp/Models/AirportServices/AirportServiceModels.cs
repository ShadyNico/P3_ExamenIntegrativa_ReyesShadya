using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AirportApp.Models.AirportServices;

public enum AirportServiceType
{
    VipLounge,
    Parking,
    InternalTransport,
    PriorityAssistance,
    Companion,
    TerminalTransfer
}

public enum ServicePriceType
{
    PerPerson,
    PerHour,
    PerDay,
    PerVehicle,
    Fixed
}

public enum ServiceReservationStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Completed
}

public enum ServiceOrderStatus
{
    Pending,
    Paid,
    Cancelled,
    Refunded
}

public enum SimulatedPaymentStatus
{
    Pending,
    Approved,
    Rejected,
    ReviewRequired,
    Refunded
}

public enum SimulatedPaymentMethod
{
    [Display(Name = "Tarjeta de crédito")]
    CreditCard,
    [Display(Name = "Tarjeta de débito")]
    DebitCard,
    [Display(Name = "PayPal simulado")]
    SimulatedPayPal,
    [Display(Name = "PayPal Sandbox")]
    PayPalSandbox,
    [Display(Name = "PayPhone")]
    PayPhone
}

public sealed class AirportReference
{
    public short AirportId { get; set; }
    public string? Iata { get; set; }
    public string Icao { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ICollection<AirportService> Services { get; set; } = [];
}

public sealed class AirportService
{
    public int AirportServiceId { get; set; }
    public short AirportId { get; set; }
    public AirportReference Airport { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AirportServiceType ServiceType { get; set; }
    public decimal BasePrice { get; set; }
    public ServicePriceType PriceType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ServiceAvailability> Availabilities { get; set; } = [];
    public ICollection<ServiceReservation> Reservations { get; set; } = [];
}

public sealed class ServiceAvailability
{
    public int ServiceAvailabilityId { get; set; }
    public int AirportServiceId { get; set; }
    public AirportService AirportService { get; set; } = null!;
    public DateOnly AvailableDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int MaximumCapacity { get; set; }
    public int ReservedCapacity { get; set; }
    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public uint Version { get; set; }

    [NotMapped]
    public int AvailableCapacity => Math.Max(0, MaximumCapacity - ReservedCapacity);

    public ICollection<ServiceReservation> Reservations { get; set; } = [];
}

public sealed class ServiceReservation
{
    public int ServiceReservationId { get; set; }
    public string ReservationCode { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int AirportServiceId { get; set; }
    public AirportService AirportService { get; set; } = null!;
    public int ServiceAvailabilityId { get; set; }
    public ServiceAvailability ServiceAvailability { get; set; } = null!;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public DateOnly ReservationDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public ServiceReservationStatus ReservationStatus { get; set; } = ServiceReservationStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ServiceOrder? Order { get; set; }
}

public sealed class ServiceOrder
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int ServiceReservationId { get; set; }
    public ServiceReservation Reservation { get; set; } = null!;
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public ServiceOrderStatus OrderStatus { get; set; } = ServiceOrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ServicePayment> Payments { get; set; } = [];
}

public sealed class ServicePayment
{
    public int PaymentId { get; set; }
    public int OrderId { get; set; }
    public ServiceOrder Order { get; set; } = null!;
    public string PaymentReference { get; set; } = string.Empty;
    public SimulatedPaymentMethod PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public SimulatedPaymentStatus PaymentStatus { get; set; } = SimulatedPaymentStatus.Pending;
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string? CardLastFourDigits { get; set; }
    public string? AuthorizationCode { get; set; }
    public string? RejectionReason { get; set; }
    public string? GatewayOrderId { get; set; }
    public string? GatewayTransactionId { get; set; }
    public string? GatewayPaymentUrl { get; set; }
    public string? GatewayResponseSanitized { get; set; }
}
