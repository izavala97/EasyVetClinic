using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
var connectionString = builder.Configuration.GetConnectionString("ClinicDatabase")
	?? throw new InvalidOperationException("The ClinicDatabase connection string is required.");
var databaseProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";
builder.Services.AddDbContext<ClinicDbContext>(options =>
{
	if (string.Equals(databaseProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
	{
		options.UseSqlServer(connectionString);
	}
	else if (string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
	{
		options.UseSqlite(connectionString);
	}
	else
	{
		throw new InvalidOperationException("Database:Provider must be Sqlite or SqlServer.");
	}
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentClinic>();
if (builder.Environment.IsDevelopment())
{
	builder.Services.AddAuthentication(DevelopmentAuthenticationHandler.SchemeName)
		.AddScheme<AuthenticationSchemeOptions, DevelopmentAuthenticationHandler>(DevelopmentAuthenticationHandler.SchemeName, _ => { });
}
else
{
	var authority = builder.Configuration["Authentication:Authority"]
		?? throw new InvalidOperationException("Authentication:Authority is required in production.");
	var audience = builder.Configuration["Authentication:Audience"]
		?? throw new InvalidOperationException("Authentication:Audience is required in production.");
	builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
		.AddJwtBearer(options =>
		{
			options.Authority = authority;
			options.Audience = audience;
			options.MapInboundClaims = false;
			options.TokenValidationParameters = new TokenValidationParameters
			{
				NameClaimType = "name"
			};
		});
}
builder.Services.AddAuthorization();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (builder.Environment.IsDevelopment())
{
	allowedOrigins = [.. allowedOrigins, "http://localhost:5173"];
}
builder.Services.AddCors(options =>
{
	options.AddDefaultPolicy(policy => policy
		.WithOrigins(allowedOrigins.Distinct(StringComparer.OrdinalIgnoreCase).ToArray())
		.AllowAnyHeader()
		.AllowAnyMethod());
});

var app = builder.Build();

app.Use(async (context, next) =>
{
	try
	{
		await next(context);
	}
	catch (ClinicAccessException)
	{
		context.Response.StatusCode = StatusCodes.Status403Forbidden;
		await context.Response.WriteAsJsonAsync(new { error = "Clinic access is not permitted." });
	}
});

if (app.Environment.IsDevelopment())
{
	using var scope = app.Services.CreateScope();
	var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
	await dbContext.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
