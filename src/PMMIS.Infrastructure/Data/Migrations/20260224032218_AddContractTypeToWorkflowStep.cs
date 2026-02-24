using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContractTypeToWorkflowStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContractType",
                table: "WorkflowSteps",
                type: "integer",
                nullable: true);

            // Migrate existing workflow steps to Works (0) contract type
            migrationBuilder.Sql("UPDATE \"WorkflowSteps\" SET \"ContractType\" = 0 WHERE \"ContractType\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContractType",
                table: "WorkflowSteps");
        }
    }
}
