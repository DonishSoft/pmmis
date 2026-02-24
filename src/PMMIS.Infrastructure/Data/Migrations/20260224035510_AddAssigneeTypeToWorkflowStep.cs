using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssigneeTypeToWorkflowStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssigneeType",
                table: "WorkflowSteps",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssigneeType",
                table: "WorkflowSteps");
        }
    }
}
