# EasyVetClinic Azure SQL Migration Plan

Status: Implementation in progress

## Scope

Move the deployed `EasyVetClinic-API` from its default local SQLite configuration to the existing Azure SQL Database `easyvetclinic-database` on `easyvetclinic-dbserver` in `EasyVetClinic-RG` (Canada Central). No Azure resources will be created or deleted.

## Current State

- The API supports both SQLite and SQL Server providers.
- Existing EF migrations were generated for SQLite and cannot be applied to SQL Server without a provider-specific migration baseline.
- Authentication now reaches the application; the existing production SQLite database has no schema.

## Planned Changes

1. Generate and validate a SQL Server EF migration baseline from the current model.
2. Add a repeatable migration command/script for the API deployment pipeline.
3. Configure the App Service to use `Database__Provider=SqlServer` and the existing database connection string.
4. Prefer App Service managed identity with Microsoft Entra authentication for database access; use a secret connection string only as a temporary bootstrap if required.
5. Apply migrations, verify `/api/me`, configure `PlatformAdministration__OwnerObjectId`, and have the owner register clinics and invite their administrators.

## Validation

- Build the API.
- Verify the SQL migration can be generated and inspected.
- Verify the database schema exists after migration.
- Verify only the configured platform owner reaches clinic registration; unassigned users require an invitation.

## Constraints

- No test or dummy clinic data will be inserted.
- The existing Azure SQL database will not be deleted or recreated.
- Deployment remains through the existing GitHub Actions workflow.# EasyVetClinic Deployment Plan

**Status:** Implementation in progress

## Application

EasyVetClinic is a multi-tenant veterinary clinic platform with an ASP.NET Core .NET 10 API in `WebAPI/` and a React TypeScript SPA in `WebClient/`.

## Azure Architecture

- Azure Static Web Apps hosts the React SPA.
- Azure App Service hosts the ASP.NET Core API.
- Azure SQL Database stores operational data with mandatory `ClinicId` isolation.
- Private Azure Blob Storage stores pet photos, logos, signed documents, and boarding-only identity attachments.
- Microsoft Entra External ID authenticates users; the API validates bearer tokens.
- Key Vault, managed identities, Application Insights, and Log Analytics secure and observe the workload.

## Deployment

- Initial region: East US 2, parameterized for later changes.
- SQL uses Entra-only authentication.
- Infrastructure will be authored in Bicep and orchestrated through Azure Developer CLI.
- Azure validation is required before deployment.

## Required Configuration

- External ID/B2C tenant and application registration identifiers.
- Azure subscription and resource group selected by the operator.
- Domain and redirect URIs for production authentication.

## Identity Implementation

- API endpoints require an authenticated principal.
- Development uses a configuration-only local identity with platform-owner access; no clinic data or memberships are seeded.
- Production uses JWT bearer validation; App Service Easy Auth is disabled.
- The API resolves the Entra object ID to an active `ClinicUsers` membership and checks clinic suspension before accessing operational data.
- The configured platform owner can register clinics, manage access, and explicitly select clinic support workspaces without a membership.
- Clinic administrators invite staff using manually shared, expiring single-use links. Acceptance requires a matching verified email claim.
- Infrastructure and production app registration configuration remain pending approval and Azure context selection.