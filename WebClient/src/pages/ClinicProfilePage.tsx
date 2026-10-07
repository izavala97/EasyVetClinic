import { useEffect, useState, type FormEvent } from 'react'
import { apiFetch, ensureSuccess, type ClinicProfile, type Clinician, getJson } from '../api'

export function ClinicProfilePage() {
  const [profile, setProfile] = useState<ClinicProfile | null>(null)
  const [clinicians, setClinicians] = useState<Clinician[]>([])
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)
  useEffect(() => {
    void Promise.all([getJson<ClinicProfile>('/api/clinic'), getJson<Clinician[]>('/api/clinicians')])
      .then(([clinic, team]) => { setProfile(clinic); setClinicians(team) })
      .catch((cause: unknown) => setError(cause instanceof Error ? cause.message : 'Clinic settings could not be loaded.'))
  }, [])

  async function save(event: FormEvent<HTMLFormElement>, objectId?: string) {
    event.preventDefault()
    if (pending) return
    const form = new FormData(event.currentTarget)
    const payload = objectId ? { professionalTitles: form.get('professionalTitles'), licenseNumber: form.get('licenseNumber') } :
      { name: form.get('name'), address: form.get('address'), logoUrl: form.get('logoUrl'), timeZoneId: form.get('timeZoneId') }
    setPending(true); setError('')
    try {
      await ensureSuccess(await apiFetch(objectId ? `/api/clinicians/${objectId}/credentials` : '/api/clinic', {
        method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload),
      }))
      setNotice('Settings saved. Existing clinical records keep their credential snapshots.')
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Settings could not be saved.') }
    finally { setPending(false) }
  }

  if (!profile) return <>{error ? <div className="notice error" role="alert">{error}</div> : <p>Loading clinic settings...</p>}</>
  return <>
    <header className="page-header"><h1>Clinic settings</h1></header>
    {notice && <div className="notice" role="status">{notice}</div>}{error && <div className="notice error" role="alert">{error}</div>}
    <form className="panel clinic-profile-form" onSubmit={event => void save(event)}>
      <fieldset disabled={pending}><h2>Clinic details</h2>
        <label>Clinic name<input name="name" defaultValue={profile.name} maxLength={200} required /></label>
        <label>Address<input name="address" defaultValue={profile.address} maxLength={500} /></label>
        <label>Logo URL<input name="logoUrl" type="url" defaultValue={profile.logoUrl} /></label>
        <label>Time zone<input name="timeZoneId" defaultValue={profile.timeZoneId} required list="clinic-time-zones" /></label>
        <datalist id="clinic-time-zones">{['UTC', 'America/Mexico_City', 'America/Monterrey', 'America/Tijuana', 'America/Cancun', 'Europe/Madrid'].map(zone => <option key={zone}>{zone}</option>)}</datalist>
        <p>Appointments use this time zone, independently of the browser. Existing appointment instants do not move when it changes.</p>
        <button className="primary-action" type="submit">Save clinic</button>
      </fieldset>
    </form>
    <section className="panel"><h2>Clinician credentials</h2><p>Set credentials before starting care. Prescriptions retain the treating clinician's snapshot.</p>
      {clinicians.filter(clinician => clinician.canEditCredentials).map(clinician => <form className="clinical-entry-form" key={clinician.objectId} onSubmit={event => void save(event, clinician.objectId)}>
        <h3>{clinician.displayName}</h3><label>Titles<input name="professionalTitles" defaultValue={clinician.professionalTitles} maxLength={300} /></label>
        <label>Professional license<input name="licenseNumber" defaultValue={clinician.licenseNumber} maxLength={100} /></label>
        <button className="secondary-action" type="submit" disabled={pending}>Save credentials</button>
      </form>)}
    </section>
  </>
}
