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

The current vertical slice includes an operational dashboard, patient search, consultation creation, and document draft preparation for health certificates, surgical consent, and boarding contracts. Local SQLite schemas are created through EF Core migrations; the repository does not seed test clinics, users, patients, guardians, appointments, inventory, or consultations.

The Azure App Service API and Azure Static Web App deployment resources are configured through GitHub Actions. The API now exposes authenticated initial-clinic onboarding and uses SQLite locally, with a separate SQL Server EF Core migration set for production. External ID/Easy Auth browser sign-in, signed PDF generation, private file storage, and complete operational modules remain before production go-live. See [DEPLOYMENT.MD](DEPLOYMENT.MD) for the Azure configuration checklist.

## Run Locally

Run one command from the repository root:

```powershell
.\scripts\start-dev.ps1
```

The script starts the API at `http://localhost:5120` and the client at `http://localhost:5173`. When the API starts in the Development environment, it automatically applies the EF Core migrations and creates `WebAPI/easyvetclinic.db` if it does not exist. The local client uses the API's development identity and does not require Microsoft Entra or `VITE_ENTRA_*` values.

To discard local data and create an empty development database:

```powershell
.\scripts\start-dev.ps1 -ResetDatabase
```

An empty database requires an initial clinic and authenticated clinic-user membership to be provisioned before protected operational endpoints can be used. Development authentication supplies a local identity, so this can be tested without a production Entra sign-in. Production provisioning is described in [DEPLOYMENT.MD](DEPLOYMENT.MD).

The SQLite database and its companion files are ignored by Git. EF Core creates and updates it automatically at API startup; no manual SQL script is needed. Use the `ClinicDatabase` connection string in [WebAPI/appsettings.json](WebAPI/appsettings.json) to point the application to a different local SQLite file.

## Verification

```powershell
dotnet build EasyVetClinic.slnx

Set-Location WebClient
npm run build
```
