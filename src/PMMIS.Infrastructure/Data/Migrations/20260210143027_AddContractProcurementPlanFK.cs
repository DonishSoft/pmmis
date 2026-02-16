using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContractProcurementPlanFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementPlans_Contracts_ContractId",
                table: "ProcurementPlans");

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedAmount",
                table: "ProcurementPlans",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<int>(
                name: "ProcurementPlanId",
                table: "Contracts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ProcurementPlanId",
                table: "Contracts",
                column: "ProcurementPlanId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_ProcurementPlans_ProcurementPlanId",
                table: "Contracts",
                column: "ProcurementPlanId",
                principalTable: "ProcurementPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementPlans_Contracts_ContractId",
                table: "ProcurementPlans",
                column: "ContractId",
                principalTable: "Contracts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_ProcurementPlans_ProcurementPlanId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcurementPlans_Contracts_ContractId",
                table: "ProcurementPlans");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ProcurementPlanId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ProcurementPlanId",
                table: "Contracts");

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedAmount",
                table: "ProcurementPlans",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcurementPlans_Contracts_ContractId",
                table: "ProcurementPlans",
                column: "ContractId",
                principalTable: "Contracts",
                principalColumn: "Id");
        }
    }
}
