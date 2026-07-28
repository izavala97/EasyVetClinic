# EasyVetClinic Deployment Plan

**Status:** Implementation in progress

## Application

EasyVetClinic is a multi-tenant veterinary clinic platform with an ASP.NET Core .NET 10 API in `WebAPI/` and a React TypeScript SPA in `WebClient/`.

## Azure Architecture

- Azure Static Web Apps hosts the React SPA.
- Azure App Service hosts the ASP.NET Core API.
- Azure SQL Database stores operational data with mandatory `ClinicId` isolation.
- Private Azure Blob Storage stores pet photos, logos, signed documents, and boarding-only identity attachments.
- Azure Entra External ID/B2C through EasyAuth authenticates users.
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
- Development uses a configuration-only local identity that maps to a seeded clinic administrator membership.
- Production expects App Service Easy Auth to provide the `X-MS-CLIENT-PRINCIPAL` header.
- The API resolves the Entra object ID to an active `ClinicUsers` membership before reading clinic data.
- Infrastructure and production app registration configuration remain pending approval and Azure context selection.