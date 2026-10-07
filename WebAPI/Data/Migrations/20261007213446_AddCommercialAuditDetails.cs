using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyVetClinic.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialAuditDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BillingAccounts_ConsultationId",
                table: "BillingAccounts");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "InventoryMovements",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceOverrideReason",
                table: "ChargeLines",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CashReceived",
                table: "AccountPayments",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ChangeGiven",
                table: "AccountPayments",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ClinicId_IdempotencyKey",
                table: "InventoryMovements",
                columns: new[] { "ClinicId", "IdempotencyKey" },
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BillingAccounts_ClinicId_ConsultationId",
                table: "BillingAccounts",
                columns: new[] { "ClinicId", "ConsultationId" },
                unique: true,
                filter: "[ConsultationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BillingAccounts_ConsultationId",
                table: "BillingAccounts",
                column: "ConsultationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_ClinicId_IdempotencyKey",
                table: "InventoryMovements");

            migrationBuilder.DropIndex(
                name: "IX_BillingAccounts_ClinicId_ConsultationId",
                table: "BillingAccounts");

            migrationBuilder.DropIndex(
                name: "IX_BillingAccounts_ConsultationId",
                table: "BillingAccounts");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "PriceOverrideReason",
                table: "ChargeLines");

            migrationBuilder.DropColumn(
                name: "CashReceived",
                table: "AccountPayments");

            migrationBuilder.DropColumn(
                name: "ChangeGiven",
                table: "AccountPayments");

            migrationBuilder.CreateIndex(
                name: "IX_BillingAccounts_ConsultationId",
                table: "BillingAccounts",
                column: "ConsultationId",
                unique: true,
                filter: "[ConsultationId] IS NOT NULL");
        }
    }
}
