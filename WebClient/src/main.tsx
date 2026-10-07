import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, createRoutesFromElements, Route, RouterProvider } from 'react-router-dom'
import { AuthenticationProvider, initializeAuthentication } from './auth.tsx'
import './index.css'
import App from './App.tsx'
import { DashboardPage } from './pages/DashboardPage.tsx'
import { DocumentsPage } from './pages/DocumentsPage.tsx'
import { PatientDetailPage } from './pages/PatientDetailPage.tsx'
import { PatientsPage } from './pages/PatientsPage.tsx'
import { GuardiansPage } from './pages/GuardiansPage.tsx'
import { CheckoutPage } from './pages/CheckoutPage.tsx'
import { CatalogPage } from './pages/CatalogPage.tsx'
import { InventoryPage } from './pages/InventoryPage.tsx'
import { ConsultationPage } from './pages/ConsultationPage.tsx'
import { ConsultationsPage } from './pages/ConsultationsPage.tsx'
import { ClinicProfilePage } from './pages/ClinicProfilePage.tsx'
import { RegistrationPage } from './pages/RegistrationPage.tsx'
import { PrescriptionPage } from './pages/PrescriptionPage.tsx'
import { SchedulePage } from './pages/SchedulePage.tsx'
import { AppointmentsPage } from './pages/AppointmentsPage.tsx'
import { OnboardingPage } from './pages/OnboardingPage.tsx'
import { PlatformAdminPage } from './pages/PlatformAdminPage.tsx'
import { ClinicAdminPage } from './pages/ClinicAdminPage.tsx'
import { InvitationPage } from './pages/InvitationPage.tsx'

await initializeAuthentication()

const router = createBrowserRouter(createRoutesFromElements(
        <Route element={<App />}>
          <Route index element={<DashboardPage />} />
          <Route path="patients" element={<PatientsPage />} />
          <Route path="patients/new" element={<RegistrationPage mode="patient" />} />
          <Route path="patients/:patientId" element={<PatientDetailPage />} />
          <Route path="guardians" element={<GuardiansPage />} />
          <Route path="guardians/new" element={<RegistrationPage mode="guardian" />} />
          <Route path="consultations/:consultationId" element={<ConsultationPage />} />
          <Route path="consultations/:consultationId/prescription" element={<PrescriptionPage />} />
          <Route path="consultations" element={<ConsultationsPage />} />
          <Route path="schedule" element={<SchedulePage />} />
          <Route path="appointments" element={<AppointmentsPage />} />
          <Route path="documents" element={<DocumentsPage />} />
          <Route path="checkout" element={<CheckoutPage />} />
          <Route path="point-of-sale" element={<CheckoutPage />} />
          <Route path="catalog" element={<CatalogPage />} />
          <Route path="inventory" element={<InventoryPage />} />
          <Route path="clinic-profile" element={<ClinicProfilePage />} />
          <Route path="onboarding" element={<OnboardingPage />} />
          <Route path="platform" element={<PlatformAdminPage />} />
          <Route path="clinic-admin" element={<ClinicAdminPage />} />
          <Route path="invite" element={<InvitationPage />} />
        </Route>
))

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthenticationProvider>
      <RouterProvider router={router} />
    </AuthenticationProvider>
  </StrictMode>,
)
