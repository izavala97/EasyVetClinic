import test from 'node:test'
import assert from 'node:assert/strict'
import i18n from '../src/i18n.ts'
import { formatMoney } from '../src/format.ts'
import { printDocument, printReceipt } from '../src/printing.ts'

function printWindow() {
  const element = tag => ({
    tag, textContent: '', children: [],
    style: {},
    append(child) { this.children.push(child) },
    replaceChildren(...children) { this.children = children },
    addEventListener() {},
  })
  const popup = {
    opener: {}, prints: 0, closed: false,
    document: { documentElement: {}, title: '', head: element('head'), body: element('body'), createElement: element },
    focus() {},
    print() { this.prints++ },
    close() { this.closed = true },
  }
  return popup
}

const content = node => [node.textContent, ...node.children.map(content)].join('\n')

function installWindow(t, value) {
  const original = globalThis.window
  globalThis.window = value
  t.after(() => {
    if (original === undefined) delete globalThis.window
    else globalThis.window = original
    i18n.changeLanguage('en')
  })
}

test('printed document labels follow the clinic language while entered text is preserved', async t => {
  let popup
  installWindow(t, { open: () => popup })
  for (const language of ['en', 'es']) {
    i18n.changeLanguage(language)
    popup = printWindow()
    await printDocument(async () => ({
      title: 'Health certificate',
      fields: { Patient: 'Luna <script>', Species: 'Feline', Date: '2026-10-09', Notes: 'Save clinic' },
      preparedBy: 'María test',
    }))
    assert.equal(popup.document.documentElement.lang, language)
    assert.equal(popup.document.title, language === 'es' ? 'Certificado de salud' : 'Health certificate')
    const text = content(popup.document.body)
    assert.ok(text.includes(language === 'es' ? 'Especie: Felino' : 'Species: Feline'))
    assert.ok(text.includes('Luna <script>'))
    assert.ok(text.includes('Save clinic'))
    assert.ok(text.includes('María test'))
    assert.equal(popup.opener, null)
    assert.equal(popup.prints, 1)
  }
})

test('receipts translate labels and payment methods without changing financial or entered values', t => {
  let popup
  installWindow(t, { open: () => popup })
  const receipt = {
    account: {
      id: 'account-test', patientId: 'patient-test', patientName: 'Luna test', currency: 'EUR',
      updatedAt: '2026-10-09T17:00:00Z', total: 100, balanceDue: 60,
      lines: [{ name: '<img src=x>', quantity: 2, unitPrice: 50, lineTotal: 100 }],
    },
    payment: {
      id: 'payment-test', kind: 'Payment', method: 'Cash', amount: 40,
      createdAt: '2026-10-09T17:00:00Z', note: 'Save clinic', cashReceived: 50, changeGiven: 10,
    },
  }
  for (const language of ['en', 'es']) {
    i18n.changeLanguage(language)
    popup = printWindow()
    printReceipt(receipt)
    const text = content(popup.document.body)
    assert.equal(popup.document.documentElement.lang, language)
    assert.ok(text.includes(language === 'es' ? 'Efectivo' : 'Cash'))
    assert.ok(text.includes(language === 'es' ? 'Cambio:' : 'Change:'))
    for (const amount of [100, 60, 50, 40, 10]) assert.ok(text.includes(formatMoney(amount, 'EUR')))
    for (const entered of ['<img src=x>', 'Luna test', 'Save clinic', 'payment-test']) assert.ok(text.includes(entered))
    assert.equal(popup.opener, null)
    assert.equal(popup.prints, 1)
    assert.ok(!popup.document.body.children.some(child => child.tag === 'img'))
  }
  receipt.account.patientName = null
  popup = printWindow()
  printReceipt({ ...receipt, payment: null })
  const text = content(popup.document.body)
  assert.ok(text.includes(i18n.t('Linked patient ({{id}})', { id: 'patient-test' })))
  assert.ok(text.includes(i18n.t('Fulfillment receipt')))
})

test('printing reports blocked popups and closes failed document previews', async t => {
  let alert
  installWindow(t, { open: () => null, alert: message => { alert = message } })
  i18n.changeLanguage('es')
  await assert.rejects(printDocument(async () => ({})), /ventanas emergentes/)
  printReceipt({ account: {} })
  assert.ok(alert.includes('ventanas emergentes'))
  const popup = printWindow()
  globalThis.window.open = () => popup
  const failure = new Error('Document API failed')
  await assert.rejects(printDocument(async () => { throw failure }), error => error === failure)
  assert.equal(popup.closed, true)
})
