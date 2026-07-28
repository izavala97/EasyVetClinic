import { useEffect, useState } from 'react'
import { Search } from 'lucide-react'
import { Link } from 'react-router-dom'
import { type ConsultationListItem, getJson } from '../api'

export function ConsultationsPage() {
  const [consultations, setConsultations] = useState<ConsultationListItem[]>([])
  const [query, setQuery] = useState('')
  const [status, setStatus] = useState('')
  const [error, setError] = useState('')

  useEffect(() => {
    const timer = window.setTimeout(() => {
      const parameters = new URLSearchParams()
      if (query) parameters.set('query', query)
      if (status) parameters.set('status', status)
      void getJson<ConsultationListItem[]>(`/api/consultations?${parameters}`)
        .then((items) => { setConsultations(items); setError('') })
        .catch(() => setError('Consultations could not be loaded.'))
    }, 180)
    return () => window.clearTimeout(timer)
  }, [query, status])

  return <><header className="page-header"><div><p className="eyebrow">Clinical records</p><h1>Consultations</h1></div></header>{error && <div className="notice error" role="alert">{error}</div>}<section className="panel consultation-directory"><div className="consultation-filters"><label className="search-field"><Search size={18} /><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Patient, guardian, clinician, or diagnosis" /></label><label className="status-filter">Status<select value={status} onChange={(event) => setStatus(event.target.value)}><option value="">All consultations</option><option value="InProgress">In progress</option><option value="Completed">Completed</option></select></label></div><div className="consultation-list">{consultations.map((consultation) => <Link className="consultation-row" to={`/consultations/${consultation.id}`} key={consultation.id}><span className="consultation-date">{new Date(consultation.startedAt).toLocaleDateString()}</span><span><strong>{consultation.patientName}</strong><small>{consultation.guardianName} · {consultation.clinicianName}</small></span><span className={`status-badge ${consultation.status === 'Completed' ? 'completed' : ''}`}>{consultation.status === 'Completed' ? 'Completed' : 'In progress'}</span><span className="consultation-diagnosis">{consultation.diagnosis || 'No diagnosis recorded'}</span></Link>)}</div>{!error && consultations.length === 0 && <p className="empty-state">No consultations match these filters.</p>}</section></>
}