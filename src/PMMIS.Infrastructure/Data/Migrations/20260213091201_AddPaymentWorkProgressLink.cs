using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentWorkProgressLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkProgressId",
                table: "Payments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_WorkProgressId",
                table: "Payments",
                column: "WorkProgressId");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_WorkProgresses_WorkProgressId",
                table: "Payments",
                column: "WorkProgressId",
                principalTable: "WorkProgresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_WorkProgresses_WorkProgressId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_WorkProgressId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "WorkProgressId",
                table: "Payments");
        }
    }
}
