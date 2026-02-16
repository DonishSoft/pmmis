using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIndicatorEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Category",
                table: "Indicators",
                newName: "SortOrder");

            migrationBuilder.AddColumn<bool>(
                name: "BoolValue",
                table: "IndicatorValues",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "Indicators",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MeasurementType",
                table: "Indicators",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ParentIndicatorId",
                table: "Indicators",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IndicatorCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndicatorCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Indicators_CategoryId",
                table: "Indicators",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Indicators_ParentIndicatorId",
                table: "Indicators",
                column: "ParentIndicatorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Indicators_IndicatorCategories_CategoryId",
                table: "Indicators",
                column: "CategoryId",
                principalTable: "IndicatorCategories",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Indicators_Indicators_ParentIndicatorId",
                table: "Indicators",
                column: "ParentIndicatorId",
                principalTable: "Indicators",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Indicators_IndicatorCategories_CategoryId",
                table: "Indicators");

            migrationBuilder.DropForeignKey(
                name: "FK_Indicators_Indicators_ParentIndicatorId",
                table: "Indicators");

            migrationBuilder.DropTable(
                name: "IndicatorCategories");

            migrationBuilder.DropIndex(
                name: "IX_Indicators_CategoryId",
                table: "Indicators");

            migrationBuilder.DropIndex(
                name: "IX_Indicators_ParentIndicatorId",
                table: "Indicators");

            migrationBuilder.DropColumn(
                name: "BoolValue",
                table: "IndicatorValues");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Indicators");

            migrationBuilder.DropColumn(
                name: "MeasurementType",
                table: "Indicators");

            migrationBuilder.DropColumn(
                name: "ParentIndicatorId",
                table: "Indicators");

            migrationBuilder.RenameColumn(
                name: "SortOrder",
                table: "Indicators",
                newName: "Category");
        }
    }
}
