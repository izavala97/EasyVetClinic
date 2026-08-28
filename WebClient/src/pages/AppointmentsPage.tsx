import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { CalendarDays, Pencil, Save, Search } from 'lucide-react'
import { useSearchParams } from 'react-router-dom'
import { apiFetch, type Patient, type ScheduleAppointment, getJson } from '../api'

const appointmentReasons = [
  'General consultation',
  'Grooming',
  'Bath',
  'Vaccination',
  'Deworming',
  'Surgery',
  'Follow-up',
]

function currentMonth() {
  return new Date().toISOString().slice(0, 7)
}

export function AppointmentsPage() {
  const [searchParams] = useSearchParams()
  const appointmentId = searchParams.get('appointmentId')
  const [month, setMonth] = useState(() => searchParams.get('month') ?? currentMonth())
  const [appointments, setAppointments] = useState<ScheduleAppointment[]>([])
  const [patients, setPatients] = useState<Patient[]>([])
  const [selectedAppointment, setSelectedAppointment] = useState<ScheduleAppointment | null>(null)
  const [patientId, setPatientId] = useState('')
  const [patientQuery, setPatientQuery] = useState('')
  const [isPatientListOpen, setIsPatientListOpen] = useState(false)
  const [date, setDate] = useState('')
  const [time, setTime] = useState('')
  const [reason, setReason] = useState('')
  const [clinicianName, setClinicianName] = useState('')
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')

  function loadAppointments(selectedMonth: string) {
    const [year, monthNumber] = selectedMonth.split('-')
    void getJson<ScheduleAppointment[]>(`/api/appointments?year=${year}&month=${monthNumber}`)
      .then((data) => {
        setAppointments(data)
        setError('')
        const appointment = data.find((candidate) => candidate.id === appointmentId)
        if (appointment) selectAppointment(appointment)
      })
      .catch(() => setError('Appointments could not be loaded.'))
  }

  useEffect(() => {
    loadAppointments(month)
  }, [month])

  useEffect(() => {
    void getJson<Patient[]>('/api/patients')
      .then(setPatients)
      .catch(() => setError('Patients could not be loaded.'))
  }, [])

  const selectedPatient = patients.find((patient) => patient.id === patientId)
  const matchingPatients = patients.filter((patient) => {
    const query = patientQuery.trim().toLowerCase()
    return !query || [patient.name, patient.guardianName, patient.guardianPhone].some((value) => value.toLowerCase().includes(query))
  })

  function selectAppointment(appointment: ScheduleAppointment) {
    setSelectedAppointment(appointment)
    setPatientId(appointment.patientId)
    setPatientQuery('')
    setDate(appointment.startsAt.slice(0, 10))
    setTime(appointment.startsAt.slice(11, 16))
    setReason(appointment.reason)
    setClinicianName(appointment.clinicianName)
    setNotice('')
    setError('')
  }

  async function saveAppointment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selectedAppointment) return
    setNotice('')
    setError('')

    try {
      const response = await apiFetch(`/api/appointments/${selectedAppointment.id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ patientId, startsAt: `${date}T${time}:00`, reason, clinicianName }),
      })
      if (response.status === 409) {
        setError('That time is already booked. Select another time.')
        return
      }
      if (!response.ok) throw new Error()
      const updatedAppointment = await response.json() as ScheduleAppointment
      setSelectedAppointment(updatedAppointment)
      setNotice('Appointment updated.')
      loadAppointments(month)
    } catch {
      setError('Appointment could not be updated. Complete all fields and try again.')
    }
  }

  const monthLabel = new Date(`${month}-01T12:00:00`).toLocaleDateString([], { month: 'long', year: 'numeric' })

  return (
    <>
      <header className="page-header"><div><p className="eyebrow">Care planning</p><h1>Appointments</h1></div><CalendarDays size={26} /></header>
      {notice && <div className="notice" role="status">{notice}</div>}
      {error && <div className="notice error" role="alert">{error}</div>}
      <section className="schedule-layout appointment-management-layout">
        <section className="panel">
          <div className="panel-heading"><div><p className="eyebrow">Scheduled visits</p><h2>{monthLabel}</h2></div><label className="month-field"><span>Month</span><input aria-label="Appointment month" type="month" value={month} onChange={(event) => { setMonth(event.target.value); setSelectedAppointment(null); setNotice('') }} /></label></div>
          <div className="appointment-management-list">
            {appointments.map((appointment) => <button className={`appointment-management-row ${selectedAppointment?.id === appointment.id ? 'selected' : ''}`} type="button" key={appointment.id} onClick={() => selectAppointment(appointment)}><time>{new Date(appointment.startsAt).toLocaleDateString([], { month: 'short', day: 'numeric' })}<span>{appointment.startsAt.slice(11, 16)}</span></time><div><strong>{appointment.patientName}</strong><span>{appointment.reason}</span></div><small>{appointment.clinicianName}</small></button>)}
            {!appointments.length && <p className="empty-state">No appointments scheduled for this month.</p>}
          </div>
        </section>
        <form className="panel appointment-form" onSubmit={(event) => void saveAppointment(event)}>
          <div className="panel-heading"><div><p className="eyebrow">Appointment details</p><h2>{selectedAppointment ? 'Edit appointment' : 'Select an appointment'}</h2></div><Pencil size={22} /></div>
          {selectedAppointment ? <><label>Patient<div className="patient-combobox"><div className="search-field"><Search size={16} /><input value={patientQuery} onChange={(event) => { setPatientQuery(event.target.value); setIsPatientListOpen(true) }} onFocus={() => setIsPatientListOpen(true)} placeholder={selectedPatient ? `${selectedPatient.name} - ${selectedPatient.guardianName}` : 'Search pet, guardian, or phone'} role="combobox" aria-expanded={isPatientListOpen} aria-controls="appointments-patient-options" aria-autocomplete="list" /></div>{isPatientListOpen && <div className="patient-options" id="appointments-patient-options" role="listbox">{matchingPatients.map((patient) => <button type="button" role="option" aria-selected={patient.id === patientId} key={patient.id} onClick={() => { setPatientId(patient.id); setPatientQuery(''); setIsPatientListOpen(false) }}><strong>{patient.name}</strong><span>{patient.guardianName} · {patient.guardianPhone}</span></button>)}{!matchingPatients.length && <p className="empty-state">No patients match this search.</p>}</div>}</div></label><label>Date<input type="date" value={date} onChange={(event) => setDate(event.target.value)} required /></label><label>Time<input type="time" value={time} onChange={(event) => setTime(event.target.value)} required /></label><label>Reason<select value={reason} onChange={(event) => setReason(event.target.value)}>{appointmentReasons.map((appointmentReason) => <option key={appointmentReason} value={appointmentReason}>{appointmentReason}</option>)}</select></label><label>Clinician<input value={clinicianName} onChange={(event) => setClinicianName(event.target.value)} required /></label><button className="primary-action" type="submit"><Save size={18} /> Save changes</button></> : <p className="empty-state">Choose an appointment from the list to view and edit it.</p>}
        </form>
      </section>
    </>
  )
}
