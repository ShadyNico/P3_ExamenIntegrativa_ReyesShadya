namespace AirportApp.Services;

public static class CommerceCalculations
{
    public static bool IsValidQuantity(int quantity, int availableStock, int maximum = 20) =>
        quantity >= 1 && quantity <= maximum && quantity <= availableStock;

    public static decimal Subtotal(int quantity, decimal unitPrice)
    {
        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice));
        }

        return quantity * unitPrice;
    }

    public static decimal Total(IEnumerable<(int Quantity, decimal UnitPrice)> items) =>
        items.Sum(item => Subtotal(item.Quantity, item.UnitPrice));

    public static int ToCents(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        return checked((int)Math.Round(amount * 100, MidpointRounding.AwayFromZero));
    }
}
