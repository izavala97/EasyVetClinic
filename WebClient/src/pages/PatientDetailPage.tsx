import { useEffect, useState } from 'react'
import { ArrowLeft, ClipboardPlus, FileText, PawPrint, Stethoscope } from 'lucide-react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { type Patient, getJson } from '../api'

const documentOptions = [
  { id: 'health-certificate', label: 'Health certificate' },
  { id: 'surgical-consent', label: 'Surgical consent' },
  { id: 'boarding-contract', label: 'Boarding contract' },
]

export function PatientDetailPage() {
  const { patientId } = useParams()
  const navigate = useNavigate()
  const [patient, setPatient] = useState<Patient | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    if (!patientId) return
    void getJson<Patient>(`/api/patients/${patientId}`).then(setPatient).catch(() => {
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
        <section className="panel"><div className="panel-heading"><div><p className="eyebrow">Patient overview</p><h2>Record details</h2></div><Stethoscope size={22} /></div><dl className="record-details"><div><dt>Species</dt><dd>{patient.species}</dd></div><div><dt>Breed</dt><dd>{patient.breed}</dd></div><div><dt>Sex</dt><dd>{patient.sex}</dd></div><div><dt>Weight</dt><dd>{patient.weight}</dd></div><div><dt>Color</dt><dd>{patient.color}</dd></div><div><dt>Allergies</dt><dd>{patient.allergies.join(', ')}</dd></div><div><dt>Last visit</dt><dd>{patient.lastVisit || 'Not recorded'}</dd></div></dl></section>
        <section className="panel"><div className="panel-heading"><div><p className="eyebrow">Guardian</p><h2>{patient.guardianName}</h2></div><PawPrint size={22} /></div><dl className="record-details"><div><dt>Phone</dt><dd>{patient.guardianPhone}</dd></div></dl></section>
      </section>
      <section className="panel documents"><div className="panel-heading"><div><p className="eyebrow">Linked documents</p><h2>Clinical forms</h2></div><FileText size={22} /></div><div className="document-actions">{documentOptions.map((document) => <button key={document.id} type="button" onClick={() => void prepareDocument(document.id)}><FileText size={18} />{document.label}</button>)}</div></section>
    </>
  )
}