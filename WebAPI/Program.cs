using EasyVetClinic.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<ClinicDbContext>(options =>
	options.UseSqlite(builder.Configuration.GetConnectionString("ClinicDatabase")));
builder.Services.AddScoped<CurrentClinic>();
builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(policy => policy
		.WithOrigins("http://localhost:5173")
		.AllowAnyHeader()
		.AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
	using var scope = app.Services.CreateScope();
	var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
	await dbContext.Database.MigrateAsync();
	await ClinicDatabaseInitializer.SeedDevelopmentDataAsync(dbContext);
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
