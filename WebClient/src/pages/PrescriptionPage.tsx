import i18n from '../i18n'
import { useTranslation } from '../useTranslation'
import { useEffect, useState } from 'react'
import { ArrowLeft, Printer } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { type ClinicProfile, type Consultation, getJson } from '../api'

export function PrescriptionPage() {
  const { t } = useTranslation()

  const { consultationId } = useParams()
  const [consultation, setConsultation] = useState<Consultation | null>(null)
  const [clinic, setClinic] = useState<ClinicProfile | null>(null)
  const [error, setError] = useState('')

  useEffect(() => {
    if (consultationId) void Promise.all([getJson<Consultation>(`/api/consultations/${consultationId}`), getJson<ClinicProfile>('/api/clinic')]).then(([consultationRecord, clinicProfile]) => {
      setConsultation(consultationRecord)
      setClinic(clinicProfile)
    }).catch((cause: unknown) => setError(cause instanceof Error ? cause.message : 'Prescription could not be loaded.'))
  }, [consultationId])

  if (error) return <div className="notice error" role="alert">{t(error)}</div>
  if (!consultation || !clinic) return <p className="loading-state">{t("Loading prescription...")}</p>
  if (consultation.status !== 'Completed') return <div className="notice error">{t("Finalize clinical care before printing a prescription.")}</div>
  const credentials = [consultation.clinicianName, consultation.clinicianTitles, consultation.clinicianLicenseNumber && t('License {{number}}', { number: consultation.clinicianLicenseNumber })].filter(Boolean).join(' · ')
  return <article className="prescription-print"><div className="print-toolbar"><Link className="back-link" to={`/consultations/${consultation.id}`}><ArrowLeft size={16} />{t("Back to consultation")}</Link><button className="primary-action" type="button" onClick={() => window.print()}><Printer size={18} />{t("Print prescription")}</button></div><header><p className="eyebrow">{clinic.name}</p><h1>{t("Veterinary prescription")}</h1><p>{credentials}</p>{clinic.address && <p className="clinic-address">{clinic.address}</p>}</header><section><div><strong>{t("Patient")}</strong><span>{consultation.patientName}</span></div><div><strong>{t("Guardian")}</strong><span>{consultation.guardianName}</span></div><div><strong>{t("Date")}</strong><span>{new Date(consultation.startedAt).toLocaleDateString(i18n.language)}</span></div></section><h2>{t("Diagnosis")}</h2><p>{consultation.diagnosis || t("Not recorded")}</p><section className="medication-directions"><h2>{t("Medication directions")}</h2><table><thead><tr><th>{t("Medication")}</th><th>{t("Presentation")}</th><th>{t("Concentration")}</th><th>{t("Directions")}</th></tr></thead><tbody>{consultation.prescriptionItems.map((item) => <tr key={item.id ?? item.sortOrder}><td>{item.medicationName}</td><td>{item.presentation}</td><td>{item.concentration}</td><td>{item.dosageDirections}</td></tr>)}</tbody></table></section><h2>{t("Instructions")}</h2><p>{consultation.instructions || t("No additional instructions.")}</p>{consultation.prescriptionLastUpdatedAt && <p className="prescription-audit">{t("Prescription last updated:")} {new Date(consultation.prescriptionLastUpdatedAt).toLocaleString(i18n.language)}</p>}</article>
}