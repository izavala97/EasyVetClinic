import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { ArrowLeft, PawPrint, Search, UserRoundPlus } from 'lucide-react'
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
  const [guardianQuery, setGuardianQuery] = useState('')
  const [isGuardianListOpen, setIsGuardianListOpen] = useState(false)
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

  function resetForm() {
    setGuardians([])
    setGuardianIds([])
    setGuardianQuery('')
    setIsGuardianListOpen(false)
    setName('')
    setPhone('')
    setAlternatePhone('')
    setAddress('')
    setIdentityType('')
    setIdentityNumber('')
    setIdentityDocumentUrl('')
    setSpecies('Canine')
    setBreed('')
    setSex('Female')
    setWeight('')
    setColor('')
    setAllergies('No known allergies')
    setError('')
  }

  useEffect(() => {
    resetForm()
    if (mode !== 'patient') return
    void getJson<Guardian[]>('/api/guardians').then((data) => {
      setGuardians(data)
      setGuardianIds([])
    }).catch(() => setError('Guardians could not be loaded.'))
  }, [mode, location.key])

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')
    try {
      if (mode === 'guardian') {
        const sanitizedPhone = phone.replace(/\D/g, '')
        const sanitizedAlternatePhone = alternatePhone.replace(/\D/g, '')
        const response = await apiFetch('/api/guardians', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, phone: sanitizedPhone, alternatePhone: sanitizedAlternatePhone, address, identityType, identityNumber, identityDocumentUrl }) })
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
  const selectedGuardians = guardians.filter((guardian) => guardianIds.includes(guardian.id))
  const matchingGuardians = guardians.filter((guardian) => {
    const query = guardianQuery.trim().toLowerCase()
    return !query || [guardian.name, guardian.phone, guardian.alternatePhone].some((value) => value.toLowerCase().includes(query))
  })

  return (
    <>
      <Link className="back-link" to={returnTo ?? '/patients'}><ArrowLeft size={16} /> Back to patients</Link>
      <header className="page-header"><div><p className="eyebrow">{isGuardian ? 'Guardian directory' : 'Clinical records'}</p><h1>{isGuardian ? 'Register guardian' : 'Register patient'}</h1></div>{isGuardian ? <UserRoundPlus size={26} /> : <PawPrint size={26} />}</header>
      {error && <div className="notice error" role="alert">{error}</div>}
      <form className="panel registration-form" onSubmit={(event) => void submit(event)}>
        {isGuardian ? <><label>Full name<input value={name} onChange={(event) => setName(event.target.value)} required /></label><label>Phone<input value={phone} onChange={(event) => setPhone(event.target.value.replace(/\D/g, ''))} inputMode="numeric" pattern="[0-9]*" required /></label><label>Alternate phone<input value={alternatePhone} onChange={(event) => setAlternatePhone(event.target.value.replace(/\D/g, ''))} inputMode="numeric" pattern="[0-9]*" /></label><label>Address<input value={address} onChange={(event) => setAddress(event.target.value)} /></label><label>Identity type<input value={identityType} onChange={(event) => setIdentityType(event.target.value)} /></label><label>Identity number<input value={identityNumber} onChange={(event) => setIdentityNumber(event.target.value)} /></label><label>Identity document URL<input value={identityDocumentUrl} onChange={(event) => setIdentityDocumentUrl(event.target.value)} type="url" /></label></> : <><div className="guardian-selector"><div className="form-actions"><strong>Guardians</strong><Link className="secondary-action" to={`/guardians/new${location.search}`}><UserRoundPlus size={16} /> Register guardian</Link></div><div className="patient-combobox"><div className="search-field"><Search size={16} /><input value={guardianQuery} onChange={(event) => { setGuardianQuery(event.target.value); setIsGuardianListOpen(true) }} onFocus={() => setIsGuardianListOpen(true)} onBlur={() => window.setTimeout(() => setIsGuardianListOpen(false), 120)} onKeyDown={(event) => { if (event.key === 'Escape') { event.preventDefault(); setGuardianQuery(''); setIsGuardianListOpen(false) } }} placeholder="Search guardian by name or phone" role="combobox" aria-expanded={isGuardianListOpen} aria-controls="guardian-search-options" aria-autocomplete="list" /></div>{isGuardianListOpen && <div className="patient-options" id="guardian-search-options" role="listbox">{matchingGuardians.map((guardian) => <button type="button" role="option" aria-selected={guardianIds.includes(guardian.id)} key={guardian.id} onClick={() => { setGuardianIds((current) => current.includes(guardian.id) ? current.filter((id) => id !== guardian.id) : [...current, guardian.id]); setGuardianQuery(''); setIsGuardianListOpen(false) }}><strong>{guardian.name}</strong><span>{guardian.phone}{guardian.alternatePhone ? ` · ${guardian.alternatePhone}` : ''}</span></button>)}{!matchingGuardians.length && <p className="empty-state">No guardians match this search.</p>}</div>}</div>{selectedGuardians.length > 0 && <div className="selected-guardian-box" aria-live="polite">{selectedGuardians.map((guardian) => <div className="guardian-chip" key={guardian.id}><span>{guardian.name}</span><button type="button" aria-label={`Remove ${guardian.name}`} onClick={() => setGuardianIds((current) => current.filter((id) => id !== guardian.id))}>×</button></div>)}</div>}</div>{!guardians.length && <p className="empty-state">Register a guardian before creating a patient.</p>}<label>Patient name<input value={name} onChange={(event) => setName(event.target.value)} required /></label><label>Species<select value={species} onChange={(event) => setSpecies(event.target.value)}><option>Canine</option><option>Feline</option><option>Avian</option><option>Other</option></select></label><label>Breed<input value={breed} onChange={(event) => setBreed(event.target.value)} required /></label><label>Sex<select value={sex} onChange={(event) => setSex(event.target.value)}><option>Female</option><option>Male</option></select></label><label>Weight (kg)<input value={weight} onChange={(event) => setWeight(event.target.value)} inputMode="decimal" placeholder="e.g. 12.5" required /></label><label>Color<input value={color} onChange={(event) => setColor(event.target.value)} required /></label><label>Allergies<input value={allergies} onChange={(event) => setAllergies(event.target.value)} /></label></>}
        <button className="primary-action" type="submit" disabled={!isGuardian && !guardianIds.length}>{isGuardian ? <UserRoundPlus size={18} /> : <PawPrint size={18} />}{isGuardian ? 'Register guardian' : 'Register patient'}</button>
      </form>
    </>
  )
}