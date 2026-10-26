using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercadoCercano.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PromotionsAndCoupons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DiscountAmount",
                table: "PromotionUsages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedAtUtc",
                table: "PromotionUsages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Promotions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MaximumDiscountAmount",
                table: "Promotions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Promotions",
                type: "TEXT",
                maxLength: 160,
                nullable: false,
                defaultValue: "Promocion");

            migrationBuilder.AddColumn<Guid>(
                name: "ProductId",
                table: "Promotions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "Promotions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<long>(
                name: "PromotionDiscountTotal",
                table: "Orders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "PromotionNameSnapshot",
                table: "Orders",
                type: "TEXT",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PromotionDiscountTotal",
                table: "OrderItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql("UPDATE \"Promotions\" SET \"UpdatedAtUtc\" = CURRENT_TIMESTAMP;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PromotionUsages_DiscountAmount",
                table: "PromotionUsages",
                sql: "\"DiscountAmount\" BETWEEN 0 AND 9000000000000");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_CategoryId",
                table: "Promotions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_ProductId",
                table: "Promotions",
                column: "ProductId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_MaximumDiscount",
                table: "Promotions",
                sql: "\"MaximumDiscountAmount\" IS NULL OR \"MaximumDiscountAmount\" BETWEEN 1 AND 9000000000000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Promotions_Scope",
                table: "Promotions",
                sql: "\"ProductId\" IS NULL OR \"CategoryId\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderItems_PromotionDiscount",
                table: "OrderItems",
                sql: "\"PromotionDiscountTotal\" BETWEEN 0 AND \"LineTotal\"");

            migrationBuilder.AddForeignKey(
                name: "FK_Promotions_Categories_CategoryId",
                table: "Promotions",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Promotions_Products_ProductId",
                table: "Promotions",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Promotions_Categories_CategoryId",
                table: "Promotions");

            migrationBuilder.DropForeignKey(
                name: "FK_Promotions_Products_ProductId",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PromotionUsages_DiscountAmount",
                table: "PromotionUsages");

            migrationBuilder.DropIndex(
                name: "IX_Promotions_CategoryId",
                table: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_Promotions_ProductId",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Promotions_MaximumDiscount",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Promotions_Scope",
                table: "Promotions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderItems_PromotionDiscount",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "PromotionUsages");

            migrationBuilder.DropColumn(
                name: "ReleasedAtUtc",
                table: "PromotionUsages");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "MaximumDiscountAmount",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "PromotionDiscountTotal",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PromotionNameSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PromotionDiscountTotal",
                table: "OrderItems");
        }
    }
}
