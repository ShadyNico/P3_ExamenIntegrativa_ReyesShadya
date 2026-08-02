namespace AirportApp.Models.Commerce;

public sealed class ShoppingCartItem
{
    public int ShoppingCartItemId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int FlightStockId { get; set; }
    public FlightStock FlightStock { get; set; } = null!;
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
