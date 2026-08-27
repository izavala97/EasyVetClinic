import { useEffect, useState } from 'react'
import { ArrowLeft, ClipboardPlus, FileText, Pencil, Save, Trash2 } from 'lucide-react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { apiFetch, type ClinicProfile, type ConsultationHistoryItem, type Guardian, type Patient, getJson } from '../api'

const documentOptions = [
  { id: 'health-certificate', label: 'Health certificate' },
  { id: 'surgical-consent', label: 'Surgical consent' },
  { id: 'boarding-contract', label: 'Boarding contract' },
]

export function PatientDetailPage() {
  const { patientId } = useParams()
  const navigate = useNavigate()
  const [patient, setPatient] = useState<Patient | null>(null)
  const [availableGuardians, setAvailableGuardians] = useState<Guardian[]>([])
  const [guardianIdToLink, setGuardianIdToLink] = useState('')
  const [consultations, setConsultations] = useState<ConsultationHistoryItem[]>([])
  const [editingPatient, setEditingPatient] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (!patientId) return
    void Promise.all([getJson<Patient>(`/api/patients/${patientId}`), getJson<ConsultationHistoryItem[]>(`/api/patients/${patientId}/consultations`)]).then(([patientRecord, history]) => {
      setPatient(patientRecord)
      setConsultations(history)
    }).catch(() => {
      setError('This patient record could not be found.')
    })
    void getJson<Guardian[]>('/api/guardians').then(setAvailableGuardians).catch(() => {
      setError('Guardians could not be loaded.')
    })
  }, [patientId])

  async function linkExistingGuardian() {
    if (!patient || !guardianIdToLink) return
    try {
      const response = await apiFetch(`/api/patients/${patient.id}/guardians/${guardianIdToLink}`, { method: 'POST' })
      if (!response.ok) throw new Error()
      setPatient(await getJson<Patient>(`/api/patients/${patient.id}`))
      setGuardianIdToLink('')
      setError('')
    } catch {
      setError('The guardian could not be linked to this patient.')
    }
  }

  async function removeGuardian(guardianId: string) {
    if (!patient) return
    try {
      const response = await apiFetch(`/api/patients/${patient.id}/guardians/${guardianId}`, { method: 'DELETE' })
      if (!response.ok) throw new Error()
      setPatient(await getJson<Patient>(`/api/patients/${patient.id}`))
      setError('')
    } catch {
      setError('The guardian could not be removed from this patient.')
    }
  }

  async function startConsultation() {
    if (!patient) return
    try {
      const profile = await getJson<ClinicProfile>('/api/clinic')
      const response = await apiFetch(`/api/patients/${patient.id}/consultations`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ clinicianName: profile.veterinarianName }),
      })
      if (!response.ok) throw new Error('Could not start consultation.')
      const consultation = await response.json() as { id: string }
      navigate(`/consultations/${consultation.id}`)
    } catch {
      setError('The consultation could not be started.')
    }
  }

  async function prepareDocument(documentType: string) {
    if (!patient) return
    try {
      const [document, profile] = await Promise.all([
        getJson<{ title: string; fields: Record<string, string> }>(`/api/patients/${patient.id}/documents/${documentType}`),
        getJson<ClinicProfile>('/api/clinic'),
      ])
      const detail = Object.entries(document.fields).map(([key, value]) => `${key}: ${value}`).join('\n')
      const preview = window.open('', '_blank', 'noopener,noreferrer')
      preview?.document.write(`<pre>${document.title}\n\n${detail}\n\nPrepared by ${profile.veterinarianName}</pre>`)
      preview?.document.close()
      preview?.print()
    } catch {
      setError('The document draft could not be prepared.')
    }
  }

  async function savePatient(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!patient) return
    const form = new FormData(event.currentTarget)
    try {
      const response = await apiFetch(`/api/patients/${patient.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: form.get('name'), species: form.get('species'), breed: form.get('breed'), sex: form.get('sex'), weight: form.get('weight'), color: form.get('color'), allergies: form.get('allergies'), distinguishingFeatures: form.get('distinguishingFeatures'), dateOfBirth: form.get('dateOfBirth') || null, photoUrl: form.get('photoUrl'), isActive: form.get('isActive') === 'on' }) })
      if (!response.ok) throw new Error()
      setPatient(await response.json() as Patient)
      setEditingPatient(false)
      setError('')
    } catch { setError('Patient information could not be updated.') }
  }

  if (error && !patient) {
    return <><Link className="back-link" to="/patients"><ArrowLeft size={16} /> Back to patients</Link><div className="notice error" role="alert">{error}</div></>
  }

  if (!patient) return <p className="loading-state">Loading patient record...</p>

  return (
    <>
      <Link className="back-link" to="/patients"><ArrowLeft size={16} /> Back to patients</Link>
      <header className="topbar patient-page-title">
        <div className="patient-title"><span className="pet-avatar large">{patient.name[0]}</span><div><p className="eyebrow">Clinical record</p><h1>{patient.name}</h1><p>{patient.breed} · {patient.sex}</p></div></div>
        <button className="primary-action" type="button" onClick={() => void startConsultation()}><ClipboardPlus size={18} /> Start consultation</button>
      </header>
      {error && <div className="notice error" role="alert">{error}</div>}
      <section className="content-grid patient-record-grid">
        <section className="panel"><div className="panel-heading"><div><p className="eyebrow">Patient overview</p><h2>Record details</h2></div><button className="icon-button" type="button" aria-label="Edit patient" onClick={() => setEditingPatient((value) => !value)}><Pencil size={17} /></button></div>{editingPatient ? <form className="record-edit-form" onSubmit={savePatient}><label>Name<input name="name" defaultValue={patient.name} required /></label><label>Species<input name="species" defaultValue={patient.species} required /></label><label>Breed<input name="breed" defaultValue={patient.breed} required /></label><label>Sex<input name="sex" defaultValue={patient.sex} required /></label><label>Weight<input name="weight" defaultValue={patient.weight} required /></label><label>Color<input name="color" defaultValue={patient.color} required /></label><label>Allergies<input name="allergies" defaultValue={patient.allergies.join('; ')} /></label><label>Distinguishing features<input name="distinguishingFeatures" defaultValue={patient.distinguishingFeatures} /></label><label>Date of birth<input name="dateOfBirth" type="date" defaultValue={patient.dateOfBirth ?? ''} /></label><label>Photo URL<input name="photoUrl" type="url" defaultValue={patient.photoUrl} /></label><label className="toggle-field"><input name="isActive" type="checkbox" defaultChecked={patient.isActive} /> Active patient</label><button className="secondary-action" type="submit"><Save size={16} /> Save patient</button></form> : <dl className="record-details"><div><dt>Species</dt><dd>{patient.species}</dd></div><div><dt>Breed</dt><dd>{patient.breed}</dd></div><div><dt>Sex</dt><dd>{patient.sex}</dd></div><div><dt>Weight</dt><dd>{patient.weight}</dd></div><div><dt>Color</dt><dd>{patient.color}</dd></div><div><dt>Allergies</dt><dd>{patient.allergies.join(', ') || 'None recorded'}</dd></div><div><dt>Features</dt><dd>{patient.distinguishingFeatures || 'Not recorded'}</dd></div><div><dt>Last visit</dt><dd>{patient.lastVisit || 'Not recorded'}</dd></div></dl>}</section>
      <section className="panel patient-consultation-history"><div className="panel-heading"><div><p className="eyebrow">Clinical timeline</p><h2>Recent consultations</h2></div><Link className="secondary-action" to={`/consultations?query=${encodeURIComponent(patient.name)}`}>View all</Link></div><div className="history-list">{consultations.slice(0, 4).map((consultation) => <Link to={`/consultations/${consultation.id}`} key={consultation.id}><span>{new Date(consultation.startedAt).toLocaleDateString()}</span><strong>{consultation.diagnosis || 'Clinical notes in progress'}</strong><small>{consultation.clinicianName} · {consultation.status === 'Completed' ? 'Completed' : 'In progress'}</small></Link>)}</div>{consultations.length === 0 && <p className="empty-state">No consultations have been recorded for this patient.</p>}</section>
      </section>
      <section className="panel patient-guardians"><div className="panel-heading"><div><p className="eyebrow">Guardians</p><h2>Linked contacts</h2></div><Link className="secondary-action" to={`/guardians/new?patientId=${patient.id}&returnTo=${encodeURIComponent(`/patients/${patient.id}`)}`}><Pencil size={16} /> Add new guardian</Link></div><div className="guardian-list">{patient.guardians.map((linkedGuardian) => <article key={linkedGuardian.id}><strong>{linkedGuardian.name}</strong><span>{linkedGuardian.phone}{linkedGuardian.alternatePhone && ` · ${linkedGuardian.alternatePhone}`}</span><small>{linkedGuardian.id === patient.guardianId ? 'Primary guardian' : 'Additional guardian'}</small>{linkedGuardian.id !== patient.guardianId && <button className="icon-button" type="button" aria-label={`Remove ${linkedGuardian.name}`} onClick={() => void removeGuardian(linkedGuardian.id)}><Trash2 size={16} /></button>}</article>)}</div>{availableGuardians.some((candidate) => !patient.guardians.some((linkedGuardian) => linkedGuardian.id === candidate.id)) && <div className="existing-guardian"><label>Link an existing guardian<select value={guardianIdToLink} onChange={(event) => setGuardianIdToLink(event.target.value)}><option value="">Select guardian</option>{availableGuardians.filter((candidate) => !patient.guardians.some((linkedGuardian) => linkedGuardian.id === candidate.id)).map((candidate) => <option key={candidate.id} value={candidate.id}>{candidate.name} - {candidate.phone}</option>)}</select></label><button className="secondary-action" type="button" disabled={!guardianIdToLink} onClick={() => void linkExistingGuardian()}>Link guardian</button></div>}</section>
      <section className="panel documents"><div className="panel-heading"><div><p className="eyebrow">Linked documents</p><h2>Clinical forms</h2></div><FileText size={22} /></div><div className="document-actions">{documentOptions.map((document) => <button key={document.id} type="button" onClick={() => void prepareDocument(document.id)}><FileText size={18} />{document.label}</button>)}</div></section>
    </>
  )
}