using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoCercano.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderOperationsAndPartialReturns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_Type",
                table: "InventoryMovements");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Payments",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CarrierName",
                table: "Orders",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAtUtc",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShippedAtUtc",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingNumber",
                table: "Orders",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    RejectionReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    RefundAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    ShippingRefundAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    RefundReference = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RefundedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RefundedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReturns", x => x.Id);
                    table.CheckConstraint("CK_OrderReturns_Amount", "\"RefundAmount\" BETWEEN 0 AND 9000000000000 AND \"ShippingRefundAmount\" BETWEEN 0 AND \"RefundAmount\"");
                    table.CheckConstraint("CK_OrderReturns_Kind", "\"Kind\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_OrderReturns_Status", "\"Status\" BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_OrderReturns_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderReturns_Users_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrderReturns_Users_RefundedByUserId",
                        column: x => x.RefundedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OrderReturns_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PaymentEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PaymentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StoredFileName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ByteLength = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEvidence", x => x.Id);
                    table.CheckConstraint("CK_PaymentEvidence_Length", "\"ByteLength\" > 0 AND \"ByteLength\" <= 768000");
                    table.ForeignKey(
                        name: "FK_PaymentEvidence_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderReturnItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderReturnId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    RestockedQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    RefundAmount = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReturnItems", x => x.Id);
                    table.CheckConstraint("CK_OrderReturnItems_Amount", "\"RefundAmount\" BETWEEN 0 AND 9000000000000");
                    table.CheckConstraint("CK_OrderReturnItems_Quantity", "\"Quantity\" > 0 AND \"RestockedQuantity\" BETWEEN 0 AND \"Quantity\"");
                    table.ForeignKey(
                        name: "FK_OrderReturnItems_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderReturnItems_OrderReturns_OrderReturnId",
                        column: x => x.OrderReturnId,
                        principalTable: "OrderReturns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_Type",
                table: "InventoryMovements",
                sql: "\"MovementType\" BETWEEN 1 AND 6");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturnItems_OrderItemId",
                table: "OrderReturnItems",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturnItems_OrderReturnId_OrderItemId",
                table: "OrderReturnItems",
                columns: new[] { "OrderReturnId", "OrderItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturns_OrderId",
                table: "OrderReturns",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturns_ReceivedByUserId",
                table: "OrderReturns",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturns_RefundedByUserId",
                table: "OrderReturns",
                column: "RefundedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturns_RequestedByUserId",
                table: "OrderReturns",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReturns_RequestId",
                table: "OrderReturns",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvidence_PaymentId",
                table: "PaymentEvidence",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvidence_StoredFileName",
                table: "PaymentEvidence",
                column: "StoredFileName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderReturnItems");

            migrationBuilder.DropTable(
                name: "PaymentEvidence");

            migrationBuilder.DropTable(
                name: "OrderReturns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryMovements_Type",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CarrierName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveredAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ShippedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TrackingNumber",
                table: "Orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryMovements_Type",
                table: "InventoryMovements",
                sql: "\"MovementType\" BETWEEN 1 AND 5");
        }
    }
}
