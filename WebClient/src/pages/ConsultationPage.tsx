import { useEffect, useState } from 'react'
import { ArrowLeft, FileOutput, Plus, Save, Stethoscope, Trash2 } from 'lucide-react'
import { Link, useBlocker, useParams } from 'react-router-dom'
import { useAuthentication } from '../auth'
import { apiFetch, ensureSuccess, type Consultation, type PrescriptionItem, type Patient, getJson } from '../api'
import { ConsultationCharges } from '../components/ConsultationCharges'
import { registerWorkspaceGuard } from '../navigationGuard'

type ClinicalDraft = {
  chiefComplaint: string
  clinicalNotes: string
  diagnosis: string
  instructions: string
  prescriptionItems: PrescriptionItem[]
}
function toDraft(record: Consultation): ClinicalDraft {
  return { chiefComplaint: record.chiefComplaint, clinicalNotes: record.clinicalNotes, diagnosis: record.diagnosis,
    instructions: record.instructions, prescriptionItems: record.prescriptionItems }
}

export function ConsultationPage() {
  const { consultationId } = useParams()
  const { currentUser } = useAuthentication()
  const [consultation, setConsultation] = useState<Consultation | null>(null)
  const [patient, setPatient] = useState<Patient | null>(null)
  const [draft, setDraft] = useState<ClinicalDraft | null>(null)
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)
  const [reason, setReason] = useState('')
  const [amendment, setAmendment] = useState('')
  const clinicalAccess = ['ClinicAdmin', 'Veterinarian', 'SuperAdmin'].includes(currentUser?.role ?? '')
  const editable = clinicalAccess && consultation?.status === 'InProgress'
  const dirty = editable && !!consultation && !!draft && JSON.stringify(draft) !== JSON.stringify(toDraft(consultation))
  const blocker = useBlocker(dirty || pending)

  useEffect(() => registerWorkspaceGuard(() => !pending && (!dirty ||
    window.confirm('Discard unsaved clinical changes and leave this clinic workspace?'))), [dirty, pending])

  useEffect(() => {
    const warn = (event: BeforeUnloadEvent) => { if (dirty || pending) { event.preventDefault(); event.returnValue = '' } }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [dirty, pending])

  useEffect(() => {
    let active = true
    setConsultation(null)
    setPatient(null)
    setDraft(null)
    setError('')
    if (consultationId) void getJson<Consultation>(`/api/consultations/${consultationId}`).then(async (data) => {
      const patientRecord = await getJson<Patient>(`/api/patients/${data.patientId}`)
      if (active) { setConsultation(data); setDraft(toDraft(data)); setPatient(patientRecord) }
    }).catch((cause: unknown) => { if (active) setError(cause instanceof Error ? cause.message : 'Consultation could not be loaded.') })
    return () => { active = false }
  }, [consultationId])

  async function save(status: 'InProgress' | 'Completed') {
    if (!consultation || !draft || pending || !editable) return
    if (status === 'Completed' && !window.confirm('Finalize the clinical record? Future corrections will be amendments. Payment can remain pending.')) return
    setPending(true); setError(''); setNotice('')
    try {
      const response = await apiFetch(`/api/consultations/${consultation.id}`, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ...draft, status, expectedVersion: consultation.version }),
      })
      await ensureSuccess(response)
      const updated = await response.json() as Consultation
      setConsultation(updated); setDraft(toDraft(updated))
      setNotice(status === 'Completed' ? 'Clinical record finalized. Charges and payment are managed separately.' : 'All clinical changes saved.')
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'The record could not be saved.') }
    finally { setPending(false) }
  }

  async function transition(action: 'cancel' | 'amendments') {
    if (!consultation || pending) return
    if (action === 'cancel' && !window.confirm(`${dirty ? 'Unsaved clinical changes will be discarded. ' : ''}Cancel this clinical visit? Financial charges are retained and must be reviewed separately.`)) return
    setPending(true); setError('')
    try {
      const response = await apiFetch(`/api/consultations/${consultation.id}/${action}`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ reason, note: amendment, expectedVersion: consultation.version }),
      })
      await ensureSuccess(response)
      const updated = await getJson<Consultation>(`/api/consultations/${consultation.id}`)
      setConsultation(updated); setDraft(toDraft(updated)); setReason(''); setAmendment('')
      setNotice(action === 'cancel' ? 'Visit cancelled. Clinical history retained.' : 'Attributed amendment recorded.')
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'The action could not be completed.') }
    finally { setPending(false) }
  }

  function changeItem(index: number, field: keyof PrescriptionItem, value: string) {
    setDraft(current => current && ({ ...current, prescriptionItems: current.prescriptionItems.map((item, position) =>
      position === index ? { ...item, [field]: value } : item) }))
  }

  if (!consultation || !draft) return <>{error ? <div className="notice error" role="alert">{error}</div> : <p>Loading consultation...</p>}</>
  return <>
    <Link className="back-link" to={`/patients/${consultation.patientId}`}><ArrowLeft size={16} /> Back to {consultation.patientName}</Link>
    <header className="page-header"><div><p className="eyebrow">{consultation.status}</p><h1>{consultation.patientName}</h1>
      <p>Guardian: {consultation.guardianName} · Clinician: {consultation.clinicianName}</p></div><Stethoscope size={26} /></header>
    {patient && <section className="panel"><strong>Allergies: {patient.allergies.join(', ') || 'Unknown / not assessed'}</strong>
      <p>Latest weight: {patient.weight || 'Not recorded'} · {patient.species} · Last visit: {patient.lastVisit || 'Not recorded'}</p></section>}
    {notice && <div className="notice" role="status">{notice}</div>}
    {error && <div className="notice error" role="alert">{error} <button type="button" onClick={() => window.location.reload()}>Reload current record</button></div>}
    {blocker.state === 'blocked' && <section className="notice error" role="alertdialog" aria-label="Unsaved clinical changes">
      {pending ? 'Wait for the clinical save to finish before leaving.' : 'You have unsaved clinical changes.'} <button type="button" onClick={() => blocker.reset()}>Keep editing</button>
      <button type="button" disabled={pending} onClick={() => blocker.proceed()}>Discard and leave</button></section>}
    <div className="clinical-workspace">
      <form className="panel consultation-form" onSubmit={event => { event.preventDefault(); void save('InProgress') }}>
        <p role="status">{pending ? 'Saving...' : dirty ? 'Unsaved changes' : 'Clinical record saved'}</p>
        <fieldset disabled={!editable || pending}>
          {(['chiefComplaint', 'clinicalNotes', 'diagnosis', 'instructions'] as const).map(field =>
            <label key={field}>{({ chiefComplaint: 'Chief complaint', clinicalNotes: 'Clinical notes', diagnosis: 'Diagnosis', instructions: 'Home care and follow-up' })[field]}
              <textarea rows={field === 'clinicalNotes' ? 8 : 3} value={draft[field]}
                onChange={event => setDraft({ ...draft, [field]: event.target.value })} /></label>)}
          <section className="prescription-editor"><div className="panel-heading"><h2>Prescription directions</h2>
            <button type="button" className="secondary-action" onClick={() => setDraft({ ...draft,
              prescriptionItems: [...draft.prescriptionItems, { medicationName: '', presentation: 'Tablet', concentration: '', dosageDirections: '', sortOrder: draft.prescriptionItems.length }] })}>
              <Plus size={16} /> Medication</button></div>
            <p>Prescribing does not sell or dispense medication. Add supplied items to Charges separately.</p>
            {draft.prescriptionItems.map((item, index) => <div className="prescription-item" key={item.id ?? index}>
              <input aria-label="Medication" value={item.medicationName} onChange={event => changeItem(index, 'medicationName', event.target.value)} required />
              <input aria-label="Presentation" value={item.presentation} onChange={event => changeItem(index, 'presentation', event.target.value)} required />
              <input aria-label="Concentration" value={item.concentration} onChange={event => changeItem(index, 'concentration', event.target.value)} />
              <input aria-label="Dosage directions" value={item.dosageDirections} onChange={event => changeItem(index, 'dosageDirections', event.target.value)} required />
              <button className="icon-button" type="button" aria-label="Remove medication" onClick={() => setDraft({ ...draft, prescriptionItems: draft.prescriptionItems.filter((_, position) => position !== index).map((value, position) => ({ ...value, sortOrder: position })) })}><Trash2 size={16} /></button>
            </div>)}
          </section>
          <div className="form-actions"><button className="secondary-action" type="submit"><Save size={16} /> Save progress</button>
            <button className="primary-action" type="button" onClick={() => void save('Completed')}>Complete clinical care</button></div>
        </fieldset>
        {consultation.status === 'Completed' && <Link className="secondary-action" to={`/consultations/${consultation.id}/prescription`}><FileOutput size={16} /> Print prescription</Link>}
      </form>
      <ConsultationCharges consultationId={consultation.id} patientId={consultation.patientId} />
    </div>
    {consultation.cancellationReason && <div className="notice">Cancellation: {consultation.cancellationReason}</div>}
    {clinicalAccess && consultation.status !== 'Cancelled' && <section className="panel clinical-entry-form">
      <h2>{consultation.status === 'Completed' ? 'Add a clinical amendment' : 'Cancel visit'}</h2>
      <label>Reason<input value={reason} maxLength={1000} onChange={event => setReason(event.target.value)} /></label>
      {consultation.status === 'Completed' && <label>Amendment note<textarea value={amendment} maxLength={10000} onChange={event => setAmendment(event.target.value)} /></label>}
      <button className="secondary-action" type="button" disabled={pending || !reason.trim() || (consultation.status === 'Completed' && !amendment.trim())}
        onClick={() => void transition(consultation.status === 'Completed' ? 'amendments' : 'cancel')}>
        {consultation.status === 'Completed' ? 'Record amendment' : 'Cancel and retain record'}</button>
    </section>}
    {!!consultation.amendments.length && <section className="panel"><h2>Amendment history</h2>{consultation.amendments.map(item =>
      <article key={item.id}><strong>{item.authorName} · {new Date(item.createdAt).toLocaleString()}</strong><p>{item.reason}</p><p>{item.note}</p></article>)}</section>}
  </>
}
