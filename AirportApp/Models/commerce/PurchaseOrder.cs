namespace AirportApp.Models.Commerce;

public sealed class PurchaseOrder
{
    public int PurchaseOrderId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserEmailSnapshot { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PurchaseOrderDetail> Details { get; set; } = [];
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = [];
}
