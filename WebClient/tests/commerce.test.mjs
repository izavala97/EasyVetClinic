import test from 'node:test'
import assert from 'node:assert/strict'
import { canFulfillAccount, confirmedPayment, createSingleFlight, outstandingProductQuantity, parsePendingAdjustment, parsePendingPayment, preserveAccountPatientContext, validCurrencyAmount } from '../src/commerceOperations.ts'

const operation = {
  amount: 50,
  method: 'Card',
  idempotencyKey: 'retry-key',
  expectedVersion: 3,
  note: 'terminal reference',
  cashReceived: null,
}
const payment = { id: 'recorded', idempotencyKey: 'retry-key', kind: 'Payment', method: 'Card', amount: 50, note: 'terminal reference', cashReceived: null, changeGiven: null, createdAt: '2026-10-08T15:00:00Z' }

test('a restored payment preserves its original key, payload and version', () => {
  assert.deepEqual(parsePendingPayment(JSON.stringify(operation)), operation)
})

test('a restored stock adjustment preserves its key and signed quantity', () => {
  const adjustment = { productId: 'product', quantityChange: -2.5, reason: 'Correction', idempotencyKey: 'stock-key' }
  assert.deepEqual(parsePendingAdjustment(JSON.stringify(adjustment)), adjustment)
})

test('cash tender survives restoring the same payment operation', () => {
  const cash = { ...operation, method: 'Cash', cashReceived: 100 }
  assert.deepEqual(parsePendingPayment(JSON.stringify(cash)), cash)
  assert.throws(() => parsePendingPayment(JSON.stringify({ ...cash, cashReceived: 25 })))
})

test('invalid stored payments cannot become new payments', () => {
  for (const invalid of [null, {}, { ...operation, amount: 0 }, { ...operation, amount: 0.001 }, { ...operation, method: 'Unknown' }, { ...operation, expectedVersion: 0 }, { ...operation, idempotencyKey: '' }]) {
    assert.throws(() => parsePendingPayment(JSON.stringify(invalid)))
  }
})

test('invalid stored stock operations cannot be replayed', () => {
  for (const invalid of [null, {}, { productId: 'product', quantityChange: 0, reason: 'Correction', idempotencyKey: 'stock-key' }]) {
    assert.throws(() => parsePendingAdjustment(JSON.stringify(invalid)))
  }
})

test('receipt payment is identified by its key even alongside identical split payments', () => {
  const unrelated = { ...payment, id: 'another', idempotencyKey: 'another-key' }
  assert.equal(confirmedPayment([payment, unrelated], operation).id, 'recorded')
})

test('an earlier payment or refund is not used as a successful payment receipt', () => {
  assert.throws(() => confirmedPayment([{ ...payment, idempotencyKey: 'earlier-key' }], operation))
  assert.throws(() => confirmedPayment([{ ...payment, kind: 'Refund' }], operation))
  assert.throws(() => confirmedPayment([], operation))
})

test('ambiguous payment responses fail explicitly instead of picking the last payment', () => {
  assert.throws(() => confirmedPayment([payment, { ...payment, id: 'duplicate' }], operation))
})

test('cash receipt uses server-persisted tender and change without recalculating a fallback', () => {
  const cashOperation = { ...operation, method: 'Cash', cashReceived: 100 }
  const persisted = { ...payment, method: 'Cash', cashReceived: 100, changeGiven: 50 }
  assert.deepEqual(confirmedPayment([persisted], cashOperation), persisted)
  assert.throws(() => confirmedPayment([{ ...persisted, cashReceived: 75 }], cashOperation))
  assert.throws(() => confirmedPayment([{ ...persisted, changeGiven: null }], cashOperation))
})

test('currency validation permits exact cents, rejects excess precision and unsafe totals', () => {
  for (const amount of [0, 0.29, 1.01, 50.75]) assert.equal(validCurrencyAmount(amount), true)
  for (const amount of [-1, NaN, Infinity, 0.001, Number.MAX_SAFE_INTEGER]) assert.equal(validCurrencyAmount(amount), false)
})

test('concurrent StrictMode account loads share one lookup and creation operation', async () => {
  const load = createSingleFlight()
  let lookups = 0
  let creations = 0
  let resolveLookup
  const lookup = new Promise(resolve => { resolveLookup = resolve })
  const operation = async () => {
    lookups++
    await lookup
    creations++
    return { id: 'linked-account' }
  }
  const first = load('clinic:consultation:patient', operation)
  const second = load('clinic:consultation:patient', operation)
  assert.equal(first, second)
  resolveLookup()
  assert.deepEqual(await Promise.all([first, second]), [{ id: 'linked-account' }, { id: 'linked-account' }])
  assert.equal(lookups, 1)
  assert.equal(creations, 1)
})

test('completed account loads are not cached across subsequent visits', async () => {
  const load = createSingleFlight()
  let requests = 0
  const operation = async () => ++requests
  assert.equal(await load('clinic:consultation', operation), 1)
  assert.equal(await load('clinic:consultation', operation), 2)
})

test('failed account loads release the pending operation for an explicit retry', async () => {
  const load = createSingleFlight()
  await assert.rejects(load('clinic:consultation', async () => { throw new Error('Unavailable') }), /Unavailable/)
  assert.equal(await load('clinic:consultation', async () => 'recovered'), 'recovered')
})

test('different clinic or consultation scopes never share account creation', async () => {
  const load = createSingleFlight()
  assert.deepEqual(await Promise.all([
    load('clinic-a:consultation', async () => 'a'),
    load('clinic-b:consultation', async () => 'b'),
    load('clinic-a:other-consultation', async () => 'c'),
  ]), ['a', 'b', 'c'])
})

test('linked patient name survives a mutation without overriding server financial values', () => {
  const previous = { id: 'account', patientId: 'patient', consultationId: 'consultation', patientName: 'Luna', total: 0, version: 1 }
  const updated = { ...previous, patientName: null, total: 150, version: 2 }
  assert.deepEqual(preserveAccountPatientContext(previous, updated), { ...updated, patientName: 'Luna' })
})

test('patient context never leaks to a different account, patient or consultation', () => {
  const previous = { id: 'account', patientId: 'patient', consultationId: 'consultation', patientName: 'Luna' }
  for (const changed of [{ id: 'other' }, { patientId: 'other' }, { consultationId: 'other' }, { patientId: null }]) {
    const updated = { ...previous, ...changed, patientName: null }
    assert.equal(preserveAccountPatientContext(previous, updated), updated)
  }
  const renamed = { ...previous, patientName: 'Luna updated' }
  assert.equal(preserveAccountPatientContext(previous, renamed), renamed)
})

test('draft product fulfillment is allowed before payment and excludes quantities already dispensed', () => {
  const product = { id: 'first', itemType: 'Product', catalogItemId: 'product', quantity: 2, fulfilledQuantity: 0 }
  const draft = { status: 'Draft', balanceDue: 100, lines: [product] }
  assert.equal(canFulfillAccount(draft), true)
  const fulfilled = { ...draft, lines: [{ ...product, fulfilledQuantity: 2 }] }
  assert.equal(canFulfillAccount(fulfilled), false)
  assert.equal(outstandingProductQuantity(fulfilled.lines, 'product'), 0)
  const additional = { ...fulfilled, lines: [...fulfilled.lines, { ...product, id: 'second', quantity: 1 }] }
  assert.equal(canFulfillAccount(additional), true)
  assert.equal(outstandingProductQuantity(additional.lines, 'product'), 1)
  assert.equal(outstandingProductQuantity(additional.lines, 'product', 'second'), 0)
  assert.equal(canFulfillAccount({ ...additional, status: 'Voided' }), false)
  assert.equal(canFulfillAccount({ status: 'Draft', lines: [{ ...product, itemType: 'Service' }] }), false)
})
