import { apiFetch } from './api'

export type CommerceKind = 'Product' | 'Service'
export type PaymentMethod = 'Cash' | 'Card' | 'Transfer'
export type AccountStatus = 'Draft' | 'Open' | 'Closed' | 'Voided'

export type CatalogItem = {
  id: string
  type: CommerceKind
  name: string
  category: string
  description: string
  sku: string | null
  unit: string
  unitPrice: number
  stockOnHand: number
  lowStockThreshold: number
  trackStock: boolean
  isFavorite: boolean
  isArchived: boolean
  version: number
}

export type CatalogItemInput = {
  name: string
  category: string
  description: string
  sku: string
  unit: string
  unitPrice: number
  stockOnHand: number
  lowStockThreshold: number
  trackStock: boolean
  isFavorite: boolean
}

export type InventoryTransaction = {
  id: string
  productId: string
  productName: string
  sku: string
  type: string
  quantityChange: number
  balanceAfter: number
  reason: string
  billingAccountId: string | null
  chargeLineId: string | null
  createdAt: string
}

export type InventoryAdjustment = {
  productId: string
  quantityChange: number
  reason: string
  idempotencyKey: string
}

export type BillingLine = {
  id: string
  catalogItemId: string
  itemType: CommerceKind
  name: string
  sku: string
  unit: string
  catalogVersion: number
  quantity: number
  unitPrice: number
  lineTotal: number
  fulfilledQuantity: number
  priceOverrideReason: string | null
}

export type BillingPayment = {
  id: string
  kind: string
  method: PaymentMethod
  amount: number
  note: string
  createdAt: string
  cashReceived: number | null
  changeGiven: number | null
  idempotencyKey: string
}

export type BillingAccount = {
  id: string
  patientId: string | null
  patientName: string | null
  consultationId: string | null
  currency: string
  status: AccountStatus
  paymentStatus: string
  version: number
  createdAt: string
  updatedAt: string
  total: number
  amountPaid: number
  balanceDue: number
  lines: BillingLine[]
  payments: BillingPayment[]
}

export type AccountReceipt = {
  account: BillingAccount
  payment: BillingPayment | null
}

export class CommerceError extends Error {
  readonly status: number
  readonly code: string | null

  constructor(message: string, status: number, code: string | null = null) {
    super(message)
    this.name = 'CommerceError'
    this.status = status
    this.code = code
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await apiFetch(path, init)
  const text = await response.text()
  let body: unknown
  if (text) {
    try {
      body = JSON.parse(text) as unknown
    } catch {
      if (response.ok) throw new Error('The commerce service returned an invalid response.')
    }
  }

  if (!response.ok) {
    const problem = body && typeof body === 'object' ? body as Record<string, unknown> : {}
    const message = [problem.error, problem.detail, problem.title].find((value): value is string => typeof value === 'string')
      ?? `The request failed (${response.status}).`
    const code = typeof problem.code === 'string' ? problem.code : null
    if (response.status === 409) {
      throw new CommerceError(`${message} Reload and review the latest account or inventory before trying again.`, response.status, code)
    }
    throw new CommerceError(message, response.status, code)
  }

  if (response.status === 204 || !text) return undefined as T
  return body as T
}

function jsonBody(value: unknown): RequestInit {
  return { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(value) }
}

function versionedBody(value: Record<string, unknown>, version: number): RequestInit {
  return jsonBody({ ...value as object, expectedVersion: version })
}

export const commerceApi = {
  listCatalog(kind: CommerceKind, includeArchived = false) {
    const collection = kind === 'Product' ? 'products' : 'services'
    return request<CatalogItem[]>(`/api/catalog/${collection}?includeArchived=${includeArchived}`)
  },
  saveCatalogItem(kind: CommerceKind, input: CatalogItemInput, id?: string, version?: number) {
    const collection = kind === 'Product' ? 'products' : 'services'
    const body = kind === 'Product'
      ? { ...input, description: null }
      : { ...input, sku: null, stockOnHand: 0, lowStockThreshold: 0, trackStock: false }
    return request<CatalogItem>(`/api/catalog/${collection}${id ? `/${encodeURIComponent(id)}${version ? `?expectedVersion=${version}` : ''}` : ''}`, {
      method: id ? 'PUT' : 'POST', ...jsonBody(body),
    })
  },
  archiveCatalogItem(kind: CommerceKind, id: string) {
    const collection = kind === 'Product' ? 'products' : 'services'
    return request<void>(`/api/catalog/${collection}/${encodeURIComponent(id)}`, { method: 'DELETE' })
  },
  setFavorite(item: CatalogItem, isFavorite: boolean) {
    if (item.isArchived) throw new CommerceError('Archived catalog items cannot be changed. Reload the catalog before trying again.', 409)
    const body: CatalogItemInput = {
      name: item.name,
      category: item.category,
      description: item.description,
      sku: item.sku ?? '',
      unit: item.unit,
      unitPrice: item.unitPrice,
      stockOnHand: item.stockOnHand,
      lowStockThreshold: item.lowStockThreshold,
      trackStock: item.trackStock,
      isFavorite,
    }
    return this.saveCatalogItem(item.type, body, item.id, item.version)
  },
  getInventory() {
    return request<CatalogItem[]>('/api/inventory')
  },
  getInventoryHistory(productId?: string) {
    const query = new URLSearchParams({ take: '250' })
    if (productId) query.set('productId', productId)
    return request<InventoryTransaction[]>(`/api/inventory/movements?${query}`)
  },
  adjustStock(input: InventoryAdjustment) {
    return request<{ productId: string; stockOnHand: number }>('/api/inventory/adjustments', {
      method: 'POST', ...jsonBody(input),
    })
  },
  listAccounts(consultationId?: string) {
    const query = consultationId ? `?consultationId=${encodeURIComponent(consultationId)}` : ''
    return request<BillingAccount[]>(`/api/billing/accounts${query}`)
  },
  getAccount(id: string) {
    return request<BillingAccount>(`/api/billing/accounts/${encodeURIComponent(id)}`)
  },
  async getConsultationAccount(consultationId: string) {
    const accounts = await this.listAccounts(consultationId)
    const account = accounts.find(item => item.consultationId === consultationId)
    if (!account) throw new CommerceError('No account has been created for this consultation yet.', 404, 'account-not-found')
    return account
  },
  createAccount(input: { patientId: string | null; consultationId?: string | null }) {
    return request<BillingAccount>('/api/billing/accounts', {
      method: 'POST', ...jsonBody({ patientId: input.patientId, consultationId: input.consultationId ?? null, currency: 'MXN' }),
    })
  },
  addAccountLine(account: BillingAccount, item: CatalogItem, quantity: number) {
    return request<BillingAccount>(`/api/billing/accounts/${encodeURIComponent(account.id)}/lines`, {
      method: 'POST',
      ...jsonBody({ catalogItemId: item.id, itemType: item.type, quantity, expectedVersion: account.version, expectedCatalogVersion: item.version }),
    })
  },
  updateAccountLine(account: BillingAccount, lineId: string, quantity: number, override?: { unitPrice: number; priceOverrideReason: string }) {
    return request<BillingAccount>(`/api/billing/accounts/${encodeURIComponent(account.id)}/lines/${encodeURIComponent(lineId)}`, {
      method: 'PUT', ...versionedBody({ quantity, unitPrice: override?.unitPrice ?? null, priceOverrideReason: override?.priceOverrideReason ?? null }, account.version),
    })
  },
  removeAccountLine(account: BillingAccount, lineId: string) {
    return request<BillingAccount>(`/api/billing/accounts/${encodeURIComponent(account.id)}/lines/${encodeURIComponent(lineId)}?expectedVersion=${account.version}`, {
      method: 'DELETE',
    })
  },
  openAccount(account: BillingAccount) {
    return request<BillingAccount>(`/api/billing/accounts/${encodeURIComponent(account.id)}/open`, {
      method: 'POST', ...versionedBody({}, account.version),
    })
  },
  payAccount(account: BillingAccount, input: { amount: number; method: PaymentMethod; idempotencyKey: string; note?: string; expectedVersion?: number; cashReceived: number | null }) {
    return request<BillingAccount>(`/api/billing/accounts/${encodeURIComponent(account.id)}/payments`, {
      method: 'POST',
      ...versionedBody({ amount: input.amount, method: input.method, idempotencyKey: input.idempotencyKey, note: input.note ?? null, cashReceived: input.cashReceived }, input.expectedVersion ?? account.version),
    })
  },
  fulfillAccount(account: BillingAccount) {
    return request<BillingAccount>(`/api/billing/accounts/${encodeURIComponent(account.id)}/fulfill`, {
      method: 'POST', ...versionedBody({}, account.version),
    })
  },
}
