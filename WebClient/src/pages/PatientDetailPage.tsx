import { useEffect, useState } from 'react'
import { ArrowLeft, ClipboardPlus, FileText, Pencil, Save } from 'lucide-react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { type ConsultationHistoryItem, type Guardian, type Patient, getJson } from '../api'

const documentOptions = [
  { id: 'health-certificate', label: 'Health certificate' },
  { id: 'surgical-consent', label: 'Surgical consent' },
  { id: 'boarding-contract', label: 'Boarding contract' },
]

export function PatientDetailPage() {
  const { patientId } = useParams()
  const navigate = useNavigate()
  const [patient, setPatient] = useState<Patient | null>(null)
  const [guardian, setGuardian] = useState<Guardian | null>(null)
  const [consultations, setConsultations] = useState<ConsultationHistoryItem[]>([])
  const [editingPatient, setEditingPatient] = useState(false)
  const [editingGuardian, setEditingGuardian] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (!patientId) return
    void Promise.all([getJson<Patient>(`/api/patients/${patientId}`), getJson<ConsultationHistoryItem[]>(`/api/patients/${patientId}/consultations`)]).then(([patientRecord, history]) => {
      setPatient(patientRecord)
      setConsultations(history)
      return getJson<Guardian>(`/api/guardians/${patientRecord.guardianId}`)
    }).then(setGuardian).catch(() => {
      setError('This patient record could not be found.')
    })
  }, [patientId])

  async function startConsultation() {
    if (!patient) return
    try {
      const response = await fetch(`/api/patients/${patient.id}/consultations`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ clinicianName: 'MVZ. Alondra Licona' }),
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
      const document = await getJson<{ title: string; fields: Record<string, string> }>(`/api/patients/${patient.id}/documents/${documentType}`)
      const detail = Object.entries(document.fields).map(([key, value]) => `${key}: ${value}`).join('\n')
      const preview = window.open('', '_blank', 'noopener,noreferrer')
      preview?.document.write(`<pre>${document.title}\n\n${detail}\n\nPrepared by MVZ. Alondra Licona</pre>`)
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
      const response = await fetch(`/api/patients/${patient.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: form.get('name'), species: form.get('species'), breed: form.get('breed'), sex: form.get('sex'), weight: form.get('weight'), color: form.get('color'), allergies: form.get('allergies'), distinguishingFeatures: form.get('distinguishingFeatures'), dateOfBirth: form.get('dateOfBirth') || null, photoUrl: form.get('photoUrl'), isActive: form.get('isActive') === 'on' }) })
      if (!response.ok) throw new Error()
      setPatient(await response.json() as Patient)
      setEditingPatient(false)
      setError('')
    } catch { setError('Patient information could not be updated.') }
  }

  async function saveGuardian(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!guardian) return
    const form = new FormData(event.currentTarget)
    try {
      const response = await fetch(`/api/guardians/${guardian.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name: form.get('name'), phone: form.get('phone'), alternatePhone: form.get('alternatePhone'), address: form.get('address'), identityType: form.get('identityType'), identityNumber: form.get('identityNumber'), identityDocumentUrl: form.get('identityDocumentUrl') }) })
      if (!response.ok) throw new Error()
      setGuardian(await response.json() as Guardian)
      setPatient((current) => current ? { ...current, guardianName: String(form.get('name')), guardianPhone: String(form.get('phone')) } : current)
      setEditingGuardian(false)
      setError('')
    } catch { setError('Guardian information could not be updated.') }
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
        <section className="panel"><div className="panel-heading"><div><p className="eyebrow">Guardian</p><h2>{patient.guardianName}</h2></div><button className="icon-button" type="button" aria-label="Edit guardian" onClick={() => setEditingGuardian((value) => !value)}><Pencil size={17} /></button></div>{editingGuardian && guardian ? <form className="record-edit-form" onSubmit={saveGuardian}><label>Name<input name="name" defaultValue={guardian.name} required /></label><label>Phone<input name="phone" defaultValue={guardian.phone} required /></label><label>Alternate phone<input name="alternatePhone" defaultValue={guardian.alternatePhone} /></label><label>Address<input name="address" defaultValue={guardian.address} /></label><label>Identity type<input name="identityType" defaultValue={guardian.identityType} /></label><label>Identity number<input name="identityNumber" defaultValue={guardian.identityNumber} /></label><label>Identity document URL<input name="identityDocumentUrl" type="url" defaultValue={guardian.identityDocumentUrl} /></label><button className="secondary-action" type="submit"><Save size={16} /> Save guardian</button></form> : <dl className="record-details"><div><dt>Phone</dt><dd>{patient.guardianPhone}</dd></div>{guardian && <><div><dt>Alternate phone</dt><dd>{guardian.alternatePhone || 'Not recorded'}</dd></div><div><dt>Address</dt><dd>{guardian.address || 'Not recorded'}</dd></div><div><dt>Identity</dt><dd>{[guardian.identityType, guardian.identityNumber].filter(Boolean).join(' · ') || 'Not recorded'}</dd></div></>}</dl>}</section>
      </section>
      <section className="panel patient-consultation-history"><div className="panel-heading"><div><p className="eyebrow">Clinical timeline</p><h2>Recent consultations</h2></div><Link className="secondary-action" to={`/consultations?query=${encodeURIComponent(patient.name)}`}>View all</Link></div><div className="history-list">{consultations.slice(0, 4).map((consultation) => <Link to={`/consultations/${consultation.id}`} key={consultation.id}><span>{new Date(consultation.startedAt).toLocaleDateString()}</span><strong>{consultation.diagnosis || 'Clinical notes in progress'}</strong><small>{consultation.clinicianName} · {consultation.status === 'Completed' ? 'Completed' : 'In progress'}</small></Link>)}</div>{consultations.length === 0 && <p className="empty-state">No consultations have been recorded for this patient.</p>}</section>
      <section className="panel documents"><div className="panel-heading"><div><p className="eyebrow">Linked documents</p><h2>Clinical forms</h2></div><FileText size={22} /></div><div className="document-actions">{documentOptions.map((document) => <button key={document.id} type="button" onClick={() => void prepareDocument(document.id)}><FileText size={18} />{document.label}</button>)}</div></section>
    </>
  )
}