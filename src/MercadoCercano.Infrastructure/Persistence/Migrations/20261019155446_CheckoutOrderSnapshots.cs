using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoCercano.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CheckoutOrderSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheckoutNoticeSnapshot",
                table: "Orders",
                type: "TEXT",
                maxLength: 5000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTestOrder",
                table: "Orders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PaymentInstructionsSnapshot",
                table: "Orders",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PickupInstructionsSnapshot",
                table: "Orders",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "Orders",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckoutNoticeSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsTestOrder",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentInstructionsSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PickupInstructionsSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "Orders");
        }
    }
}
