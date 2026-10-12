import { formatDateTime } from '../format'
import { useTranslation } from '../useTranslation'
import { useCallback, useEffect, useRef, useState } from 'react'
import { AlertTriangle, ArrowDownToLine, ClipboardList, Package, Plus, RefreshCw } from 'lucide-react'
import { useAuthentication } from '../auth'
import { CommerceError, commerceApi, type CatalogItem, type InventoryAdjustment, type InventoryTransaction } from '../commerce'
import { parsePendingAdjustment } from '../commerceOperations'
import '../commerce.css'

const timestamp = { format: formatDateTime }

export function InventoryPage() {
  const { t } = useTranslation()

  const { currentUser } = useAuthentication()
  const canAdjust = currentUser?.role === 'ClinicAdmin' || currentUser?.role === 'SuperAdmin' || currentUser?.isPlatformOwner === true
  const [items, setItems] = useState<CatalogItem[]>([])
  const [lowStock, setLowStock] = useState<CatalogItem[]>([])
  const [history, setHistory] = useState<InventoryTransaction[]>([])
  const [productId, setProductId] = useState('')
  const [quantity, setQuantity] = useState('')
  const [reason, setReason] = useState('')
  const [operation, setOperation] = useState<'receive' | 'adjust'>('receive')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [reviewRequired, setReviewRequired] = useState(false)
  const [pendingAdjustment, setPendingAdjustment] = useState<InventoryAdjustment | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const mutationLock = useRef(false)
  const trackedItems = items.filter(item => item.trackStock)
  const pendingStorageKey = `easyvet.inventory.pending.${currentUser?.clinicId}`

  const load = useCallback(async () => {
    const [inventory, transactions] = await Promise.all([
      commerceApi.getInventory(),
      commerceApi.getInventoryHistory(),
    ])
    setItems(inventory)
    setLowStock(inventory.filter(item => item.trackStock && item.stockOnHand <= item.lowStockThreshold))
    setHistory(transactions)
    setProductId(current => inventory.some(item => item.id === current) ? current : inventory[0]?.id ?? '')
  }, [])

  useEffect(() => {
    setPendingAdjustment(null)
    setReviewRequired(false)
    try {
      const stored = sessionStorage.getItem(pendingStorageKey)
      if (stored) {
        const input = parsePendingAdjustment(stored)
        setPendingAdjustment(input)
        setProductId(input.productId)
        setQuantity(String(input.quantityChange))
        setReason(input.reason)
        setReviewRequired(true)
        setError('A stock movement is awaiting confirmation. Retry the same movement to safely resolve its result.')
      }
    } catch (failure) {
      setReviewRequired(true)
      setError(failure instanceof Error ? failure.message : 'Pending inventory operations could not be restored.')
    }
    void load().catch((failure: unknown) => setError(failure instanceof Error ? failure.message : 'Inventory could not be loaded.'))
      .finally(() => setLoading(false))
  }, [load, pendingStorageKey])

  async function recordAdjustment(input: InventoryAdjustment) {
    if (mutationLock.current) return
    const replay = pendingAdjustment !== null
    mutationLock.current = true
    setPendingAdjustment(input)
    setBusy(true)
    setError('')
    setNotice('')
    let saved = false
    try {
      sessionStorage.setItem(pendingStorageKey, JSON.stringify(input))
      await commerceApi.adjustStock(input)
      saved = true
      sessionStorage.removeItem(pendingStorageKey)
      setPendingAdjustment(null)
      setReviewRequired(false)
      setNotice('Stock movement recorded.')
      setQuantity('')
      setReason('')
      await load()
    } catch (failure) {
      const uncertain = !saved && (replay || !(failure instanceof CommerceError) || failure.status >= 500 || failure.status === 408)
      if (!uncertain) {
        try {
          sessionStorage.removeItem(pendingStorageKey)
          setPendingAdjustment(null)
        } catch (storageFailure) {
          setError(t("The stock movement result must be reconciled before proceeding. Pending operation storage could not be cleared: {{value0}}", { value0: t(storageFailure instanceof Error ? storageFailure.message : 'Storage unavailable.') }))
          setReviewRequired(true)
          return
        }
      }
      setError(t("{{value0}}{{value1}}", { value0: t(failure instanceof Error ? failure.message : 'The inventory change could not be saved.'), value1: uncertain ? ` ${t('The result is uncertain. Retry the same operation to confirm it without recording stock twice.')}` : saved ? ` ${t('The movement was saved, but the inventory view could not be refreshed.')}` : '' }))
      setReviewRequired(true)
    } finally {
      mutationLock.current = false
      setBusy(false)
    }
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (mutationLock.current || reviewRequired || pendingAdjustment) return
    const amount = Number(quantity)
    if (!Number.isFinite(amount) || amount === 0 || Math.abs(amount) > Number.MAX_SAFE_INTEGER / 1000 || Math.round(amount * 1000) !== amount * 1000 || (operation === 'receive' && amount < 0)) {
      setError(operation === 'receive' ? 'Received quantity must be a positive amount with at most three decimal places.' : 'Adjustment must be a non-zero amount with at most three decimal places.')
      return
    }
    const product = items.find(item => item.id === productId)
    if (!product) {
      setError('Select an inventory product before continuing.')
      return
    }
    if (product.stockOnHand + amount < 0) {
      setError('This adjustment cannot make stock negative.')
      return
    }
    await recordAdjustment({ productId, quantityChange: amount, reason: reason.trim(), idempotencyKey: crypto.randomUUID() })
  }

  async function refreshAfterFailure() {
    setLoading(true)
    try {
      await load()
      const unresolved = sessionStorage.getItem(pendingStorageKey) !== null
      setError(unresolved ? 'A stock movement is awaiting confirmation. Retry the same movement before recording another.' : '')
      setNotice('Inventory and movement history refreshed. Review the latest movement before retrying.')
      if (!unresolved) setReviewRequired(false)
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Inventory could not be refreshed.')
    } finally {
      setLoading(false)
    }
  }

  return <>
    <header className="page-header"><div><p className="eyebrow">{t("Commercial")}</p><h1>{t("Inventory")}</h1><p className="commerce-subtitle">{t("Track stock, receive deliveries, and record adjustments.")}</p></div><Package size={26} /></header>
    {notice && <div className="notice" role="status">{t(notice)}</div>}
    {error && <div className="notice error" role="alert">{t(error)}{reviewRequired && <button className="secondary-action refresh-commerce" type="button" disabled={busy || loading} onClick={() => void refreshAfterFailure()}>{t("Refresh inventory and history")}</button>}</div>}
    {pendingAdjustment && <div className="notice" role="status">{t("Unconfirmed stock movement:")} {pendingAdjustment.quantityChange} {t("units ·")} {pendingAdjustment.reason}<button className="secondary-action refresh-commerce" type="button" disabled={busy || loading || !canAdjust} onClick={() => void recordAdjustment(pendingAdjustment)}>{t("Retry the same stock movement")}</button></div>}
    <section className="metrics commerce-metrics">
      <article><strong>{t("Tracked products")}</strong><span>{trackedItems.length}</span></article>
      <article className={lowStock.length ? 'metric-warning' : ''}><strong>{t("Low stock")}</strong><span>{lowStock.length}</span></article>
      <article><strong>{t("Recent movements")}</strong><span>{history.length}</span></article>
    </section>
    {lowStock.length > 0 && <section className="panel low-stock-panel"><div className="panel-heading"><div><p className="eyebrow">{t("Reorder attention")}</p><h2>{t("Low-stock products")}</h2></div><AlertTriangle size={22} /></div><div className="commerce-list">{lowStock.map(item => <div className="commerce-list-row" key={item.id}><div className="commerce-item-info"><strong>{item.name}</strong><span>{item.category || t("Uncategorized")}</span></div><strong>{item.stockOnHand ?? 0} {t("in stock")}{item.lowStockThreshold !== null ? ` · ${t('threshold {{value0}}', { value0: item.lowStockThreshold })}` : ''}</strong></div>)}</div></section>}
    <section className="inventory-layout">
      <form className="panel commerce-form" onSubmit={event => void submit(event)}>
        <div className="panel-heading"><div><p className="eyebrow">{t("Stock operations")}</p><h2>{t("Receive or adjust")}</h2></div><ArrowDownToLine size={22} /></div>
        {canAdjust ? <fieldset className="commerce-fields" disabled={busy || !!pendingAdjustment}><label>{t("Operation")}<select value={operation} onChange={event => setOperation(event.target.value as 'receive' | 'adjust')}><option value="receive">{t("Receive stock")}</option><option value="adjust">{t("Adjust stock")}</option></select></label>
        <label>{t("Product")}<select required value={productId} onChange={event => setProductId(event.target.value)}><option value="">{t("Select a product")}</option>{trackedItems.map(item => <option key={item.id} value={item.id}>{item.name} · {item.stockOnHand} {t("in stock")}</option>)}</select></label>
        <label>{operation === 'receive' ? t("Quantity received") : t("Adjustment (+/-)")}<input type="number" step="0.001" min={operation === 'receive' ? '0.001' : undefined} required value={quantity} onChange={event => setQuantity(event.target.value)} placeholder={operation === 'receive' ? t("e.g. 12") : t("e.g. 3 or -2")} /></label>
        <label>{t("Reason")}<input required maxLength={250} value={reason} onChange={event => setReason(event.target.value)} placeholder={operation === 'receive' ? t("Supplier delivery, invoice, or reference") : t("Count correction or damaged stock")} /></label>
        <button className="primary-action" type="submit" disabled={busy || loading || reviewRequired || trackedItems.length === 0}><Plus size={17} />{busy ? t("Saving...") : operation === 'receive' ? t("Record received stock") : t("Save adjustment")}</button></fieldset> : <p className="empty-state">{t("Only clinic administrators can receive or adjust stock.")}</p>}
      </form>
      <section className="panel">
        <div className="panel-heading"><div><p className="eyebrow">{t("On hand")}</p><h2>{t("Stock levels")}</h2></div><button className="icon-button" type="button" disabled={busy || loading} onClick={() => { setLoading(true); setError(''); void load().catch((failure: unknown) => setError(failure instanceof Error ? failure.message : 'Inventory could not be refreshed.')).finally(() => setLoading(false)) }} aria-label={t("Refresh inventory")}><RefreshCw size={16} /></button></div>
        {loading ? <p className="loading-state">{t("Loading inventory...")}</p> : trackedItems.length === 0 ? <p className="empty-state">{t("There are no tracked products in the catalog.")}</p> : <div className="commerce-list">{trackedItems.map(item => <article className="commerce-list-row" key={item.id}><div className="commerce-item-info"><strong>{item.name}</strong><span>{item.category || t("Uncategorized")}</span></div><div className="stock-count"><strong>{item.stockOnHand}</strong><small>{t("in stock")}</small></div></article>)}</div>}
      </section>
    </section>
    <section className="panel inventory-history">
      <div className="panel-heading"><div><p className="eyebrow">{t("Audit trail")}</p><h2>{t("Inventory history")}</h2></div><ClipboardList size={22} /></div>
      {loading ? <p className="loading-state">{t("Loading history...")}</p> : history.length === 0 ? <p className="empty-state">{t("No inventory movements have been recorded.")}</p> : <div className="commerce-table-wrap"><table className="commerce-table"><thead><tr><th>{t("Date")}</th><th>{t("Product")}</th><th>{t("Type")}</th><th>{t("Change")}</th><th>{t("Stock after")}</th><th>{t("Reason")}</th><th>{t("Account")}</th></tr></thead><tbody>{history.map(entry => <tr key={entry.id}><td>{timestamp.format(new Date(entry.createdAt))}</td><td>{entry.productName}{entry.sku ? ` · ${entry.sku}` : ''}</td><td>{t(entry.type)}</td><td>{entry.quantityChange > 0 ? '+' : ''}{entry.quantityChange}</td><td>{entry.balanceAfter}</td><td>{entry.reason}</td><td>{entry.billingAccountId ?? '—'}</td></tr>)}</tbody></table></div>}
    </section>
  </>
}
