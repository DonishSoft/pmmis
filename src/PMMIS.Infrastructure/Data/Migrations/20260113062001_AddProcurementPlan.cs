using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProcurementPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcurementPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReferenceNo = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    DescriptionTj = table.Column<string>(type: "text", nullable: true),
                    DescriptionEn = table.Column<string>(type: "text", nullable: true),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    EstimatedAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    PlannedBidOpeningDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PlannedContractSigningDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PlannedCompletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualBidOpeningDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualContractSigningDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualCompletionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Comments = table.Column<string>(type: "text", nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    ComponentId = table.Column<int>(type: "integer", nullable: true),
                    SubComponentId = table.Column<int>(type: "integer", nullable: true),
                    ContractId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcurementPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcurementPlans_Components_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "Components",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProcurementPlans_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProcurementPlans_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcurementPlans_SubComponents_SubComponentId",
                        column: x => x.SubComponentId,
                        principalTable: "SubComponents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlans_ComponentId",
                table: "ProcurementPlans",
                column: "ComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlans_ContractId",
                table: "ProcurementPlans",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlans_ProjectId",
                table: "ProcurementPlans",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcurementPlans_SubComponentId",
                table: "ProcurementPlans",
                column: "SubComponentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcurementPlans");
        }
    }
}
