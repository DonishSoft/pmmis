using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentWorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentId",
                table: "WorkflowHistories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrentStepOrder",
                table: "Payments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowHistories_PaymentId",
                table: "WorkflowHistories",
                column: "PaymentId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowHistories_Payments_PaymentId",
                table: "WorkflowHistories",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowHistories_Payments_PaymentId",
                table: "WorkflowHistories");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowHistories_PaymentId",
                table: "WorkflowHistories");

            migrationBuilder.DropColumn(
                name: "PaymentId",
                table: "WorkflowHistories");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CurrentStepOrder",
                table: "Payments");
        }
    }
}
