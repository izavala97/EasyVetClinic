import { useEffect, useState } from 'react'
import { Search, Stethoscope, UserRoundPlus } from 'lucide-react'
import { Link } from 'react-router-dom'
import { type Patient, getJson } from '../api'

export function PatientsPage() {
  const [patients, setPatients] = useState<Patient[]>([])
  const [query, setQuery] = useState('')
  const [error, setError] = useState('')

  useEffect(() => {
    const timer = window.setTimeout(() => {
      void getJson<Patient[]>(`/api/patients?query=${encodeURIComponent(query)}`)
        .then((patientData) => {
          setPatients(patientData)
          setError('')
        })
        .catch(() => setError('Patients could not be loaded.'))
    }, 180)

    return () => window.clearTimeout(timer)
  }, [query])

  return (
    <>
      <header className="page-header"><div><p className="eyebrow">Clinical records</p><h1>Patients</h1></div><div className="header-actions"><Link className="secondary-action" to="/guardians/new"><UserRoundPlus size={17} /> Register guardian</Link><Link className="primary-action" to="/patients/new"><Stethoscope size={18} /> Register patient</Link></div></header>
      {error && <div className="notice error" role="alert">{error}</div>}
      <section className="panel patient-directory">
        <label className="search-field"><Search size={18} /><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Phone, guardian, or patient" /></label>
        <div className="patient-list directory-list">
          {patients.map((patient) => (
            <Link className="patient-row" to={`/patients/${patient.id}`} key={patient.id}>
              <span className="pet-avatar">{patient.name[0]}</span>
              <span><strong>{patient.name}</strong><small>{patient.species} · {patient.guardianName}</small></span>
            </Link>
          ))}
        </div>
        {!error && patients.length === 0 && <p className="empty-state">No patients match this search.</p>}
      </section>
    </>
  )
}