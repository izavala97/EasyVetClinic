import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { ArrowLeft, CalendarPlus, CalendarRange, PawPrint, Search } from 'lucide-react'
import { Link } from 'react-router-dom'
import { apiFetch, type ClinicProfile, type Patient, type ScheduleAppointment, getJson } from '../api'

function currentDate() {
  return new Date().toISOString().slice(0, 10)
}

function currentMonth() {
  return currentDate().slice(0, 7)
}

const appointmentReasons = [
  'General consultation',
  'Grooming',
  'Bath',
  'Vaccination',
  'Deworming',
  'Surgery',
  'Follow-up',
]

export function SchedulePage() {
  const [date, setDate] = useState(currentDate)
  const [month, setMonth] = useState(currentMonth)
  const [selectedDay, setSelectedDay] = useState<string | null>(null)
  const [appointments, setAppointments] = useState<ScheduleAppointment[]>([])
  const [patients, setPatients] = useState<Patient[]>([])
  const [patientId, setPatientId] = useState('')
  const [patientQuery, setPatientQuery] = useState('')
  const [isPatientListOpen, setIsPatientListOpen] = useState(false)
  const [clinicianName, setClinicianName] = useState('')
  const [time, setTime] = useState('09:00')
  const [reason, setReason] = useState('General consultation')
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')

  function loadAppointments(selectedMonth: string) {
    const [year, monthNumber] = selectedMonth.split('-')
    void getJson<ScheduleAppointment[]>(`/api/appointments?year=${year}&month=${monthNumber}`)
      .then((data) => {
        setAppointments(data)
        setError('')
      })
      .catch(() => setError('Appointments could not be loaded.'))
  }

  useEffect(() => {
    loadAppointments(month)
  }, [month])

  useEffect(() => {
    void getJson<Patient[]>('/api/patients')
      .then((data) => {
        setPatients(data)
        setPatientId(data[0]?.id ?? '')
      })
      .catch(() => setError('Patients could not be loaded.'))
    void getJson<ClinicProfile>('/api/clinic')
      .then((profile) => setClinicianName(profile.veterinarianName))
      .catch(() => setError('Clinic profile could not be loaded.'))
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
          startsAt: `${date}T${time}:00`,
          reason,
          clinicianName,
        }),
      })
      if (response.status === 409) {
        setError('That time is already booked. Select another time.')
        return
      }
      if (!response.ok) throw new Error()
      setNotice('Appointment added to the schedule.')
      loadAppointments(month)
    } catch {
      setError('The appointment could not be created. Complete all fields and try again.')
    }
  }

  const selectedPatient = patients.find((patient) => patient.id === patientId)
  const matchingPatients = patients.filter((patient) => {
    const query = patientQuery.trim().toLowerCase()
    return !query || [patient.name, patient.guardianName, patient.guardianPhone].some((value) => value.toLowerCase().includes(query))
  })
  const monthStart = new Date(`${month}-01T12:00:00`)
  const monthLabel = monthStart.toLocaleDateString([], { month: 'long', year: 'numeric' })
  const leadingDays = monthStart.getDay()
  const daysInMonth = new Date(monthStart.getFullYear(), monthStart.getMonth() + 1, 0).getDate()
  const calendarDays = Array.from({ length: leadingDays + daysInMonth }, (_, index) => index < leadingDays ? null : index - leadingDays + 1)
  const appointmentDates = new Set(appointments.map((appointment) => appointment.startsAt.slice(0, 10)))
  const selectedDayAppointments = appointments.filter((appointment) => appointment.startsAt.slice(0, 10) === selectedDay)
  const isSelectedTimeBooked = appointments.some((appointment) => appointment.startsAt.slice(0, 10) === date && appointment.startsAt.slice(11, 16) === time)

  function selectDay(day: number) {
    const selectedDate = `${month}-${String(day).padStart(2, '0')}`
    setDate(selectedDate)
    setSelectedDay(selectedDate)
  }

  return (
    <>
      <header className="page-header"><div><p className="eyebrow">Care planning</p><h1>Schedule</h1></div><CalendarRange size={26} /></header>
      {notice && <div className="notice" role="status">{notice}</div>}
      {error && <div className="notice error" role="alert">{error}</div>}
      <section className="schedule-layout">
        <section className="panel">{selectedDay ? <><div className="panel-heading"><div><p className="eyebrow">Daily schedule</p><h2>{new Date(`${selectedDay}T12:00:00`).toLocaleDateString([], { weekday: 'long', month: 'long', day: 'numeric' })}</h2></div><button className="secondary-action" type="button" onClick={() => setSelectedDay(null)}><ArrowLeft size={16} /> Back to month</button></div><div className="schedule-list">{selectedDayAppointments.length ? selectedDayAppointments.map((appointment) => <Link className="schedule-item" key={appointment.id} to={`/appointments?appointmentId=${appointment.id}&month=${appointment.startsAt.slice(0, 7)}`}><time>{appointment.startsAt.slice(11, 16)}</time><div><strong>{appointment.patientName}</strong><span>{appointment.reason}</span></div><small>{appointment.clinicianName}</small></Link>) : <p className="empty-state">No appointments scheduled for this day.</p>}</div></> : <><div className="panel-heading"><div><p className="eyebrow">Monthly calendar</p><h2>{monthLabel}</h2></div><label className="month-field"><span>Month</span><input aria-label="Schedule month" type="month" value={month} onChange={(event) => { setMonth(event.target.value); setDate(`${event.target.value}-01`); setSelectedDay(null) }} /></label></div>
          <div className="month-calendar" role="grid" aria-label={monthLabel}>{['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].map((weekday) => <span className="calendar-weekday" key={weekday}>{weekday}</span>)}{calendarDays.map((day, index) => day ? <button className={`calendar-day ${date === `${month}-${String(day).padStart(2, '0')}` ? 'selected' : ''}`} type="button" role="gridcell" aria-label={`${monthLabel} ${day}${appointmentDates.has(`${month}-${String(day).padStart(2, '0')}`) ? ', appointments scheduled' : ''}`} key={day} onClick={() => selectDay(day)}><span>{day}</span>{appointmentDates.has(`${month}-${String(day).padStart(2, '0')}`) && <i aria-hidden="true" />}</button> : <span className="calendar-day empty" aria-hidden="true" key={`empty-${index}`} />)}</div></>}
        </section>
        <form className="panel appointment-form" onSubmit={(event) => void createAppointment(event)}><div className="panel-heading"><div><p className="eyebrow">New appointment</p><h2>Add to calendar</h2></div><CalendarPlus size={22} /></div><Link className="secondary-action" to="/patients/new?returnTo=/schedule"><PawPrint size={16} /> Register patient and guardian</Link>
          <label>Patient<div className="patient-combobox"><div className="search-field"><Search size={16} /><input value={patientQuery} onChange={(event) => { setPatientQuery(event.target.value); setIsPatientListOpen(true) }} onFocus={() => setIsPatientListOpen(true)} placeholder={selectedPatient ? `${selectedPatient.name} - ${selectedPatient.guardianName}` : 'Search pet, guardian, or phone'} role="combobox" aria-expanded={isPatientListOpen} aria-controls="schedule-patient-options" aria-autocomplete="list" /></div>{isPatientListOpen && <div className="patient-options" id="schedule-patient-options" role="listbox">{matchingPatients.map((patient) => <button type="button" role="option" aria-selected={patient.id === patientId} key={patient.id} onClick={() => { setPatientId(patient.id); setPatientQuery(''); setIsPatientListOpen(false) }}><strong>{patient.name}</strong><span>{patient.guardianName} · {patient.guardianPhone}</span></button>)}{!matchingPatients.length && <p className="empty-state">No patients match this search.</p>}</div>}</div></label>
          <label>Date<input type="date" value={date} onChange={(event) => setDate(event.target.value)} required /></label>
          <label>Time<input type="time" value={time} onChange={(event) => setTime(event.target.value)} required /></label>
          {isSelectedTimeBooked && <p className="field-error">This time is already booked.</p>}
          <label>Reason<select value={reason} onChange={(event) => setReason(event.target.value)}>{appointmentReasons.map((appointmentReason) => <option key={appointmentReason} value={appointmentReason}>{appointmentReason}</option>)}</select></label>
          <button className="primary-action" type="submit" disabled={!patientId || !clinicianName || isSelectedTimeBooked}><CalendarPlus size={18} /> Add appointment</button>
        </form>
      </section>
    </>
  )
}