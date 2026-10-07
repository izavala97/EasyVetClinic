import { useEffect, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { apiFetch, ensureSuccess, getJson, type Guardian, type Patient } from '../api'

export function RegistrationPage({ mode }: { mode: 'guardian' | 'patient' }) {
  const navigate = useNavigate()
  const location = useLocation()
  const params = new URLSearchParams(location.search)
  const requestedReturn = params.get('returnTo')
  const returnTo = requestedReturn?.startsWith('/') && !requestedReturn.startsWith('//') ? requestedReturn : null
  const [guardians, setGuardians] = useState<Guardian[]>([])
  const [query, setQuery] = useState('')
  const [guardianIds, setGuardianIds] = useState<string[]>([])
  const [primaryId, setPrimaryId] = useState('')
  const [newContact, setNewContact] = useState(mode === 'guardian')
  const [contact, setContact] = useState({ name: '', phone: '', alternatePhone: '', address: '', identityType: '', identityNumber: '', identityDocumentUrl: '' })
  const [patient, setPatient] = useState({ name: '', species: 'Canine', breed: '', sex: 'Unknown', weight: '', color: '', allergies: '' })
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    void getJson<Guardian[]>('/api/guardians').then(data => { if (active) setGuardians(data) })
      .catch((cause: unknown) => { if (active) setError(cause instanceof Error ? cause.message : 'Contacts could not be loaded.') })
    return () => { active = false }
  }, [])

  const search = query.trim().toLowerCase()
  const selectedGuardians = guardians.filter(item => guardianIds.includes(item.id))
  const matching = search.length >= 2 ? guardians.filter(item => !guardianIds.includes(item.id) &&
    [item.name, item.phone, item.alternatePhone].some(value => value.toLowerCase().includes(search))) : []
  const duplicate = guardians.some(item => item.phone.replace(/\D/g, '') === contact.phone.replace(/\D/g, '') && contact.phone !== '')

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (pending) return
    setPending(true); setError('')
    try {
      const isGuardian = mode === 'guardian'
      const response = await apiFetch(isGuardian ? '/api/guardians' : '/api/patients', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(isGuardian ? contact : { ...patient, guardianIds,
          primaryGuardianId: primaryId || null, newGuardian: newContact ? contact : null }),
      })
      await ensureSuccess(response)
      if (isGuardian) {
        const guardian = await response.json() as Guardian
        const patientId = params.get('patientId')
        if (patientId) {
          await ensureSuccess(await apiFetch(`/api/patients/${patientId}/guardians/${guardian.id}`, { method: 'POST' }))
        }
        navigate(returnTo ?? '/patients/new')
      } else {
        const created = await response.json() as Patient
        navigate(returnTo ?? `/patients/${created.id}`)
      }
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Registration could not be completed.') }
    finally { setPending(false) }
  }

  return <>
    <Link className="back-link" to={returnTo ?? '/patients'}>Back to patients</Link>
    <header className="page-header"><div><p className="eyebrow">Quick intake</p><h1>{mode === 'guardian' ? 'Register guardian' : 'Register patient and guardian'}</h1></div></header>
    {error && <div className="notice error" role="alert">{error}</div>}
    <form className="panel registration-form" onSubmit={event => void submit(event)}>
      <fieldset disabled={pending}>
        {mode === 'patient' && <>
          <h2>Patient</h2>
          {(['name', 'breed', 'weight', 'color', 'allergies'] as const).map(field => <label key={field}>
            {({ name: 'Patient name', breed: 'Breed (optional)', weight: 'Initial weight (optional)', color: 'Color (optional)', allergies: 'Allergies (blank means unknown / not assessed)' })[field]}
            <input value={patient[field]} required={field === 'name'} onChange={event => setPatient({ ...patient, [field]: event.target.value })} />
          </label>)}
          <label>Species<select value={patient.species} onChange={event => setPatient({ ...patient, species: event.target.value })}>
            {['Canine', 'Feline', 'Avian', 'Other'].map(value => <option key={value}>{value}</option>)}</select></label>
          <label>Sex<select value={patient.sex} onChange={event => setPatient({ ...patient, sex: event.target.value })}>
            {['Unknown', 'Female', 'Male'].map(value => <option key={value}>{value}</option>)}</select></label>
          <h2>Guardians</h2>
          {!!selectedGuardians.length && <div className="selected-guardians" role="group" aria-label="Selected guardians">
            {selectedGuardians.map(item => <div className="selected-guardian" key={item.id}>
              <span><strong>{item.name}</strong><small>{item.phone}</small></span>
              <button className="secondary-action" type="button" aria-label={`Remove guardian ${item.name}`} onClick={() => {
                setGuardianIds(guardianIds.filter(id => id !== item.id))
                if (primaryId === item.id) setPrimaryId('')
              }}>Remove</button>
            </div>)}
          </div>}
          <label>Search existing contacts<input value={query} onChange={event => setQuery(event.target.value)} placeholder="Name or phone" aria-describedby="guardian-search-help" /></label>
          <p id="guardian-search-help" className="guardian-search-help" role="status">
            {search.length < 2 ? 'Type at least 2 characters to find a guardian.' :
              !matching.length ? 'No unselected contacts match your search.' :
              matching.length > 5 ? `Showing 5 of ${matching.length} matches. Refine your search to find another guardian.` :
              `${matching.length} matching contact${matching.length === 1 ? '' : 's'}.`}
          </p>
          {!!matching.length && <div className="guardian-options" role="group" aria-label="Matching guardians">
            {matching.slice(0, 5).map(item => <button type="button" className="guardian-option" key={item.id}
              aria-label={`Add guardian ${item.name}`} onClick={() => {
                setGuardianIds([...guardianIds, item.id])
                setQuery('')
              }}><span><strong>{item.name}</strong><small>{item.phone}</small></span><span>Add</span></button>)}
          </div>}
          {!!guardianIds.length && <label>Primary guardian<select required value={primaryId} onChange={event => setPrimaryId(event.target.value)}>
            <option value="">Select primary guardian</option>
            {selectedGuardians.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select></label>}
          <label className="toggle-field"><input type="checkbox" checked={newContact} onChange={event => setNewContact(event.target.checked)} /> Register a new guardian without leaving this form</label>
        </>}
        {newContact && <section className="intake-contact-form">
          <h2>New guardian</h2>
          {(['name', 'phone', 'alternatePhone', 'address'] as const).map(field => <label key={field}>
            {({ name: 'Full name', phone: 'Phone', alternatePhone: 'Alternate phone', address: 'Address' })[field]}
            <input value={contact[field]} type={field.includes('Phone') || field === 'phone' ? 'tel' : 'text'}
              required={field === 'name' || field === 'phone'} onChange={event => setContact({ ...contact, [field]: event.target.value })} />
          </label>)}
          {duplicate && <p className="notice error">A contact with this phone already exists. Select that contact to avoid duplicates.</p>}
          <details><summary>Identity details (optional)</summary>
            {(['identityType', 'identityNumber', 'identityDocumentUrl'] as const).map(field => <label key={field}>
              {({ identityType: 'Identity type', identityNumber: 'Identity number', identityDocumentUrl: 'Identity document URL' })[field]}
              <input type={field === 'identityDocumentUrl' ? 'url' : 'text'} value={contact[field]} onChange={event => setContact({ ...contact, [field]: event.target.value })} />
            </label>)}
          </details>
        </section>}
        <button className="primary-action" type="submit" disabled={mode === 'patient' && ((!guardianIds.length && !newContact) || (newContact && duplicate))}>
          {pending ? 'Registering...' : mode === 'guardian' ? 'Register guardian' : 'Register patient'}
        </button>
      </fieldset>
    </form>
  </>
}
