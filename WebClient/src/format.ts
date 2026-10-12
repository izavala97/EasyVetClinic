import i18n from './i18n.ts'

export function formatMoney(value: number, currency = 'MXN') {
  return new Intl.NumberFormat(i18n.language, { style: 'currency', currency }).format(value)
}

export function formatDateTime(value: Date) {
  return new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(value)
}

export function formatDate(value: string) {
  return new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(new Date(`${value}T12:00:00`))
}
