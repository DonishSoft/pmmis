using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContractWorkItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractWorkItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    TargetQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    AchievedQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractWorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractWorkItems_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractWorkItemId = table.Column<int>(type: "integer", nullable: false),
                    WorkProgressId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemProgresses_ContractWorkItems_ContractWorkItemId",
                        column: x => x.ContractWorkItemId,
                        principalTable: "ContractWorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItemProgresses_WorkProgresses_WorkProgressId",
                        column: x => x.WorkProgressId,
                        principalTable: "WorkProgresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractWorkItems_ContractId",
                table: "ContractWorkItems",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemProgresses_ContractWorkItemId",
                table: "WorkItemProgresses",
                column: "ContractWorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemProgresses_WorkProgressId",
                table: "WorkItemProgresses",
                column: "WorkProgressId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemProgresses");

            migrationBuilder.DropTable(
                name: "ContractWorkItems");
        }
    }
}
