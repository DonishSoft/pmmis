using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIndicatorProgressItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IndicatorProgressItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractIndicatorProgressId = table.Column<int>(type: "integer", nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    VillageId = table.Column<int>(type: "integer", nullable: true),
                    SchoolId = table.Column<int>(type: "integer", nullable: true),
                    HealthFacilityId = table.Column<int>(type: "integer", nullable: true),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    NumericValue = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndicatorProgressItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndicatorProgressItems_ContractIndicatorProgresses_Contract~",
                        column: x => x.ContractIndicatorProgressId,
                        principalTable: "ContractIndicatorProgresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IndicatorProgressItems_HealthFacilities_HealthFacilityId",
                        column: x => x.HealthFacilityId,
                        principalTable: "HealthFacilities",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IndicatorProgressItems_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IndicatorProgressItems_Villages_VillageId",
                        column: x => x.VillageId,
                        principalTable: "Villages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndicatorProgressItems_ContractIndicatorProgressId",
                table: "IndicatorProgressItems",
                column: "ContractIndicatorProgressId");

            migrationBuilder.CreateIndex(
                name: "IX_IndicatorProgressItems_HealthFacilityId",
                table: "IndicatorProgressItems",
                column: "HealthFacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_IndicatorProgressItems_SchoolId",
                table: "IndicatorProgressItems",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_IndicatorProgressItems_VillageId",
                table: "IndicatorProgressItems",
                column: "VillageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndicatorProgressItems");
        }
    }
}
