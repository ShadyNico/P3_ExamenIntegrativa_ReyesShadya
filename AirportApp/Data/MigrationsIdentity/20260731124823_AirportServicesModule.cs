using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AirportApp.Data.MigrationsIdentity
{
    /// <inheritdoc />
    public partial class AirportServicesModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AirportServices",
                schema: "app",
                columns: table => new
                {
                    AirportServiceId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AirportId = table.Column<short>(type: "smallint", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ServiceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BasePrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PriceType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirportServices", x => x.AirportServiceId);
                    table.CheckConstraint("CK_AirportServices_BasePrice", "\"BasePrice\" >= 0");
                    table.ForeignKey(
                        name: "FK_AirportServices_airport_AirportId",
                        column: x => x.AirportId,
                        principalSchema: "airportdb",
                        principalTable: "airport",
                        principalColumn: "airport_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceAvailability",
                schema: "app",
                columns: table => new
                {
                    ServiceAvailabilityId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AirportServiceId = table.Column<int>(type: "integer", nullable: false),
                    AvailableDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    MaximumCapacity = table.Column<int>(type: "integer", nullable: false),
                    ReservedCapacity = table.Column<int>(type: "integer", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceAvailability", x => x.ServiceAvailabilityId);
                    table.CheckConstraint("CK_ServiceAvailability_Capacity", "\"MaximumCapacity\" > 0 AND \"ReservedCapacity\" >= 0 AND \"ReservedCapacity\" <= \"MaximumCapacity\"");
                    table.CheckConstraint("CK_ServiceAvailability_Time", "\"EndTime\" > \"StartTime\"");
                    table.ForeignKey(
                        name: "FK_ServiceAvailability_AirportServices_AirportServiceId",
                        column: x => x.AirportServiceId,
                        principalSchema: "app",
                        principalTable: "AirportServices",
                        principalColumn: "AirportServiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceReservations",
                schema: "app",
                columns: table => new
                {
                    ServiceReservationId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReservationCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AirportServiceId = table.Column<int>(type: "integer", nullable: false),
                    ServiceAvailabilityId = table.Column<int>(type: "integer", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    CustomerPhone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReservationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Tax = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ReservationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceReservations", x => x.ServiceReservationId);
                    table.CheckConstraint("CK_ServiceReservations_Amounts", "\"UnitPrice\" >= 0 AND \"Subtotal\" >= 0 AND \"Tax\" >= 0 AND \"Total\" >= 0");
                    table.CheckConstraint("CK_ServiceReservations_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_ServiceReservations_AirportServices_AirportServiceId",
                        column: x => x.AirportServiceId,
                        principalSchema: "app",
                        principalTable: "AirportServices",
                        principalColumn: "AirportServiceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceReservations_ServiceAvailability_ServiceAvailability~",
                        column: x => x.ServiceAvailabilityId,
                        principalSchema: "app",
                        principalTable: "ServiceAvailability",
                        principalColumn: "ServiceAvailabilityId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                schema: "app",
                columns: table => new
                {
                    OrderId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ServiceReservationId = table.Column<int>(type: "integer", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Tax = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    OrderStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.OrderId);
                    table.CheckConstraint("CK_Orders_Amounts", "\"Subtotal\" >= 0 AND \"Tax\" >= 0 AND \"Total\" >= 0");
                    table.ForeignKey(
                        name: "FK_Orders_ServiceReservations_ServiceReservationId",
                        column: x => x.ServiceReservationId,
                        principalSchema: "app",
                        principalTable: "ServiceReservations",
                        principalColumn: "ServiceReservationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                schema: "app",
                columns: table => new
                {
                    PaymentId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrderId = table.Column<int>(type: "integer", nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PaymentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CardLastFourDigits = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    AuthorizationCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.PaymentId);
                    table.CheckConstraint("CK_Payments_Amount", "\"Amount\" >= 0");
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalSchema: "app",
                        principalTable: "Orders",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AirportServices_AirportId_IsActive",
                schema: "app",
                table: "AirportServices",
                columns: new[] { "AirportId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AirportServices_AirportId_ServiceType",
                schema: "app",
                table: "AirportServices",
                columns: new[] { "AirportId", "ServiceType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                schema: "app",
                table: "Orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ServiceReservationId",
                schema: "app",
                table: "Orders",
                column: "ServiceReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId_TransactionDate",
                schema: "app",
                table: "Payments",
                columns: new[] { "OrderId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PaymentReference",
                schema: "app",
                table: "Payments",
                column: "PaymentReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAvailability_AirportServiceId_AvailableDate_StartTim~",
                schema: "app",
                table: "ServiceAvailability",
                columns: new[] { "AirportServiceId", "AvailableDate", "StartTime", "EndTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceAvailability_AvailableDate_IsAvailable",
                schema: "app",
                table: "ServiceAvailability",
                columns: new[] { "AvailableDate", "IsAvailable" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReservations_AirportServiceId",
                schema: "app",
                table: "ServiceReservations",
                column: "AirportServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReservations_ReservationCode",
                schema: "app",
                table: "ServiceReservations",
                column: "ReservationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReservations_ServiceAvailabilityId",
                schema: "app",
                table: "ServiceReservations",
                column: "ServiceAvailabilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceReservations_UserId_CreatedAt",
                schema: "app",
                table: "ServiceReservations",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments",
                schema: "app");

            migrationBuilder.DropTable(
                name: "Orders",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ServiceReservations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "ServiceAvailability",
                schema: "app");

            migrationBuilder.DropTable(
                name: "AirportServices",
                schema: "app");
        }
    }
}
