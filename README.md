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

The current vertical slice includes an operational dashboard, patient and multi-guardian management, consultation creation, document draft preparation for health certificates, surgical consent, and boarding contracts. Scheduling provides monthly and daily calendar views, prevents time collisions, and lets staff edit an appointment from its schedule or dashboard row. Point of sale supports inventory products and manually priced service charges; product stock is decremented only for inventory lines. Local SQLite schemas are created through EF Core migrations; the repository does not seed test clinics, users, patients, guardians, appointments, inventory, or consultations.

The Azure App Service API and Azure Static Web App deployment resources are configured through GitHub Actions. The API uses SQLite locally, with a separate SQL Server EF Core migration set for production. Entra External ID browser sign-in, owner-only clinic registration, clinic staff administration, and manual subscription suspension are implemented. Signed PDF generation, private file storage, automatic billing integration, and complete operational modules remain before production go-live. See [DEPLOYMENT.MD](DEPLOYMENT.MD) for the Azure configuration checklist.

## Clinic onboarding and administration

- **Platform owner:** a server-configured Entra object ID, independent of clinic memberships. Only this identity can register clinics, list all clinics, suspend/restore clinic access, and open any clinic workspace for support. Clinic roles cannot grant platform ownership.
- **Clinic administrator:** manages only their selected clinic's profile, invitations, and members. Can assign `ClinicAdmin`, `Veterinarian`, or `Staff`, change roles, or deactivate/reactivate members. The last active clinic administrator cannot be removed or demoted.
- **Invitation flow:** the owner registers a clinic, opens its support workspace, and invites its first administrator from **Clinic administration**. That administrator subsequently invites staff. Invitations are single-use, expire after seven days, and can be revoked. The link is shown only when created and must be shared manually; no email service is configured. Recipients sign in with the invited, verified email address and explicitly accept.
- **Unassigned users:** see an access-required screen, not clinic registration. Account details displays their Entra object ID for owner configuration.
- **Multiple clinics:** select an authorized clinic in the sidebar. Selection is per browser session in memory and checked by the API on every operational request.
- **Suspension:** manually suspend a clinic with a reason from **Platform administration**. Members lose all operational API access on their next request, including staff administration and invitation acceptance. Data and memberships are retained. The platform owner retains support access. No payment-provider webhook or automatic subscription calculation is implemented.
- Administrative changes and owner support requests are logged without invitation tokens. Invitation tokens are stored hashed in the database and kept out of URL query strings.

Production owner access is disabled until `PlatformAdministration__OwnerObjectId` is configured. The intended owner is `maizorin@hotmail.es`; email alone never grants ownership. Local development configures `local-developer` as owner; never use Development authentication in production.

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

An empty database starts in **Platform administration** for the configured local owner. Register a clinic, open its workspace, and invite its administrator. Development authentication supplies a local identity, so this can be tested without a production Entra sign-in. To test invitation acceptance locally, configure `DevelopmentIdentity__ObjectId` and `DevelopmentIdentity__Email` for a different local identity and restart the API. Production provisioning is described in [DEPLOYMENT.MD](DEPLOYMENT.MD).

The SQLite database and its companion files are ignored by Git. EF Core creates and updates it automatically at API startup; no manual SQL script is needed. Use the `ClinicDatabase` connection string in [WebAPI/appsettings.json](WebAPI/appsettings.json) to point the application to a different local SQLite file.

## Verification

```powershell
dotnet build EasyVetClinic.slnx

Set-Location WebClient
npm run build
```

Administration integration checks (isolated temporary SQLite database, no production data):

```powershell
.\scripts\test-administration.ps1
```
