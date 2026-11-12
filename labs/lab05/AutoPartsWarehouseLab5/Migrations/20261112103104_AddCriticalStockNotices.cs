using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoPartsWarehouse.Migrations
{
    /// <inheritdoc />
    public partial class AddCriticalStockNotices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CriticalStockNotices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AutoPartId = table.Column<int>(type: "INTEGER", nullable: false),
                    QuantityAtNotice = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NotifiedByUserId = table.Column<string>(type: "TEXT", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AcknowledgedByUserId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriticalStockNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CriticalStockNotices_AspNetUsers_NotifiedByUserId",
                        column: x => x.NotifiedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CriticalStockNotices_AutoParts_AutoPartId",
                        column: x => x.AutoPartId,
                        principalTable: "AutoParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CriticalStockNotices_AutoPartId",
                table: "CriticalStockNotices",
                column: "AutoPartId",
                unique: true,
                filter: "\"AcknowledgedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CriticalStockNotices_NotifiedByUserId",
                table: "CriticalStockNotices",
                column: "NotifiedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CriticalStockNotices");
        }
    }
}
