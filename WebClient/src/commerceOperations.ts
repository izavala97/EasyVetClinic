import type { BillingAccount, BillingLine, BillingPayment, InventoryAdjustment, PaymentMethod } from './commerce'

export function outstandingProductQuantity(lines: BillingLine[], productId: string, excludeLineId?: string): number {
  return lines.filter(line => line.itemType === 'Product' && line.catalogItemId === productId && line.id !== excludeLineId)
    .reduce((total, line) => total + Math.max(0, line.quantity - line.fulfilledQuantity), 0)
}

export function canFulfillAccount(account: BillingAccount): boolean {
  return account.status !== 'Voided' && account.lines.some(line => line.itemType === 'Product' && line.quantity > line.fulfilledQuantity)
}

export function preserveAccountPatientContext(previous: BillingAccount, updated: BillingAccount): BillingAccount {
  if (updated.id !== previous.id || updated.patientId !== previous.patientId ||
    updated.consultationId !== previous.consultationId || updated.patientName !== null) return updated
  return { ...updated, patientName: previous.patientName }
}

export function createSingleFlight<T>() {
  const pending = new Map<string, Promise<T>>()
  return (key: string, operation: () => Promise<T>): Promise<T> => {
    const existing = pending.get(key)
    if (existing) return existing
    const request = Promise.resolve().then(operation).finally(() => {
      if (pending.get(key) === request) pending.delete(key)
    })
    pending.set(key, request)
    return request
  }
}

export type PendingPayment = {
  amount: number
  method: PaymentMethod
  idempotencyKey: string
  expectedVersion: number
  note: string
  cashReceived: number | null
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
}

function nonemptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0
}

export function validCurrencyAmount(value: number): boolean {
  const cents = value * 100
  return Number.isFinite(value) && value >= 0 && Number.isSafeInteger(Math.round(cents)) &&
    Math.abs(cents - Math.round(cents)) < 0.000001
}

export function parsePendingAdjustment(stored: string): InventoryAdjustment {
  const input: unknown = JSON.parse(stored)
  if (!isRecord(input) || !nonemptyString(input.productId) || typeof input.quantityChange !== 'number' ||
    !Number.isFinite(input.quantityChange) || input.quantityChange === 0 || !nonemptyString(input.reason) || !nonemptyString(input.idempotencyKey)) {
    throw new Error('The stored pending stock movement is invalid. Reconcile it before recording another movement.')
  }
  return { productId: input.productId, quantityChange: input.quantityChange, reason: input.reason, idempotencyKey: input.idempotencyKey }
}

export function parsePendingPayment(stored: string): PendingPayment {
  const input: unknown = JSON.parse(stored)
  if (!isRecord(input) || typeof input.amount !== 'number' || !validCurrencyAmount(input.amount) || input.amount <= 0 ||
    !['Cash', 'Card', 'Transfer'].includes(String(input.method)) || !nonemptyString(input.idempotencyKey) ||
    typeof input.expectedVersion !== 'number' || !Number.isInteger(input.expectedVersion) || input.expectedVersion < 1 ||
    typeof input.note !== 'string' ||
    (input.method === 'Cash' ? typeof input.cashReceived !== 'number' || !validCurrencyAmount(input.cashReceived) || input.cashReceived < input.amount : input.cashReceived !== null)) {
    throw new Error('The stored pending payment is invalid. Reconcile it before recording another payment.')
  }
  const method = input.method
  if (method !== 'Cash' && method !== 'Card' && method !== 'Transfer') throw new Error('The stored payment method is invalid.')
  const cashReceived = input.cashReceived
  if (cashReceived !== null && typeof cashReceived !== 'number') throw new Error('The stored cash received is invalid.')
  return { amount: input.amount, method, idempotencyKey: input.idempotencyKey, expectedVersion: input.expectedVersion, note: input.note, cashReceived }
}

export function confirmedPayment(payments: BillingPayment[], operation: PendingPayment): BillingPayment {
  const candidates = payments.filter(payment => payment.idempotencyKey === operation.idempotencyKey &&
    payment.kind === 'Payment' && payment.method === operation.method && payment.amount === operation.amount &&
    payment.note === operation.note && payment.cashReceived === operation.cashReceived)
  if (candidates.length !== 1) throw new Error('The payment response could not be matched to this operation. Refresh and reconcile the account before recording another payment.')
  const payment = candidates[0]
  if (operation.method === 'Cash' && (payment.changeGiven === null || !validCurrencyAmount(payment.changeGiven))) {
    throw new Error('The cash payment response is missing valid persisted change. Reconcile the payment before proceeding.')
  }
  return payment
}
