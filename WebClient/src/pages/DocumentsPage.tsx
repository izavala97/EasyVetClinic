import { useEffect, useState } from 'react'
import { FileOutput, FileText } from 'lucide-react'
import { type Patient, getJson } from '../api'

const documentTypes = [
  { id: 'health-certificate', label: 'Health certificate', description: 'Patient health and identification summary.' },
  { id: 'surgical-consent', label: 'Surgical consent', description: 'Consent draft for anesthesia and surgery.' },
  { id: 'boarding-contract', label: 'Boarding contract', description: 'Care agreement for a boarding stay.' },
]

export function DocumentsPage() {
  const [patients, setPatients] = useState<Patient[]>([])
  const [patientId, setPatientId] = useState('')
  const [error, setError] = useState('')

  useEffect(() => {
    void getJson<Patient[]>('/api/patients')
      .then((data) => {
        setPatients(data)
        setPatientId(data[0]?.id ?? '')
      })
      .catch(() => setError('Patients could not be loaded.'))
  }, [])

  async function printDocument(documentType: string) {
    if (!patientId) return
    setError('')
    try {
      const document = await getJson<{ title: string; fields: Record<string, string>; preparedBy: string }>(`/api/patients/${patientId}/documents/${documentType}`)
      const preview = window.open('', '_blank', 'noopener,noreferrer')
      if (!preview) throw new Error()
      const content = [document.title, '', ...Object.entries(document.fields).map(([key, value]) => `${key}: ${value}`), '', `Prepared by ${document.preparedBy}`].join('\n')
      preview.document.title = document.title
      preview.document.body.innerHTML = '<pre></pre>'
      preview.document.querySelector('pre')!.textContent = content
      preview.document.close()
      preview.print()
    } catch {
      setError('The document draft could not be prepared. Allow pop-ups and try again.')
    }
  }

  return (
    <>
      <header className="page-header"><div><p className="eyebrow">Clinical administration</p><h1>Documents</h1></div><FileText size={26} /></header>
      {error && <div className="notice error" role="alert">{error}</div>}
      <section className="panel document-picker"><div><p className="eyebrow">Patient context</p><h2>Prepare a document</h2></div><label>Patient<select value={patientId} onChange={(event) => setPatientId(event.target.value)}>{patients.map((patient) => <option key={patient.id} value={patient.id}>{patient.name} - {patient.guardianName}</option>)}</select></label></section>
      <section className="document-library">
        {documentTypes.map((document) => <article className="panel document-card" key={document.id}><FileText size={24} /><div><h2>{document.label}</h2><p>{document.description}</p></div><button className="secondary-action" type="button" disabled={!patientId} onClick={() => void printDocument(document.id)}><FileOutput size={17} /> Print draft</button></article>)}
      </section>
    </>
  )
}