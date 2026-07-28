import { useEffect, useState } from 'react'
import { Building2, Save } from 'lucide-react'
import { apiFetch, type ClinicProfile, getJson } from '../api'

export function ClinicProfilePage() {
  const [profile, setProfile] = useState<ClinicProfile | null>(null)
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')

  useEffect(() => {
    void getJson<ClinicProfile>('/api/clinic').then(setProfile).catch(() => setError('Clinic profile could not be loaded.'))
  }, [])

  async function saveProfile(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    try {
      const response = await apiFetch('/api/clinic', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: form.get('name'), address: form.get('address'), logoUrl: form.get('logoUrl'), veterinarianName: form.get('veterinarianName'), veterinarianTitles: form.get('veterinarianTitles'), veterinarianLicenseNumber: form.get('veterinarianLicenseNumber') }) })
      if (!response.ok) throw new Error()
      setProfile(await response.json() as ClinicProfile)
      setNotice('Clinic profile saved.')
      setError('')
    } catch { setError('Clinic profile could not be saved.') }
  }

  if (!profile) return error ? <div className="notice error" role="alert">{error}</div> : <p className="loading-state">Loading clinic profile...</p>
  return <><header className="page-header"><div><p className="eyebrow">Clinic settings</p><h1>Clinic and clinician profile</h1></div><Building2 size={28} /></header>{notice && <div className="notice" role="status">{notice}</div>}{error && <div className="notice error" role="alert">{error}</div>}<form className="panel clinic-profile-form" onSubmit={saveProfile}><div className="panel-heading"><div><p className="eyebrow">Prescription header</p><h2>Clinic details</h2></div></div><label>Clinic name<input name="name" defaultValue={profile.name} required /></label><label>Address<input name="address" defaultValue={profile.address} /></label><label>Logo URL<input name="logoUrl" type="url" defaultValue={profile.logoUrl} /></label><div className="panel-heading clinician-profile-heading"><div><p className="eyebrow">Clinician</p><h2>Professional credentials</h2></div></div><label>Veterinarian name<input name="veterinarianName" defaultValue={profile.veterinarianName} required /></label><label>Titles and specialties<input name="veterinarianTitles" defaultValue={profile.veterinarianTitles} placeholder="Dipl. Canine Medicine · Dog Specialist" /></label><label>Professional license number<input name="veterinarianLicenseNumber" defaultValue={profile.veterinarianLicenseNumber} /></label><button className="primary-action" type="submit"><Save size={18} /> Save profile</button></form></>
}