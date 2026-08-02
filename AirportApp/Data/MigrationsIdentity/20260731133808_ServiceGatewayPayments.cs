using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AirportApp.Data.MigrationsIdentity
{
    /// <inheritdoc />
    public partial class ServiceGatewayPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GatewayOrderId",
                schema: "app",
                table: "Payments",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayPaymentUrl",
                schema: "app",
                table: "Payments",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayResponseSanitized",
                schema: "app",
                table: "Payments",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayTransactionId",
                schema: "app",
                table: "Payments",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_GatewayOrderId",
                schema: "app",
                table: "Payments",
                column: "GatewayOrderId",
                unique: true,
                filter: "\"GatewayOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_GatewayTransactionId",
                schema: "app",
                table: "Payments",
                column: "GatewayTransactionId",
                unique: true,
                filter: "\"GatewayTransactionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_GatewayOrderId",
                schema: "app",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_GatewayTransactionId",
                schema: "app",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "GatewayOrderId",
                schema: "app",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "GatewayPaymentUrl",
                schema: "app",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "GatewayResponseSanitized",
                schema: "app",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "GatewayTransactionId",
                schema: "app",
                table: "Payments");
        }
    }
}
