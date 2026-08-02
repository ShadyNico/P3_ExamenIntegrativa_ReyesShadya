using AirportApp.Models.Commerce;
using AirportApp.Models.AirportServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AirportApp.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext(options)
{
    public DbSet<FlightStock> FlightStocks => Set<FlightStock>();
    public DbSet<ShoppingCartItem> ShoppingCartItems => Set<ShoppingCartItem>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderDetail> PurchaseOrderDetails => Set<PurchaseOrderDetail>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<AirportReference> AirportReferences => Set<AirportReference>();
    public DbSet<AirportService> AirportServices => Set<AirportService>();
    public DbSet<ServiceAvailability> ServiceAvailabilities => Set<ServiceAvailability>();
    public DbSet<ServiceReservation> ServiceReservations => Set<ServiceReservation>();
    public DbSet<ServiceOrder> ServiceOrders => Set<ServiceOrder>();
    public DbSet<ServicePayment> ServicePayments => Set<ServicePayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("app");

        modelBuilder.Entity<IdentityUser>().ToTable("AspNetUsers", "app");
        modelBuilder.Entity<IdentityRole>().ToTable("AspNetRoles", "app");
        modelBuilder.Entity<IdentityUserRole<string>>().ToTable("AspNetUserRoles", "app");
        modelBuilder.Entity<IdentityUserClaim<string>>().ToTable("AspNetUserClaims", "app");
        modelBuilder.Entity<IdentityRoleClaim<string>>().ToTable("AspNetRoleClaims", "app");
        modelBuilder.Entity<IdentityUserLogin<string>>().ToTable("AspNetUserLogins", "app");
        modelBuilder.Entity<IdentityUserToken<string>>().ToTable("AspNetUserTokens", "app");

        modelBuilder.Entity<FlightStock>(entity =>
        {
            entity.ToTable("FlightStock", "app", table =>
            {
                table.HasCheckConstraint("CK_FlightStock_Stock", "\"Stock\" >= 0");
                table.HasCheckConstraint("CK_FlightStock_UnitPrice", "\"UnitPrice\" >= 0");
            });
            entity.HasKey(x => x.FlightStockId);
            entity.HasIndex(x => x.DomainEntityId).IsUnique();
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.UnitPrice).HasPrecision(12, 2);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.Version).IsRowVersion();
        });

        modelBuilder.Entity<ShoppingCartItem>(entity =>
        {
            entity.ToTable("ShoppingCartItem", "app", table =>
                table.HasCheckConstraint("CK_ShoppingCartItem_Quantity", "\"Quantity\" > 0"));
            entity.HasKey(x => x.ShoppingCartItemId);
            entity.HasIndex(x => new { x.UserId, x.FlightStockId }).IsUnique();
            entity.Property(x => x.UserId).HasMaxLength(450);
            entity.Property(x => x.AddedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(x => x.FlightStock).WithMany(x => x.CartItems)
                .HasForeignKey(x => x.FlightStockId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("PurchaseOrder", "app", table =>
                table.HasCheckConstraint("CK_PurchaseOrder_Total", "\"Total\" >= 0"));
            entity.HasKey(x => x.PurchaseOrderId);
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.Property(x => x.UserId).HasMaxLength(450);
            entity.Property(x => x.UserEmailSnapshot).HasMaxLength(320);
            entity.Property(x => x.Total).HasPrecision(12, 2);
            entity.Property(x => x.Status).HasMaxLength(20);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<PurchaseOrderDetail>(entity =>
        {
            entity.ToTable("PurchaseOrderDetail", "app", table =>
            {
                table.HasCheckConstraint("CK_PurchaseOrderDetail_Quantity", "\"Quantity\" > 0");
                table.HasCheckConstraint("CK_PurchaseOrderDetail_UnitPrice", "\"UnitPrice\" >= 0");
                table.HasCheckConstraint("CK_PurchaseOrderDetail_Subtotal", "\"Subtotal\" >= 0");
            });
            entity.HasKey(x => x.PurchaseOrderDetailId);
            entity.Property(x => x.ItemTitleSnapshot).HasMaxLength(200);
            entity.Property(x => x.UnitPrice).HasPrecision(12, 2);
            entity.Property(x => x.Subtotal).HasPrecision(12, 2);
            entity.HasOne(x => x.PurchaseOrder).WithMany(x => x.Details)
                .HasForeignKey(x => x.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.FlightStock).WithMany(x => x.OrderDetails)
                .HasForeignKey(x => x.FlightStockId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.ToTable("PaymentTransaction", "app", table =>
                table.HasCheckConstraint("CK_PaymentTransaction_Amount", "\"AmountInCents\" >= 0"));
            entity.HasKey(x => x.PaymentTransactionId);
            entity.HasIndex(x => x.ClientTransactionId).IsUnique();
            entity.HasIndex(x => x.PayphoneTransactionId)
                .IsUnique()
                .HasFilter("\"PayphoneTransactionId\" IS NOT NULL");
            entity.HasIndex(x => x.PayPalOrderId)
                .IsUnique()
                .HasFilter("\"PayPalOrderId\" IS NOT NULL");
            entity.HasIndex(x => x.PayPalCaptureId)
                .IsUnique()
                .HasFilter("\"PayPalCaptureId\" IS NOT NULL");
            entity.Property(x => x.Provider).HasMaxLength(30);
            entity.Property(x => x.ClientTransactionId).HasMaxLength(100);
            entity.Property(x => x.PayphonePaymentUrl).HasMaxLength(2048);
            entity.Property(x => x.PayphoneTransactionId).HasMaxLength(150);
            entity.Property(x => x.PayPalOrderId).HasMaxLength(150);
            entity.Property(x => x.PayPalCaptureId).HasMaxLength(150);
            entity.Property(x => x.PayPalApprovalUrl).HasMaxLength(2048);
            entity.Property(x => x.GatewayResponseSanitized).HasMaxLength(4000);
            entity.Property(x => x.Status).HasMaxLength(20);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne(x => x.PurchaseOrder).WithMany(x => x.PaymentTransactions)
                .HasForeignKey(x => x.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AirportReference>(entity =>
        {
            entity.ToTable("airport", "airportdb", table => table.ExcludeFromMigrations());
            entity.HasKey(x => x.AirportId).HasName("airport_pkey");
            entity.Property(x => x.AirportId).HasColumnName("airport_id");
            entity.Property(x => x.Iata).HasColumnName("iata").HasMaxLength(3).IsFixedLength();
            entity.Property(x => x.Icao).HasColumnName("icao").HasMaxLength(4).IsFixedLength();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(50);
        });

        modelBuilder.Entity<AirportService>(entity =>
        {
            entity.ToTable("AirportServices", "app", table =>
                table.HasCheckConstraint("CK_AirportServices_BasePrice", "\"BasePrice\" >= 0"));
            entity.HasKey(x => x.AirportServiceId);
            entity.HasIndex(x => new { x.AirportId, x.IsActive });
            entity.HasIndex(x => new { x.AirportId, x.ServiceType }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.ServiceType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.PriceType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.BasePrice).HasPrecision(12, 2);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(x => x.Airport).WithMany(x => x.Services)
                .HasForeignKey(x => x.AirportId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_AirportServices_airport_AirportId");
        });

        modelBuilder.Entity<ServiceAvailability>(entity =>
        {
            entity.ToTable("ServiceAvailability", "app", table =>
            {
                table.HasCheckConstraint(
                    "CK_ServiceAvailability_Capacity",
                    "\"MaximumCapacity\" > 0 AND \"ReservedCapacity\" >= 0 AND \"ReservedCapacity\" <= \"MaximumCapacity\"");
                table.HasCheckConstraint(
                    "CK_ServiceAvailability_Time",
                    "\"EndTime\" > \"StartTime\"");
            });
            entity.HasKey(x => x.ServiceAvailabilityId);
            entity.HasIndex(x => new
            {
                x.AirportServiceId,
                x.AvailableDate,
                x.StartTime,
                x.EndTime
            }).IsUnique();
            entity.HasIndex(x => new { x.AvailableDate, x.IsAvailable });
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.Version).IsRowVersion();
            entity.HasOne(x => x.AirportService).WithMany(x => x.Availabilities)
                .HasForeignKey(x => x.AirportServiceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServiceReservation>(entity =>
        {
            entity.ToTable("ServiceReservations", "app", table =>
            {
                table.HasCheckConstraint("CK_ServiceReservations_Quantity", "\"Quantity\" > 0");
                table.HasCheckConstraint("CK_ServiceReservations_Amounts",
                    "\"UnitPrice\" >= 0 AND \"Subtotal\" >= 0 AND \"Tax\" >= 0 AND \"Total\" >= 0");
            });
            entity.HasKey(x => x.ServiceReservationId);
            entity.HasIndex(x => x.ReservationCode).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.CreatedAt });
            entity.HasIndex(x => x.ServiceAvailabilityId);
            entity.Property(x => x.ReservationCode).HasMaxLength(32);
            entity.Property(x => x.UserId).HasMaxLength(450);
            entity.Property(x => x.CustomerName).HasMaxLength(150);
            entity.Property(x => x.CustomerEmail).HasMaxLength(320);
            entity.Property(x => x.CustomerPhone).HasMaxLength(30);
            entity.Property(x => x.UnitPrice).HasPrecision(12, 2);
            entity.Property(x => x.Subtotal).HasPrecision(12, 2);
            entity.Property(x => x.Tax).HasPrecision(12, 2);
            entity.Property(x => x.Total).HasPrecision(12, 2);
            entity.Property(x => x.ReservationStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(x => x.AirportService).WithMany(x => x.Reservations)
                .HasForeignKey(x => x.AirportServiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ServiceAvailability).WithMany(x => x.Reservations)
                .HasForeignKey(x => x.ServiceAvailabilityId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServiceOrder>(entity =>
        {
            entity.ToTable("Orders", "app", table =>
                table.HasCheckConstraint("CK_Orders_Amounts",
                    "\"Subtotal\" >= 0 AND \"Tax\" >= 0 AND \"Total\" >= 0"));
            entity.HasKey(x => x.OrderId);
            entity.HasIndex(x => x.OrderNumber).IsUnique();
            entity.HasIndex(x => x.ServiceReservationId).IsUnique();
            entity.Property(x => x.OrderNumber).HasMaxLength(32);
            entity.Property(x => x.Subtotal).HasPrecision(12, 2);
            entity.Property(x => x.Tax).HasPrecision(12, 2);
            entity.Property(x => x.Total).HasPrecision(12, 2);
            entity.Property(x => x.OrderStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(x => x.Reservation).WithOne(x => x.Order)
                .HasForeignKey<ServiceOrder>(x => x.ServiceReservationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ServicePayment>(entity =>
        {
            entity.ToTable("Payments", "app", table =>
                table.HasCheckConstraint("CK_Payments_Amount", "\"Amount\" >= 0"));
            entity.HasKey(x => x.PaymentId);
            entity.HasIndex(x => x.PaymentReference).IsUnique();
            entity.HasIndex(x => x.GatewayOrderId)
                .IsUnique()
                .HasFilter("\"GatewayOrderId\" IS NOT NULL");
            entity.HasIndex(x => x.GatewayTransactionId)
                .IsUnique()
                .HasFilter("\"GatewayTransactionId\" IS NOT NULL");
            entity.HasIndex(x => new { x.OrderId, x.TransactionDate });
            entity.Property(x => x.PaymentReference).HasMaxLength(40);
            entity.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Amount).HasPrecision(12, 2);
            entity.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.TransactionDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(x => x.CardLastFourDigits).HasMaxLength(4);
            entity.Property(x => x.AuthorizationCode).HasMaxLength(20);
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.Property(x => x.GatewayOrderId).HasMaxLength(150);
            entity.Property(x => x.GatewayTransactionId).HasMaxLength(150);
            entity.Property(x => x.GatewayPaymentUrl).HasMaxLength(2048);
            entity.Property(x => x.GatewayResponseSanitized).HasMaxLength(4000);
            entity.HasOne(x => x.Order).WithMany(x => x.Payments)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
