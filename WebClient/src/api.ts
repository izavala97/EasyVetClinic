export type Dashboard = {
  clinicName: string
  todayAppointments: number
  activePatients: number
  boardingGuests: number
  upcomingAppointments: { id: string; startsAt: string; time: string; patientName: string; reason: string; clinicianName: string }[]
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
  guardians: Guardian[]
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

export type CurrentUser = {
  objectId: string
  displayName: string
  clinicId: string | null
  clinicName: string | null
  role: string | null
  isPlatformOwner: boolean
  isSuspended: boolean
  clinics: ClinicMembership[]
}

export type ClinicMembership = { clinicId: string; clinicName: string; role: string; isSuspended: boolean }
export type PlatformClinic = { id: string; name: string; isSuspended: boolean; suspensionReason: string; userCount: number }
export type ClinicUser = { id: string; displayName: string; role: string; isActive: boolean }
export type Invitation = { id: string; email: string; role: string; expiresAt: string; isUsed: boolean; isRevoked: boolean }
export type CreatedInvitation = { invitation: Invitation; token: string }

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

type AccessTokenProvider = () => Promise<string | null>

let accessTokenProvider: AccessTokenProvider | undefined
let selectedClinicId: string | null = null

export function setSelectedClinicId(clinicId: string | null) {
  selectedClinicId = clinicId
}

export function setAccessTokenProvider(provider: AccessTokenProvider | undefined) {
  accessTokenProvider = provider
}

function apiUrl(path: string): string {
  return `${import.meta.env.VITE_API_BASE_URL ?? ''}${path}`
}

export async function apiFetch(path: string, init?: RequestInit): Promise<Response> {
  const headers = new Headers(init?.headers)
  if (selectedClinicId) headers.set('X-EasyVet-Clinic-Id', selectedClinicId)
  const accessToken = await accessTokenProvider?.()
  if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`)
  }

  return fetch(apiUrl(path), { ...init, headers })
}

export async function getJson<T>(path: string): Promise<T> {
  const response = await apiFetch(path)
  await ensureSuccess(response)

  return response.json() as Promise<T>
}

export async function ensureSuccess(response: Response): Promise<void> {
  if (response.ok) return
  const body = await response.text()
  let message = `Request failed with status ${response.status}.`
  if (body && response.headers.get('content-type')?.includes('json')) {
    const problem: { error?: string; detail?: string; title?: string } = JSON.parse(body)
    message = problem.error ?? problem.detail ?? problem.title ?? message
  }
  throw new Error(message)
}