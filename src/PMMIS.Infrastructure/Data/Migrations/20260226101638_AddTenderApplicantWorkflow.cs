using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenderApplicantWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvaluationDocPath",
                table: "TenderApplicants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsParticipant",
                table: "TenderApplicants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsWinner",
                table: "TenderApplicants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvaluationDocPath",
                table: "TenderApplicants");

            migrationBuilder.DropColumn(
                name: "IsParticipant",
                table: "TenderApplicants");

            migrationBuilder.DropColumn(
                name: "IsWinner",
                table: "TenderApplicants");
        }
    }
}
