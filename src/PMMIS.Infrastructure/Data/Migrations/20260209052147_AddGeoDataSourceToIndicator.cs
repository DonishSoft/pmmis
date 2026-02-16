using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGeoDataSourceToIndicator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GeoDataSource",
                table: "Indicators",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ContractIndicatorVillages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractIndicatorId = table.Column<int>(type: "integer", nullable: false),
                    VillageId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractIndicatorVillages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractIndicatorVillages_ContractIndicators_ContractIndica~",
                        column: x => x.ContractIndicatorId,
                        principalTable: "ContractIndicators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractIndicatorVillages_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractIndicatorVillages_ContractIndicatorId_VillageId",
                table: "ContractIndicatorVillages",
                columns: new[] { "ContractIndicatorId", "VillageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractIndicatorVillages_VillageId",
                table: "ContractIndicatorVillages",
                column: "VillageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractIndicatorVillages");

            migrationBuilder.DropColumn(
                name: "GeoDataSource",
                table: "Indicators");
        }
    }
}
