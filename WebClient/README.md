# EasyVetClinic Web Client

The EasyVetClinic administrative web application is built with React, TypeScript, Vite, and Lucide icons.

## Scripts

```powershell
npm install
npm run dev
npm run build
npm run lint
```

During development, Vite proxies `/api` and `/health` to `http://localhost:5094`. Start the API from `../WebAPI` before launching the client.

## Current Experience

- Clinic dashboard with appointment, patient, and boarding indicators.
- Patient search by patient name, guardian name, or phone number.
- Consultation creation from the selected patient record.
- Access to health certificate, surgical consent, and boarding contract drafts.

## Conventions

Components, types, properties, API clients, and other technical identifiers use English. User-facing content will be localized through the client as the clinical workflows are completed.

The current client consumes the local API. EasyAuth, protected routes, and permission handling will be added before production.
