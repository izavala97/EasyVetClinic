import { useTranslation } from '../useTranslation'
import { useEffect, useState } from 'react'
import { FileOutput, FileText, Search } from 'lucide-react'
import { type Patient, getJson } from '../api'
import { printDocument as printDraft, type PrintableDocument } from '../printing'

const documentTypes = [
  { id: 'health-certificate', label: 'Health certificate', description: 'Patient health and identification summary.' },
  { id: 'surgical-consent', label: 'Surgical consent', description: 'Consent draft for anesthesia and surgery.' },
  { id: 'boarding-contract', label: 'Boarding contract', description: 'Care agreement for a boarding stay.' },
]

export function DocumentsPage() {
  const { t } = useTranslation()

  const [patients, setPatients] = useState<Patient[]>([])
  const [patientId, setPatientId] = useState('')
  const [patientQuery, setPatientQuery] = useState('')
  const [isPatientListOpen, setIsPatientListOpen] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    void getJson<Patient[]>('/api/patients')
      .then((data) => {
        setPatients(data)
      })
      .catch(() => setError('Patients could not be loaded.'))
  }, [])

  const selectedPatient = patients.find((patient) => patient.id === patientId)
  const matchingPatients = patients.filter((patient) => {
    const query = patientQuery.trim().toLowerCase()
    return !query || [patient.name, patient.guardianName, patient.guardianPhone].some((value) => value.toLowerCase().includes(query))
  })

  async function printDocument(documentType: string) {
    if (!patientId) return
    setError('')
    try {
      await printDraft(() => getJson<PrintableDocument>(`/api/patients/${patientId}/documents/${documentType}`))
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'The document draft could not be prepared.') }
  }

  return (
    <>
      <header className="page-header"><div><p className="eyebrow">{t("Clinical administration")}</p><h1>{t("Documents")}</h1></div><FileText size={26} /></header>
      {error && <div className="notice error" role="alert">{t(error)}</div>}
      <section className="panel document-picker"><div><p className="eyebrow">{t("Patient context")}</p><h2>{t("Prepare a document")}</h2></div><label>{t("Patient")}<div className="patient-combobox"><div className="search-field"><Search size={16} /><input value={patientQuery} onChange={(event) => { setPatientQuery(event.target.value); setIsPatientListOpen(true) }} onFocus={() => setIsPatientListOpen(true)} placeholder={selectedPatient ? `${selectedPatient.name} - ${selectedPatient.guardianName}` : t("Search pet, guardian, or phone")} role="combobox" aria-expanded={isPatientListOpen} aria-controls="documents-patient-options" aria-autocomplete="list" /></div>{isPatientListOpen && <div className="patient-options" id="documents-patient-options" role="listbox">{matchingPatients.map((patient) => <button type="button" role="option" aria-selected={patient.id === patientId} key={patient.id} onClick={() => { setPatientId(patient.id); setPatientQuery(''); setIsPatientListOpen(false) }}><strong>{patient.name}</strong><span>{patient.guardianName} · {patient.guardianPhone}</span></button>)}{!matchingPatients.length && <p className="empty-state">{t("No patients match this search.")}</p>}</div>}</div></label></section>
      <section className="document-library">
        {documentTypes.map((document) => <article className="panel document-card" key={document.id}><FileText size={24} /><div><h2>{t(document.label)}</h2><p>{t(document.description)}</p></div><button className="secondary-action" type="button" disabled={!patientId} onClick={() => void printDocument(document.id)}><FileOutput size={17} />{t("Print draft")}</button></article>)}
      </section>
    </>
  )
}