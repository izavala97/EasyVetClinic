import i18n from '../i18n'
import { useTranslation } from '../useTranslation'
import { useEffect, useState } from 'react'
import { apiFetch, ensureSuccess, getJson, type ClinicUser, type CreatedInvitation, type Invitation } from '../api'
import { useAuthentication } from '../auth'

const roles = ['ClinicAdmin', 'Veterinarian', 'Staff']

export function ClinicAdminPage() {
  const { t } = useTranslation()

  const { refreshCurrentUser } = useAuthentication()
  const [users, setUsers] = useState<ClinicUser[]>([])
  const [invitations, setInvitations] = useState<Invitation[]>([])
  const [link, setLink] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)

  async function load() {
    const [members, pending] = await Promise.all([
      getJson<ClinicUser[]>('/api/clinic/users'), getJson<Invitation[]>('/api/clinic/invitations'),
    ])
    setUsers(members)
    setInvitations(pending)
  }

  useEffect(() => {
    void load().catch(error => setError(error instanceof Error ? error.message : 'Staff could not be loaded.'))
      .finally(() => setLoading(false))
  }, [])

  async function invite(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const values = new FormData(form)
    setBusy(true)
    setError('')
    setLink('')
    try {
      const response = await apiFetch('/api/clinic/invitations', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: values.get('email'), role: values.get('role') }),
      })
      await ensureSuccess(response)
      const invitation: CreatedInvitation = await response.json()
      setLink(`${window.location.origin}/invite#token=${invitation.token}`)
      form.reset()
      await load()
    } catch (error) {
      setError(error instanceof Error ? error.message : 'Invitation could not be created.')
    } finally { setBusy(false) }
  }

  async function updateUser(event: React.FormEvent<HTMLFormElement>, user: ClinicUser) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setBusy(true)
    setError('')
    try {
      await ensureSuccess(await apiFetch(`/api/clinic/users/${user.id}`, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ role: form.get('role'), isActive: form.get('isActive') === 'on' }),
      }))
      await refreshCurrentUser()
    } catch (error) {
      setError(error instanceof Error ? error.message : 'Staff access could not be updated.')
    } finally { setBusy(false) }
  }

  async function revoke(id: string) {
    setBusy(true)
    setError('')
    try {
      await ensureSuccess(await apiFetch(`/api/clinic/invitations/${id}`, { method: 'DELETE' }))
      await load()
    } catch (error) {
      setError(error instanceof Error ? error.message : 'Invitation could not be revoked.')
    } finally { setBusy(false) }
  }

  return <>
    <header className="page-header"><div><p className="eyebrow">{t("Clinic administrator")}</p><h1>{t("Staff and invitations")}</h1></div></header>
    {error && <div className="notice error" role="alert">{t(error)}</div>}
    <form className="panel clinic-profile-form" onSubmit={event => void invite(event)}>
      <h2>{t("Invite a team member")}</h2><p>{t("The recipient signs in with this email to accept a single-use link valid for seven days. Share the link yourself; no email is sent automatically.")}</p>
      <label>{t("Email")}<input name="email" type="email" maxLength={254} required /></label>
      <label>{t("Role")}<select name="role" defaultValue="Staff">{roles.map(role => <option value={role} key={role}>{t(role)}</option>)}</select></label>
      <button className="primary-action" disabled={busy || loading}>{t("Create invitation link")}</button>
    </form>
    {link && <div className="notice"><label>{t("Copy and share this link now (shown only once)")}<input className="invitation-link" aria-label={t("Invitation link")} readOnly value={link} onFocus={event => event.currentTarget.select()} /></label></div>}
    <section className="panel admin-section"><h2>{t("Team members")}</h2>
      {loading ? <p>{t("Loading staff...")}</p> : users.length === 0 && <p>{t("No members yet. Invite the first clinic administrator above.")}</p>}
      {users.map(user => <form className="admin-member" key={user.id} onSubmit={event => void updateUser(event, user)}>
        <strong>{user.displayName}</strong>
        <label>{t("Role")}<select name="role" defaultValue={user.role} disabled={user.role === 'SuperAdmin'}>{(user.role === 'SuperAdmin' ? ['SuperAdmin'] : roles).map(role => <option value={role} key={role}>{t(role)}</option>)}</select></label>
        <label><input name="isActive" type="checkbox" defaultChecked={user.isActive} disabled={user.role === 'SuperAdmin'} />{t("Active")}</label>
        <button className="secondary-action" disabled={busy || user.role === 'SuperAdmin'}>{t("Save access")}</button>
      </form>)}
    </section>
    <section className="panel admin-section"><h2>{t("Invitations")}</h2>
      {invitations.length === 0 && <p>{t("No invitations.")}</p>}
      {invitations.map(invitation => {
        const pending = !invitation.isUsed && !invitation.isRevoked && new Date(invitation.expiresAt) > new Date()
        return <div className="admin-member" key={invitation.id}><div><strong>{invitation.email}</strong><p>{t(invitation.role)} · {invitation.isUsed ? t("Accepted") : invitation.isRevoked ? t("Revoked") : pending ? t("Pending") : t("Expired")} {t("· Expires")} {new Date(invitation.expiresAt).toLocaleString(i18n.language)}</p></div>{pending && <button className="secondary-action" disabled={busy} onClick={() => void revoke(invitation.id)}>{t("Revoke")}</button>}</div>
      })}
    </section>
  </>
}
