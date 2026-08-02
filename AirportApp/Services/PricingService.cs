using AirportApp.Services.Interfaces;

namespace AirportApp.Services;

public sealed class PricingService : IPricingService
{
    public const decimal TaxRate = 0.15m;

    public PricingBreakdown Calculate(decimal unitPrice, int quantity)
    {
        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        var subtotal = decimal.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero);
        var tax = decimal.Round(subtotal * TaxRate, 2, MidpointRounding.AwayFromZero);
        return new PricingBreakdown(unitPrice, quantity, subtotal, tax, subtotal + tax);
    }
}
