import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { CalendarPlus, CalendarRange } from 'lucide-react'
import { apiFetch, type Patient, type ScheduleAppointment, getJson } from '../api'

function currentDate() {
  return new Date().toISOString().slice(0, 10)
}

export function SchedulePage() {
  const [date, setDate] = useState(currentDate)
  const [appointments, setAppointments] = useState<ScheduleAppointment[]>([])
  const [patients, setPatients] = useState<Patient[]>([])
  const [patientId, setPatientId] = useState('')
  const [time, setTime] = useState('09:00')
  const [reason, setReason] = useState('General consultation')
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')

  function loadAppointments(selectedDate: string) {
    void getJson<ScheduleAppointment[]>(`/api/appointments?date=${selectedDate}`)
      .then((data) => {
        setAppointments(data)
        setError('')
      })
      .catch(() => setError('Appointments could not be loaded.'))
  }

  useEffect(() => {
    loadAppointments(date)
  }, [date])

  useEffect(() => {
    void getJson<Patient[]>('/api/patients')
      .then((data) => {
        setPatients(data)
        setPatientId(data[0]?.id ?? '')
      })
      .catch(() => setError('Patients could not be loaded.'))
  }, [])

  async function createAppointment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setNotice('')
    setError('')
    try {
      const response = await apiFetch('/api/appointments', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          patientId,
          startsAt: `${date}T${time}:00.000Z`,
          reason,
          clinicianName: 'MVZ. Alondra Licona',
        }),
      })
      if (!response.ok) throw new Error()
      setNotice('Appointment added to the schedule.')
      loadAppointments(date)
    } catch {
      setError('The appointment could not be created. Complete all fields and try again.')
    }
  }

  return (
    <>
      <header className="page-header"><div><p className="eyebrow">Care planning</p><h1>Schedule</h1></div><CalendarRange size={26} /></header>
      {notice && <div className="notice" role="status">{notice}</div>}
      {error && <div className="notice error" role="alert">{error}</div>}
      <section className="schedule-layout">
        <section className="panel"><div className="panel-heading"><div><p className="eyebrow">Daily calendar</p><h2>Appointments</h2></div><label className="date-field"><span>Date</span><input aria-label="Schedule date" type="date" value={date} onChange={(event) => setDate(event.target.value)} /></label></div>
          <div className="schedule-list">
            {appointments.map((appointment) => <article className="schedule-item" key={appointment.id}><time>{new Date(appointment.startsAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</time><div><strong>{appointment.patientName}</strong><span>{appointment.reason}</span></div><small>{appointment.clinicianName}</small></article>)}
            {!appointments.length && <p className="empty-state">No appointments are scheduled for this day.</p>}
          </div>
        </section>
        <form className="panel appointment-form" onSubmit={(event) => void createAppointment(event)}><div className="panel-heading"><div><p className="eyebrow">New appointment</p><h2>Add to calendar</h2></div><CalendarPlus size={22} /></div>
          <label>Patient<select value={patientId} onChange={(event) => setPatientId(event.target.value)} required>{patients.map((patient) => <option key={patient.id} value={patient.id}>{patient.name} - {patient.guardianName}</option>)}</select></label>
          <label>Time<input type="time" value={time} onChange={(event) => setTime(event.target.value)} required /></label>
          <label>Reason<input value={reason} onChange={(event) => setReason(event.target.value)} required /></label>
          <button className="primary-action" type="submit" disabled={!patientId}><CalendarPlus size={18} /> Add appointment</button>
        </form>
      </section>
    </>
  )
}