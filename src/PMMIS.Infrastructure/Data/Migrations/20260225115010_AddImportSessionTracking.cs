using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImportSessionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ContractId = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PeriodName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImportedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TotalItems = table.Column<int>(type: "integer", nullable: false),
                    MatchedItems = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportSessions_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportSessionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImportSessionId = table.Column<int>(type: "integer", nullable: false),
                    ContractWorkItemId = table.Column<int>(type: "integer", nullable: false),
                    ThisPeriodQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    ThisPeriodAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CumulativeQuantity = table.Column<decimal>(type: "numeric", nullable: false),
                    CumulativeAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportSessionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportSessionItems_ContractWorkItems_ContractWorkItemId",
                        column: x => x.ContractWorkItemId,
                        principalTable: "ContractWorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImportSessionItems_ImportSessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "ImportSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportSessionItems_ContractWorkItemId",
                table: "ImportSessionItems",
                column: "ContractWorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportSessionItems_ImportSessionId",
                table: "ImportSessionItems",
                column: "ImportSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportSessions_ContractId",
                table: "ImportSessions",
                column: "ContractId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportSessionItems");

            migrationBuilder.DropTable(
                name: "ImportSessions");
        }
    }
}
