import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { ArrowLeft, PawPrint, UserRoundPlus } from 'lucide-react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { apiFetch, type Guardian, type Patient, getJson } from '../api'

type RegistrationMode = 'guardian' | 'patient'

export function RegistrationPage({ mode }: { mode: RegistrationMode }) {
  const navigate = useNavigate()
  const location = useLocation()
  const patientIdToLink = new URLSearchParams(location.search).get('patientId')
  const returnTo = new URLSearchParams(location.search).get('returnTo')
  const [guardians, setGuardians] = useState<Guardian[]>([])
  const [guardianIds, setGuardianIds] = useState<string[]>([])
  const [name, setName] = useState('')
  const [phone, setPhone] = useState('')
  const [alternatePhone, setAlternatePhone] = useState('')
  const [address, setAddress] = useState('')
  const [identityType, setIdentityType] = useState('')
  const [identityNumber, setIdentityNumber] = useState('')
  const [identityDocumentUrl, setIdentityDocumentUrl] = useState('')
  const [species, setSpecies] = useState('Canine')
  const [breed, setBreed] = useState('')
  const [sex, setSex] = useState('Female')
  const [weight, setWeight] = useState('')
  const [color, setColor] = useState('')
  const [allergies, setAllergies] = useState('No known allergies')
  const [error, setError] = useState('')

  useEffect(() => {
    if (mode !== 'patient') return
    void getJson<Guardian[]>('/api/guardians').then((data) => {
      setGuardians(data)
      setGuardianIds(data[0] ? [data[0].id] : [])
    }).catch(() => setError('Guardians could not be loaded.'))
  }, [mode])

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    try {
      if (mode === 'guardian') {
        const response = await apiFetch('/api/guardians', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, phone, alternatePhone, address, identityType, identityNumber, identityDocumentUrl }) })
        if (!response.ok) throw new Error()
        const guardian = await response.json() as Guardian
        if (patientIdToLink) {
          const linkResponse = await apiFetch(`/api/patients/${patientIdToLink}/guardians/${guardian.id}`, { method: 'POST' })
          if (!linkResponse.ok) throw new Error()
        }
        navigate(returnTo ?? '/patients/new')
        return
      }

      const response = await apiFetch('/api/patients', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ guardianIds, name, species, breed, sex, weight, color, allergies }),
      })
      if (!response.ok) throw new Error()
      const patient = await response.json() as Patient
      navigate(returnTo ?? `/patients/${patient.id}`)
    } catch {
      setError(mode === 'guardian' ? 'The guardian could not be registered.' : 'The patient could not be registered. Complete all required fields and try again.')
    }
  }

  const isGuardian = mode === 'guardian'
  return (
    <>
      <Link className="back-link" to={returnTo ?? '/patients'}><ArrowLeft size={16} /> Back to patients</Link>
      <header className="page-header"><div><p className="eyebrow">{isGuardian ? 'Guardian directory' : 'Clinical records'}</p><h1>{isGuardian ? 'Register guardian' : 'Register patient'}</h1></div>{isGuardian ? <UserRoundPlus size={26} /> : <PawPrint size={26} />}</header>
      {error && <div className="notice error" role="alert">{error}</div>}
      <form className="panel registration-form" onSubmit={(event) => void submit(event)}>
        {isGuardian ? <><label>Full name<input value={name} onChange={(event) => setName(event.target.value)} required /></label><label>Phone<input value={phone} onChange={(event) => setPhone(event.target.value)} inputMode="tel" required /></label><label>Alternate phone<input value={alternatePhone} onChange={(event) => setAlternatePhone(event.target.value)} inputMode="tel" /></label><label>Address<input value={address} onChange={(event) => setAddress(event.target.value)} /></label><label>Identity type<input value={identityType} onChange={(event) => setIdentityType(event.target.value)} /></label><label>Identity number<input value={identityNumber} onChange={(event) => setIdentityNumber(event.target.value)} /></label><label>Identity document URL<input value={identityDocumentUrl} onChange={(event) => setIdentityDocumentUrl(event.target.value)} type="url" /></label></> : <><div className="guardian-selector"><div className="form-actions"><strong>Guardians</strong><Link className="secondary-action" to={`/guardians/new${location.search}`}><UserRoundPlus size={16} /> Register guardian</Link></div>{guardians.map((guardian) => <label className="toggle-field" key={guardian.id}><input type="checkbox" checked={guardianIds.includes(guardian.id)} onChange={(event) => setGuardianIds((current) => event.target.checked ? [...current, guardian.id] : current.filter((id) => id !== guardian.id))} />{guardian.name} - {guardian.phone}</label>)}</div>{!guardians.length && <p className="empty-state">Register a guardian before creating a patient.</p>}<label>Patient name<input value={name} onChange={(event) => setName(event.target.value)} required /></label><label>Species<select value={species} onChange={(event) => setSpecies(event.target.value)}><option>Canine</option><option>Feline</option><option>Avian</option><option>Other</option></select></label><label>Breed<input value={breed} onChange={(event) => setBreed(event.target.value)} required /></label><label>Sex<select value={sex} onChange={(event) => setSex(event.target.value)}><option>Female</option><option>Male</option></select></label><label>Weight<input value={weight} onChange={(event) => setWeight(event.target.value)} placeholder="e.g. 12.5 kg" required /></label><label>Color<input value={color} onChange={(event) => setColor(event.target.value)} required /></label><label>Allergies<input value={allergies} onChange={(event) => setAllergies(event.target.value)} /></label></>}
        <button className="primary-action" type="submit" disabled={!isGuardian && !guardianIds.length}>{isGuardian ? <UserRoundPlus size={18} /> : <PawPrint size={18} />}{isGuardian ? 'Register guardian' : 'Register patient'}</button>
      </form>
    </>
  )
}