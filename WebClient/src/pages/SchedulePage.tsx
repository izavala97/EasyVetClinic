import { useTranslation } from '../useTranslation'
import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ensureSuccess, getJson, type Patient, type ScheduleAppointment, type Clinician, type ClinicProfile } from '../api'
import { appointmentReasons, clinicDateTime } from '../schedule'

export function SchedulePage() {
  const { t } = useTranslation()

  const [zone, setZone] = useState<string | null>(null)
  const [month, setMonth] = useState('')
  const [date, setDate] = useState('')
  const [time, setTime] = useState('09:00')
  const [duration, setDuration] = useState(30)
  const [reason, setReason] = useState(appointmentReasons[0])
  const [appointments, setAppointments] = useState<ScheduleAppointment[]>([])
  const [patients, setPatients] = useState<Patient[]>([])
  const [clinicians, setClinicians] = useState<Clinician[]>([])
  const [patientId, setPatientId] = useState('')
  const [clinicianId, setClinicianId] = useState('')
  const [query, setQuery] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  useEffect(() => {
    void Promise.all([getJson<ClinicProfile>('/api/clinic'), getJson<Clinician[]>('/api/clinicians')])
      .then(([profile, team]) => {
        setZone(profile.timeZoneId); setClinicians(team)
        const today = clinicDateTime(new Date(), profile.timeZoneId).date
        setDate(today); setMonth(today.slice(0, 7))
      }).catch((cause: unknown) => setError(cause instanceof Error ? cause.message : 'Schedule configuration could not be loaded.'))
  }, [])

  useEffect(() => {
    let active = true
    const timer = window.setTimeout(() => {
      void getJson<Patient[]>(`/api/patients?take=100&query=${encodeURIComponent(query)}`).then(data => { if (active) setPatients(data.filter(item => item.isActive)) })
        .catch((cause: unknown) => { if (active) setError(cause instanceof Error ? cause.message : 'Patient search failed.') })
    }, 200)
    return () => { active = false; window.clearTimeout(timer) }
  }, [query])

  useEffect(() => {
    if (!month) return
    let active = true
    const [year, number] = month.split('-')
    void getJson<ScheduleAppointment[]>(`/api/appointments?year=${year}&month=${number}`)
      .then(data => { if (active) setAppointments(data) }).catch((cause: unknown) => { if (active) setError(cause instanceof Error ? cause.message : 'Appointments could not be loaded.') })
    return () => { active = false }
  }, [month])

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (pending || !zone) return
    setPending(true); setError(''); setNotice('')
    try {
      await ensureSuccess(await apiFetch('/api/appointments', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ patientId, clinicianObjectId: clinicianId, clinicianName: '', durationMinutes: duration, reason,
          startsAt: new Date(`${date}T${time}`).toISOString(), localStartsAt: `${date}T${time}` }),
      }))
      const [year, number] = date.slice(0, 7).split('-')
      const updated = await getJson<ScheduleAppointment[]>(`/api/appointments?year=${year}&month=${number}`)
      setMonth(date.slice(0, 7)); setAppointments(updated); setNotice('Appointment scheduled.')
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Appointment could not be scheduled.') }
    finally { setPending(false) }
  }

  const start = new Date(`${month || '2000-01'}-01T12:00:00`)
  const days = new Date(start.getFullYear(), start.getMonth() + 1, 0).getDate()
  const cells = Array.from({ length: start.getDay() + days }, (_, index) => index < start.getDay() ? null : index - start.getDay() + 1)
  const daily = zone ? appointments.filter(item => clinicDateTime(item.startsAt, zone).date === date) : []

  return <>
    <header className="page-header"><h1>{t("Schedule")}</h1><p>{t("Clinic time zone:")} {zone ?? t("Loading...")}</p></header>
    {error && <div className="notice error" role="alert">{t(error)}</div>}
    {notice && <div className="notice" role="status">{t(notice)}</div>}
    <section className="schedule-layout"><section className="panel">
      <label>{t("Month")}<input type="month" value={month} onChange={event => { setMonth(event.target.value); setDate(`${event.target.value}-01`) }} /></label>
      <div className="month-calendar" role="grid" aria-label={t("Appointment calendar")}>
        {['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'].map(day => <span key={day}>{t(day)}</span>)}
        {cells.map((day, index) => day ? <button type="button" className={`calendar-day ${date === `${month}-${String(day).padStart(2, '0')}` ? 'selected' : ''}`}
          key={day} onClick={() => setDate(`${month}-${String(day).padStart(2, '0')}`)}>{day}
          {zone && appointments.some(item => clinicDateTime(item.startsAt, zone).date === `${month}-${String(day).padStart(2, '0')}`) && <i />}</button> :
          <span key={`blank-${index}`} className="calendar-day empty" />)}
      </div>
      <h2>{date}</h2>
      {daily.map(item => <Link className="schedule-item" key={item.id} to={`/appointments?appointmentId=${item.id}&month=${month}`}>
        <time>{zone && clinicDateTime(item.startsAt, zone).time}</time><div><strong>{item.patientName}</strong>
          <span>{t(item.reason)} · {item.durationMinutes} {t("min ·")} {t(item.status)}</span></div><small>{item.clinicianName}</small></Link>)}
      {!daily.length && <p>{t("No appointments on this day.")}</p>}
    </section>
      <form className="panel appointment-form" onSubmit={event => void submit(event)}><h2>{t("Schedule a visit")}</h2>
        <Link to="/patients/new?returnTo=/schedule">{t("Register patient and guardian")}</Link>
        <fieldset disabled={pending || !zone}>
          <label>{t("Search patients")}<input value={query} onChange={event => setQuery(event.target.value)} placeholder={t("Patient, guardian or phone")} /></label>
          <label>{t("Patient")}<select value={patientId} required onChange={event => setPatientId(event.target.value)}>
            <option value="">{t("Select patient")}</option>{patients.map(item => <option key={item.id} value={item.id}>{item.name} · {item.guardianName}</option>)}</select></label>
          <label>{t("Clinician")}<select value={clinicianId} required onChange={event => setClinicianId(event.target.value)}>
            <option value="">{t("Select clinician")}</option>{clinicians.map(item => <option key={item.objectId} value={item.objectId}>{item.displayName}</option>)}</select></label>
          <label>{t("Date")}<input type="date" value={date} required onChange={event => setDate(event.target.value)} /></label>
          <label>{t("Clinic-local time")}<input type="time" value={time} required onChange={event => setTime(event.target.value)} /></label>
          <label>{t("Duration (minutes)")}<input type="number" min={5} max={480} value={duration} required onChange={event => setDuration(Number(event.target.value))} /></label>
          <label>{t("Reason")}<select value={reason} onChange={event => setReason(event.target.value)}>{appointmentReasons.map(value => <option value={value} key={value}>{t(value)}</option>)}</select></label>
          <button className="primary-action" type="submit">{pending ? t("Scheduling...") : t("Schedule visit")}</button>
        </fieldset>
      </form>
    </section>
  </>
}
