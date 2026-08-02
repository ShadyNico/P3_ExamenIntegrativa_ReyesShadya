using System.ComponentModel.DataAnnotations;
using AirportApp.Models.AirportServices;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AirportApp.Models.ViewModels;

public sealed class AirportSelectionViewModel
{
    public short AirportId { get; init; }
    public string Iata { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int ActiveServiceCount { get; init; }
}

public sealed class AirportDetailsViewModel
{
    public short AirportId { get; init; }
    public string Iata { get; init; } = string.Empty;
    public string Icao { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<ServiceSelectionViewModel> Services { get; init; } = [];
}

public sealed class ServiceSelectionViewModel
{
    public int AirportServiceId { get; init; }
    public short AirportId { get; init; }
    public string AirportName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public AirportServiceType ServiceType { get; init; }
    public ServicePriceType PriceType { get; init; }
    public decimal BasePrice { get; init; }
    public int AvailableSlotCount { get; init; }
}

public sealed class AvailabilitySlotViewModel
{
    public int ServiceAvailabilityId { get; init; }
    public DateOnly AvailableDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public int MaximumCapacity { get; init; }
    public int ReservedCapacity { get; init; }
    public int AvailableCapacity { get; init; }
}

public sealed class AvailabilitySearchViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Selecciona un aeropuerto.")]
    [Display(Name = "Aeropuerto")]
    public short? AirportId { get; set; }

    [Required(ErrorMessage = "Selecciona un servicio.")]
    [Display(Name = "Servicio")]
    public int? AirportServiceId { get; set; }

    [Required(ErrorMessage = "Selecciona una fecha.")]
    [DataType(DataType.Date)]
    [Display(Name = "Fecha")]
    public DateOnly? Date { get; set; }

    public IReadOnlyList<SelectListItem> Airports { get; set; } = [];
    public IReadOnlyList<SelectListItem> Services { get; set; } = [];
    public IReadOnlyList<AvailabilitySlotViewModel> Results { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Date is not null && Date < DateOnly.FromDateTime(DateTime.Today))
        {
            yield return new ValidationResult(
                "La fecha no puede estar en el pasado.",
                [nameof(Date)]);
        }
    }
}

public sealed class AirportServiceFormViewModel
{
    public int AirportServiceId { get; set; }

    [Required(ErrorMessage = "Selecciona un aeropuerto.")]
    [Display(Name = "Aeropuerto")]
    public short AirportId { get; set; }

    [Required, StringLength(120)]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    [Display(Name = "Descripción")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Tipo de servicio")]
    public AirportServiceType ServiceType { get; set; }

    [Range(typeof(decimal), "0.01", "999999.99")]
    [Display(Name = "Precio base")]
    public decimal BasePrice { get; set; }

    [Required]
    [Display(Name = "Tipo de precio")]
    public ServicePriceType PriceType { get; set; }

    [Display(Name = "Activo")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<SelectListItem> Airports { get; set; } = [];
}

public sealed class ReservationCreateViewModel
{
    [Required]
    public int AirportServiceId { get; set; }

    [Required(ErrorMessage = "Selecciona un horario disponible.")]
    [Display(Name = "Horario")]
    public int ServiceAvailabilityId { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Nombre completo")]
    public string CustomerName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(320)]
    [Display(Name = "Correo electrónico")]
    public string CustomerEmail { get; set; } = string.Empty;

    [Required, Phone, StringLength(30)]
    [Display(Name = "Teléfono")]
    public string CustomerPhone { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    [Display(Name = "Personas o unidades")]
    public int Quantity { get; set; } = 1;

    public string AirportName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string PriceTypeLabel { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public IReadOnlyList<SelectListItem> AvailableSlots { get; set; } = [];
}

public sealed class ReservationSummaryViewModel
{
    public int ServiceReservationId { get; init; }
    public int OrderId { get; init; }
    public string ReservationCode { get; init; } = string.Empty;
    public string OrderNumber { get; init; } = string.Empty;
    public string AirportName { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public DateOnly ReservationDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Subtotal { get; init; }
    public decimal Tax { get; init; }
    public decimal Total { get; init; }
    public ServiceReservationStatus ReservationStatus { get; init; }
    public ServiceOrderStatus OrderStatus { get; init; }
}

public sealed class CheckoutViewModel
{
    public ReservationSummaryViewModel Summary { get; init; } = new();
    public string Notice { get; init; } =
        "El precio se recalculó en el servidor. Revisa el resumen antes de pagar.";
}

public sealed class PaymentViewModel : IValidatableObject
{
    [Required]
    public int OrderId { get; set; }

    [Required]
    [Display(Name = "Método de pago")]
    public SimulatedPaymentMethod PaymentMethod { get; set; }

    [StringLength(150)]
    [Display(Name = "Nombre del titular")]
    public string? CardholderName { get; set; }

    [StringLength(25)]
    [Display(Name = "Número de tarjeta")]
    public string? CardNumber { get; set; }

    [StringLength(4, MinimumLength = 3)]
    [Display(Name = "CVV")]
    public string? Cvv { get; set; }

    [Range(1, 12)]
    [Display(Name = "Mes")]
    public int? ExpirationMonth { get; set; }

    [Range(2026, 2100)]
    [Display(Name = "Año")]
    public int? ExpirationYear { get; set; }

    [EmailAddress]
    [Display(Name = "Correo PayPal")]
    public string? PayPalEmail { get; set; }

    public CheckoutViewModel? Checkout { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PaymentMethod is SimulatedPaymentMethod.PayPalSandbox or
            SimulatedPaymentMethod.PayPhone)
        {
            yield break;
        }

        if (PaymentMethod == SimulatedPaymentMethod.SimulatedPayPal)
        {
            if (string.IsNullOrWhiteSpace(PayPalEmail))
            {
                yield return new ValidationResult(
                    "Ingresa el correo de PayPal simulado.",
                    [nameof(PayPalEmail)]);
            }

            yield break;
        }

        if (string.IsNullOrWhiteSpace(CardholderName))
        {
            yield return new ValidationResult(
                "Ingresa el nombre del titular.",
                [nameof(CardholderName)]);
        }

        if (string.IsNullOrWhiteSpace(CardNumber))
        {
            yield return new ValidationResult(
                "Ingresa el número de tarjeta.",
                [nameof(CardNumber)]);
        }

        if (string.IsNullOrWhiteSpace(Cvv))
        {
            yield return new ValidationResult("Ingresa el CVV.", [nameof(Cvv)]);
        }

        if (ExpirationMonth is null || ExpirationYear is null)
        {
            yield return new ValidationResult(
                "Ingresa la fecha de expiración.",
                [nameof(ExpirationMonth), nameof(ExpirationYear)]);
        }
    }
}

public sealed class ReceiptViewModel
{
    public int PaymentId { get; init; }
    public int OrderId { get; init; }
    public string ReservationCode { get; init; } = string.Empty;
    public string OrderNumber { get; init; } = string.Empty;
    public string PaymentReference { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string CustomerPhone { get; init; } = string.Empty;
    public string AirportName { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public DateOnly ReservationDate { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Subtotal { get; init; }
    public decimal Tax { get; init; }
    public decimal Total { get; init; }
    public SimulatedPaymentMethod PaymentMethod { get; init; }
    public SimulatedPaymentStatus PaymentStatus { get; init; }
    public ServiceReservationStatus ReservationStatus { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? CardLastFourDigits { get; init; }
    public string? AuthorizationCode { get; init; }
    public string? GatewayTransactionId { get; init; }
}
