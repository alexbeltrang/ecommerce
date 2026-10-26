using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoCercano.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 140, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.CheckConstraint("CK_Categories_SortOrder", "\"SortOrder\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    BusinessNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    DiscountType = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscountValue = table.Column<long>(type: "INTEGER", nullable: false),
                    MinimumOrderAmount = table.Column<long>(type: "INTEGER", nullable: false),
                    MaxUses = table.Column<int>(type: "INTEGER", nullable: true),
                    UsedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                    table.CheckConstraint("CK_Promotions_DateRange", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                    table.CheckConstraint("CK_Promotions_DiscountType", "\"DiscountType\" IN (1, 2)");
                    table.CheckConstraint("CK_Promotions_DiscountValue", "\"DiscountValue\" > 0");
                    table.CheckConstraint("CK_Promotions_MaxUses", "\"MaxUses\" IS NULL OR \"MaxUses\" > 0");
                    table.CheckConstraint("CK_Promotions_MinimumOrderAmount", "\"MinimumOrderAmount\" >= 0");
                    table.CheckConstraint("CK_Promotions_Percentage", "\"DiscountType\" <> 1 OR \"DiscountValue\" <= 100");
                    table.CheckConstraint("CK_Promotions_UsageCount", "\"UsedCount\" >= 0 AND (\"MaxUses\" IS NULL OR \"UsedCount\" <= \"MaxUses\")");
                });

            migrationBuilder.CreateTable(
                name: "StoreSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: false),
                    IsPublic = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_Role", "\"Role\" IN (1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: false),
                    Price = table.Column<long>(type: "INTEGER", nullable: false),
                    Cost = table.Column<long>(type: "INTEGER", nullable: false),
                    PromotionalPrice = table.Column<long>(type: "INTEGER", nullable: true),
                    PromotionStartUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PromotionEndUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StockQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    LowStockThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    WeightGrams = table.Column<int>(type: "INTEGER", nullable: true),
                    Presentation = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    LotNumber = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    ExpirationDateUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SupplierLabelingJson = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: true),
                    IsFeatured = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.CheckConstraint("CK_Products_Cost", "\"Cost\" >= 0");
                    table.CheckConstraint("CK_Products_LowStockThreshold", "\"LowStockThreshold\" >= 0");
                    table.CheckConstraint("CK_Products_Price", "\"Price\" > 0");
                    table.CheckConstraint("CK_Products_PromotionalPrice", "\"PromotionalPrice\" IS NULL OR (\"PromotionalPrice\" > 0 AND \"PromotionalPrice\" < \"Price\")");
                    table.CheckConstraint("CK_Products_PromotionDates", "(\"PromotionStartUtc\" IS NULL AND \"PromotionEndUtc\" IS NULL) OR (\"PromotionStartUtc\" IS NOT NULL AND \"PromotionEndUtc\" > \"PromotionStartUtc\")");
                    table.CheckConstraint("CK_Products_StockQuantity", "\"StockQuantity\" >= 0");
                    table.CheckConstraint("CK_Products_WeightGrams", "\"WeightGrams\" IS NULL OR \"WeightGrams\" > 0");
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Addresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Municipality = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Neighborhood = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    AddressLine = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    References = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Addresses_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    EntityId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    DetailsJson = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderNumber = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CustomerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CustomerNameSnapshot = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CustomerPhoneSnapshot = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CustomerEmailSnapshot = table.Column<string>(type: "TEXT", maxLength: 254, nullable: true),
                    MunicipalitySnapshot = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    NeighborhoodSnapshot = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    AddressSnapshot = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    AddressReferencesSnapshot = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DeliveryMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Subtotal = table.Column<long>(type: "INTEGER", nullable: false),
                    DiscountTotal = table.Column<long>(type: "INTEGER", nullable: false),
                    ShippingCost = table.Column<long>(type: "INTEGER", nullable: false),
                    ShippingCostConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    GrandTotal = table.Column<long>(type: "INTEGER", nullable: false),
                    PromotionCodeSnapshot = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    CustomerNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CancellationReason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TrackingTokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    TermsAcceptedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_DeliveryMethod", "\"DeliveryMethod\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_Orders_DiscountTotal", "\"DiscountTotal\" >= 0 AND \"DiscountTotal\" <= \"Subtotal\"");
                    table.CheckConstraint("CK_Orders_GrandTotal", "\"GrandTotal\" = \"Subtotal\" - \"DiscountTotal\" + \"ShippingCost\"");
                    table.CheckConstraint("CK_Orders_PaymentMethod", "\"PaymentMethod\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_Orders_PaymentStatus", "\"PaymentStatus\" BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Orders_ShippingCost", "\"ShippingCost\" >= 0");
                    table.CheckConstraint("CK_Orders_Status", "\"Status\" BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_Orders_Subtotal", "\"Subtotal\" >= 0");
                    table.ForeignKey(
                        name: "FK_Orders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Orders_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MovementType = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityDelta = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    NewQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ReferenceType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ReferenceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.CheckConstraint("CK_InventoryMovements_NewQuantity", "\"NewQuantity\" >= 0");
                    table.CheckConstraint("CK_InventoryMovements_PreviousQuantity", "\"PreviousQuantity\" >= 0");
                    table.CheckConstraint("CK_InventoryMovements_QuantityDelta", "\"QuantityDelta\" <> 0");
                    table.CheckConstraint("CK_InventoryMovements_QuantityReconciliation", "\"NewQuantity\" = \"PreviousQuantity\" + \"QuantityDelta\"");
                    table.CheckConstraint("CK_InventoryMovements_Type", "\"MovementType\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    AltText = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.CheckConstraint("CK_ProductImages_SortOrder", "\"SortOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductNameSnapshot = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    SkuSnapshot = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPrice = table.Column<long>(type: "INTEGER", nullable: false),
                    UnitCostSnapshot = table.Column<long>(type: "INTEGER", nullable: false),
                    LineTotal = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItems_LineTotal", "\"LineTotal\" >= 0");
                    table.CheckConstraint("CK_OrderItems_Quantity", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_OrderItems_UnitCostSnapshot", "\"UnitCostSnapshot\" >= 0");
                    table.CheckConstraint("CK_OrderItems_UnitPrice", "\"UnitPrice\" > 0");
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PreviousStatus = table.Column<int>(type: "INTEGER", nullable: true),
                    NewStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistory", x => x.Id);
                    table.CheckConstraint("CK_OrderStatusHistory_NewStatus", "\"NewStatus\" BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_OrderStatusHistory_PreviousStatus", "\"PreviousStatus\" IS NULL OR \"PreviousStatus\" BETWEEN 1 AND 7");
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistory_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Method = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<long>(type: "INTEGER", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    ConfirmedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount", "\"Amount\" >= 0");
                    table.CheckConstraint("CK_Payments_Method", "\"Method\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_Payments_Status", "\"Status\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PromotionUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PromotionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromotionUsages_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromotionUsages_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_CustomerId_IsDefault",
                table: "Addresses",
                columns: new[] { "CustomerId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAtUtc",
                table: "AuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_IsActive_SortOrder",
                table: "Categories",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Email",
                table: "Customers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Phone",
                table: "Customers",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ProductId_CreatedAtUtc",
                table: "InventoryMovements",
                columns: new[] { "ProductId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ReferenceType_ReferenceId",
                table: "InventoryMovements",
                columns: new[] { "ReferenceType", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_UserId",
                table: "InventoryMovements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CancelledByUserId",
                table: "Orders",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId",
                table: "Orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerPhoneSnapshot",
                table: "Orders",
                column: "CustomerPhoneSnapshot");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_IdempotencyKey",
                table: "Orders",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentStatus_CreatedAtUtc",
                table: "Orders",
                columns: new[] { "PaymentStatus", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_CreatedAtUtc",
                table: "Orders",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TrackingTokenHash",
                table: "Orders",
                column: "TrackingTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_OrderId_CreatedAtUtc",
                table: "OrderStatusHistory",
                columns: new[] { "OrderId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistory_UserId",
                table: "OrderStatusHistory",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ConfirmedByUserId",
                table: "Payments",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId_CreatedAtUtc",
                table: "Payments",
                columns: new[] { "OrderId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId_SortOrder",
                table: "ProductImages",
                columns: new[] { "ProductId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive_CategoryId",
                table: "Products",
                columns: new[] { "IsActive", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive_IsFeatured",
                table: "Products",
                columns: new[] { "IsActive", "IsFeatured" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                table: "Products",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_Code",
                table: "Promotions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_IsActive_StartsAtUtc_EndsAtUtc",
                table: "Promotions",
                columns: new[] { "IsActive", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionUsages_OrderId",
                table: "PromotionUsages",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionUsages_PromotionId_OrderId",
                table: "PromotionUsages",
                columns: new[] { "PromotionId", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoreSettings_IsPublic",
                table: "StoreSettings",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_StoreSettings_Key",
                table: "StoreSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive",
                table: "Users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedEmail",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Addresses");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "OrderStatusHistory");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "ProductImages");

            migrationBuilder.DropTable(
                name: "PromotionUsages");

            migrationBuilder.DropTable(
                name: "StoreSettings");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Promotions");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
