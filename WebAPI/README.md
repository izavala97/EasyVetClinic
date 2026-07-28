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

The Development environment uses SQLite. On application startup, EF Core applies pending migrations and creates `easyvetclinic.db` in the `WebAPI` directory. The development database is seeded only when the `Clinics` table is empty.

To reset it, stop the API and run this command from the repository root:

```powershell
.\scripts\start-dev.ps1 -ResetDatabase
```

The `ClinicDatabase` connection string is configured in [appsettings.json](appsettings.json). Use environment-specific configuration for a different local database location or for the future Azure SQL connection.

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

The API uses EF Core with SQLite for local development. The next backend slice will move the production connection to Azure SQL and replace the configured development clinic with a clinic identity resolved from EasyAuth/External ID claims.