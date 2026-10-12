import { useTranslation } from '../useTranslation'
import { useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { apiFetch, ensureSuccess, getJson, type PlatformClinic } from '../api'
import { useAuthentication } from '../auth'

export function PlatformAdminPage() {
  const { t } = useTranslation()

  const { selectClinic, refreshCurrentUser } = useAuthentication()
  const navigate = useNavigate()
  const [clinics, setClinics] = useState<PlatformClinic[]>([])
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    void getJson<PlatformClinic[]>('/api/platform/clinics').then(setClinics)
      .catch(error => setError(error instanceof Error ? error.message : 'Clinics could not be loaded.'))
      .finally(() => setLoading(false))
  }, [])

  async function updateAccess(event: React.FormEvent<HTMLFormElement>, clinic: PlatformClinic) {
    event.preventDefault()
    const reason = String(new FormData(event.currentTarget).get('reason') ?? '')
    setBusy(true)
    setError('')
    try {
      await ensureSuccess(await apiFetch(`/api/platform/clinics/${clinic.id}/access`, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ isSuspended: !clinic.isSuspended, reason }),
      }))
      await refreshCurrentUser()
    } catch (error) {
      setError(error instanceof Error ? error.message : 'Clinic access could not be updated.')
    } finally { setBusy(false) }
  }

  async function openClinic(clinic: PlatformClinic) {
    await selectClinic(clinic.id)
    navigate('/clinic-admin')
  }

  return <>
    <header className="page-header"><div><p className="eyebrow">{t("Platform owner")}</p><h1>{t("Platform administration")}</h1></div><Link className="primary-action" to="/onboarding">{t("Register clinic")}</Link></header>
    <p>{t("Open any clinic workspace for support. Suspension blocks all clinic members immediately on their next API request; your support access remains available. Billing enforcement is manual, not connected to a payment provider.")}</p>
    {error && <div className="notice error" role="alert">{t(error)}</div>}
    {loading ? <p className="loading-state">{t("Loading clinics...")}</p> : clinics.length === 0 && <p>{t("No clinics registered yet.")}</p>}
    <div className="admin-list">{clinics.map(clinic => <article className="panel" key={clinic.id}>
      <h2>{clinic.name}</h2><p>{clinic.userCount} {t("active members ·")} {clinic.isSuspended ? t("Suspended") : t("Active")}</p>
      {clinic.isSuspended && <p>{t("Reason:")} {clinic.suspensionReason}</p>}
      <button type="button" className="secondary-action" disabled={busy} onClick={() => void openClinic(clinic)}>{t("Open support workspace / manage staff")}</button>
      <form className="admin-access-form" onSubmit={event => void updateAccess(event, clinic)}>
        {!clinic.isSuspended && <label>{t("Suspension reason")}<input name="reason" maxLength={500} required placeholder={t("Subscription unpaid")} /></label>}
        <button type="submit" className="secondary-action" disabled={busy}>{clinic.isSuspended ? t("Restore clinic access") : t("Suspend clinic access")}</button>
      </form>
    </article>)}</div>
  </>
}
