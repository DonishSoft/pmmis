using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedById",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectedById",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ApprovedById",
                table: "Payments",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_RejectedById",
                table: "Payments",
                column: "RejectedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_AspNetUsers_ApprovedById",
                table: "Payments",
                column: "ApprovedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_AspNetUsers_RejectedById",
                table: "Payments",
                column: "RejectedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_AspNetUsers_ApprovedById",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_AspNetUsers_RejectedById",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ApprovedById",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_RejectedById",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ApprovedById",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RejectedById",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Payments");
        }
    }
}
