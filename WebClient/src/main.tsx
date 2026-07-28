import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthenticationProvider, initializeAuthentication } from './auth.tsx'
import './index.css'
import App from './App.tsx'
import { DashboardPage } from './pages/DashboardPage.tsx'
import { DocumentsPage } from './pages/DocumentsPage.tsx'
import { PatientDetailPage } from './pages/PatientDetailPage.tsx'
import { PatientsPage } from './pages/PatientsPage.tsx'
import { PointOfSalePage } from './pages/PointOfSalePage.tsx'
import { ConsultationPage } from './pages/ConsultationPage.tsx'
import { ConsultationsPage } from './pages/ConsultationsPage.tsx'
import { ClinicProfilePage } from './pages/ClinicProfilePage.tsx'
import { RegistrationPage } from './pages/RegistrationPage.tsx'
import { PrescriptionPage } from './pages/PrescriptionPage.tsx'
import { SchedulePage } from './pages/SchedulePage.tsx'
import { OnboardingPage } from './pages/OnboardingPage.tsx'

await initializeAuthentication()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthenticationProvider>
      <BrowserRouter>
      <Routes>
        <Route element={<App />}>
          <Route index element={<DashboardPage />} />
          <Route path="patients" element={<PatientsPage />} />
          <Route path="patients/new" element={<RegistrationPage mode="patient" />} />
          <Route path="patients/:patientId" element={<PatientDetailPage />} />
          <Route path="guardians/new" element={<RegistrationPage mode="guardian" />} />
          <Route path="consultations/:consultationId" element={<ConsultationPage />} />
          <Route path="consultations/:consultationId/prescription" element={<PrescriptionPage />} />
          <Route path="consultations" element={<ConsultationsPage />} />
          <Route path="schedule" element={<SchedulePage />} />
          <Route path="documents" element={<DocumentsPage />} />
          <Route path="point-of-sale" element={<PointOfSalePage />} />
          <Route path="clinic-profile" element={<ClinicProfilePage />} />
          <Route path="onboarding" element={<OnboardingPage />} />
        </Route>
      </Routes>
      </BrowserRouter>
    </AuthenticationProvider>
  </StrictMode>,
)
