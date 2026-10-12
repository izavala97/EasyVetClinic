import { spanish } from './locales/es.ts'

export const languages = [{ code: 'en', name: 'English' }, { code: 'es', name: 'Español' }] as const
export type Language = typeof languages[number]['code']
type Variables = Record<string, string | number>
const resources: Record<Language, Record<string, string>> = {
  en: {
    '{{count}} matching contacts._one': '{{count}} matching contact.',
    '{{count}} matching contacts._other': '{{count}} matching contacts.',
    '{{count}} lines_one': '{{count}} line',
    '{{count}} lines_other': '{{count}} lines',
    '{{count}} items match this view._one': '{{count}} item matches this view.',
    '{{count}} items match this view._other': '{{count}} items match this view.',
  },
  es: spanish,
}
const listeners = new Set<() => void>()
let language: Language = 'en'

export function translate(key: string, variables: Variables = {}, locale: Language = language): string {
  const plural = typeof variables.count === 'number' ? new Intl.PluralRules(locale).select(variables.count) : null
  const template = (plural && resources[locale][`${key}_${plural}`]) || resources[locale][key] ||
    (plural && resources.en[`${key}_${plural}`]) || resources.en[key] || key
  return template.replace(/\{\{(\w+)\}\}/g, (_, name: string) => {
    if (variables[name] === undefined) throw new Error(`Missing translation variable: ${name}`)
    return String(variables[name])
  })
}

export function subscribeLanguage(listener: () => void) {
  listeners.add(listener)
  return () => { listeners.delete(listener) }
}

const i18n = {
  get language() { return language },
  t: translate,
  changeLanguage(value: string) {
    const supported = languages.find(item => item.code === value)
    if (!supported) throw new Error(`Unsupported clinic language: ${value}`)
    if (supported.code === language) return
    language = supported.code
    for (const listener of listeners) listener()
  },
}

export default i18n
