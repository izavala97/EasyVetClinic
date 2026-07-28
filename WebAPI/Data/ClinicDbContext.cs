using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Data;

public class ClinicDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<ClinicUser> ClinicUsers => Set<ClinicUser>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Consultation> Consultations => Set<Consultation>();
    public DbSet<WeightRecord> WeightRecords => Set<WeightRecord>();
    public DbSet<VaccinationRecord> VaccinationRecords => Set<VaccinationRecord>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Clinic>(entity =>
        {
            entity.HasKey(clinic => clinic.Id);
            entity.Property(clinic => clinic.Name).HasMaxLength(200).IsRequired();
            entity.Property(clinic => clinic.Address).HasMaxLength(500);
            entity.Property(clinic => clinic.VeterinarianName).HasMaxLength(200);
            entity.Property(clinic => clinic.VeterinarianTitles).HasMaxLength(300);
            entity.Property(clinic => clinic.VeterinarianLicenseNumber).HasMaxLength(100);
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

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(appointment => appointment.Id);
            entity.HasIndex(appointment => new { appointment.ClinicId, appointment.StartsAt });
            entity.HasOne(appointment => appointment.Clinic).WithMany(clinic => clinic.Appointments).HasForeignKey(appointment => appointment.ClinicId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(appointment => appointment.Patient).WithMany(patient => patient.Appointments).HasForeignKey(appointment => appointment.PatientId);
        });

        modelBuilder.Entity<Consultation>(entity =>
        {
            entity.HasKey(consultation => consultation.Id);
            entity.HasIndex(consultation => new { consultation.ClinicId, consultation.PatientId });
            entity.HasOne(consultation => consultation.Patient).WithMany(patient => patient.Consultations).HasForeignKey(consultation => consultation.PatientId);
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
            entity.HasIndex(product => new { product.ClinicId, product.Name }).IsUnique();
            entity.HasOne(product => product.Clinic).WithMany(clinic => clinic.Products).HasForeignKey(product => product.ClinicId);
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