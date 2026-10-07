using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Data;

internal static class ClinicScheduling
{
    public static Task<int> LockAsync(ClinicDbContext dbContext, string clinicId) =>
        dbContext.Clinics.Where(clinic => clinic.Id == clinicId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(clinic => clinic.SchedulingVersion, clinic => clinic.SchedulingVersion + 1));
}
