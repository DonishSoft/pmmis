using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMMIS.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemImportFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These columns were added to the model but the migration was never created
            migrationBuilder.Sql(@"
                ALTER TABLE ""ContractWorkItems"" ADD COLUMN IF NOT EXISTS ""UnitPrice"" numeric NOT NULL DEFAULT 0;
                ALTER TABLE ""ContractWorkItems"" ADD COLUMN IF NOT EXISTS ""TotalAmount"" numeric NOT NULL DEFAULT 0;
                ALTER TABLE ""ContractWorkItems"" ADD COLUMN IF NOT EXISTS ""Category"" text;
                ALTER TABLE ""ContractWorkItems"" ADD COLUMN IF NOT EXISTS ""ItemNumber"" text;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""ContractWorkItems"" DROP COLUMN IF EXISTS ""UnitPrice"";
                ALTER TABLE ""ContractWorkItems"" DROP COLUMN IF EXISTS ""TotalAmount"";
                ALTER TABLE ""ContractWorkItems"" DROP COLUMN IF EXISTS ""Category"";
                ALTER TABLE ""ContractWorkItems"" DROP COLUMN IF EXISTS ""ItemNumber"";
            ");
        }
    }
}
