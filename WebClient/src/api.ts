export type Dashboard = {
  clinicName: string
  todayAppointments: number
  activePatients: number
  boardingGuests: number
  upcomingAppointments: { time: string; patientName: string; reason: string; clinicianName: string }[]
}

export type Patient = {
  id: string
  guardianId: string
  name: string
  species: string
  breed: string
  sex: string
  weight: string
  guardianName: string
  guardianPhone: string
  color: string
  allergies: string[]
  lastVisit: string
  distinguishingFeatures: string
  dateOfBirth: string | null
  photoUrl: string
  isActive: boolean
}

export type Guardian = {
  id: string
  name: string
  phone: string
  alternatePhone: string
  address: string
  identityType: string
  identityNumber: string
  identityDocumentUrl: string
}

export type ClinicProfile = {
  name: string
  address: string
  logoUrl: string
  veterinarianName: string
  veterinarianTitles: string
  veterinarianLicenseNumber: string
}

export type ConsultationHistoryItem = {
  id: string
  startedAt: string
  clinicianName: string
  status: 'InProgress' | 'Completed'
  diagnosis: string
}

export type ConsultationListItem = ConsultationHistoryItem & {
  patientId: string
  patientName: string
  guardianName: string
}

export type Consultation = {
  id: string
  patientId: string
  patientName: string
  guardianName: string
  clinicianName: string
  startedAt: string
  status: 'InProgress' | 'Completed'
  chiefComplaint: string
  clinicalNotes: string
  diagnosis: string
  instructions: string
  prescriptionLastUpdatedAt: string | null
  prescriptionItems: PrescriptionItem[]
}

export type PrescriptionItem = {
  id?: string
  medicationName: string
  presentation: string
  concentration: string
  dosageDirections: string
  sortOrder: number
}

export type ScheduleAppointment = {
  id: string
  patientId: string
  patientName: string
  startsAt: string
  reason: string
  clinicianName: string
}

export type Product = {
  id: string
  name: string
  category: string
  unitPrice: number
  stockOnHand: number
}

export type SaleReceipt = {
  id: string
  completedAt: string
  total: number
  paymentMethod: string
  lines: { productName: string; quantity: number; unitPrice: number }[]
}

export async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path)
  if (!response.ok) {
    throw new Error(`Request failed with status ${response.status}.`)
  }

  return response.json() as Promise<T>
}