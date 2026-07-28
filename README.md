# EasyVetClinic

EasyVetClinic is a multi-tenant SaaS platform for veterinary clinic operations. It will provide clinical records, scheduling, printable documents, boarding, inventory, and point-of-sale workflows.

## Repository Structure

| Directory | Responsibility |
| --- | --- |
| [WebAPI](WebAPI) | ASP.NET Core .NET 10 REST API. |
| [WebClient](WebClient) | React, TypeScript, and Vite web application. |
| [.azure](.azure) | Azure deployment planning and future infrastructure configuration. |

`WebAPI/` directly contains `WebAPI.csproj`; there is no redundant nested API project directory.

## Language Convention

All repository documentation and technical artifacts are written in English. Source code, API contracts, infrastructure, database schemas, tables, columns, indexes, migrations, and identifiers must use English names. User-facing clinic content may be localized later through the web client.

## Current Implementation

The current vertical slice includes an Alito's Vet operational dashboard, appointment summary, patient search, consultation creation, and document draft preparation for health certificates, surgical consent, and boarding contracts. Development data is persisted in a local SQLite database through EF Core migrations.

Upcoming work includes Azure SQL production persistence, EasyAuth/External ID authentication, claim-based tenant resolution, authorization roles, signed PDF generation, private file storage, and complete operational modules.

## Run Locally

Run one command from the repository root:

```powershell
.\scripts\start-dev.ps1
```

The script starts the API at `http://localhost:5120` and the client at `http://localhost:5173`. When the API starts in the Development environment, it automatically applies the EF Core migrations and creates `WebAPI/easyvetclinic.db` if it does not exist.

To discard local data and create a clean development database with the sample records:

```powershell
.\scripts\start-dev.ps1 -ResetDatabase
```

The SQLite database and its companion files are ignored by Git. Use the `ClinicDatabase` connection string in [WebAPI/appsettings.json](WebAPI/appsettings.json) to point the application to a different local SQLite file.

## Verification

```powershell
dotnet build EasyVetClinic.slnx

Set-Location WebClient
npm run build
```
