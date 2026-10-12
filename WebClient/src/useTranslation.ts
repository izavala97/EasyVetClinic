import { useSyncExternalStore } from 'react'
import i18n, { subscribeLanguage } from './i18n'

export function useTranslation() {
  const language = useSyncExternalStore(subscribeLanguage, () => i18n.language)
  return { t: i18n.t, language }
}
