using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoPartsWarehouse.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VehicleModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Brand = table.Column<string>(type: "TEXT", nullable: false),
                    ModelName = table.Column<string>(type: "TEXT", nullable: false),
                    ProductionYear = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoPartVehicleModel",
                columns: table => new
                {
                    AutoPartsId = table.Column<int>(type: "INTEGER", nullable: false),
                    VehicleModelsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoPartVehicleModel", x => new { x.AutoPartsId, x.VehicleModelsId });
                    table.ForeignKey(
                        name: "FK_AutoPartVehicleModel_AutoParts_AutoPartsId",
                        column: x => x.AutoPartsId,
                        principalTable: "AutoParts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AutoPartVehicleModel_VehicleModels_VehicleModelsId",
                        column: x => x.VehicleModelsId,
                        principalTable: "VehicleModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutoPartVehicleModel_VehicleModelsId",
                table: "AutoPartVehicleModel",
                column: "VehicleModelsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutoPartVehicleModel");

            migrationBuilder.DropTable(
                name: "VehicleModels");
        }
    }
}
