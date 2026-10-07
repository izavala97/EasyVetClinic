# EasyVetClinic Web Client

The EasyVetClinic administrative web application is built with React, TypeScript, Vite, and Lucide icons.

## Scripts

```powershell
npm install
npm run dev
npm run build
npm run lint
npm test
```

During development, Vite proxies `/api` and `/health` to `http://localhost:5120`. Start the API from `../WebAPI` before launching the client. The Vite development server uses the API's local development identity, so no Microsoft Entra or `VITE_ENTRA_*` configuration is required to test the local app. Production builds require the Entra variables documented in [.env.example](.env.example).

## Current Experience

- Today workspace with active care, pending accounts, balances by currency, low-stock counts, and clinic-local appointments.
- Patient search by patient name, guardian name, or phone number, plus multiple linked guardians per patient.
- Monthly and daily scheduling with clinician-specific interval conflict prevention, visit duration, clinic time zones, and appointment-to-consultation linkage.
- Combined patient/guardian intake, primary-contact selection, weight and vaccination histories.
- Version-checked clinical records, unsaved-change guards, finalization, cancellation, and attributed amendments.
- Clinic-specific product/service catalog administration and persistent consultation charge sheets.
- Checkout with partial/split payments, explicit stock fulfillment, inventory movement history, and receipts.
- Access to health certificate, surgical consent, and boarding contract drafts.

## Conventions

Components, types, properties, API clients, and other technical identifiers use English. User-facing content will be localized through the client as the clinical workflows are completed.

Form controls inside `panel` containers share label spacing, input styles, and visible keyboard focus. Use `workflow-controls` for equivalent controls outside a panel and `clinical-entry-form` for vertically spaced clinical forms. Guardian intake search starts at two characters, shows at most five matches in a bounded `guardian-options` list, and keeps selected contacts visible separately. `patient-options` is reserved for the positioned patient-combobox popup.

The client uses MSAL and bearer tokens in production. Local development uses the API development authentication handler so that clinic workflows can be tested without contacting Microsoft Entra.
- Platform-owner administration for registering clinics, opening support workspaces, and manually suspending/restoring subscription access.
- Clinic-admin staff management, role changes, deactivation/reactivation, and manually shared invitation links.
- Signed-in invitation acceptance, explicit access-required/suspended screens, and authorized clinic selection in the sidebar.
- Account details exposes the signed-in Entra object ID for secure owner configuration; see [deployment setup](../DEPLOYMENT.MD).

See [Clinical and commercial workflows](../docs/WORKFLOWS.md) for lifecycle distinctions and deferred features. Completing care is independent of payment; prescribing and collecting payment do not reduce stock.
