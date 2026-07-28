using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EasyVetClinic.Api.Data;

public sealed class SqlServerClinicDbContext(DbContextOptions<SqlServerClinicDbContext> options)
    : ClinicDbContext(options)
{
}

public sealed class SqlServerClinicDbContextFactory : IDesignTimeDbContextFactory<SqlServerClinicDbContext>
{
    public SqlServerClinicDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ClinicDatabase")
            ?? "Server=(localdb)\\mssqllocaldb;Database=EasyVetClinicDesign;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<SqlServerClinicDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new SqlServerClinicDbContext(options);
    }
}