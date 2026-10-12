import { useTranslation } from '../useTranslation'
import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useAuthentication } from '../auth'
import { apiFetch, ensureSuccess, getJson, type ScheduleAppointment, type Clinician, type ClinicProfile } from '../api'
import { appointmentReasons, clinicDateTime } from '../schedule'

export function AppointmentsPage() {
  const { t } = useTranslation()

  const [params] = useSearchParams()
  const navigate = useNavigate()
  const { currentUser } = useAuthentication()
  const [month, setMonth] = useState(params.get('month') ?? '')
  const [zone, setZone] = useState<string | null>(null)
  const [appointments, setAppointments] = useState<ScheduleAppointment[]>([])
  const [clinicians, setClinicians] = useState<Clinician[]>([])
  const [selected, setSelected] = useState<ScheduleAppointment | null>(null)
  const [date, setDate] = useState('')
  const [time, setTime] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [pending, setPending] = useState(false)
  const requestedId = params.get('appointmentId')
  const canStart = ['ClinicAdmin', 'Veterinarian', 'SuperAdmin'].includes(currentUser?.role ?? '')

  useEffect(() => {
    void Promise.all([getJson<ClinicProfile>('/api/clinic'), getJson<Clinician[]>('/api/clinicians')]).then(([profile, team]) => {
      setZone(profile.timeZoneId); setClinicians(team)
      if (!params.get('month')) setMonth(clinicDateTime(new Date(), profile.timeZoneId).date.slice(0, 7))
    }).catch((cause: unknown) => setError(cause instanceof Error ? cause.message : 'Schedule configuration could not be loaded.'))
  }, [params])

  useEffect(() => {
    if (!month || !zone) return
    let active = true
    const [year, number] = month.split('-')
    void getJson<ScheduleAppointment[]>(`/api/appointments?year=${year}&month=${number}`).then(data => {
      if (!active) return
      setAppointments(data)
      const requested = data.find(item => item.id === requestedId)
      if (requested) { setSelected(requested); const local = clinicDateTime(requested.startsAt, zone); setDate(local.date); setTime(local.time) }
    }).catch((cause: unknown) => { if (active) setError(cause instanceof Error ? cause.message : 'Appointments could not be loaded.') })
    return () => { active = false }
  }, [month, zone, requestedId])

  function select(item: ScheduleAppointment) {
    if (!zone) return
    setSelected(item); setError(''); setNotice('')
    const local = clinicDateTime(item.startsAt, zone)
    setDate(local.date); setTime(local.time)
  }

  async function save(event: FormEvent) {
    event.preventDefault()
    if (!selected || pending) return
    setPending(true); setError('')
    try {
      const response = await apiFetch(`/api/appointments/${selected.id}`, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ...selected, expectedVersion: selected.version, startsAt: new Date(`${date}T${time}`).toISOString(),
          localStartsAt: `${date}T${time}` }),
      })
      await ensureSuccess(response)
      const updated = await response.json() as ScheduleAppointment
      setSelected(updated); setAppointments(items => items.map(item => item.id === updated.id ? updated : item))
      setNotice('Appointment updated.')
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Appointment could not be saved.') }
    finally { setPending(false) }
  }

  async function start() {
    if (!selected || pending) return
    if (selected.consultationId) { navigate(`/consultations/${selected.consultationId}`); return }
    setPending(true); setError('')
    try {
      const response = await apiFetch(`/api/patients/${selected.patientId}/consultations`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ appointmentId: selected.id, clinicianObjectId: selected.clinicianObjectId || currentUser?.objectId }),
      })
      await ensureSuccess(response)
      const record = await response.json() as { id: string }
      navigate(`/consultations/${record.id}`)
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Consultation could not be started.') }
    finally { setPending(false) }
  }

  return <>
    <header className="page-header"><h1>{t("Appointments")}</h1><Link to="/schedule">{t("Schedule a visit")}</Link></header>
    <p>{t("Clinic time zone:")} {zone ?? t("Loading...")}</p>
    {error && <div className="notice error" role="alert">{t(error)}</div>}
    {notice && <div className="notice" role="status">{t(notice)}</div>}
    <section className="schedule-layout">
      <section className="panel"><label>{t("Month")}<input type="month" value={month} onChange={event => { setMonth(event.target.value); setSelected(null) }} /></label>
        {appointments.map(item => <button className="appointment-management-row" type="button" key={item.id} onClick={() => select(item)}>
          <strong>{item.patientName}</strong><span>{zone && `${clinicDateTime(item.startsAt, zone).date} ${clinicDateTime(item.startsAt, zone).time}`}</span>
          <span>{item.clinicianName} · {t(item.status)}</span></button>)}
        {!appointments.length && <p>{t("No appointments this month.")}</p>}
      </section>
      {selected ? <section className="panel"><h2>{selected.patientName}</h2>
        {selected.consultationId ? <Link to={`/consultations/${selected.consultationId}`}>{t("Open linked consultation")}</Link> :
          <form onSubmit={event => void save(event)}><fieldset disabled={pending}>
            <label>{t("Date")}<input type="date" value={date} required onChange={event => setDate(event.target.value)} /></label>
            <label>{t("Clinic-local time")}<input type="time" value={time} required onChange={event => setTime(event.target.value)} /></label>
            <label>{t("Duration")}<input type="number" min={5} max={480} value={selected.durationMinutes} onChange={event => setSelected({ ...selected, durationMinutes: Number(event.target.value) })} /></label>
            <label>{t("Clinician")}<select value={selected.clinicianObjectId} required onChange={event => setSelected({ ...selected, clinicianObjectId: event.target.value })}>
              <option value="">{t("Select clinician")}</option>{clinicians.map(item => <option key={item.objectId} value={item.objectId}>{item.displayName}</option>)}</select></label>
            <label>{t("Reason")}<select value={selected.reason} onChange={event => setSelected({ ...selected, reason: event.target.value })}>
              {appointmentReasons.map(value => <option value={value} key={value}>{t(value)}</option>)}</select></label>
            <label>{t("Status")}<select value={selected.status} onChange={event => setSelected({ ...selected, status: event.target.value })}>
              {['Scheduled', 'CheckedIn', 'Cancelled', 'NoShow'].map(value => <option value={value} key={value}>{t(value)}</option>)}</select></label>
            <button className="primary-action" type="submit">{t("Save appointment")}</button>
          </fieldset></form>}
        {canStart && ['Scheduled', 'CheckedIn', 'InProgress'].includes(selected.status) &&
          <button className="secondary-action" type="button" disabled={pending} onClick={() => void start()}>{selected.consultationId ? t("Resume consultation") : t("Start consultation")}</button>}
      </section> : <p>{t("Select an appointment.")}</p>}
    </section>
  </>
}
