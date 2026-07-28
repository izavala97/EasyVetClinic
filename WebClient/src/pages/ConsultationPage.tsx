import { useEffect, useState } from 'react'
import { ArrowLeft, FileOutput, Plus, Save, Stethoscope, Trash2 } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { apiFetch, type Consultation, type PrescriptionItem, getJson } from '../api'

export function ConsultationPage() {
  const { consultationId } = useParams()
  const [consultation, setConsultation] = useState<Consultation | null>(null)
  const [chiefComplaint, setChiefComplaint] = useState('')
  const [clinicalNotes, setClinicalNotes] = useState('')
  const [diagnosis, setDiagnosis] = useState('')
  const [instructions, setInstructions] = useState('')
  const [prescriptionItems, setPrescriptionItems] = useState<PrescriptionItem[]>([])
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')

  useEffect(() => {
    if (!consultationId) return
    void getJson<Consultation>(`/api/consultations/${consultationId}`).then((data) => {
      setConsultation(data)
      setChiefComplaint(data.chiefComplaint)
      setClinicalNotes(data.clinicalNotes)
      setDiagnosis(data.diagnosis)
      setInstructions(data.instructions)
      setPrescriptionItems(data.prescriptionItems)
    }).catch(() => setError('This consultation could not be found.'))
  }, [consultationId])

  async function saveConsultation(status: Consultation['status']) {
    if (!consultation) return
    setError('')
    try {
      const response = await apiFetch(`/api/consultations/${consultation.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ chiefComplaint, clinicalNotes, diagnosis, instructions, status, prescriptionItems }) })
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

  function addPrescriptionItem() {
    setPrescriptionItems((items) => [...items, { medicationName: '', presentation: 'Tablet', concentration: '', dosageDirections: '', sortOrder: items.length }])
  }

  function updatePrescriptionItem(index: number, field: keyof PrescriptionItem, value: string) {
    setPrescriptionItems((items) => items.map((item, itemIndex) => itemIndex === index ? { ...item, [field]: value } : item))
  }

  function removePrescriptionItem(index: number) {
    setPrescriptionItems((items) => items.filter((_, itemIndex) => itemIndex !== index).map((item, itemIndex) => ({ ...item, sortOrder: itemIndex })))
  }

  return (
    <>
      <Link className="back-link" to={`/patients/${consultation.patientId}`}><ArrowLeft size={16} /> Back to {consultation.patientName}</Link>
      <header className="page-header"><div><p className="eyebrow">{consultation.status === 'Completed' ? 'Completed consultation' : 'Active consultation'}</p><h1>{consultation.patientName}</h1><p className="consultation-subtitle">Guardian: {consultation.guardianName} · {consultation.clinicianName}</p></div><Stethoscope size={26} /></header>
      {notice && <div className="notice" role="status">{notice}</div>}{error && <div className="notice error" role="alert">{error}</div>}
      <form className="panel consultation-form" onSubmit={(event) => { event.preventDefault(); void saveConsultation(consultation.status) }}><label>Chief complaint<input value={chiefComplaint} onChange={(event) => setChiefComplaint(event.target.value)} placeholder="Reason for today’s visit" /></label><label>Clinical notes<textarea value={clinicalNotes} onChange={(event) => setClinicalNotes(event.target.value)} placeholder="History, examination findings, assessment, and plan" rows={8} /></label><label>Diagnosis<textarea value={diagnosis} onChange={(event) => setDiagnosis(event.target.value)} placeholder="Clinical diagnosis" rows={3} /></label><label>Instructions<textarea value={instructions} onChange={(event) => setInstructions(event.target.value)} placeholder="Home care and follow-up instructions" rows={3} /></label><section className="prescription-editor"><div className="panel-heading"><div><p className="eyebrow">Prescription</p><h2>Medication directions</h2>{consultation.prescriptionLastUpdatedAt && <p className="prescription-updated">Last updated {new Date(consultation.prescriptionLastUpdatedAt).toLocaleString()}</p>}</div><button className="icon-button" type="button" onClick={addPrescriptionItem} aria-label="Add medication"><Plus size={18} /></button></div>{prescriptionItems.map((item, index) => <div className="prescription-item" key={item.id ?? index}><input value={item.medicationName} onChange={(event) => updatePrescriptionItem(index, 'medicationName', event.target.value)} placeholder="Medication" /><select value={item.presentation} onChange={(event) => updatePrescriptionItem(index, 'presentation', event.target.value)}><option>Tablet</option><option>Capsule</option><option>Suspension</option><option>Injection</option><option>Topical</option><option>Other</option></select><input value={item.concentration} onChange={(event) => updatePrescriptionItem(index, 'concentration', event.target.value)} placeholder="Concentration" /><input value={item.dosageDirections} onChange={(event) => updatePrescriptionItem(index, 'dosageDirections', event.target.value)} placeholder="Dosage and directions" /><button className="icon-button" type="button" onClick={() => removePrescriptionItem(index)} aria-label="Remove medication"><Trash2 size={16} /></button></div>)}{!prescriptionItems.length && <p className="empty-state">Add a medication when a prescription is needed.</p>}</section><div className="form-actions"><button className="secondary-action" type="submit"><Save size={17} /> {consultation.status === 'Completed' ? 'Save prescription changes' : 'Save progress'}</button>{consultation.status === 'Completed' && <Link className="secondary-action" to={`/consultations/${consultation.id}/prescription`}><FileOutput size={17} /> Print prescription</Link>}<button className="primary-action" type="button" disabled={consultation.status === 'Completed'} onClick={() => void saveConsultation('Completed')}><Stethoscope size={18} /> Complete consultation</button></div></form>
    </>
  )
}