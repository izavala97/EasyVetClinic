using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Data;

public sealed class ClinicDbContext(DbContextOptions<ClinicDbContext> options) : DbContext(options)
{
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Consultation> Consultations => Set<Consultation>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Clinic>(entity =>
        {
            entity.HasKey(clinic => clinic.Id);
            entity.Property(clinic => clinic.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<Guardian>(entity =>
        {
            entity.HasKey(guardian => guardian.Id);
            entity.Property(guardian => guardian.Name).HasMaxLength(200).IsRequired();
            entity.Property(guardian => guardian.Phone).HasMaxLength(50).IsRequired();
            entity.HasIndex(guardian => new { guardian.ClinicId, guardian.Phone });
            entity.HasOne(guardian => guardian.Clinic).WithMany(clinic => clinic.Guardians).HasForeignKey(guardian => guardian.ClinicId);
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasKey(patient => patient.Id);
            entity.Property(patient => patient.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(patient => new { patient.ClinicId, patient.Name });
            entity.HasOne(patient => patient.Clinic).WithMany(clinic => clinic.Patients).HasForeignKey(patient => patient.ClinicId);
            entity.HasOne(patient => patient.Guardian).WithMany(guardian => guardian.Patients).HasForeignKey(patient => patient.GuardianId);
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(appointment => appointment.Id);
            entity.HasIndex(appointment => new { appointment.ClinicId, appointment.StartsAt });
            entity.HasOne(appointment => appointment.Clinic).WithMany(clinic => clinic.Appointments).HasForeignKey(appointment => appointment.ClinicId);
            entity.HasOne(appointment => appointment.Patient).WithMany(patient => patient.Appointments).HasForeignKey(appointment => appointment.PatientId);
        });

        modelBuilder.Entity<Consultation>(entity =>
        {
            entity.HasKey(consultation => consultation.Id);
            entity.HasIndex(consultation => new { consultation.ClinicId, consultation.PatientId });
            entity.HasOne(consultation => consultation.Patient).WithMany(patient => patient.Consultations).HasForeignKey(consultation => consultation.PatientId);
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
            entity.HasOne(sale => sale.Clinic).WithMany(clinic => clinic.Sales).HasForeignKey(sale => sale.ClinicId);
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