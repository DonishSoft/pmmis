using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSchoolAndAddEducationTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaleStudents",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "NameRu",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "NameTj",
                table: "Schools");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "Schools",
                newName: "FemaleTeachersCount");

            migrationBuilder.RenameColumn(
                name: "Address",
                table: "Schools",
                newName: "Name");

            migrationBuilder.AddColumn<int>(
                name: "TypeId",
                table: "Schools",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EducationInstitutionTypes",
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
                    table.PrimaryKey("PK_EducationInstitutionTypes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Schools_TypeId",
                table: "Schools",
                column: "TypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Schools_EducationInstitutionTypes_TypeId",
                table: "Schools",
                column: "TypeId",
                principalTable: "EducationInstitutionTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Schools_EducationInstitutionTypes_TypeId",
                table: "Schools");

            migrationBuilder.DropTable(
                name: "EducationInstitutionTypes");

            migrationBuilder.DropIndex(
                name: "IX_Schools_TypeId",
                table: "Schools");

            migrationBuilder.DropColumn(
                name: "TypeId",
                table: "Schools");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Schools",
                newName: "Address");

            migrationBuilder.RenameColumn(
                name: "FemaleTeachersCount",
                table: "Schools",
                newName: "Type");

            migrationBuilder.AddColumn<int>(
                name: "MaleStudents",
                table: "Schools",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Schools",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameRu",
                table: "Schools",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NameTj",
                table: "Schools",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
