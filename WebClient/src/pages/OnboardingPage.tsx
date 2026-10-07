import { useState } from 'react'
import { Building2, Save } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { apiFetch, ensureSuccess } from '../api'
import { useAuthentication } from '../auth'

export function OnboardingPage() {
  const navigate = useNavigate()
  const { refreshCurrentUser } = useAuthentication()
  const [error, setError] = useState('')
  const [isSaving, setIsSaving] = useState(false)

  async function createClinic(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setError('')
    setIsSaving(true)
    try {
      const response = await apiFetch('/api/onboarding/clinic', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: form.get('name'),
          address: form.get('address'),
          logoUrl: form.get('logoUrl'),
          veterinarianName: form.get('veterinarianName'),
          veterinarianTitles: form.get('veterinarianTitles'),
          veterinarianLicenseNumber: form.get('veterinarianLicenseNumber'),
        }),
      })
      await ensureSuccess(response)
      await refreshCurrentUser()
      navigate('/platform', { replace: true })
    } catch (error) {
      setError(error instanceof Error ? error.message : 'The clinic could not be created.')
    } finally {
      setIsSaving(false)
    }
  }

  return <main className="onboarding-shell"><section className="panel onboarding-panel"><header className="page-header"><div><p className="eyebrow">Platform setup</p><h1>Register a clinic</h1></div><Building2 size={28} /></header><p>After creating the clinic, open its workspace and invite its first clinic administrator from Clinic administration.</p>{error && <div className="notice error" role="alert">{error}</div>}<form className="clinic-profile-form" onSubmit={(event) => void createClinic(event)}><label>Clinic name<input name="name" maxLength={200} required /></label><label>Address<input name="address" maxLength={500} /></label><label>Logo URL<input name="logoUrl" type="url" /></label><label>Veterinarian name<input name="veterinarianName" maxLength={200} required /></label><label>Titles and specialties<input name="veterinarianTitles" maxLength={300} /></label><label>Professional license number<input name="veterinarianLicenseNumber" maxLength={100} /></label><button className="primary-action" type="submit" disabled={isSaving}><Save size={18} />{isSaving ? 'Creating clinic...' : 'Create clinic'}</button></form></section></main>
}