import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { type ConsultationListItem, getJson } from '../api'
import { useAuthentication } from '../auth'

export function ConsultationsPage() {
  const { currentUser } = useAuthentication()
  const [consultations, setConsultations] = useState<ConsultationListItem[]>([])
  const [query, setQuery] = useState('')
  const [status, setStatus] = useState('')
  const [error, setError] = useState('')
  useEffect(() => {
    let active = true
    const timer = window.setTimeout(() => {
      const parameters = new URLSearchParams({ query, status })
      void getJson<ConsultationListItem[]>(`/api/consultations?${parameters}`).then(items => {
        if (active) { setConsultations(items); setError('') }
      }).catch((cause: unknown) => { if (active) setError(cause instanceof Error ? cause.message : 'Consultations could not be loaded.') })
    }, 180)
    return () => { active = false; window.clearTimeout(timer) }
  }, [query, status])

  return <>
    <header className="page-header"><div><p className="eyebrow">Clinical records</p><h1>Consultations</h1></div>
      {['ClinicAdmin', 'Veterinarian', 'SuperAdmin'].includes(currentUser?.role ?? '') && <Link className="primary-action" to="/patients">Start consultation</Link>}
    </header>
    {error && <div className="notice error" role="alert">{error}</div>}
    <section className="panel consultation-directory">
      <div className="consultation-filters"><label>Search<input value={query} onChange={event => setQuery(event.target.value)} placeholder="Patient, guardian, clinician, or diagnosis" /></label>
        <label>Status<select value={status} onChange={event => setStatus(event.target.value)}>
          <option value="">All consultations</option><option value="InProgress">In progress</option><option>Completed</option><option>Cancelled</option>
        </select></label></div>
      {consultations.map(item => <Link className="consultation-row" to={`/consultations/${item.id}`} key={item.id}>
        <span>{new Date(item.startedAt).toLocaleDateString()}</span><span><strong>{item.patientName}</strong><small>{item.guardianName} · {item.clinicianName}</small></span>
        <span className={`status-badge ${item.status === 'Completed' ? 'completed' : ''}`}>{item.status === 'InProgress' ? 'In progress' : item.status}</span>
        <span>{item.diagnosis || 'No diagnosis recorded'}</span></Link>)}
      {!error && !consultations.length && <p>No consultations match these filters.</p>}
    </section>
  </>
}
