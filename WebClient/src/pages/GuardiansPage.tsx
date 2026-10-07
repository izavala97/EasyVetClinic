import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch, ensureSuccess, getJson, type Guardian } from '../api'

export function GuardiansPage() {
  const [contacts, setContacts] = useState<Guardian[]>([])
  const [selected, setSelected] = useState<Guardian | null>(null)
  const [query, setQuery] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [pending, setPending] = useState(false)
  useEffect(() => {
    let active = true
    const timer = window.setTimeout(() => {
      void getJson<Guardian[]>(`/api/guardians?take=100&query=${encodeURIComponent(query)}`).then(data => { if (active) setContacts(data) })
        .catch((cause: unknown) => { if (active) setError(cause instanceof Error ? cause.message : 'Contacts could not be loaded.') })
    }, 200)
    return () => { active = false; window.clearTimeout(timer) }
  }, [query])

  async function save(event: FormEvent) {
    event.preventDefault()
    if (!selected || pending) return
    setPending(true); setError(''); setNotice('')
    try {
      const response = await apiFetch(`/api/guardians/${selected.id}`, {
        method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(selected),
      })
      await ensureSuccess(response)
      const updated = await response.json() as Guardian
      setSelected(updated); setContacts(items => items.map(item => item.id === updated.id ? updated : item)); setNotice('Guardian updated.')
    } catch (cause) { setError(cause instanceof Error ? cause.message : 'Contact could not be saved.') }
    finally { setPending(false) }
  }

  return <>
    <header className="page-header"><h1>Guardians</h1><Link className="primary-action" to="/guardians/new">Register guardian</Link></header>
    {error && <div className="notice error" role="alert">{error}</div>}{notice && <div className="notice" role="status">{notice}</div>}
    <section className="content-grid"><section className="panel">
      <label>Search contacts<input value={query} onChange={event => setQuery(event.target.value)} placeholder="Name or phone" /></label>
      {contacts.map(contact => <button className="appointment-management-row" key={contact.id} type="button" onClick={() => setSelected(contact)}>
        <strong>{contact.name}</strong><span>{contact.phone}</span></button>)}
      {!contacts.length && <p>No matching guardians.</p>}
    </section>{selected && <form className="panel" onSubmit={event => void save(event)}><fieldset disabled={pending}>
      <h2>Edit guardian</h2>
      {(['name', 'phone', 'alternatePhone', 'address', 'identityType', 'identityNumber', 'identityDocumentUrl'] as const).map(field =>
        <label key={field}>{({ name: 'Name', phone: 'Phone', alternatePhone: 'Alternate phone', address: 'Address', identityType: 'Identity type',
          identityNumber: 'Identity number', identityDocumentUrl: 'Identity document URL' })[field]}
          <input value={selected[field]} type={field === 'identityDocumentUrl' ? 'url' : 'text'} required={field === 'name' || field === 'phone'}
            onChange={event => setSelected({ ...selected, [field]: event.target.value })} /></label>)}
      <button type="submit" className="primary-action">Save guardian</button>
    </fieldset></form>}</section>
  </>
}
