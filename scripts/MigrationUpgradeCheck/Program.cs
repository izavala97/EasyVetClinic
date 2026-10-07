using EasyVetClinic.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

if (args.Length != 1) throw new ArgumentException("An isolated database path is required.");
var options = new DbContextOptionsBuilder<ClinicDbContext>()
    .UseSqlite($"Data Source={args[0]}").Options;
await using var db = new ClinicDbContext(options);
var migrator = db.GetService<IMigrator>();
await migrator.MigrateAsync("20261007200003_AddClinicAdministration");
await db.Database.ExecuteSqlRawAsync("""
    INSERT INTO Clinics (Id,Name,Address,LogoUrl,VeterinarianName,VeterinarianTitles,VeterinarianLicenseNumber)
    VALUES ('upgrade-clinic','Existing clinic','','','Doctor','','');
    INSERT INTO Guardians (Id,ClinicId,Name,Phone,AlternatePhone,Address,IdentityType,IdentityNumber,IdentityDocumentUrl)
    VALUES ('upgrade-guardian','upgrade-clinic','Existing guardian','555','','','','','');
    INSERT INTO Patients (Id,ClinicId,GuardianId,Name,Species,Breed,Sex,Weight,Color,Allergies,DistinguishingFeatures,PhotoUrl,IsActive)
    VALUES ('upgrade-patient','upgrade-clinic','upgrade-guardian','Archived Luna','Dog','','Female','12','','','','',0);
    INSERT INTO Products (Id,ClinicId,Name,Category,UnitPrice,StockOnHand)
    VALUES ('upgrade-product','upgrade-clinic','Existing medication','Medication','19.95',17),
           ('upgrade-zero','upgrade-clinic','Empty medication','Medication','0.25',0);
    INSERT INTO Appointments (Id,ClinicId,PatientId,StartsAt,Reason,ClinicianName)
    VALUES ('upgrade-appointment','upgrade-clinic','upgrade-patient','2026-10-01 12:00:00+00:00','Existing appointment','Doctor');
    INSERT INTO Consultations (Id,ClinicId,PatientId,ClinicianName,StartedAt,Status,ChiefComplaint,ClinicalNotes,Diagnosis,Instructions)
    VALUES ('upgrade-consultation','upgrade-clinic','upgrade-patient','Doctor','2026-10-01 12:00:00+00:00','Completed','Existing complaint','Preserved notes','Preserved diagnosis','Preserved instructions');
    """);
await migrator.MigrateAsync();
var product = await db.Products.SingleAsync(item => item.Id == "upgrade-product");
Require(product.StockOnHand == 17m && product.UnitPrice == 19.95m &&
    product.TrackStock && product.Unit == "each" && product.Version == 1,
    "Existing product stock, price, and catalog defaults must survive upgrade.");
var zero = await db.Products.SingleAsync(item => item.Id == "upgrade-zero");
Require(zero.StockOnHand == 0 && zero.UnitPrice == 0.25m, "Zero stock and fractional price must survive upgrade.");
var patient = await db.Patients.SingleAsync(item => item.Id == "upgrade-patient");
Require(!patient.IsActive && patient.Name == "Archived Luna", "Inactive clinical rows must remain inactive and preserved.");
var consultation = await db.Consultations.SingleAsync(item => item.Id == "upgrade-consultation");
Require(consultation.Status == "Completed" && consultation.ClinicalNotes == "Preserved notes" &&
    consultation.Diagnosis == "Preserved diagnosis" && consultation.Version == 1,
    "Completed clinical history and version defaults must survive upgrade.");
var appointment = await db.Appointments.SingleAsync(item => item.Id == "upgrade-appointment");
Require(appointment.DurationMinutes == 30 && appointment.Status == "Scheduled" && appointment.Version == 1,
    "Existing appointments require duration 30, Scheduled status, and version 1.");
Require((await db.Clinics.SingleAsync(item => item.Id == "upgrade-clinic")).TimeZoneId == "UTC",
    "Existing clinics require UTC.");
var obsoleteClinicColumns = await db.Database.SqlQueryRaw<int>("""
    SELECT COUNT(*) AS Value FROM pragma_table_info('Clinics')
    WHERE name IN ('VeterinarianName', 'VeterinarianTitles', 'VeterinarianLicenseNumber')
    """).SingleAsync();
Require(obsoleteClinicColumns == 0, "Clinic-wide clinician columns must be dropped, not retained for compatibility.");
var movements = await db.InventoryMovements.Where(item => item.ProductId == product.Id).ToListAsync();
Require(movements.Count == 1 && movements[0].QuantityChange == 17m && movements[0].BalanceAfter == 17m,
    "Migrated stock must have one matching opening ledger balance.");
await migrator.MigrateAsync();
Require(await db.InventoryMovements.CountAsync(item => item.ProductId == product.Id) == 1,
    "Repeated migration must not duplicate opening inventory.");
Console.WriteLine("Pre-commerce populated SQLite upgrade checks passed: 9");

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
