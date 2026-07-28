import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import './index.css'
import App from './App.tsx'
import { DashboardPage } from './pages/DashboardPage.tsx'
import { DocumentsPage } from './pages/DocumentsPage.tsx'
import { PatientDetailPage } from './pages/PatientDetailPage.tsx'
import { PatientsPage } from './pages/PatientsPage.tsx'
import { PointOfSalePage } from './pages/PointOfSalePage.tsx'
import { ConsultationPage } from './pages/ConsultationPage.tsx'
import { RegistrationPage } from './pages/RegistrationPage.tsx'
import { SchedulePage } from './pages/SchedulePage.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route element={<App />}>
          <Route index element={<DashboardPage />} />
          <Route path="patients" element={<PatientsPage />} />
          <Route path="patients/new" element={<RegistrationPage mode="patient" />} />
          <Route path="patients/:patientId" element={<PatientDetailPage />} />
          <Route path="guardians/new" element={<RegistrationPage mode="guardian" />} />
          <Route path="consultations/:consultationId" element={<ConsultationPage />} />
          <Route path="schedule" element={<SchedulePage />} />
          <Route path="documents" element={<DocumentsPage />} />
          <Route path="point-of-sale" element={<PointOfSalePage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  </StrictMode>,
)
