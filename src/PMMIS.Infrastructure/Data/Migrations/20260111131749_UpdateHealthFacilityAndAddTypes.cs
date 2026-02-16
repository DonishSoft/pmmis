using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateHealthFacilityAndAddTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DoctorsCount",
                table: "HealthFacilities");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "HealthFacilities");

            migrationBuilder.DropColumn(
                name: "NameRu",
                table: "HealthFacilities");

            migrationBuilder.DropColumn(
                name: "NameTj",
                table: "HealthFacilities");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "HealthFacilities",
                newName: "TotalStaff");

            migrationBuilder.RenameColumn(
                name: "ServedPopulation",
                table: "HealthFacilities",
                newName: "PatientsPerDay");

            migrationBuilder.RenameColumn(
                name: "NursesCount",
                table: "HealthFacilities",
                newName: "FemaleStaff");

            migrationBuilder.RenameColumn(
                name: "BedsCount",
                table: "HealthFacilities",
                newName: "TypeId");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "HealthFacilities",
                newName: "Name");

            migrationBuilder.CreateTable(
                name: "HealthFacilityTypes",
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
                    table.PrimaryKey("PK_HealthFacilityTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HealthFacilities_TypeId",
                table: "HealthFacilities",
                column: "TypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_HealthFacilities_HealthFacilityTypes_TypeId",
                table: "HealthFacilities",
                column: "TypeId",
                principalTable: "HealthFacilityTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HealthFacilities_HealthFacilityTypes_TypeId",
                table: "HealthFacilities");

            migrationBuilder.DropTable(
                name: "HealthFacilityTypes");

            migrationBuilder.DropIndex(
                name: "IX_HealthFacilities_TypeId",
                table: "HealthFacilities");

            migrationBuilder.RenameColumn(
                name: "TypeId",
                table: "HealthFacilities",
                newName: "BedsCount");

            migrationBuilder.RenameColumn(
                name: "TotalStaff",
                table: "HealthFacilities",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "PatientsPerDay",
                table: "HealthFacilities",
                newName: "ServedPopulation");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "HealthFacilities",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "FemaleStaff",
                table: "HealthFacilities",
                newName: "NursesCount");

            migrationBuilder.AddColumn<int>(
                name: "DoctorsCount",
                table: "HealthFacilities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "HealthFacilities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameRu",
                table: "HealthFacilities",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameTj",
                table: "HealthFacilities",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
