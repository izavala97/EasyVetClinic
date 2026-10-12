import { formatMoney } from '../format'
import { useTranslation } from '../useTranslation'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Archive, Heart, PackagePlus, Pencil, Plus, Search, Wrench } from 'lucide-react'
import { useAuthentication } from '../auth'
import { commerceApi, type CatalogItem, type CatalogItemInput, type CommerceKind } from '../commerce'
import '../commerce.css'

const currency = { format: formatMoney }
const emptyForm = { name: '', description: '', category: '', sku: '', unit: 'each', unitPrice: '', stockOnHand: '0', lowStockThreshold: '0', trackStock: true, isFavorite: false }

export function CatalogPage() {
  const { t } = useTranslation()

  const { currentUser } = useAuthentication()
  const canManageCatalog = currentUser?.role === 'ClinicAdmin' || currentUser?.role === 'SuperAdmin' || currentUser?.isPlatformOwner === true
  const [kind, setKind] = useState<CommerceKind>('Product')
  const [items, setItems] = useState<CatalogItem[]>([])
  const [includeArchived, setIncludeArchived] = useState(false)
  const [query, setQuery] = useState('')
  const [editing, setEditing] = useState<CatalogItem | null>(null)
  const [form, setForm] = useState(emptyForm)
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    setItems(await commerceApi.listCatalog(kind, includeArchived))
  }, [kind, includeArchived])

  useEffect(() => {
    setLoading(true)
    setError('')
    void load().catch((failure: unknown) => setError(failure instanceof Error ? failure.message : 'The catalog could not be loaded.'))
      .finally(() => setLoading(false))
  }, [load])

  const visibleItems = useMemo(() => {
    const normalized = query.trim().toLocaleLowerCase()
    if (!normalized) return items
    return items.filter(item => [item.name, item.description, item.category].some(value => value.toLocaleLowerCase().includes(normalized)))
  }, [items, query])

  function startCreate() {
    setEditing(null)
    setForm(emptyForm)
    setError('')
  }

  function startEdit(item: CatalogItem) {
    setEditing(item)
    setForm({
      name: item.name,
      description: item.description,
      category: item.category,
      sku: item.sku ?? '',
      unit: item.unit,
      unitPrice: String(item.unitPrice),
      stockOnHand: String(item.stockOnHand),
      lowStockThreshold: String(item.lowStockThreshold),
      trackStock: item.trackStock,
      isFavorite: item.isFavorite,
    })
    setError('')
  }

  async function save(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const unitPrice = Number(form.unitPrice)
    const threshold = Number(form.lowStockThreshold)
    const stock = Number(form.stockOnHand)
    if (!Number.isFinite(unitPrice) || unitPrice < 0 || !Number.isFinite(threshold) || threshold < 0 || !Number.isFinite(stock) || stock < 0) {
      setError('Enter a non-negative price, stock level, and low-stock threshold.')
      return
    }
    const input: CatalogItemInput = {
      name: form.name.trim(),
      description: form.description.trim(),
      category: form.category.trim(),
      sku: form.sku.trim(),
      unit: form.unit.trim(),
      unitPrice,
      stockOnHand: kind === 'Product' && form.trackStock ? (editing?.stockOnHand ?? stock) : 0,
      lowStockThreshold: kind === 'Product' && form.trackStock ? threshold : 0,
      trackStock: kind === 'Product' && form.trackStock,
      isFavorite: form.isFavorite,
    }
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await commerceApi.saveCatalogItem(kind, input, editing?.id, editing?.version)
      setNotice(t("{{value0}} saved.", { value0: t(kind) }))
      setForm(emptyForm)
      setEditing(null)
      await load()
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : t(kind === 'Product' ? 'Product could not be saved.' : 'Service could not be saved.'))
    } finally {
      setBusy(false)
    }
  }

  async function toggleFavorite(item: CatalogItem) {
    setBusy(true)
    setError('')
    try {
      await commerceApi.setFavorite(item, !item.isFavorite)
      await load()
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Favorite status could not be changed.')
    } finally {
      setBusy(false)
    }
  }

  async function archive(item: CatalogItem) {
    if (!window.confirm(t("Archive {{value0}}? It will remain on past accounts but cannot be added to new ones.", { value0: item.name }))) return
    setBusy(true)
    setError('')
    try {
      await commerceApi.archiveCatalogItem(kind, item.id)
      setNotice(t("{{value0}} archived.", { value0: item.name }))
      await load()
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'The catalog item could not be archived.')
    } finally {
      setBusy(false)
    }
  }

  return <>
    <header className="page-header"><div><p className="eyebrow">{t("Commercial")}</p><h1>{t("Catalog")}</h1><p className="commerce-subtitle">{t("Manage the products and services available to the clinic.")}</p></div><PackagePlus size={26} /></header>
    {notice && <div className="notice" role="status">{t(notice)}</div>}
    {error && <div className="notice error" role="alert">{t(error)}</div>}
    <div className="commerce-tabs" role="tablist" aria-label={t("Catalog item type")}>
      <button type="button" role="tab" aria-selected={kind === 'Product'} onClick={() => { setKind('Product'); setEditing(null); setForm(emptyForm) }}><PackagePlus size={17} />{t("Products")}</button>
      <button type="button" role="tab" aria-selected={kind === 'Service'} onClick={() => { setKind('Service'); setEditing(null); setForm(emptyForm) }}><Wrench size={17} />{t("Services")}</button>
    </div>
    <section className="catalog-layout">
      <section className="panel">
        <div className="commerce-toolbar">
          <label className="search-field"><Search size={16} /><input value={query} onChange={event => setQuery(event.target.value)} placeholder={t(kind === 'Product' ? 'Search products' : 'Search services')} aria-label={t(kind === 'Product' ? 'Search products' : 'Search services')} /></label>
          {canManageCatalog && <button className="primary-action" type="button" onClick={startCreate}><Plus size={17} />{t(kind === 'Product' ? 'Add product' : 'Add service')}</button>}
        </div>
        {canManageCatalog && <label className="inline-check"><input type="checkbox" checked={includeArchived} onChange={event => setIncludeArchived(event.target.checked)} />{t("Include archived")}</label>}
        {loading ? <p className="loading-state">{t("Loading catalog...")}</p> : visibleItems.length === 0 ? <p className="empty-state">{t(kind === 'Product' ? 'No products match this view.' : 'No services match this view.')}</p> : <div className="commerce-list">
          {visibleItems.map(item => <article className={`commerce-list-row${item.isArchived ? ' is-archived' : ''}`} key={item.id}>
            {canManageCatalog && <button className="favorite-button" type="button" disabled={busy || item.isArchived} aria-label={t(item.isFavorite ? 'Remove {{name}} from favorites' : 'Add {{name}} to favorites', { name: item.name })} onClick={() => void toggleFavorite(item)}><Heart size={17} fill={item.isFavorite ? 'currentColor' : 'none'} /></button>}
            <div className="commerce-item-info"><strong>{item.name}</strong><span>{item.category || t("Uncategorized")}{item.isArchived ? t(" · Archived") : ''}{item.sku ? ` · SKU ${item.sku}` : ''}</span>{item.description && <small>{item.description}</small>}</div>
            <div className="commerce-item-price"><strong>{currency.format(item.unitPrice)}</strong>{kind === 'Product' && <small>{item.stockOnHand ?? 0} {t("in stock")}</small>}</div>
            {canManageCatalog && <div className="commerce-row-actions"><button className="icon-button" type="button" disabled={busy || item.isArchived} onClick={() => startEdit(item)} aria-label={t("Edit {{value0}}", { value0: item.name })}><Pencil size={16} /></button>{!item.isArchived && <button className="icon-button" type="button" disabled={busy} onClick={() => void archive(item)} aria-label={t("Archive {{value0}}", { value0: item.name })}><Archive size={16} /></button>}</div>}
          </article>)}
        </div>}
      </section>
      {canManageCatalog && <form className="panel commerce-form" onSubmit={event => void save(event)}>
        <p className="eyebrow">{editing ? t("Update catalog") : t("Catalog maintenance")}</p>
        <h2>{t(editing ? (kind === 'Product' ? 'Edit product' : 'Edit service') : (kind === 'Product' ? 'Add product' : 'Add service'))}</h2>
        <label>{t("Name")}<input required maxLength={160} value={form.name} onChange={event => setForm(current => ({ ...current, name: event.target.value }))} /></label>
        <label>{t("Category")}<input required maxLength={100} value={form.category} onChange={event => setForm(current => ({ ...current, category: event.target.value }))} /></label>
        <label>{t("Unit")}<input required maxLength={50} value={form.unit} onChange={event => setForm(current => ({ ...current, unit: event.target.value }))} placeholder={t("each, dose, hour...")} /></label>
        {kind === 'Product' && <label>{t("SKU (optional)")}<input maxLength={100} value={form.sku} onChange={event => setForm(current => ({ ...current, sku: event.target.value }))} /></label>}
        {kind === 'Service' && <label>{t("Description")}<textarea rows={3} maxLength={500} value={form.description} onChange={event => setForm(current => ({ ...current, description: event.target.value }))} /></label>}
        <label>{t("Price")}<input required type="number" min="0" step="0.01" value={form.unitPrice} onChange={event => setForm(current => ({ ...current, unitPrice: event.target.value }))} /></label>
        <label className="inline-check">{t("Favorite for quick-add")}<input type="checkbox" checked={form.isFavorite} onChange={event => setForm(current => ({ ...current, isFavorite: event.target.checked }))} /></label>
        {kind === 'Product' && <><label>{t("Track stock")}<input type="checkbox" checked={form.trackStock} disabled={!!editing} onChange={event => setForm(current => ({ ...current, trackStock: event.target.checked }))} /></label>{form.trackStock && <>{!editing && <label>{t("Initial stock")}<input type="number" min="0" step="0.001" value={form.stockOnHand} onChange={event => setForm(current => ({ ...current, stockOnHand: event.target.value }))} /></label>}<label>{t("Low-stock alert threshold")}<input type="number" min="0" step="0.001" value={form.lowStockThreshold} onChange={event => setForm(current => ({ ...current, lowStockThreshold: event.target.value }))} /></label></>}</>}
        <div className="form-actions"><button className="primary-action" type="submit" disabled={busy}>{editing ? t("Save changes") : t(kind === 'Product' ? 'Create product' : 'Create service')}</button>{editing && <button className="secondary-action" type="button" disabled={busy} onClick={startCreate}>{t("Cancel")}</button>}</div>
      </form>}
      {!canManageCatalog && <aside className="panel commerce-access-note"><h2>{t("Catalog maintenance")}</h2><p>{t("Only clinic administrators can create, edit, favorite, or archive catalog items. All active items remain available in checkout.")}</p></aside>}
    </section>
  </>
}
