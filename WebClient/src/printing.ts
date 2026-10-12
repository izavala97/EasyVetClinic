import type { AccountReceipt } from './commerce'
import i18n from './i18n.ts'
import { formatDate, formatDateTime, formatMoney } from './format.ts'

export type PrintableDocument = { title: string; fields: Record<string, string>; preparedBy: string }

export async function printDocument(load: () => Promise<PrintableDocument>) {
  const preview = window.open('', '_blank')
  if (!preview) throw new Error(i18n.t('Allow pop-ups to print the document.'))
  preview.opener = null
  preview.document.documentElement.lang = i18n.language
  preview.document.title = i18n.t('Preparing document')
  preview.document.body.textContent = i18n.t('Preparing document...')
  try {
    const draft = await load()
    preview.document.title = i18n.t(draft.title)
    const content = preview.document.createElement('pre')
    content.style.whiteSpace = 'pre-wrap'
    content.style.font = '16px system-ui'
    content.textContent = [i18n.t(draft.title), i18n.t('Draft — review before signing'), '',
      ...Object.entries(draft.fields).map(([key, value]) =>
        `${i18n.t(key)}: ${key === 'Species' ? i18n.t(value) : key === 'Date' ? formatDate(value) : value}`),
      '', i18n.t('Prepared by {{name}}', { name: draft.preparedBy })].join('\n')
    preview.document.body.replaceChildren(content)
    preview.focus()
    preview.print()
  } catch (cause) {
    preview.close()
    throw cause
  }
}

export function printReceipt(receipt: AccountReceipt) {
  const t = i18n.t
  const account = receipt.account
  const popup = window.open('', '_blank', 'width=720,height=800')
  if (!popup) {
    window.alert(t('The receipt window was blocked. Allow pop-ups for this site and try again.'))
    return
  }
  popup.opener = null
  const doc = popup.document
  doc.documentElement.lang = i18n.language
  doc.title = t('EasyVetClinic receipt')
  const style = doc.createElement('style')
  style.textContent = 'body{font:16px Arial,sans-serif;max-width:680px;margin:40px auto;color:#222}h1{font-size:24px}table{width:100%;border-collapse:collapse;margin:24px 0}th,td{text-align:left;padding:10px;border-bottom:1px solid #ccc}td:last-child,th:last-child{text-align:right}@media print{button{display:none}}'
  doc.head.append(style)
  const append = (parent: HTMLElement, tag: string, text = '') => {
    const element = doc.createElement(tag)
    element.textContent = text
    parent.append(element)
    return element
  }
  const money = (value: number) => formatMoney(value, account.currency)
  append(doc.body, 'h1', t('EasyVetClinic receipt'))
  append(doc.body, 'p', `${t('Account')} ${account.id} · ${formatDateTime(new Date(account.updatedAt))}`)
  append(doc.body, 'p', `${t('Patient')}: ${account.patientName ?? (account.patientId ? t('Linked patient ({{id}})', { id: account.patientId }) : t('Walk-in account'))}`)
  const table = append(doc.body, 'table')
  const header = append(append(table, 'thead'), 'tr')
  for (const label of ['Item', 'Qty', 'Unit price', 'Total']) append(header, 'th', t(label))
  const body = append(table, 'tbody')
  for (const line of account.lines) {
    const row = append(body, 'tr')
    for (const value of [line.name, String(line.quantity), money(line.unitPrice), money(line.lineTotal)]) append(row, 'td', value)
  }
  append(doc.body, 'p', `${t('Account total')}: ${money(account.total)}`)
  const payment = receipt.payment
  if (payment) {
    append(doc.body, 'h2', t(payment.kind))
    append(doc.body, 'p', `${t('Payment ID')}: ${payment.id}`)
    append(doc.body, 'p', formatDateTime(new Date(payment.createdAt)))
    append(doc.body, 'p', `${t(payment.method)} · ${money(payment.amount)}${payment.note ? ` · ${payment.note}` : ''}`)
    if (payment.cashReceived !== null) append(doc.body, 'p', `${t('Cash received')}: ${money(payment.cashReceived)}`)
    if (payment.changeGiven !== null) append(doc.body, 'p', `${t('Change')}: ${money(payment.changeGiven)}`)
  } else append(doc.body, 'p', t('Fulfillment receipt'))
  append(doc.body, 'p', `${t('Balance due')}: ${money(account.balanceDue)}`)
  const button = append(doc.body, 'button', t('Print receipt'))
  button.addEventListener('click', () => popup.print())
  popup.focus()
  popup.print()
}
