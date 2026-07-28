import { useEffect, useState } from 'react'
import { Minus, Plus, ReceiptText, ShoppingBag } from 'lucide-react'
import { apiFetch, type Patient, type Product, type SaleReceipt, getJson } from '../api'

const money = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'MXN' })

export function PointOfSalePage() {
  const [products, setProducts] = useState<Product[]>([])
  const [patients, setPatients] = useState<Patient[]>([])
  const [cart, setCart] = useState<Record<string, number>>({})
  const [patientId, setPatientId] = useState('')
  const [paymentMethod, setPaymentMethod] = useState('Cash')
  const [receipt, setReceipt] = useState<SaleReceipt | null>(null)
  const [error, setError] = useState('')

  function loadProducts() {
    void getJson<Product[]>('/api/products').then(setProducts).catch(() => setError('Inventory could not be loaded.'))
  }

  useEffect(() => {
    loadProducts()
    void getJson<Patient[]>('/api/patients').then(setPatients).catch(() => setError('Patients could not be loaded.'))
  }, [])

  function changeQuantity(product: Product, delta: number) {
    setCart((current) => {
      const quantity = Math.max(0, Math.min(product.stockOnHand, (current[product.id] ?? 0) + delta))
      const next = { ...current }
      if (quantity) next[product.id] = quantity
      else delete next[product.id]
      return next
    })
  }

  const cartProducts = products.filter((product) => cart[product.id])
  const total = cartProducts.reduce((sum, product) => sum + product.unitPrice * cart[product.id], 0)

  async function checkout() {
    setError('')
    setReceipt(null)
    try {
      const response = await apiFetch('/api/sales', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ patientId: patientId || null, paymentMethod, lines: Object.entries(cart).map(([productId, quantity]) => ({ productId, quantity })) }) })
      if (!response.ok) throw new Error()
      setReceipt(await response.json() as SaleReceipt)
      setCart({})
      loadProducts()
    } catch {
      setError('Checkout could not be completed. Refresh inventory and try again.')
    }
  }

  return (
    <>
      <header className="page-header"><div><p className="eyebrow">Inventory and payments</p><h1>Point of sale</h1></div><ShoppingBag size={26} /></header>
      {error && <div className="notice error" role="alert">{error}</div>}
      {receipt && <div className="notice" role="status">Sale {receipt.id.slice(0, 8)} completed for {money.format(receipt.total)}.</div>}
      <section className="pos-layout">
        <section className="panel"><div className="panel-heading"><div><p className="eyebrow">Inventory</p><h2>Clinic products</h2></div><ShoppingBag size={22} /></div><div className="product-list">{products.map((product) => <article className="product-row" key={product.id}><div><strong>{product.name}</strong><span>{product.category} · {product.stockOnHand} in stock</span></div><b>{money.format(product.unitPrice)}</b><button className="icon-button" type="button" onClick={() => changeQuantity(product, 1)} disabled={product.stockOnHand === 0} aria-label={`Add ${product.name}`}><Plus size={17} /></button></article>)}</div></section>
        <section className="panel checkout-panel"><div className="panel-heading"><div><p className="eyebrow">Current sale</p><h2>Checkout</h2></div><ReceiptText size={22} /></div><label>Patient (optional)<select value={patientId} onChange={(event) => setPatientId(event.target.value)}><option value="">Walk-in sale</option>{patients.map((patient) => <option key={patient.id} value={patient.id}>{patient.name} - {patient.guardianName}</option>)}</select></label><label>Payment method<select value={paymentMethod} onChange={(event) => setPaymentMethod(event.target.value)}><option>Cash</option><option>Card</option><option>Transfer</option></select></label><div className="cart-lines">{cartProducts.map((product) => <div key={product.id}><span>{product.name}</span><div className="quantity-control"><button type="button" onClick={() => changeQuantity(product, -1)} aria-label={`Remove one ${product.name}`}><Minus size={14} /></button><b>{cart[product.id]}</b><button type="button" onClick={() => changeQuantity(product, 1)} aria-label={`Add one ${product.name}`}><Plus size={14} /></button></div><strong>{money.format(product.unitPrice * cart[product.id])}</strong></div>)}{!cartProducts.length && <p className="empty-state">Add inventory items to start a sale.</p>}</div><div className="sale-total"><span>Total</span><strong>{money.format(total)}</strong></div><button className="primary-action" type="button" disabled={!cartProducts.length} onClick={() => void checkout()}><ReceiptText size={18} /> Complete sale</button></section>
      </section>
    </>
  )
}