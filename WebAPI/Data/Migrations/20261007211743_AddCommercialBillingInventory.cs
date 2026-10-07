using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyVetClinic.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialBillingInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "StockOnHand",
                table: "Products",
                type: "TEXT",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LowStockThreshold",
                table: "Products",
                type: "TEXT",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                table: "Products",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TrackStock",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "Products",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "each");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "Consultations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClinicianLicenseNumber",
                table: "Consultations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClinicianObjectId",
                table: "Consultations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClinicianTitles",
                table: "Consultations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Consultations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "LicenseNumber",
                table: "ClinicUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProfessionalTitles",
                table: "ClinicUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SchedulingVersion",
                table: "Clinics",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Clinics",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "UTC");

            migrationBuilder.AddColumn<string>(
                name: "ClinicianObjectId",
                table: "Appointments",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConsultationId",
                table: "Appointments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Appointments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Appointments",
                type: "TEXT",
                nullable: false,
                defaultValue: "Scheduled");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Appointments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "BillingAccounts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ClinicId = table.Column<string>(type: "TEXT", nullable: false),
                    PatientId = table.Column<string>(type: "TEXT", nullable: true),
                    ConsultationId = table.Column<string>(type: "TEXT", nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    VoidReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Total = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillingAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillingAccounts_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BillingAccounts_Consultations_ConsultationId",
                        column: x => x.ConsultationId,
                        principalTable: "Consultations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BillingAccounts_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CatalogServices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ClinicId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    IsFavorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogServices_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalAmendments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ConsultationId = table.Column<string>(type: "TEXT", nullable: false),
                    ClinicId = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorObjectId = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalAmendments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicalAmendments_Consultations_ConsultationId",
                        column: x => x.ConsultationId,
                        principalTable: "Consultations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ClinicId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductId = table.Column<string>(type: "TEXT", nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SkuSnapshot = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    QuantityChange = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    OperationKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    BillingAccountId = table.Column<string>(type: "TEXT", nullable: true),
                    ChargeLineId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AccountPayments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ClinicId = table.Column<string>(type: "TEXT", nullable: false),
                    BillingAccountId = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Method = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountPayments_BillingAccounts_BillingAccountId",
                        column: x => x.BillingAccountId,
                        principalTable: "BillingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChargeLines",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ClinicId = table.Column<string>(type: "TEXT", nullable: false),
                    BillingAccountId = table.Column<string>(type: "TEXT", nullable: false),
                    CatalogItemId = table.Column<string>(type: "TEXT", nullable: false),
                    ItemType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    NameSnapshot = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SkuSnapshot = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UnitSnapshot = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CatalogVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    FulfilledQuantity = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChargeLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChargeLines_BillingAccounts_BillingAccountId",
                        column: x => x.BillingAccountId,
                        principalTable: "BillingAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ClinicId_Sku",
                table: "Products",
                columns: new[] { "ClinicId", "Sku" },
                unique: true,
                filter: "[Sku] IS NOT NULL AND [Sku] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ClinicId_ConsultationId",
                table: "Appointments",
                columns: new[] { "ClinicId", "ConsultationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountPayments_BillingAccountId",
                table: "AccountPayments",
                column: "BillingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountPayments_ClinicId_IdempotencyKey",
                table: "AccountPayments",
                columns: new[] { "ClinicId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BillingAccounts_ClinicId_CreatedAt",
                table: "BillingAccounts",
                columns: new[] { "ClinicId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BillingAccounts_ConsultationId",
                table: "BillingAccounts",
                column: "ConsultationId",
                unique: true,
                filter: "[ConsultationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_BillingAccounts_PatientId",
                table: "BillingAccounts",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogServices_ClinicId_Name",
                table: "CatalogServices",
                columns: new[] { "ClinicId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChargeLines_BillingAccountId",
                table: "ChargeLines",
                column: "BillingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalAmendments_ConsultationId",
                table: "ClinicalAmendments",
                column: "ConsultationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ClinicId_OperationKey",
                table: "InventoryMovements",
                columns: new[] { "ClinicId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_ProductId_CreatedAt",
                table: "InventoryMovements",
                columns: new[] { "ProductId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountPayments");

            migrationBuilder.DropTable(
                name: "CatalogServices");

            migrationBuilder.DropTable(
                name: "ChargeLines");

            migrationBuilder.DropTable(
                name: "ClinicalAmendments");

            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "BillingAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Products_ClinicId_Sku",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_ClinicId_ConsultationId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LowStockThreshold",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Sku",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TrackStock",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "ClinicianLicenseNumber",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "ClinicianObjectId",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "ClinicianTitles",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "LicenseNumber",
                table: "ClinicUsers");

            migrationBuilder.DropColumn(
                name: "ProfessionalTitles",
                table: "ClinicUsers");

            migrationBuilder.DropColumn(
                name: "SchedulingVersion",
                table: "Clinics");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Clinics");

            migrationBuilder.DropColumn(
                name: "ClinicianObjectId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "ConsultationId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Appointments");

            migrationBuilder.AlterColumn<int>(
                name: "StockOnHand",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "TEXT",
                oldPrecision: 18,
                oldScale: 3);
        }
    }
}
