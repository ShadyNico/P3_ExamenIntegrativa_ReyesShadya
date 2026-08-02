using System.ComponentModel.DataAnnotations;

namespace AirportApp.Models.ViewModels;

public sealed class FlightFormViewModel : IValidatableObject
{
    public int FlightId { get; set; }

    [Required, StringLength(8)]
    [Display(Name = "Número de vuelo programado")]
    public string FlightNo { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Salida")]
    public DateTime Departure { get; set; }

    [Required]
    [Display(Name = "Llegada")]
    public DateTime Arrival { get; set; }

    [Range(1, int.MaxValue)]
    [Display(Name = "Avión")]
    public int AirplaneId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Arrival <= Departure)
        {
            yield return new ValidationResult(
                "La llegada debe ser posterior a la salida.",
                [nameof(Arrival)]);
        }
    }
}

public sealed class BookingFormViewModel
{
    public int BookingId { get; set; }

    [Range(1, int.MaxValue)]
    [Display(Name = "ID del vuelo")]
    public int FlightId { get; set; }

    [Range(1, int.MaxValue)]
    [Display(Name = "ID del pasajero")]
    public int PassengerId { get; set; }

    [StringLength(4)]
    [Display(Name = "Asiento")]
    public string? Seat { get; set; }

    [Range(typeof(decimal), "0.01", "99999999.99")]
    [Display(Name = "Precio")]
    public decimal Price { get; set; }
}
