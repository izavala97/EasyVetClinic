using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyVetClinic.Api.Data.SqlServerMigrations
{
    /// <inheritdoc />
    public partial class BackfillLegacyInventoryOpening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO InventoryMovements
                    (Id,ClinicId,ProductId,ProductNameSnapshot,SkuSnapshot,Type,QuantityChange,BalanceAfter,Reason,OperationKey,CreatedAt)
                SELECT 'legacy-opening:' + p.Id,p.ClinicId,p.Id,p.Name,COALESCE(p.Sku,''),'Opening',
                    p.StockOnHand,p.StockOnHand,'Opening stock preserved from pre-commerce inventory',
                    'legacy-opening:' + p.Id,SYSDATETIMEOFFSET()
                FROM Products p
                WHERE p.TrackStock = 1 AND p.StockOnHand <> 0
                    AND NOT EXISTS (SELECT 1 FROM InventoryMovements m WHERE m.ClinicId = p.ClinicId AND m.ProductId = p.Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM InventoryMovements
                WHERE OperationKey = 'legacy-opening:' + ProductId
                    AND Reason = 'Opening stock preserved from pre-commerce inventory';
                """);
        }
    }
}
