import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { apiFetch, ensureSuccess, type ClinicMembership } from '../api'
import { useAuthentication } from '../auth'

export function InvitationPage() {
  const { selectClinic, signOut } = useAuthentication()
  const navigate = useNavigate()
  const [token] = useState(() => sessionStorage.getItem('clinicInvitation'))
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  async function accept() {
    setBusy(true)
    setError('')
    try {
      const response = await apiFetch('/api/invitations/accept', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ token }),
      })
      await ensureSuccess(response)
      const clinic: ClinicMembership = await response.json()
      sessionStorage.removeItem('clinicInvitation')
      await selectClinic(clinic.clinicId)
      navigate('/')
    } catch (error) {
      setError(error instanceof Error ? error.message : 'Invitation could not be accepted.')
    } finally { setBusy(false) }
  }

  function cancel() {
    sessionStorage.removeItem('clinicInvitation')
    navigate('/')
  }

  return <section className="panel"><p className="eyebrow">Clinic invitation</p><h1>Join your clinic</h1><p>Sign in with the invited email address, then accept to join the clinic. Invitations expire after seven days and can only be accepted once.</p>
    {error && <div className="notice error" role="alert">{error}</div>}
    {!token && <div className="notice error">No invitation link found. Open the complete link provided by your administrator.</div>}
    <div className="form-actions"><button className="primary-action" disabled={!token || busy} onClick={() => void accept()}>Accept invitation</button><button className="secondary-action" disabled={busy} onClick={cancel}>Cancel</button><button className="secondary-action" disabled={busy} onClick={() => void signOut()}>Sign out to switch accounts</button></div>
  </section>
}
