# EasyVetClinic Azure Deployment Checklist

This checklist describes the Azure resources and configuration needed before production deployment. Do not place secrets in this repository.

## Current deployment baseline

- Resource group: `EasyVetClinic-RG`.
- API App Service: `EasyVetClinic-API` on Linux, deployed by [main_easyvetclinic-api.yml](.github/workflows/main_easyvetclinic-api.yml).
- Static Web App: `EasyVetClinic-WebCli`, deployed by [azure-static-web-apps-red-plant-0ec64f60f.yml](.github/workflows/azure-static-web-apps-red-plant-0ec64f60f.yml).
- Static Web App production domain: `https://red-plant-0ec64f60f.7.azurestaticapps.net`.
- API default domain: `https://easyvetclinic-api-ayhmbsb2cbajeef2.canadacentral-01.azurewebsites.net`.
- Local development defaults to SQLite. Production uses the existing Azure SQL database after its SQL Server EF Core migrations have been applied by the release process.

## GitHub Actions configuration

- Add the repository Actions variable `VITE_API_BASE_URL` with the API default domain above, without a trailing slash. This is intentionally a variable, not a secret: Vite embeds it in the browser bundle.
- Add these non-secret repository Actions variables for the Static Web App build. They are embedded in the browser bundle and therefore must not contain secrets:
	- `VITE_ENTRA_TENANT_ID`: `134a684d-d19f-4215-bae2-e45dcb315751`
	- `VITE_ENTRA_CLIENT_ID`: `1cf9c954-6737-4d9f-b192-f1f5c43a6f44`
	- `VITE_ENTRA_API_SCOPE`: `api://a0c84c31-b8c4-4d60-8e25-74ace0817d54/access`
	- `VITE_ENTRA_AUTHORITY`: `https://easyvetclinic.ciamlogin.com`. The frontend appends the configured tenant ID to construct the OpenID discovery authority.
	Their expected names and formats are in [WebClient/.env.example](WebClient/.env.example).
- The Static Web App deployment token is already stored as the GitHub secret created by Azure. Do not copy or commit that token.
- The API workflow uses the OpenID Connect credentials created by Azure. Do not rename or delete the `AZUREAPPSERVICE_*` GitHub secrets unless the federated credential is replaced at the same time.
- Pushes to `main` deploy both apps. Check the GitHub Actions runs after each deployment.

## 1. Azure subscription and naming

- Azure subscription and resource group.
- Primary deployment region.
- Production domain name for the web app.
- Naming prefix for the resource group, App Service, Static Web App, SQL server, storage account, Key Vault, and Application Insights.

## 2. Microsoft Entra External ID

- An External ID tenant for clinic users.
- A SPA application registration for the React web client.
- An API application registration for EasyVetClinic API.
- An API scope, such as `api://<api-application-id>/access`.
- SPA redirect URIs for the production domain and local development.
- Production sign-out redirect URI.
- Authentication policy with MFA and approved user flows.
- The External ID tenant ID, SPA client ID, API client ID, and API scope value.
- Configured values: tenant ID `134a684d-d19f-4215-bae2-e45dcb315751`; SPA client ID `1cf9c954-6737-4d9f-b192-f1f5c43a6f44`; API client ID `a0c84c31-b8c4-4d60-8e25-74ace0817d54`; API scope `api://a0c84c31-b8c4-4d60-8e25-74ace0817d54/access`.
- The `EasyVetClinicSignUpSignIn` flow uses email/password with display name and email address.

## 3. API bearer-token validation

- An Azure App Service plan and Linux App Service for the API.
- App Service Authentication is disabled. The API validates bearer tokens itself through ASP.NET Core JWT bearer authentication.
- The API validates issuer `https://134a684d-d19f-4215-bae2-e45dcb315751.ciamlogin.com/134a684d-d19f-4215-bae2-e45dcb315751/v2.0` and audience `a0c84c31-b8c4-4d60-8e25-74ace0817d54`.
- HTTPS-only enabled and a custom domain/certificate configured if applicable.

## 4. Azure SQL Database

- Existing Azure SQL logical server: `easyvetclinic-dbserver.database.windows.net`.
- Existing database: `easyvetclinic-database` in Canada Central.
- Microsoft Entra-only authentication enabled.
- An Entra administrator for the SQL server.
- An App Service system-assigned managed identity.
- A contained database user for the App Service managed identity with required application permissions.
- SQL networking that permits the App Service connection: use private networking for production, or temporarily enable the server's "Allow Azure services and resources to access this server" firewall option while private networking is not configured.
- Production backup retention, geo-redundancy, and sizing decisions.
- The API applies pending EF Core migrations at startup, so the database schema is updated automatically for each deployment.
- You can still generate the SQL Server EF Core migration script for auditing or controlled release review with `dotnet ef migrations script --context SqlServerClinicDbContext --output scripts/Apply-SqlServerMigrations.sql`.
- Create the managed identity database user as the SQL Entra administrator, then grant `db_datareader` and `db_datawriter` to that user. The application does not need schema-change permissions at runtime because migrations run through the app startup path.
- Set these App Service settings together after the migration succeeds:
	- `Database__Provider=SqlServer`
	- `ConnectionStrings__ClinicDatabase=Server=tcp:easyvetclinic-dbserver.database.windows.net,1433;Initial Catalog=easyvetclinic-database;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;`

## 5. Storage and secrets

- Storage account with private Blob containers for pet photos, clinic logos, identity documents, and generated documents.
- App Service managed identity granted `Storage Blob Data Contributor` only where required.
- Key Vault with secrets/configuration references for SQL and Entra settings where managed identity cannot be used directly.
- App Service managed identity granted Key Vault secret-read permission.
- Blob retention and document lifecycle policy.

## 6. Observability and network security

- Application Insights and Log Analytics workspace.
- Alerts for availability, HTTP 5xx responses, authentication failures, SQL failures, and storage failures.
- Set the API App Service setting `Cors__AllowedOrigins__0` to `https://red-plant-0ec64f60f.7.azurestaticapps.net`. Add custom production domains as additional numbered entries, such as `Cors__AllowedOrigins__1`.
- TLS and custom-domain configuration.
- Optional private endpoints/VNet integration if clinic data policy requires private database and storage access.

## 7. Application configuration to supply as App Service settings

- `Cors__AllowedOrigins__0` for the Static Web App production domain.
- `Database__Provider=SqlServer` and `ConnectionStrings__ClinicDatabase` after the Azure SQL migration is applied.
- Storage account/container settings.
- Application Insights connection string.
- Allowed SPA origin.

## 8. Initial access setup

- Sign in to the web app as `maizorin@hotmail.es` using the EasyVetClinic External ID tenant. Expand **Account details** in the sidebar and copy the **Entra object ID** (also returned by authenticated `GET /api/me`). This is the tenant-specific `oid`, not the SPA application ID or an object ID from another tenant.
- Set the API App Service setting `PlatformAdministration__OwnerObjectId` to that exact object ID. Restart/reload and verify **Platform administration** appears only for that account. Keep the value empty until the correct account is confirmed. Ownership is never inferred from an email or a clinic's `SuperAdmin` membership.
- The owner creates clinics through **Register clinic** (`POST /api/onboarding/clinic`). Multiple clinics are supported; there is no first-user-wins bootstrap and no automatic clinic membership for the owner.
- The owner opens each clinic workspace and creates a `ClinicAdmin` invitation from **Clinic administration**. Copy the generated link and send it manually to the intended administrator. That administrator signs in with the matching email and accepts the invitation, then invites veterinarians and staff.
- Configure the API access token to emit an authoritative, verified `email` or `emails` claim from the External ID sign-in flow. Do not map an editable profile attribute or `preferred_username` as a verified email. Acceptance is denied if the token does not contain the invited email. Test this in production before sending invitations; the app does not create Entra users or send mail itself.
- Invitation links expire after seven days, are single-use, and can be revoked by an administrator. Share links privately. The plaintext token is shown only on creation; the database stores its SHA-256 hash. The frontend temporarily retains the token in tab-scoped session storage across sign-in/account switching and clears it on acceptance/cancellation.
- `GET /api/me` returns owner status and all active memberships with suspension flags. Unassigned users see access instructions rather than clinic registration.
- The SPA sends `X-EasyVet-Clinic-Id` for the selected workspace. The API verifies membership and clinic suspension on every request; only the configured platform owner can select arbitrary clinics for support.
- Use **Platform administration** to manually suspend/restore a clinic. Suspension blocks all member operational access and invitation acceptance without deleting data. Owner support access remains available and is logged. Automatic payment-provider integration is not implemented.
- Both SQLite and SQL Server migration sets include `AddClinicAdministration`; apply the matching migration set before using these screens. Existing clinic memberships are retained.
- Assign only `ClinicAdmin` or `Veterinarian` to users who may create clinical entries, administer vaccinations, record weights, or edit prescriptions. Only `ClinicAdmin` and `SuperAdmin` may edit clinic profile credentials.

## Before Go-Live

- Confirm `GET https://easyvetclinic-api-ayhmbsb2cbajeef2.canadacentral-01.azurewebsites.net/health` returns HTTP 200 after an API deployment.
- Run database migrations in the release pipeline.
- Verify cross-clinic access is denied with integration tests.
- Confirm authenticated requests succeed and unauthenticated requests return HTTP 401.
- Confirm backups, alert rules, and restore procedures.
- Confirm no patient, guardian, identity, token, or prescription details are written to telemetry.