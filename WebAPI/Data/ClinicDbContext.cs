using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Data;

public class ClinicDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<ClinicUser> ClinicUsers => Set<ClinicUser>();
    public DbSet<ClinicInvitation> ClinicInvitations => Set<ClinicInvitation>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientGuardian> PatientGuardians => Set<PatientGuardian>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Consultation> Consultations => Set<Consultation>();
    public DbSet<ClinicalAmendment> ClinicalAmendments => Set<ClinicalAmendment>();
    public DbSet<WeightRecord> WeightRecords => Set<WeightRecord>();
    public DbSet<VaccinationRecord> VaccinationRecords => Set<VaccinationRecord>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CatalogService> CatalogServices => Set<CatalogService>();
    public DbSet<BillingAccount> BillingAccounts => Set<BillingAccount>();
    public DbSet<ChargeLine> ChargeLines => Set<ChargeLine>();
    public DbSet<AccountPayment> AccountPayments => Set<AccountPayment>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Clinic>(entity =>
        {
            entity.HasKey(clinic => clinic.Id);
            entity.Property(clinic => clinic.Name).HasMaxLength(200).IsRequired();
            entity.Property(clinic => clinic.Address).HasMaxLength(500);
            entity.Property(clinic => clinic.SuspensionReason).HasMaxLength(500);
            entity.Property(clinic => clinic.TimeZoneId).HasMaxLength(100);
        });

        modelBuilder.Entity<ClinicInvitation>(entity =>
        {
            entity.HasKey(invitation => invitation.Id);
            entity.Property(invitation => invitation.Email).HasMaxLength(254).IsRequired();
            entity.Property(invitation => invitation.Role).HasMaxLength(50).IsRequired();
            entity.Property(invitation => invitation.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(invitation => invitation.TokenHash).IsUnique();
            entity.HasOne(invitation => invitation.Clinic).WithMany().HasForeignKey(invitation => invitation.ClinicId);
        });

        modelBuilder.Entity<Guardian>(entity =>
        {
            entity.HasKey(guardian => guardian.Id);
            entity.Property(guardian => guardian.Name).HasMaxLength(200).IsRequired();
            entity.Property(guardian => guardian.Phone).HasMaxLength(50).IsRequired();
            entity.Property(guardian => guardian.AlternatePhone).HasMaxLength(50);
            entity.Property(guardian => guardian.Address).HasMaxLength(500);
            entity.Property(guardian => guardian.IdentityType).HasMaxLength(100);
            entity.Property(guardian => guardian.IdentityNumber).HasMaxLength(100);
            entity.HasIndex(guardian => new { guardian.ClinicId, guardian.Phone });
            entity.HasOne(guardian => guardian.Clinic).WithMany(clinic => clinic.Guardians).HasForeignKey(guardian => guardian.ClinicId);
        });

        modelBuilder.Entity<ClinicUser>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.EntraObjectId).HasMaxLength(100).IsRequired();
            entity.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(50).IsRequired();
            entity.HasIndex(user => new { user.ClinicId, user.EntraObjectId }).IsUnique();
            entity.HasOne(user => user.Clinic).WithMany(clinic => clinic.Users).HasForeignKey(user => user.ClinicId);
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(patient => patient.Id);
            entity.Property(patient => patient.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(patient => new { patient.ClinicId, patient.Name });
            entity.HasOne(patient => patient.Clinic).WithMany(clinic => clinic.Patients).HasForeignKey(patient => patient.ClinicId);
            entity.HasOne(patient => patient.Guardian).WithMany(guardian => guardian.Patients).HasForeignKey(patient => patient.GuardianId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PatientGuardian>(entity =>
        {
            entity.HasKey(link => new { link.PatientId, link.GuardianId });
            entity.HasOne(link => link.Patient).WithMany(patient => patient.PatientGuardians).HasForeignKey(link => link.PatientId);
            entity.HasOne(link => link.Guardian).WithMany(guardian => guardian.PatientGuardians).HasForeignKey(link => link.GuardianId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(appointment => appointment.Id);
            entity.Property(appointment => appointment.Version).IsConcurrencyToken();
            entity.HasIndex(appointment => new { appointment.ClinicId, appointment.ConsultationId });
            entity.HasIndex(appointment => new { appointment.ClinicId, appointment.StartsAt });
            entity.HasOne(appointment => appointment.Clinic).WithMany(clinic => clinic.Appointments).HasForeignKey(appointment => appointment.ClinicId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(appointment => appointment.Patient).WithMany(patient => patient.Appointments).HasForeignKey(appointment => appointment.PatientId);
        });

        modelBuilder.Entity<Consultation>(entity =>
        {
            entity.HasKey(consultation => consultation.Id);
            entity.Property(consultation => consultation.Version).IsConcurrencyToken();
            entity.HasIndex(consultation => new { consultation.ClinicId, consultation.PatientId });
            entity.HasOne(consultation => consultation.Patient).WithMany(patient => patient.Consultations).HasForeignKey(consultation => consultation.PatientId);
        });

        modelBuilder.Entity<ClinicalAmendment>(entity =>
        {
            entity.HasKey(amendment => amendment.Id);
            entity.HasOne(amendment => amendment.Consultation).WithMany(consultation => consultation.Amendments)
                .HasForeignKey(amendment => amendment.ConsultationId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<WeightRecord>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Unit).HasMaxLength(20).IsRequired();
            entity.Property(record => record.RecordedBy).HasMaxLength(200).IsRequired();
            entity.HasIndex(record => new { record.ClinicId, record.PatientId, record.MeasuredOn });
            entity.HasOne(record => record.Patient).WithMany(patient => patient.WeightRecords).HasForeignKey(record => record.PatientId);
        });

        modelBuilder.Entity<VaccinationRecord>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.VaccineName).HasMaxLength(200).IsRequired();
            entity.Property(record => record.LotNumber).HasMaxLength(100);
            entity.Property(record => record.VeterinarianName).HasMaxLength(200).IsRequired();
            entity.HasIndex(record => new { record.ClinicId, record.PatientId, record.AdministeredOn });
            entity.HasOne(record => record.Patient).WithMany(patient => patient.VaccinationRecords).HasForeignKey(record => record.PatientId);
        });

        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.HasKey(prescription => prescription.Id);
            entity.HasIndex(prescription => prescription.ConsultationId).IsUnique();
            entity.HasOne(prescription => prescription.Consultation).WithOne(consultation => consultation.Prescription).HasForeignKey<Prescription>(prescription => prescription.ConsultationId);
        });

        modelBuilder.Entity<PrescriptionItem>(entity =>
        {
            entity.HasKey(item => item.Id);
            entity.Property(item => item.MedicationName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Presentation).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Concentration).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => new { item.PrescriptionId, item.SortOrder });
            entity.HasOne(item => item.Prescription).WithMany(prescription => prescription.Items).HasForeignKey(item => item.PrescriptionId);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Name).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Category).HasMaxLength(100).IsRequired();
            entity.Property(product => product.Sku).HasMaxLength(100);
            entity.Property(product => product.Unit).HasMaxLength(50).IsRequired();
            entity.Property(product => product.UnitPrice).HasPrecision(18, 2);
            entity.Property(product => product.StockOnHand).HasPrecision(18, 3);
            entity.Property(product => product.LowStockThreshold).HasPrecision(18, 3);
            entity.Property(product => product.Version).IsConcurrencyToken();
            entity.HasIndex(product => new { product.ClinicId, product.Name }).IsUnique();
            entity.HasIndex(product => new { product.ClinicId, product.Sku })
                .IsUnique()
                .HasFilter("[Sku] IS NOT NULL AND [Sku] <> ''");
            entity.HasOne(product => product.Clinic).WithMany(clinic => clinic.Products).HasForeignKey(product => product.ClinicId);
        });

        modelBuilder.Entity<CatalogService>(entity =>
        {
            entity.HasKey(service => service.Id);
            entity.Property(service => service.Name).HasMaxLength(200).IsRequired();
            entity.Property(service => service.Category).HasMaxLength(100).IsRequired();
            entity.Property(service => service.Description).HasMaxLength(1000).IsRequired();
            entity.Property(service => service.Unit).HasMaxLength(50).IsRequired();
            entity.Property(service => service.UnitPrice).HasPrecision(18, 2);
            entity.Property(service => service.Version).IsConcurrencyToken();
            entity.HasIndex(service => new { service.ClinicId, service.Name }).IsUnique();
            entity.HasOne<Clinic>().WithMany().HasForeignKey(service => service.ClinicId);
        });

        modelBuilder.Entity<BillingAccount>(entity =>
        {
            entity.HasKey(account => account.Id);
            entity.Property(account => account.Currency).HasMaxLength(3).IsRequired();
            entity.Property(account => account.Status).HasMaxLength(20).IsRequired();
            entity.Property(account => account.VoidReason).HasMaxLength(500);
            entity.Property(account => account.Total).HasPrecision(18, 2);
            entity.Property(account => account.Version).IsConcurrencyToken();
            entity.HasIndex(account => new { account.ClinicId, account.CreatedAt });
            entity.HasIndex(account => new { account.ClinicId, account.ConsultationId }).IsUnique().HasFilter("[ConsultationId] IS NOT NULL");
            entity.HasOne<Clinic>().WithMany().HasForeignKey(account => account.ClinicId);
            entity.HasOne<Patient>().WithMany().HasForeignKey(account => account.PatientId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Consultation>().WithMany().HasForeignKey(account => account.ConsultationId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ChargeLine>(entity =>
        {
            entity.HasKey(line => line.Id);
            entity.Property(line => line.ItemType).HasMaxLength(20).IsRequired();
            entity.Property(line => line.NameSnapshot).HasMaxLength(200).IsRequired();
            entity.Property(line => line.SkuSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(line => line.UnitSnapshot).HasMaxLength(50).IsRequired();
            entity.Property(line => line.Quantity).HasPrecision(18, 3);
            entity.Property(line => line.UnitPrice).HasPrecision(18, 2);
            entity.Property(line => line.LineTotal).HasPrecision(18, 2);
            entity.Property(line => line.FulfilledQuantity).HasPrecision(18, 3);
            entity.Property(line => line.PriceOverrideReason).HasMaxLength(500);
            entity.HasOne(line => line.BillingAccount).WithMany(account => account.Lines)
                .HasForeignKey(line => line.BillingAccountId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccountPayment>(entity =>
        {
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Kind).HasMaxLength(20).IsRequired();
            entity.Property(payment => payment.Method).HasMaxLength(20).IsRequired();
            entity.Property(payment => payment.IdempotencyKey).HasMaxLength(100).IsRequired();
            entity.Property(payment => payment.Amount).HasPrecision(18, 2);
            entity.Property(payment => payment.CashReceived).HasPrecision(18, 2);
            entity.Property(payment => payment.ChangeGiven).HasPrecision(18, 2);
            entity.Property(payment => payment.Note).HasMaxLength(500).IsRequired();
            entity.HasIndex(payment => new { payment.ClinicId, payment.IdempotencyKey }).IsUnique();
            entity.HasOne(payment => payment.BillingAccount).WithMany(account => account.Payments)
                .HasForeignKey(payment => payment.BillingAccountId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasKey(movement => movement.Id);
            entity.Property(movement => movement.ProductNameSnapshot).HasMaxLength(200).IsRequired();
            entity.Property(movement => movement.SkuSnapshot).HasMaxLength(100).IsRequired();
            entity.Property(movement => movement.Type).HasMaxLength(20).IsRequired();
            entity.Property(movement => movement.QuantityChange).HasPrecision(18, 3);
            entity.Property(movement => movement.BalanceAfter).HasPrecision(18, 3);
            entity.Property(movement => movement.Reason).HasMaxLength(500).IsRequired();
            entity.Property(movement => movement.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(movement => movement.IdempotencyKey).HasMaxLength(100);
            entity.HasIndex(movement => new { movement.ClinicId, movement.IdempotencyKey })
                .IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
            entity.HasIndex(movement => new { movement.ClinicId, movement.OperationKey }).IsUnique();
            entity.HasIndex(movement => new { movement.ProductId, movement.CreatedAt });
            entity.HasOne<Product>().WithMany().HasForeignKey(movement => movement.ProductId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(sale => sale.Id);
            entity.Property(sale => sale.PaymentMethod).HasMaxLength(50).IsRequired();
            entity.HasIndex(sale => new { sale.ClinicId, sale.CompletedAt });
            entity.HasOne(sale => sale.Clinic).WithMany(clinic => clinic.Sales).HasForeignKey(sale => sale.ClinicId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(sale => sale.Patient).WithMany().HasForeignKey(sale => sale.PatientId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<SaleLine>(entity =>
        {
            entity.HasKey(line => line.Id);
            entity.Property(line => line.ProductName).HasMaxLength(200).IsRequired();
            entity.HasOne(line => line.Sale).WithMany(sale => sale.Lines).HasForeignKey(line => line.SaleId);
        });
    }
}