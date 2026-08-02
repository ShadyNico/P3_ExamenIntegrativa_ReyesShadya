namespace AirportApp.Models.Commerce;

public sealed class FlightStock
{
    public int FlightStockId { get; set; }
    public int DomainEntityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public uint Version { get; set; }

    public ICollection<ShoppingCartItem> CartItems { get; set; } = [];
    public ICollection<PurchaseOrderDetail> OrderDetails { get; set; } = [];
}
