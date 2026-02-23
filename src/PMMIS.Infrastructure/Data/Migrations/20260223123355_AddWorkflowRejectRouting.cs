using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowRejectRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanReject",
                table: "WorkflowSteps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RejectToStepOrder",
                table: "WorkflowSteps",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanReject",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "RejectToStepOrder",
                table: "WorkflowSteps");
        }
    }
}
