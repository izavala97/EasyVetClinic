import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ArrowRight, Check, CircleDollarSign, Plus, Printer, ReceiptText, ShoppingBag, Stethoscope } from 'lucide-react'
import { getJson, type Patient } from '../api'
import { useAuthentication } from '../auth'
import { CommerceError, commerceApi, type AccountReceipt, type BillingAccount, type BillingPayment, type CatalogItem, type PaymentMethod } from '../commerce'
import { canFulfillAccount, confirmedPayment, createSingleFlight, outstandingProductQuantity, parsePendingPayment, preserveAccountPatientContext, validCurrencyAmount, type PendingPayment } from '../commerceOperations'
import '../commerce.css'

const currency = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'MXN' })
const timeFormat = new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'short' })
const paymentMethods: { value: PaymentMethod; label: string }[] = [
  { value: 'Cash', label: 'Cash' },
  { value: 'Card', label: 'Card' },
  { value: 'Transfer', label: 'Bank transfer' },
]
const loadLinkedAccount = createSingleFlight<BillingAccount>()

type ConsultationChargesProps = {
  consultationId: string
  patientId: string
  disabled?: boolean
}

export function ConsultationCharges({ consultationId, patientId, disabled = false }: ConsultationChargesProps) {
  const { currentUser } = useAuthentication()
  const [account, setAccount] = useState<BillingAccount | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  useEffect(() => {
    let active = true
    setLoading(true)
    setError('')
    setAccount(null)
    const key = JSON.stringify([currentUser?.objectId, currentUser?.clinicId, consultationId, patientId, disabled])
    void loadLinkedAccount(key, async () => {
      try {
        return await commerceApi.getConsultationAccount(consultationId)
      } catch (failure) {
        if (!(failure instanceof CommerceError) || failure.status !== 404 || disabled) throw failure
        try {
          return await commerceApi.createAccount({ patientId, consultationId })
        } catch (createFailure) {
          if (!(createFailure instanceof CommerceError) || createFailure.status !== 409) throw createFailure
          return await commerceApi.getConsultationAccount(consultationId)
        }
      }
    }).then(existing => {
      if (active) setAccount(existing)
    }).catch((failure: unknown) => {
      if (active) setError(failure instanceof Error ? failure.message : 'Consultation charges could not be loaded.')
    }).finally(() => {
      if (active) setLoading(false)
    })
    return () => { active = false }
  }, [consultationId, patientId, disabled, currentUser?.objectId, currentUser?.clinicId])

  if (loading) return <section className="panel consultation-charges"><p className="loading-state">Loading consultation account...</p></section>
  if (error) return <section className="panel consultation-charges"><div className="notice error" role="alert">{error}</div></section>
  if (!account) return <section className="panel consultation-charges"><p className="empty-state">No billing account is available for this consultation.</p></section>
  return <section className="panel consultation-charges"><div className="panel-heading"><div><p className="eyebrow">Consultation billing</p><h2>Charges and payments</h2></div><Stethoscope size={22} /></div><AccountWorkspace account={account} setAccount={setAccount} disabled={disabled} showPayments /><Link className="account-consultation-link" to={`/checkout?consultationId=${encodeURIComponent(consultationId)}`}>Open account in checkout</Link></section>
}

export function CheckoutPage() {
  const [searchParams] = useSearchParams()
  const consultationId = searchParams.get('consultationId')
  const [patients, setPatients] = useState<Patient[]>([])
  const [accounts, setAccounts] = useState<BillingAccount[]>([])
  const [selectedAccount, setSelectedAccount] = useState<BillingAccount | null>(null)
  const [patientId, setPatientId] = useState('')
  const [showHistory, setShowHistory] = useState(false)
  const [error, setError] = useState('')
  const [creationNeedsRefresh, setCreationNeedsRefresh] = useState(false)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const accountCreateLock = useRef(false)

  const load = useCallback(async () => {
    const [patientData, accountData] = await Promise.all([getJson<Patient[]>('/api/patients'), commerceApi.listAccounts()])
    setPatients(patientData)
    setAccounts(accountData)
    if (consultationId) {
      const linkedAccount = await commerceApi.getConsultationAccount(consultationId)
      setAccounts(current => [linkedAccount, ...current.filter(item => item.id !== linkedAccount.id)])
      setSelectedAccount(linkedAccount)
      setShowHistory(linkedAccount.status === 'Closed' || linkedAccount.status === 'Voided')
    } else {
      setSelectedAccount(current => current ? accountData.find(account => account.id === current.id) ?? null : null)
    }
  }, [consultationId])

  useEffect(() => {
    setLoading(true)
    setError('')
    setSelectedAccount(null)
    void load().catch((failure: unknown) => setError(failure instanceof Error ? failure.message : 'Checkout data could not be loaded.'))
      .finally(() => setLoading(false))
  }, [load])

  const pendingAccounts = accounts.filter(account => account.status === 'Draft' || account.status === 'Open')
  const historyAccounts = accounts.filter(account => account.status === 'Closed' || account.status === 'Voided')

  async function startAccount() {
    if (accountCreateLock.current) return
    accountCreateLock.current = true
    setBusy(true)
    setError('')
    try {
      const created = await commerceApi.createAccount({ patientId: patientId || null })
      setAccounts(current => [created, ...current])
      setSelectedAccount(created)
      setCreationNeedsRefresh(false)
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'A new checkout account could not be created.')
      setCreationNeedsRefresh(true)
    } finally {
      accountCreateLock.current = false
      setBusy(false)
    }
  }

  function updateAccount(updated: BillingAccount) {
    setSelectedAccount(updated)
    setAccounts(current => [updated, ...current.filter(account => account.id !== updated.id)])
  }

  async function refreshAccounts() {
    setLoading(true)
    try {
      await load()
      setCreationNeedsRefresh(false)
      setError('')
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Accounts could not be refreshed.')
    } finally {
      setLoading(false)
    }
  }

  return <>
    <header className="page-header"><div><p className="eyebrow">Commercial</p><h1>Checkout</h1><p className="commerce-subtitle">Keep accounts open as needed, accept partial or split payments, and fulfill stock separately.</p></div><ShoppingBag size={26} /></header>
    {error && <div className="notice error" role="alert">{error}{creationNeedsRefresh && <button className="secondary-action refresh-commerce" type="button" disabled={busy || loading} onClick={() => void refreshAccounts()}>Refresh accounts</button>}</div>}
    <section className="checkout-layout">
      <div className="checkout-account-list">
        <section className="panel">
          <div className="panel-heading"><div><p className="eyebrow">Start checkout</p><h2>New account</h2></div><ReceiptText size={22} /></div>
          <label className="commerce-field">Patient (optional)<select value={patientId} onChange={event => setPatientId(event.target.value)}><option value="">Walk-in account</option>{patients.map(patient => <option key={patient.id} value={patient.id}>{patient.name} · {patient.guardianName}</option>)}</select></label>
          <button className="primary-action" type="button" disabled={busy || loading || creationNeedsRefresh} onClick={() => void startAccount()}><ReceiptText size={17} /> Create account</button>
        </section>
        <section className="panel">
          <div className="panel-heading"><div><p className="eyebrow">Unsettled</p><h2>Pending accounts</h2></div><CircleDollarSign size={22} /></div>
          {loading ? <p className="loading-state">Loading accounts...</p> : pendingAccounts.length === 0 ? <p className="empty-state">There are no pending accounts.</p> : <div className="account-list">{pendingAccounts.map(account => <AccountListItem key={account.id} account={account} selected={selectedAccount?.id === account.id} onSelect={() => setSelectedAccount(account)} />)}</div>}
        </section>
        <section className="panel">
          <button className="history-toggle" type="button" aria-expanded={showHistory} onClick={() => setShowHistory(value => !value)}><span><p className="eyebrow">Records</p><strong>Checkout history ({historyAccounts.length})</strong></span><ArrowRight size={17} className={showHistory ? 'rotated' : ''} /></button>
          {showHistory && (historyAccounts.length ? <div className="account-list">{historyAccounts.map(account => <AccountListItem key={account.id} account={account} selected={selectedAccount?.id === account.id} onSelect={() => setSelectedAccount(account)} />)}</div> : <p className="empty-state">No completed or cancelled accounts yet.</p>)}
        </section>
      </div>
      <section className="panel checkout-detail">
        {selectedAccount
          ? <AccountWorkspace key={selectedAccount.id} account={selectedAccount} setAccount={updateAccount} showPayments />
          : <div className="checkout-empty"><ReceiptText size={36} /><h2>Choose an account</h2><p>Create a new account or select a pending account to add items, take payment, or fulfill products.</p></div>}
      </section>
    </section>
  </>
}

function AccountListItem({ account, selected, onSelect }: { account: BillingAccount; selected: boolean; onSelect: () => void }) {
  return <button className={`account-list-item${selected ? ' selected' : ''}`} type="button" onClick={onSelect}>
    <span><strong>{accountPatientLabel(account)}</strong><small>{timeFormat.format(new Date(account.updatedAt))} · {account.lines.length} line{account.lines.length === 1 ? '' : 's'}</small></span>
    <span className="account-list-balance"><strong>{currency.format(account.balanceDue)}</strong><small>{account.status}</small></span>
  </button>
}

function accountPatientLabel(account: BillingAccount) {
  return account.patientName ?? (account.patientId ? `Linked patient (${account.patientId})` : 'Walk-in account')
}

function AccountWorkspace({ account, setAccount, disabled = false, showPayments }: {
  account: BillingAccount
  setAccount: (account: BillingAccount) => void
  disabled?: boolean
  showPayments: boolean
}) {
  const { currentUser } = useAuthentication()
  const canOverridePrice = currentUser?.role === 'ClinicAdmin' || currentUser?.role === 'SuperAdmin' || currentUser?.isPlatformOwner === true
  const [catalog, setCatalog] = useState<CatalogItem[]>([])
  const [catalogQuery, setCatalogQuery] = useState('')
  const [selectedItemId, setSelectedItemId] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [paymentAmount, setPaymentAmount] = useState('')
  const [method, setMethod] = useState<PaymentMethod>('Cash')
  const [cashReceived, setCashReceived] = useState('')
  const [reference, setReference] = useState('')
  const [overrideLineId, setOverrideLineId] = useState('')
  const [overridePrice, setOverridePrice] = useState('')
  const [overrideReason, setOverrideReason] = useState('')
  const [receipt, setReceipt] = useState<AccountReceipt | null>(null)
  const [error, setError] = useState('')
  const [needsRefresh, setNeedsRefresh] = useState(false)
  const [busy, setBusy] = useState(false)
  const [pendingPayment, setPendingPayment] = useState<PendingPayment | null>(null)
  const mutationLock = useRef(false)
  const pendingStorageKey = `easyvet.payment.pending.${currentUser?.clinicId}.${account.id}`

  useEffect(() => {
    setPendingPayment(null)
    setNeedsRefresh(false)
    try {
      const stored = sessionStorage.getItem(pendingStorageKey)
      if (stored) {
        setPendingPayment(parsePendingPayment(stored))
        setNeedsRefresh(true)
        setError('A payment is awaiting confirmation. Retry the same payment before recording another.')
      }
    } catch (failure) {
      setNeedsRefresh(true)
      setError(failure instanceof Error ? failure.message : 'Pending payment operations could not be restored.')
    }
  }, [pendingStorageKey])

  useEffect(() => {
    void Promise.all([commerceApi.listCatalog('Product'), commerceApi.listCatalog('Service')])
      .then(([products, services]) => setCatalog([...products, ...services].filter(item => !item.isArchived)))
      .catch((failure: unknown) => setError(failure instanceof Error ? failure.message : 'The catalog could not be loaded.'))
  }, [])

  const selectedItem = catalog.find(item => item.id === selectedItemId)
  const matchingCatalog = catalog.filter(item => [item.name, item.category, item.sku ?? ''].some(value => value.toLocaleLowerCase().includes(catalogQuery.trim().toLocaleLowerCase())))

  async function mutate(operation: () => Promise<BillingAccount>, receiptFor?: (updated: BillingAccount) => AccountReceipt) {
    if (mutationLock.current || needsRefresh || pendingPayment || disabled) return false
    mutationLock.current = true
    setBusy(true)
    setError('')
    setReceipt(null)
    try {
      const updated = preserveAccountPatientContext(account, await operation())
      setAccount(updated)
      setReceipt(receiptFor?.(updated) ?? null)
      setNeedsRefresh(false)
      return true
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'The account could not be updated.')
      setNeedsRefresh(true)
      return false
    } finally {
      mutationLock.current = false
      setBusy(false)
    }
  }

  async function addLine(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!selectedItem) {
      setError('Select a catalog item before adding a charge.')
      return
    }
    await addCatalogItem(selectedItem)
  }

  async function addCatalogItem(item: CatalogItem) {
    const count = Number(quantity)
    if (!Number.isFinite(count) || count < 0.001 || Math.abs(count * 1000 - Math.round(count * 1000)) > 0.000001) {
      setError('Select a catalog item and enter a positive quantity with at most three decimal places.')
      return
    }
    const alreadyAdded = outstandingProductQuantity(account.lines, item.id)
    if (item.type === 'Product' && item.trackStock && alreadyAdded + count > item.stockOnHand) {
      setError(`Only ${item.stockOnHand} ${item.name} available. No stock has been reserved yet.`)
      return
    }
    await mutate(() => commerceApi.addAccountLine(account, item, count))
  }

  async function updateLine(lineId: string, count: number) {
    const line = account.lines.find(item => item.id === lineId)
    if (!line) return
    if (line.fulfilledQuantity > 0) {
      setError('Fulfilled charge lines are immutable. Add a new charge line for additional products.')
      return
    }
    if (count < 0.001) {
      await mutate(() => commerceApi.removeAccountLine(account, lineId))
      return
    }
    const catalogItem = catalog.find(item => item.id === line.catalogItemId)
    const otherQuantity = outstandingProductQuantity(account.lines, line.catalogItemId, lineId)
    if (line.itemType === 'Product' && catalogItem?.trackStock && otherQuantity + count > catalogItem.stockOnHand) {
      setError(`Only ${catalogItem.stockOnHand} ${catalogItem.name} available.`)
      return
    }
    await mutate(() => commerceApi.updateAccountLine(account, lineId, count))
  }

  async function takePayment(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (busy || needsRefresh || pendingPayment || disabled) return
    const amount = Number(paymentAmount)
    const received = method === 'Cash' ? Number(cashReceived) : null
    if (!validCurrencyAmount(amount) || amount <= 0 || amount > account.balanceDue) {
      setError('Payment amount must have at most two decimal places, be greater than zero, and be no more than the current balance.')
      return
    }
    if (method === 'Cash' && (received === null || !validCurrencyAmount(received) || received < amount)) {
      setError('Cash received must be at least the payment amount.')
      return
    }
    await recordPayment({ amount, method, idempotencyKey: crypto.randomUUID(), expectedVersion: account.version, note: method === 'Cash' ? '' : reference.trim(), cashReceived: received })
  }

  async function recordPayment(input: PendingPayment) {
    if (mutationLock.current || disabled) return
    const replay = pendingPayment !== null
    mutationLock.current = true
    setPendingPayment(input)
    setBusy(true)
    setError('')
    setReceipt(null)
    try {
      sessionStorage.setItem(pendingStorageKey, JSON.stringify(input))
      const updated = preserveAccountPatientContext(account, await commerceApi.payAccount(account, input))
      setAccount(updated)
      const payment = confirmedPayment(updated.payments, input)
      sessionStorage.removeItem(pendingStorageKey)
      setPendingPayment(null)
      setReceipt({ account: updated, payment })
      setNeedsRefresh(false)
      setPaymentAmount('')
      setCashReceived('')
      setReference('')
    } catch (failure) {
      const uncertain = replay || !(failure instanceof CommerceError) || failure.status >= 500 || failure.status === 408
      if (!uncertain) {
        try {
          sessionStorage.removeItem(pendingStorageKey)
          setPendingPayment(null)
        } catch (storageFailure) {
          setNeedsRefresh(true)
          setError(`Pending payment storage could not be cleared. Reconcile the payment before proceeding: ${storageFailure instanceof Error ? storageFailure.message : 'Storage unavailable.'}`)
          return
        }
      }
      setError(`${failure instanceof Error ? failure.message : 'The payment could not be confirmed.'}${uncertain ? ' Retry the same payment to safely confirm the result.' : ''}`)
      setNeedsRefresh(true)
    } finally {
      mutationLock.current = false
      setBusy(false)
    }
  }

  async function openAccount() {
    await mutate(() => commerceApi.openAccount(account))
  }

  async function overrideLinePrice(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const line = account.lines.find(item => item.id === overrideLineId)
    const price = Number(overridePrice)
    if (!canOverridePrice || !line || line.fulfilledQuantity > 0 || !overridePrice.trim() || !validCurrencyAmount(price) || !overrideReason.trim()) {
      setError('Select a charge line, enter a non-negative price with at most two decimal places, and provide an override reason.')
      return
    }
    const saved = await mutate(() => commerceApi.updateAccountLine(account, line.id, line.quantity, { unitPrice: price, priceOverrideReason: overrideReason.trim() }))
    if (saved) {
      setOverrideLineId('')
      setOverridePrice('')
      setOverrideReason('')
    }
  }

  async function refreshAccount() {
    setBusy(true)
    try {
      const [response, products, services] = await Promise.all([commerceApi.getAccount(account.id), commerceApi.listCatalog('Product'), commerceApi.listCatalog('Service')])
      const latest = preserveAccountPatientContext(account, response)
      setAccount(latest)
      setCatalog([...products, ...services].filter(item => !item.isArchived))
      const unresolved = sessionStorage.getItem(pendingStorageKey) !== null
      setNeedsRefresh(unresolved)
      setError(unresolved ? 'A payment is awaiting confirmation. Retry the same payment before recording another.' : '')
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'The account could not be refreshed.')
    } finally {
      setBusy(false)
    }
  }

  async function fulfill() {
    if (!canFulfillAccount(account)) {
      setError('This account has no product quantities available for fulfillment.')
      return
    }
    if (!window.confirm('Fulfill all currently unfulfilled products? This reduces stock now and locks those lines. A draft account remains editable for new charges; payment is separate.')) return
    const saved = await mutate(() => commerceApi.fulfillAccount(account), updated => ({ account: updated, payment: null }))
    if (saved) {
      setOverrideLineId('')
      setOverridePrice('')
      setOverrideReason('')
      try {
        const [products, services] = await Promise.all([commerceApi.listCatalog('Product'), commerceApi.listCatalog('Service')])
        setCatalog([...products, ...services].filter(item => !item.isArchived))
      } catch (failure) {
        setError(`Fulfillment was recorded, but catalog stock could not be refreshed: ${failure instanceof Error ? failure.message : 'Catalog unavailable.'}`)
        setNeedsRefresh(true)
      }
    }
  }

  return <>
    <div className="account-heading"><div><p className="eyebrow">{account.consultationId ? 'Consultation account' : 'Checkout account'}</p><h2>{accountPatientLabel(account)}</h2><p>{account.consultationId ? 'Charges are linked to the consultation and its patient.' : account.patientId ? 'Linked to the selected patient.' : 'No patient linked.'}</p></div><span className={`account-status status-${account.status.toLowerCase()}`}>{account.status}</span></div>
    {error && <div className="notice error" role="alert">{error}{needsRefresh && <button className="secondary-action refresh-commerce" type="button" disabled={busy} onClick={() => void refreshAccount()}>Refresh account</button>}</div>}
    {pendingPayment && <div className="notice" role="status">Unconfirmed payment: {pendingPayment.method} · {currency.format(pendingPayment.amount)}<button className="secondary-action refresh-commerce" type="button" disabled={busy || disabled} onClick={() => void recordPayment(pendingPayment)}>Retry the same payment</button></div>}
    {receipt && <div className="notice commerce-receipt-ready" role="status"><span>{receipt.payment ? `Payment received: ${currency.format(receipt.payment.amount)}.` : 'Fulfillment recorded.'}</span><button className="secondary-action" type="button" onClick={() => printReceipt(receipt)}><Printer size={16} /> Print receipt</button></div>}
    {account.status === 'Draft' && !disabled && <form className="add-charge-form" onSubmit={event => void addLine(event)}>
      <label className="commerce-field">Search catalog<input type="search" value={catalogQuery} onChange={event => { setCatalogQuery(event.target.value); setSelectedItemId('') }} placeholder="Name, category, or SKU" /></label>
      <label className="commerce-field">Add product or service<select value={selectedItemId} onChange={event => setSelectedItemId(event.target.value)}><option value="">Choose from catalog</option>{matchingCatalog.map(item => <option key={item.id} value={item.id}>{item.isFavorite ? '★ ' : ''}{item.name} · {item.type}{item.type === 'Product' && item.trackStock ? ` · ${item.stockOnHand} in stock` : ''} · {currency.format(item.unitPrice)}</option>)}</select></label>
      <label className="commerce-field quantity-field">Quantity<input type="number" min="0.001" step="0.001" value={quantity} onChange={event => setQuantity(event.target.value)} /></label>
      <button className="primary-action" type="submit" disabled={busy || needsRefresh || !selectedItemId}><ShoppingBag size={16} /> Add line</button>
    </form>}
    {account.status === 'Draft' && !disabled && catalog.some(item => item.isFavorite) && <section className="favorite-quick-add" aria-label="Favorite catalog quick-add">
      <h3>Favorite quick-add</h3><p>Uses the quantity above (default 1). Click a favorite to add it immediately.</p>
      <div>{catalog.filter(item => item.isFavorite).map(item => <button className="secondary-action" type="button" key={item.id} disabled={busy || needsRefresh || !!pendingPayment} onClick={() => void addCatalogItem(item)}><Plus size={16} />{item.name} · {currency.format(item.unitPrice)}</button>)}</div>
    </section>}
    <div className="account-lines">
      {account.lines.map(line => <div className="account-line" key={line.id}>
        <div className="commerce-item-info"><strong>{line.name}</strong><small>{line.itemType} · {currency.format(line.unitPrice)} / {line.unit}{line.itemType === 'Product' && line.fulfilledQuantity > 0 ? ` · ${line.fulfilledQuantity} fulfilled` : ''}</small>{line.priceOverrideReason && <small>Price override: {line.priceOverrideReason}</small>}</div>
        {account.status === 'Draft' && !disabled && line.fulfilledQuantity === 0 ? <div className="quantity-stepper"><button type="button" aria-label={`Remove one ${line.name}`} disabled={busy || needsRefresh} onClick={() => void updateLine(line.id, line.quantity - 1)}>−</button><strong>{line.quantity}</strong><button type="button" aria-label={`Add one ${line.name}`} disabled={busy || needsRefresh} onClick={() => void updateLine(line.id, line.quantity + 1)}>+</button></div> : <span>Qty {line.quantity}</span>}
        <strong>{currency.format(line.lineTotal)}</strong>
      </div>)}
      {!account.lines.length && <p className="empty-state">{disabled ? 'No charges were recorded for this consultation.' : 'Add a catalog item to create the account.'}</p>}
    </div>
    {account.status === 'Draft' && !disabled && canOverridePrice && account.lines.some(line => line.fulfilledQuantity === 0) && <form className="commerce-form price-override-form" onSubmit={event => void overrideLinePrice(event)}>
      <h3>Administrator price override</h3>
      <fieldset className="payment-fields commerce-fields" disabled={busy || needsRefresh || !!pendingPayment}>
        <label>Charge line<select required value={overrideLineId} onChange={event => { const line = account.lines.find(item => item.id === event.target.value); setOverrideLineId(event.target.value); setOverridePrice(line ? String(line.unitPrice) : ''); setOverrideReason('') }}><option value="">Select a line</option>{account.lines.filter(line => line.fulfilledQuantity === 0).map(line => <option key={line.id} value={line.id}>{line.name} · {currency.format(line.unitPrice)}</option>)}</select></label>
        <label>Unit price<input type="number" min="0" step="0.01" required value={overridePrice} onChange={event => setOverridePrice(event.target.value)} /></label>
        <label>Required reason<input required maxLength={500} value={overrideReason} onChange={event => setOverrideReason(event.target.value)} /></label>
      </fieldset>
      <button className="secondary-action" type="submit" disabled={busy || needsRefresh || !overrideLineId}>Save price override</button>
    </form>}
    <div className="account-totals"><div><span>Total</span><strong>{currency.format(account.total)}</strong></div><div><span>Paid</span><strong>{currency.format(account.amountPaid)}</strong></div><div className="balance-total"><span>Balance due</span><strong>{currency.format(account.balanceDue)}</strong></div></div>
    {showPayments && account.payments.length > 0 && <section className="payment-history"><h3>Payments</h3>{account.payments.map(payment => <PaymentRow key={payment.id} payment={payment} onPrint={() => printReceipt({ account, payment })} />)}</section>}
    {showPayments && account.status === 'Draft' && !disabled && account.lines.length > 0 && <div className="fulfillment-panel"><div><strong>Ready to accept payment?</strong><small>Open this account to finalize its current line items. Changes to lines are locked afterward.</small></div><button className="secondary-action" type="button" disabled={busy || needsRefresh} onClick={() => void openAccount()}><Check size={16} /> Open account</button></div>}
    {showPayments && account.status === 'Open' && !disabled && <>
      <form className="payment-form" onSubmit={event => void takePayment(event)}>
        <h3>Record a payment</h3>
        <fieldset className="payment-fields commerce-fields" disabled={busy || needsRefresh || !!pendingPayment}>
          <label className="commerce-field">Amount<input type="number" min="0.01" max={account.balanceDue} step="0.01" required value={paymentAmount} onChange={event => setPaymentAmount(event.target.value)} /></label>
          <label className="commerce-field">Method<select value={method} onChange={event => setMethod(event.target.value as PaymentMethod)}>{paymentMethods.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}</select></label>
          {method === 'Cash' && <label className="commerce-field">Cash received<input type="number" min={paymentAmount || '0.01'} step="0.01" required value={cashReceived} onChange={event => setCashReceived(event.target.value)} /></label>}
          {method !== 'Cash' && <label className="commerce-field">Reference (optional)<input maxLength={120} value={reference} onChange={event => setReference(event.target.value)} /></label>}
        </fieldset>
        <button className="primary-action" type="submit" disabled={busy || needsRefresh || account.balanceDue <= 0}><CircleDollarSign size={17} /> Record payment</button>
        {method === 'Cash' && Number(cashReceived) >= Number(paymentAmount) && Number(paymentAmount) > 0 && <p className="cash-change">Change due: <strong>{currency.format(Number(cashReceived) - Number(paymentAmount))}</strong></p>}
      </form>
    </>}
    {canFulfillAccount(account) && !disabled && <div className="fulfillment-panel"><div><strong>Administer or dispense products now.</strong><small>Explicit fulfillment reduces stock independently of payment and locks fulfilled lines. Draft accounts stay open for additional charges; newly added products can be fulfilled later.</small></div><button className="secondary-action" type="button" disabled={busy || needsRefresh || !!pendingPayment} onClick={() => void fulfill()}><Check size={16} /> Fulfill current products</button></div>}
    {account.status === 'Closed' && !canFulfillAccount(account) && <p className="notice">Account paid and fulfilled.</p>}
    {account.consultationId && <Link className="account-consultation-link" to={`/consultations/${account.consultationId}`}>Return to consultation</Link>}
  </>
}

function PaymentRow({ payment, onPrint }: { payment: BillingPayment; onPrint: () => void }) {
  return <div className="payment-row"><span><strong>{payment.kind} · {payment.method}</strong><small>{timeFormat.format(new Date(payment.createdAt))}{payment.note ? ` · ${payment.note}` : ''}</small>{payment.cashReceived !== null && <small>Cash received: {currency.format(payment.cashReceived)}{payment.changeGiven !== null ? ` · Change: ${currency.format(payment.changeGiven)}` : ''}</small>}</span><span className="payment-amount"><strong>{currency.format(payment.amount)}</strong><button className="icon-button" type="button" onClick={onPrint} aria-label={`Print receipt for ${payment.method} ${currency.format(payment.amount)} on ${timeFormat.format(new Date(payment.createdAt))}`}><Printer size={16} /></button></span></div>
}

function printReceipt(receipt: AccountReceipt) {
  const account = receipt.account
  const popup = window.open('', '_blank', 'width=720,height=800')
  if (!popup) {
    window.alert('The receipt window was blocked. Allow pop-ups for this site and try again.')
    return
  }
  const escape = (text: string) => text.replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character]!)
  const paymentDetails = receipt.payment
    ? `<h2>${escape(receipt.payment.kind)}</h2><p>Payment ID: ${escape(receipt.payment.id)}</p><p>${escape(timeFormat.format(new Date(receipt.payment.createdAt)))}</p><p>${escape(receipt.payment.method)} · ${currency.format(receipt.payment.amount)}${receipt.payment.note ? ` · ${escape(receipt.payment.note)}` : ''}</p>${receipt.payment.cashReceived !== null ? `<p>Cash received: ${currency.format(receipt.payment.cashReceived)}</p>` : ''}${receipt.payment.changeGiven !== null ? `<p>Change: ${currency.format(receipt.payment.changeGiven)}</p>` : ''}`
    : '<p>Fulfillment receipt</p>'
  popup.document.write(`<!doctype html><html lang="en"><head><meta charset="utf-8"><title>EasyVetClinic receipt</title><style>body{font:16px Arial,sans-serif;max-width:680px;margin:40px auto;color:#222}h1{font-size:24px}table{width:100%;border-collapse:collapse;margin:24px 0}th,td{text-align:left;padding:10px;border-bottom:1px solid #ccc}td:last-child,th:last-child{text-align:right}.total{text-align:right;font-size:20px;font-weight:bold}.meta{color:#555}@media print{button{display:none}}</style></head><body><h1>EasyVetClinic receipt</h1><p class="meta">Account ${escape(account.id)} · ${escape(timeFormat.format(new Date(account.updatedAt)))}</p><p>Patient: ${escape(accountPatientLabel(account))}</p><table><thead><tr><th>Item</th><th>Qty</th><th>Unit price</th><th>Total</th></tr></thead><tbody>${account.lines.map(line => `<tr><td>${escape(line.name)}</td><td>${line.quantity}</td><td>${currency.format(line.unitPrice)}</td><td>${currency.format(line.lineTotal)}</td></tr>`).join('')}</tbody></table><p class="total">Account total: ${currency.format(account.total)}</p>${paymentDetails}<p>Balance due: ${currency.format(account.balanceDue)}</p><button onclick="window.print()">Print receipt</button><script>window.addEventListener('load',()=>window.print())</script></body></html>`)
  popup.document.close()
}

export { ConsultationCharges as ConsultationChargesComponent }
