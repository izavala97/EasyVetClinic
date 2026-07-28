import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { ArrowLeft, PawPrint, UserRoundPlus } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import { apiFetch, type Guardian, type Patient, getJson } from '../api'

type RegistrationMode = 'guardian' | 'patient'

export function RegistrationPage({ mode }: { mode: RegistrationMode }) {
  const navigate = useNavigate()
  const [guardians, setGuardians] = useState<Guardian[]>([])
  const [guardianId, setGuardianId] = useState('')
  const [name, setName] = useState('')
  const [phone, setPhone] = useState('')
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
      setGuardianId(data[0]?.id ?? '')
    }).catch(() => setError('Guardians could not be loaded.'))
  }, [mode])

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    try {
      if (mode === 'guardian') {
        const response = await apiFetch('/api/guardians', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, phone }) })
        if (!response.ok) throw new Error()
        navigate('/patients/new')
        return
      }

      const response = await apiFetch('/api/patients', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ guardianId, name, species, breed, sex, weight, color, allergies }),
      })
      if (!response.ok) throw new Error()
      const patient = await response.json() as Patient
      navigate(`/patients/${patient.id}`)
    } catch {
      setError(mode === 'guardian' ? 'The guardian could not be registered.' : 'The patient could not be registered. Complete all required fields and try again.')
    }
  }

  const isGuardian = mode === 'guardian'
  return (
    <>
      <Link className="back-link" to="/patients"><ArrowLeft size={16} /> Back to patients</Link>
      <header className="page-header"><div><p className="eyebrow">{isGuardian ? 'Guardian directory' : 'Clinical records'}</p><h1>{isGuardian ? 'Register guardian' : 'Register patient'}</h1></div>{isGuardian ? <UserRoundPlus size={26} /> : <PawPrint size={26} />}</header>
      {error && <div className="notice error" role="alert">{error}</div>}
      <form className="panel registration-form" onSubmit={(event) => void submit(event)}>
        {isGuardian ? <><label>Full name<input value={name} onChange={(event) => setName(event.target.value)} required /></label><label>Phone<input value={phone} onChange={(event) => setPhone(event.target.value)} inputMode="tel" required /></label></> : <><label>Guardian<select value={guardianId} onChange={(event) => setGuardianId(event.target.value)} required>{guardians.map((guardian) => <option key={guardian.id} value={guardian.id}>{guardian.name} - {guardian.phone}</option>)}</select></label>{!guardians.length && <p className="empty-state">Register a guardian before creating a patient.</p>}<label>Patient name<input value={name} onChange={(event) => setName(event.target.value)} required /></label><label>Species<select value={species} onChange={(event) => setSpecies(event.target.value)}><option>Canine</option><option>Feline</option><option>Avian</option><option>Other</option></select></label><label>Breed<input value={breed} onChange={(event) => setBreed(event.target.value)} required /></label><label>Sex<select value={sex} onChange={(event) => setSex(event.target.value)}><option>Female</option><option>Male</option></select></label><label>Weight<input value={weight} onChange={(event) => setWeight(event.target.value)} placeholder="e.g. 12.5 kg" required /></label><label>Color<input value={color} onChange={(event) => setColor(event.target.value)} required /></label><label>Allergies<input value={allergies} onChange={(event) => setAllergies(event.target.value)} /></label></>}
        <button className="primary-action" type="submit" disabled={!isGuardian && !guardianId}>{isGuardian ? <UserRoundPlus size={18} /> : <PawPrint size={18} />}{isGuardian ? 'Register guardian' : 'Register patient'}</button>
      </form>
    </>
  )
}