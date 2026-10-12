import { useEffect, useState, type ReactNode } from 'react'
import { useTranslation } from './useTranslation'
import { useAuthentication } from './auth'
import { getJson, type ClinicProfile } from './api'
import i18n from './i18n'

export function ClinicLanguage({ children }: { children: ReactNode }) {
  const { currentUser } = useAuthentication()
  const { t, language } = useTranslation()
  const clinicId = currentUser?.isSuspended && !currentUser.isPlatformOwner ? null : currentUser?.clinicId ?? null
  const [loadedClinic, setLoadedClinic] = useState<string | null>(null)
  const [error, setError] = useState('')
  const [retry, setRetry] = useState(0)

  useEffect(() => { document.documentElement.lang = language }, [language])

  useEffect(() => {
    let active = true
    setError('')
    if (!clinicId) {
      void i18n.changeLanguage('en')
      setLoadedClinic(null)
      return
    }
    void getJson<ClinicProfile>('/api/clinic').then(profile => {
      if (!active) return
      i18n.changeLanguage(profile.language)
      if (active) setLoadedClinic(clinicId)
    }).catch((cause: unknown) => {
      if (active) setError(cause instanceof Error ? cause.message : 'Clinic language could not be loaded.')
    })
    return () => { active = false }
  }, [clinicId, retry])

  if (clinicId && error) return <main className="authentication-shell"><section className="panel">
    <p className="notice error" role="alert">{t(error)}</p>
    <button className="secondary-action" onClick={() => setRetry(value => value + 1)}>{t('Retry')}</button>
  </section></main>
  if (clinicId && loadedClinic !== clinicId) return <main className="authentication-shell"><p>{t('Loading clinic settings...')}</p></main>
  return children
}
