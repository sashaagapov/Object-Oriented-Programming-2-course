using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoPartsWarehouse.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueStatusAndPaymentAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartIssues_AutoParts_AutoPartId",
                table: "PartIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_AutoParts_AutoPartId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "PartIssues",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaymentConfirmed",
                table: "PartIssues",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "PartIssues",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_PartIssues_AutoParts_AutoPartId",
                table: "PartIssues",
                column: "AutoPartId",
                principalTable: "AutoParts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_AutoParts_AutoPartId",
                table: "PurchaseOrderItems",
                column: "AutoPartId",
                principalTable: "AutoParts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PartIssues_AutoParts_AutoPartId",
                table: "PartIssues");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_AutoParts_AutoPartId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "PartIssues");

            migrationBuilder.DropColumn(
                name: "IsPaymentConfirmed",
                table: "PartIssues");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "PartIssues");

            migrationBuilder.AddForeignKey(
                name: "FK_PartIssues_AutoParts_AutoPartId",
                table: "PartIssues",
                column: "AutoPartId",
                principalTable: "AutoParts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_AutoParts_AutoPartId",
                table: "PurchaseOrderItems",
                column: "AutoPartId",
                principalTable: "AutoParts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
