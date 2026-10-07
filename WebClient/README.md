# EasyVetClinic Web Client

The EasyVetClinic administrative web application is built with React, TypeScript, Vite, and Lucide icons.

## Scripts

```powershell
npm install
npm run dev
npm run build
npm run lint
```

During development, Vite proxies `/api` and `/health` to `http://localhost:5120`. Start the API from `../WebAPI` before launching the client. The Vite development server uses the API's local development identity, so no Microsoft Entra or `VITE_ENTRA_*` configuration is required to test the local app. Production builds require the Entra variables documented in [.env.example](.env.example).

## Current Experience

- Clinic dashboard with appointment, patient, and boarding indicators, including all appointments for today and tomorrow.
- Patient search by patient name, guardian name, or phone number, plus multiple linked guardians per patient.
- Monthly and daily scheduling calendar with time-conflict prevention; click an appointment row to open its editor.
- Appointment editing for patient, date, time, reason, and clinician.
- Consultation creation from the selected patient record.
- Point of sale for stocked products and manually priced services, with optional patient association and payment method selection.
- Access to health certificate, surgical consent, and boarding contract drafts.

## Conventions

Components, types, properties, API clients, and other technical identifiers use English. User-facing content will be localized through the client as the clinical workflows are completed.

The client uses MSAL and bearer tokens in production. Local development uses the API development authentication handler so that clinic workflows can be tested without contacting Microsoft Entra.
- Platform-owner administration for registering clinics, opening support workspaces, and manually suspending/restoring subscription access.
- Clinic-admin staff management, role changes, deactivation/reactivation, and manually shared invitation links.
- Signed-in invitation acceptance, explicit access-required/suspended screens, and authorized clinic selection in the sidebar.
- Account details exposes the signed-in Entra object ID for secure owner configuration; see [deployment setup](../DEPLOYMENT.MD).
