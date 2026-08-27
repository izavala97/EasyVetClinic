# EasyVetClinic Web API

The EasyVetClinic REST API is built with ASP.NET Core on .NET 10. The project file is `WebAPI.csproj`; source-code namespaces use `EasyVetClinic.Api`.

## Run

From the repository root, use the shared development launcher:

```powershell
.\scripts\start-dev.ps1
```

To run the API by itself:

```powershell
dotnet run --launch-profile http
```

The API listens at `http://localhost:5120` in the HTTP development profile.

## Local Database

The Development environment uses SQLite. On application startup, EF Core applies pending migrations and creates `easyvetclinic.db` in the `WebAPI` directory. No sample clinic, user, patient, guardian, appointment, consultation, inventory, or sale data is seeded.

To reset it to an empty schema, stop the API and run this command from the repository root:

```powershell
.\scripts\start-dev.ps1 -ResetDatabase
```

The `ClinicDatabase` connection string and `Database:Provider` are configured in [appsettings.json](appsettings.json). Local development defaults to `Sqlite`. EF Core creates and updates the SQLite file automatically when the API starts; do not run a SQL script locally.

## Migrations

The provider-specific migration paths are intentional:

- `Data/Migrations/` contains SQLite migrations for local development. `ClinicDbContextFactory` ensures `dotnet ef` commands targeting `ClinicDbContext` use SQLite.
- `Data/SqlServerMigrations/` contains SQL Server migrations for Azure SQL production. They must be applied through a controlled deployment process, never by the API at production startup.

Create a local SQLite migration after changing the EF model:

```powershell
dotnet ef migrations add <MigrationName> --context ClinicDbContext --output-dir Data/Migrations
```

Generate the SQL Server deployment script from its EF Core migrations when preparing a release:

```powershell
dotnet ef migrations script --context SqlServerClinicDbContext --output scripts/Apply-SqlServerMigrations.sql
```

## Current Endpoints

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/health` | API health status. |
| `GET` | `/api/dashboard` | Active clinic dashboard summary. |
| `GET` | `/api/patients?query=` | Search patients by pet, guardian, or phone number. |
| `GET` | `/api/patients/{patientId}` | Retrieve a patient summary. |
| `POST` | `/api/patients/{patientId}/consultations` | Create an in-progress consultation. |
| `GET` | `/api/patients/{patientId}/documents/{documentType}` | Prepare a clinical document draft. |

`documentType` supports `health-certificate`, `surgical-consent`, and `boarding-contract`.

## Technical Conventions

- Source code, DTOs, models, endpoints, and HTTP contracts use English.
- Future Azure SQL tables, columns, keys, migrations, and indexes use English.
- Every tenant-owned operational entity includes `ClinicId`.

## Current State

The API uses EF Core with SQLite for local development and a separate SQL Server migration history for production. Clinic identity is resolved from Easy Auth/External ID claims and active `ClinicUsers` memberships. The initial clinic can be created once by an authenticated user through `POST /api/onboarding/clinic`; this endpoint is unavailable after the first clinic exists.