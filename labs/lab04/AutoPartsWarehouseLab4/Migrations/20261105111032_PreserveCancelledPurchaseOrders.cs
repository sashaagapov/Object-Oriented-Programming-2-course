using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoPartsWarehouse.Migrations
{
    /// <inheritdoc />
    public partial class PreserveCancelledPurchaseOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "PurchaseOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "PurchaseOrders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByUserId",
                table: "PurchaseOrders",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "PurchaseOrders");
        }
    }
}
