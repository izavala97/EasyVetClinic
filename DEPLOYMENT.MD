# EasyVetClinic Azure Deployment Checklist

This checklist describes the Azure resources and configuration needed before production deployment. Do not place secrets in this repository.

## Current deployment baseline

- Resource group: `EasyVetClinic-RG`.
- API App Service: `EasyVetClinic-API` on Linux, deployed by [main_easyvetclinic-api.yml](.github/workflows/main_easyvetclinic-api.yml).
- Static Web App: `EasyVetClinic-WebCli`, deployed by [azure-static-web-apps-red-plant-0ec64f60f.yml](.github/workflows/azure-static-web-apps-red-plant-0ec64f60f.yml).
- Static Web App production domain: `https://red-plant-0ec64f60f.7.azurestaticapps.net`.
- API default domain: `https://easyvetclinic-api-ayhmbsb2cbajeef2.canadacentral-01.azurewebsites.net`.
- The API defaults to SQLite through `Database__Provider=Sqlite`. Azure SQL support is installed but must not be enabled until SQL Server migrations have been generated and applied.

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

## 3. App Service Easy Auth

- An Azure App Service plan and Linux App Service for the API.
- Easy Auth enabled on the API App Service.
- Microsoft Entra External ID selected as the identity provider.
- Configure the provider issuer URL as `https://134a684d-d19f-4215-bae2-e45dcb315751.ciamlogin.com/134a684d-d19f-4215-bae2-e45dcb315751/v2.0`. This is the canonical issuer returned by the External ID OpenID discovery document.
- Configure both `api://a0c84c31-b8c4-4d60-8e25-74ace0817d54` and `a0c84c31-b8c4-4d60-8e25-74ace0817d54` as allowed token audiences. The External ID delegated access token uses the raw API client ID in its `aud` claim.
- Unauthenticated requests set to return HTTP 401.
- HTTPS-only enabled and a custom domain/certificate configured if applicable.

## 4. Azure SQL Database

- Azure SQL logical server and database.
- Microsoft Entra-only authentication enabled.
- An Entra administrator for the SQL server.
- An App Service system-assigned managed identity.
- A contained database user for the App Service managed identity with required application permissions.
- Production backup retention, geo-redundancy, and sizing decisions.
- A deployment migration process that runs before the application release, not at API startup.
- When SQL Server migrations are ready, set `Database__Provider=SqlServer` and `ConnectionStrings__ClinicDatabase` together in the API App Service. Do not set only one of these values.

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
- `ConnectionStrings__ClinicDatabase` for Azure SQL only after SQL Server provider support and migrations are implemented.
- `Authentication__EasyAuth__ExpectedAudience` when token audience validation is added.
- Storage account/container settings.
- Application Insights connection string.
- Allowed SPA origin.

## 8. Initial access setup

- The first authenticated External ID user creates the initial clinic through `POST /api/onboarding/clinic`; that operation creates an active `ClinicAdmin` membership and is rejected once any clinic exists.
- The onboarding request requires the App Service Easy Auth principal and is never available anonymously.
- `GET /api/me` returns the authenticated user and current membership context for the frontend shell. A user without a membership receives identity context with no clinic values and can be routed to initial onboarding after browser sign-in is implemented.
- Create the super administrator Entra user and map their Entra object ID to the `SuperAdmin` role when platform-level administration is introduced.
- Create each clinic and map clinic administrators, veterinarians, and staff through `ClinicUsers`.
- Verify every production user has an active clinic membership before granting access.
- For users assigned to multiple clinics, have the SPA send `X-EasyVet-Clinic-Id` after the user selects an authorized clinic. The API verifies this header against the authenticated user's memberships.
- Assign only `ClinicAdmin` or `Veterinarian` to users who may create clinical entries, administer vaccinations, record weights, or edit prescriptions. Only `ClinicAdmin` and `SuperAdmin` may edit clinic profile credentials.

## Before Go-Live

- Confirm `GET https://easyvetclinic-api-ayhmbsb2cbajeef2.canadacentral-01.azurewebsites.net/health` returns HTTP 200 after an API deployment.
- Run database migrations in the release pipeline.
- Verify cross-clinic access is denied with integration tests.
- Confirm authenticated requests succeed and unauthenticated requests return HTTP 401.
- Confirm backups, alert rules, and restore procedures.
- Confirm no patient, guardian, identity, token, or prescription details are written to telemetry.