# EasyVetClinic Azure Deployment Checklist

This checklist describes the Azure resources and configuration needed before production deployment. Do not place secrets in this repository.

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

## 3. App Service Easy Auth

- An Azure App Service plan and Linux App Service for the API.
- Easy Auth enabled on the API App Service.
- Microsoft Entra External ID selected as the identity provider.
- API application registration configured as the allowed audience.
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

## 5. Storage and secrets

- Storage account with private Blob containers for pet photos, clinic logos, identity documents, and generated documents.
- App Service managed identity granted `Storage Blob Data Contributor` only where required.
- Key Vault with secrets/configuration references for SQL and Entra settings where managed identity cannot be used directly.
- App Service managed identity granted Key Vault secret-read permission.
- Blob retention and document lifecycle policy.

## 6. Observability and network security

- Application Insights and Log Analytics workspace.
- Alerts for availability, HTTP 5xx responses, authentication failures, SQL failures, and storage failures.
- Production CORS origin restricted to the SPA domain.
- TLS and custom-domain configuration.
- Optional private endpoints/VNet integration if clinic data policy requires private database and storage access.

## 7. Application configuration to supply as App Service settings

- `ConnectionStrings__ClinicDatabase` for Azure SQL.
- `Authentication__EasyAuth__ExpectedAudience` when token audience validation is added.
- Storage account/container settings.
- Application Insights connection string.
- Allowed SPA origin.

## 8. Initial access setup

- Create the super administrator Entra user and map their Entra object ID to the `SuperAdmin` role.
- Create each clinic and map clinic administrators, veterinarians, and staff through `ClinicUsers`.
- Verify every production user has an active clinic membership before granting access.
- For users assigned to multiple clinics, have the SPA send `X-EasyVet-Clinic-Id` after the user selects an authorized clinic. The API verifies this header against the authenticated user's memberships.
- Assign only `ClinicAdmin` or `Veterinarian` to users who may create clinical entries, administer vaccinations, record weights, or edit prescriptions. Only `ClinicAdmin` and `SuperAdmin` may edit clinic profile credentials.

## Before Go-Live

- Run database migrations in the release pipeline.
- Verify cross-clinic access is denied with integration tests.
- Confirm authenticated requests succeed and unauthenticated requests return HTTP 401.
- Confirm backups, alert rules, and restore procedures.
- Confirm no patient, guardian, identity, token, or prescription details are written to telemetry.