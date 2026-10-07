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

The Development environment uses SQLite. On application startup, EF Core applies any pending migrations and creates `easyvetclinic.db` in the `WebAPI` directory. No sample clinic, user, patient, guardian, appointment, consultation, inventory, or sale data is seeded.

To reset it to an empty schema, stop the API and run this command from the repository root:

```powershell
.\scripts\start-dev.ps1 -ResetDatabase
```

The `ClinicDatabase` connection string and `Database:Provider` are configured in [appsettings.json](appsettings.json). Local development defaults to `Sqlite`. EF Core creates and updates the SQLite file automatically when the API starts.

## Migrations

The provider-specific migration paths are intentional:

- `Data/Migrations/` contains SQLite migrations for local development. `ClinicDbContextFactory` ensures `dotnet ef` commands targeting `ClinicDbContext` use SQLite.
- `Data/SqlServerMigrations/` contains SQL Server migrations for Azure SQL production.
- The API applies pending EF Core migrations at startup for both providers, so database schema updates happen automatically on each deploy.

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
| `GET` | `/api/me` | Identity, platform ownership, selected clinic, and active memberships including suspension status. |
| `POST` | `/api/onboarding/clinic` | Register a clinic; configured platform owner only. |
| `GET` | `/api/platform/clinics` | List all clinics, active user counts, and suspension reasons; owner only. |
| `PUT` | `/api/platform/clinics/{clinicId}/access` | Suspend/restore a clinic; owner only. |
| `GET` | `/api/clinic/users` | List members of the selected clinic; clinic administrator or owner. |
| `PUT` | `/api/clinic/users/{userId}` | Update a clinic role or active status; protects the last active administrator. |
| `GET`, `POST` | `/api/clinic/invitations` | List or create seven-day, single-use invitations; clinic administrator or owner. |
| `DELETE` | `/api/clinic/invitations/{invitationId}` | Revoke an invitation in the selected clinic. |
| `POST` | `/api/invitations/accept` | Accept a token with a matching verified email claim and create a clinic membership. |
| `GET` | `/api/dashboard` | Active clinic dashboard summary. |
| `GET` | `/api/operations/today` | Active care, pending accounts, low stock, and balances grouped by currency. |
| `GET` | `/api/patients?query=` | Search patients by pet, guardian, or phone number. |
| `GET` | `/api/patients/{patientId}` | Retrieve a patient summary. |
| `GET` | `/api/appointments?date=` | Retrieve appointments for a day; use `year` and `month` for a calendar month. |
| `POST` | `/api/appointments` | Create a clinician-specific visit with duration and clinic-local time; overlapping intervals return `409 Conflict`. |
| `PUT` | `/api/appointments/{appointmentId}` | Update appointment lifecycle and scheduling using its expected version. |
| `GET`, `POST`, `PUT`, `DELETE` | `/api/catalog/products`, `/api/catalog/services` | Clinic-scoped catalogs; DELETE archives an item and preserves historical charges. |
| `GET`, `POST` | `/api/billing/accounts` | Find or create walk-in / consultation-linked accounts. |
| `POST`, `PUT`, `DELETE` | `/api/billing/accounts/{id}/lines` | Draft charge entries with server-side prices and expected versions; PUT/DELETE include the line ID. |
| `POST` | `/api/billing/accounts/{id}/open` | Lock current charges and open collection. |
| `POST` | `/api/billing/accounts/{id}/payments` | Record partial/full payment independently of stock fulfillment. |
| `POST` | `/api/billing/accounts/{id}/fulfill` | Explicitly record physical product fulfillment. |
| `POST` | `/api/billing/accounts/{id}/refunds`, `/api/billing/accounts/{id}/void` | Controlled administrator corrections without automatic restocking. |
| `GET` | `/api/inventory`, `/api/inventory/movements` | Stock balances and movement history. |
| `POST` | `/api/inventory/adjustments` | Attributed stock adjustment with a reason. |
| `POST` | `/api/patients/{patientId}/consultations` | Create an in-progress consultation. |
| `PUT` | `/api/consultations/{id}` | Save or finalize clinical care using an expected version. |
| `POST` | `/api/consultations/{id}/amendments`, `/api/consultations/{id}/cancel` | Preserve the clinical record while adding attributed corrections or cancellation. |
| `GET`, `POST` | `/api/patients/{id}/weights`, `/api/patients/{id}/vaccinations` | Structured clinical timelines; recording is not billing or dispensing. |
| `GET` | `/api/clinicians` | Active clinicians available for care and scheduling. |
| `PUT` | `/api/clinicians/{objectId}/credentials` | Administrator-maintained professional credentials, snapshotted when care starts. |
| `GET` | `/api/patients/{patientId}/documents/{documentType}` | Prepare a clinical document draft. |

`documentType` supports `health-certificate`, `surgical-consent`, and `boarding-contract`.

## Technical Conventions

- Source code, DTOs, models, endpoints, and HTTP contracts use English.
- Future Azure SQL tables, columns, keys, migrations, and indexes use English.
- Every tenant-owned operational entity includes `ClinicId`.

## Current State

The API uses EF Core with SQLite for local development and a separate SQL Server migration history for production. Production validates Entra External ID bearer tokens. Clinic identity is resolved from the `oid` claim and active `ClinicUsers` memberships; suspended clinics deny member operational access. Only the configured `PlatformAdministration:OwnerObjectId` can register clinics and select arbitrary clinic workspaces for support. It is empty by default in production; local Development configures `local-developer` as owner. Owner support access and administrative mutations are logged.

Invitations require matching authoritative `email`/`emails` claims on acceptance, retain only a hash of the token, and cannot grant platform ownership. They do not send email or create identity-provider accounts. See [the deployment checklist](../DEPLOYMENT.MD) for owner and verified-email configuration.

Catalog and inventory administration are available through the application. Legacy sales remain historical records; new commercial workflows must use billing accounts rather than bypassing the inventory ledger.

See [Clinical and commercial workflows](../docs/WORKFLOWS.md) for additive migrations, concurrency, verification commands, and first-release limitations.