import { useEffect, useState } from 'react'
import { ArrowLeft, Save, Stethoscope } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { type Consultation, getJson } from '../api'

export function ConsultationPage() {
  const { consultationId } = useParams()
  const [consultation, setConsultation] = useState<Consultation | null>(null)
  const [chiefComplaint, setChiefComplaint] = useState('')
  const [clinicalNotes, setClinicalNotes] = useState('')
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')

  useEffect(() => {
    if (!consultationId) return
    void getJson<Consultation>(`/api/consultations/${consultationId}`).then((data) => {
      setConsultation(data)
      setChiefComplaint(data.chiefComplaint)
      setClinicalNotes(data.clinicalNotes)
    }).catch(() => setError('This consultation could not be found.'))
  }, [consultationId])

  async function saveConsultation(status: Consultation['status']) {
    if (!consultation) return
    setError('')
    try {
      const response = await fetch(`/api/consultations/${consultation.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ chiefComplaint, clinicalNotes, status }) })
      if (!response.ok) throw new Error()
      const updated = await response.json() as Consultation
      setConsultation(updated)
      setNotice(status === 'Completed' ? 'Consultation completed.' : 'Consultation progress saved.')
    } catch {
      setError('The consultation could not be saved.')
    }
  }

  if (error && !consultation) return <><Link className="back-link" to="/patients"><ArrowLeft size={16} /> Back to patients</Link><div className="notice error" role="alert">{error}</div></>
  if (!consultation) return <p className="loading-state">Loading consultation...</p>

  return (
    <>
      <Link className="back-link" to={`/patients/${consultation.patientId}`}><ArrowLeft size={16} /> Back to {consultation.patientName}</Link>
      <header className="page-header"><div><p className="eyebrow">{consultation.status === 'Completed' ? 'Completed consultation' : 'Active consultation'}</p><h1>{consultation.patientName}</h1><p className="consultation-subtitle">Guardian: {consultation.guardianName} · {consultation.clinicianName}</p></div><Stethoscope size={26} /></header>
      {notice && <div className="notice" role="status">{notice}</div>}{error && <div className="notice error" role="alert">{error}</div>}
      <form className="panel consultation-form" onSubmit={(event) => { event.preventDefault(); void saveConsultation('InProgress') }}><label>Chief complaint<input value={chiefComplaint} onChange={(event) => setChiefComplaint(event.target.value)} placeholder="Reason for today’s visit" /></label><label>Clinical notes<textarea value={clinicalNotes} onChange={(event) => setClinicalNotes(event.target.value)} placeholder="History, examination findings, assessment, and plan" rows={12} /></label><div className="form-actions"><button className="secondary-action" type="submit"><Save size={17} /> Save progress</button><button className="primary-action" type="button" onClick={() => void saveConsultation('Completed')}><Stethoscope size={18} /> Complete consultation</button></div></form>
    </>
  )
}