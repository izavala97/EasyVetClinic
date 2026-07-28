import { useEffect, useState } from 'react'
import { ArrowLeft, Printer } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { type ClinicProfile, type Consultation, getJson } from '../api'

export function PrescriptionPage() {
  const { consultationId } = useParams()
  const [consultation, setConsultation] = useState<Consultation | null>(null)
  const [clinic, setClinic] = useState<ClinicProfile | null>(null)

  useEffect(() => {
    if (consultationId) void Promise.all([getJson<Consultation>(`/api/consultations/${consultationId}`), getJson<ClinicProfile>('/api/clinic')]).then(([consultationRecord, clinicProfile]) => {
      setConsultation(consultationRecord)
      setClinic(clinicProfile)
    })
  }, [consultationId])

  if (!consultation || !clinic) return <p className="loading-state">Loading prescription...</p>
  const credentials = [clinic.veterinarianName, clinic.veterinarianTitles, clinic.veterinarianLicenseNumber && `License ${clinic.veterinarianLicenseNumber}`].filter(Boolean).join(' · ')
  return <article className="prescription-print"><div className="print-toolbar"><Link className="back-link" to={`/consultations/${consultation.id}`}><ArrowLeft size={16} /> Back to consultation</Link><button className="primary-action" type="button" onClick={() => window.print()}><Printer size={18} /> Print prescription</button></div><header><p className="eyebrow">{clinic.name}</p><h1>Veterinary prescription</h1><p>{credentials}</p>{clinic.address && <p className="clinic-address">{clinic.address}</p>}</header><section><div><strong>Patient</strong><span>{consultation.patientName}</span></div><div><strong>Guardian</strong><span>{consultation.guardianName}</span></div><div><strong>Date</strong><span>{new Date(consultation.startedAt).toLocaleDateString()}</span></div></section><h2>Diagnosis</h2><p>{consultation.diagnosis || 'Not recorded'}</p><section className="medication-directions"><h2>Medication directions</h2><table><thead><tr><th>Medication</th><th>Presentation</th><th>Concentration</th><th>Directions</th></tr></thead><tbody>{consultation.prescriptionItems.map((item) => <tr key={item.id ?? item.sortOrder}><td>{item.medicationName}</td><td>{item.presentation}</td><td>{item.concentration}</td><td>{item.dosageDirections}</td></tr>)}</tbody></table></section><h2>Instructions</h2><p>{consultation.instructions || 'No additional instructions.'}</p>{consultation.prescriptionLastUpdatedAt && <p className="prescription-audit">Prescription last updated: {new Date(consultation.prescriptionLastUpdatedAt).toLocaleString()}</p>}</article>
}