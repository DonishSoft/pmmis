using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvrWorkflowAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "WorkProgresses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DirectorApprovedAt",
                table: "WorkProgresses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DirectorApprovedById",
                table: "WorkProgresses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DirectorComment",
                table: "WorkProgresses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerComment",
                table: "WorkProgresses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManagerReviewedAt",
                table: "WorkProgresses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerReviewedById",
                table: "WorkProgresses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "WorkProgresses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "WorkProgresses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContractorId",
                table: "Documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CuratorId",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectManagerId",
                table: "Contracts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ContractMilestones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    TitleTj = table.Column<string>(type: "text", nullable: true),
                    TitleEn = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    WorkProgressId = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractMilestones_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractMilestones_WorkProgresses_WorkProgressId",
                        column: x => x.WorkProgressId,
                        principalTable: "WorkProgresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkProgresses_DirectorApprovedById",
                table: "WorkProgresses",
                column: "DirectorApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkProgresses_ManagerReviewedById",
                table: "WorkProgresses",
                column: "ManagerReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ContractorId",
                table: "Documents",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CuratorId",
                table: "Contracts",
                column: "CuratorId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_ProjectManagerId",
                table: "Contracts",
                column: "ProjectManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractMilestones_ContractId",
                table: "ContractMilestones",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractMilestones_WorkProgressId",
                table: "ContractMilestones",
                column: "WorkProgressId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_CuratorId",
                table: "Contracts",
                column: "CuratorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Contracts_AspNetUsers_ProjectManagerId",
                table: "Contracts",
                column: "ProjectManagerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Contractors_ContractorId",
                table: "Documents",
                column: "ContractorId",
                principalTable: "Contractors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkProgresses_AspNetUsers_DirectorApprovedById",
                table: "WorkProgresses",
                column: "DirectorApprovedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkProgresses_AspNetUsers_ManagerReviewedById",
                table: "WorkProgresses",
                column: "ManagerReviewedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_CuratorId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Contracts_AspNetUsers_ProjectManagerId",
                table: "Contracts");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Contractors_ContractorId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkProgresses_AspNetUsers_DirectorApprovedById",
                table: "WorkProgresses");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkProgresses_AspNetUsers_ManagerReviewedById",
                table: "WorkProgresses");

            migrationBuilder.DropTable(
                name: "ContractMilestones");

            migrationBuilder.DropIndex(
                name: "IX_WorkProgresses_DirectorApprovedById",
                table: "WorkProgresses");

            migrationBuilder.DropIndex(
                name: "IX_WorkProgresses_ManagerReviewedById",
                table: "WorkProgresses");

            migrationBuilder.DropIndex(
                name: "IX_Documents_ContractorId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_CuratorId",
                table: "Contracts");

            migrationBuilder.DropIndex(
                name: "IX_Contracts_ProjectManagerId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "WorkProgresses");

            migrationBuilder.DropColumn(
                name: "DirectorApprovedAt",
                table: "WorkProgresses");

            migrationBuilder.DropColumn(
                name: "DirectorApprovedById",
                table: "WorkProgresses");

            migrationBuilder.DropColumn(
                name: "DirectorComment",
                table: "WorkProgresses");

            migrationBuilder.DropColumn(
                name: "ManagerComment",
                table: "WorkProgresses");

            migrationBuilder.DropColumn(
                name: "ManagerReviewedAt",
                table: "WorkProgresses");

            migrationBuilder.DropColumn(
                name: "ManagerReviewedById",
                table: "WorkProgresses");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "WorkProgresses");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "WorkProgresses");

            migrationBuilder.DropColumn(
                name: "ContractorId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "CuratorId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "ProjectManagerId",
                table: "Contracts");
        }
    }
}
