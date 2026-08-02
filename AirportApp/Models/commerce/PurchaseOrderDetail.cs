namespace AirportApp.Models.Commerce;

public sealed class PurchaseOrderDetail
{
    public int PurchaseOrderDetailId { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public int FlightStockId { get; set; }
    public FlightStock FlightStock { get; set; } = null!;
    public string ItemTitleSnapshot { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}
