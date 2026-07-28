using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Data;

public static class ClinicDatabaseInitializer
{
    public static async Task SeedDevelopmentDataAsync(ClinicDbContext dbContext)
    {
        const string clinicId = "clinic-alitos-vet";
        if (await dbContext.Clinics.AnyAsync())
        {
            await SeedProductsAsync(dbContext, clinicId);
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var clinic = new Clinic { Id = clinicId, Name = "Alito's Vet" };
        var guardians = new[]
        {
            new Guardian { Id = "guardian-karinka", ClinicId = clinicId, Name = "Karinka Dominique Estrada Guzman", Phone = "5527126698" },
            new Guardian { Id = "guardian-sofia", ClinicId = clinicId, Name = "Sofia Hernandez", Phone = "3312457789" },
            new Guardian { Id = "guardian-diego", ClinicId = clinicId, Name = "Diego Ramirez", Phone = "3314219087" }
        };
        var patients = new[]
        {
            new Patient { Id = "pet-kira", ClinicId = clinicId, GuardianId = "guardian-karinka", Name = "Kira", Species = "Feline", Breed = "Domestic longhair", Sex = "Female", Weight = "1.3 kg", Color = "Black and white", Allergies = "No known allergies", LastVisit = today.AddDays(-42) },
            new Patient { Id = "pet-luna", ClinicId = clinicId, GuardianId = "guardian-sofia", Name = "Luna", Species = "Canine", Breed = "Mixed breed", Sex = "Female", Weight = "18.2 kg", Color = "Brown", Allergies = "Chicken", LastVisit = today.AddDays(-47) },
            new Patient { Id = "pet-bruno", ClinicId = clinicId, GuardianId = "guardian-diego", Name = "Bruno", Species = "Canine", Breed = "Labrador", Sex = "Male", Weight = "31.4 kg", Color = "Black", Allergies = "No known allergies", LastVisit = today.AddDays(-55) }
        };
        var appointments = new[]
        {
            new Appointment { Id = "appointment-kira", ClinicId = clinicId, PatientId = "pet-kira", StartsAt = today.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc), Reason = "General consultation", ClinicianName = "MVZ. Alondra Licona" },
            new Appointment { Id = "appointment-luna", ClinicId = clinicId, PatientId = "pet-luna", StartsAt = today.ToDateTime(new TimeOnly(11, 30), DateTimeKind.Utc), Reason = "Vaccination", ClinicianName = "MVZ. Alondra Licona" },
            new Appointment { Id = "appointment-bruno", ClinicId = clinicId, PatientId = "pet-bruno", StartsAt = today.ToDateTime(new TimeOnly(16, 0), DateTimeKind.Utc), Reason = "Post-operative checkup", ClinicianName = "MVZ. Alondra Licona" },
            new Appointment { Id = "appointment-bruno-followup", ClinicId = clinicId, PatientId = "pet-bruno", StartsAt = today.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc), Reason = "Follow-up", ClinicianName = "MVZ. Alondra Licona" }
        };

        dbContext.Add(clinic);
        dbContext.AddRange(guardians);
        dbContext.AddRange(patients);
        dbContext.AddRange(appointments);
        await dbContext.SaveChangesAsync();
        await SeedProductsAsync(dbContext, clinicId);
    }

    private static async Task SeedProductsAsync(ClinicDbContext dbContext, string clinicId)
    {
        if (await dbContext.Products.AnyAsync(product => product.ClinicId == clinicId))
        {
            return;
        }

        dbContext.Products.AddRange(
            new Product { Id = "product-rabies-vaccine", ClinicId = clinicId, Name = "Rabies vaccine", Category = "Vaccines", UnitPrice = 350m, StockOnHand = 18 },
            new Product { Id = "product-flea-treatment", ClinicId = clinicId, Name = "Flea treatment", Category = "Preventives", UnitPrice = 280m, StockOnHand = 12 },
            new Product { Id = "product-recovery-food", ClinicId = clinicId, Name = "Recovery diet 156 g", Category = "Nutrition", UnitPrice = 95m, StockOnHand = 9 },
            new Product { Id = "product-e-collar", ClinicId = clinicId, Name = "Elizabethan collar", Category = "Supplies", UnitPrice = 180m, StockOnHand = 6 });
        await dbContext.SaveChangesAsync();
    }
}