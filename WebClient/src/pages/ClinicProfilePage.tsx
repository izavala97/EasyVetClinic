import { useTranslation } from '../useTranslation'
import { useEffect, useState, type FormEvent } from 'react'
import { apiFetch, ensureSuccess, type ClinicProfile, type Clinician, getJson } from '../api'
import i18n, { languages } from '../i18n'

export function ClinicProfilePage() {
  const { t } = useTranslation()

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
      { name: form.get('name'), address: form.get('address'), logoUrl: form.get('logoUrl'), timeZoneId: form.get('timeZoneId'), language: form.get('language') }
    setPending(true); setError('')
    try {
      const response = await apiFetch(objectId ? `/api/clinicians/${objectId}/credentials` : '/api/clinic', {
        method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload),
      })
      await ensureSuccess(response)
      if (!objectId) {
        const saved: ClinicProfile = await response.json()
        setProfile(saved)
        await i18n.changeLanguage(saved.language)
      }
      setNotice('Settings saved. Existing clinical records keep their credential snapshots.')
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Settings could not be saved.') }
    finally { setPending(false) }
  }

  if (!profile) return <>{error ? <div className="notice error" role="alert">{t(error)}</div> : <p>{t("Loading clinic settings...")}</p>}</>
  return <>
    <header className="page-header"><h1>{t("Clinic settings")}</h1></header>
    {notice && <div className="notice" role="status">{t(notice)}</div>}{error && <div className="notice error" role="alert">{t(error)}</div>}
    <form className="panel clinic-profile-form" onSubmit={event => void save(event)}>
      <fieldset disabled={pending}><h2>{t("Clinic details")}</h2>
        <label>{t("Clinic name")}<input name="name" defaultValue={profile.name} maxLength={200} required /></label>
        <label>{t("Address")}<input name="address" defaultValue={profile.address} maxLength={500} /></label>
        <label>{t("Logo URL")}<input name="logoUrl" type="url" defaultValue={profile.logoUrl} /></label>
        <label>{t("Time zone")}<input name="timeZoneId" defaultValue={profile.timeZoneId} required list="clinic-time-zones" /></label>
        <label>{t("Language")}<select name="language" defaultValue={profile.language}>
          {languages.map(language => <option key={language.code} value={language.code}>{language.name}</option>)}
        </select></label>
        <p>{t("This language is used by all staff in this clinic. Clinical notes and catalog names are not translated.")}</p>
        <datalist id="clinic-time-zones">{['UTC', 'America/Mexico_City', 'America/Monterrey', 'America/Tijuana', 'America/Cancun', 'Europe/Madrid'].map(zone => <option key={zone}>{zone}</option>)}</datalist>
        <p>{t("Appointments use this time zone, independently of the browser. Existing appointment instants do not move when it changes.")}</p>
        <button className="primary-action" type="submit">{t("Save clinic")}</button>
      </fieldset>
    </form>
    <section className="panel"><h2>{t("Clinician credentials")}</h2><p>{t("Set credentials before starting care. Prescriptions retain the treating clinician's snapshot.")}</p>
      {clinicians.filter(clinician => clinician.canEditCredentials).map(clinician => <form className="clinical-entry-form" key={clinician.objectId} onSubmit={event => void save(event, clinician.objectId)}>
        <h3>{clinician.displayName}</h3><label>{t("Titles")}<input name="professionalTitles" defaultValue={clinician.professionalTitles} maxLength={300} /></label>
        <label>{t("Professional license")}<input name="licenseNumber" defaultValue={clinician.licenseNumber} maxLength={100} /></label>
        <button className="secondary-action" type="submit" disabled={pending}>{t("Save credentials")}</button>
      </form>)}
    </section>
  </>
}
