import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'
import { BrowserCacheLocation, InteractionRequiredAuthError, PublicClientApplication } from '@azure/msal-browser'
import { MsalProvider, useMsal } from '@azure/msal-react'
import { type CurrentUser, getJson, setAccessTokenProvider, setSelectedClinicId } from './api'
import { canLeaveWorkspace } from './navigationGuard'

const tenantId = import.meta.env.VITE_ENTRA_TENANT_ID
const clientId = import.meta.env.VITE_ENTRA_CLIENT_ID
const authorityHost = import.meta.env.VITE_ENTRA_AUTHORITY?.replace(/\/+$/, '')
const apiScope = import.meta.env.VITE_ENTRA_API_SCOPE
const authority = authorityHost && tenantId ? `${authorityHost}/${tenantId}` : undefined
const useDevelopmentAuthentication = import.meta.env.DEV

const msalInstance = tenantId && clientId && authority && apiScope
  ? new PublicClientApplication({
      auth: {
        clientId,
        authority,
        knownAuthorities: [new URL(authority).host],
        redirectUri: window.location.origin,
        postLogoutRedirectUri: window.location.origin,
      },
      cache: { cacheLocation: BrowserCacheLocation.LocalStorage },
    })
  : null

type AuthenticationContextValue = {
  configured: boolean
  isLoading: boolean
  currentUser: CurrentUser | null
  error: string | null
  signIn: () => Promise<void>
  signOut: () => Promise<void>
  refreshCurrentUser: () => Promise<void>
  selectClinic: (clinicId: string | null) => Promise<void>
}

const AuthenticationContext = createContext<AuthenticationContextValue | null>(null)

export async function initializeAuthentication() {
  if (window.location.pathname === '/invite' && window.location.hash.startsWith('#token=')) {
    sessionStorage.setItem('clinicInvitation', window.location.hash.slice('#token='.length))
    window.history.replaceState(null, '', '/invite')
  }
  await msalInstance?.initialize()
}

export function AuthenticationProvider({ children }: { children: ReactNode }) {
  if (useDevelopmentAuthentication) {
    return <DevelopmentAuthenticatedApplication>{children}</DevelopmentAuthenticatedApplication>
  }

  if (!msalInstance) {
    return <AuthenticationContext.Provider value={{
      configured: false,
      isLoading: false,
      currentUser: null,
      error: 'Authentication is not configured for this environment.',
      signIn: async () => undefined,
      signOut: async () => undefined,
      refreshCurrentUser: async () => undefined,
      selectClinic: async () => undefined,
    }}>{children}</AuthenticationContext.Provider>
  }

  return <MsalProvider instance={msalInstance}><AuthenticatedApplication>{children}</AuthenticatedApplication></MsalProvider>
}

function DevelopmentAuthenticatedApplication({ children }: { children: ReactNode }) {
  const [currentUser, setCurrentUser] = useState<CurrentUser | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  async function refreshCurrentUser() {
    setIsLoading(true)
    try {
      const user = await getJson<CurrentUser>('/api/me')
      setSelectedClinicId(user.clinicId)
      setCurrentUser(user)
      setError(null)
    } catch {
      setCurrentUser(null)
      setError('Your local clinic account could not be verified by the API.')
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    setAccessTokenProvider(undefined)
    void refreshCurrentUser()
  }, [])

  return <AuthenticationContext.Provider value={{
    configured: true,
    isLoading,
    currentUser,
    error,
    signIn: async () => undefined,
    signOut: async () => undefined,
    refreshCurrentUser,
    selectClinic: async (clinicId) => {
      if (!canLeaveWorkspace()) return
      setSelectedClinicId(clinicId)
      await refreshCurrentUser()
    },
  }}>{children}</AuthenticationContext.Provider>
}

function AuthenticatedApplication({ children }: { children: ReactNode }) {
  const { instance, accounts } = useMsal()
  const [currentUser, setCurrentUser] = useState<CurrentUser | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const account = instance.getActiveAccount() ?? accounts[0] ?? null

  async function getAccessToken() {
    if (!account) return null
    try {
      const result = await instance.acquireTokenSilent({ account, scopes: [apiScope] })
      return result.accessToken
    } catch (error) {
      if (error instanceof InteractionRequiredAuthError) {
        await instance.acquireTokenRedirect({ account, scopes: [apiScope] })
      }
      throw error
    }
  }

  async function refreshCurrentUser() {
    if (!account) {
      setCurrentUser(null)
      setIsLoading(false)
      return
    }

    setIsLoading(true)
    try {
      const user = await getJson<CurrentUser>('/api/me')
      setSelectedClinicId(user.clinicId)
      setCurrentUser(user)
      setError(null)
    } catch {
      setCurrentUser(null)
      setError('Your account could not be verified by the clinic service.')
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    if (account) instance.setActiveAccount(account)
    setAccessTokenProvider(getAccessToken)
    void refreshCurrentUser()
    return () => setAccessTokenProvider(undefined)
  }, [account?.homeAccountId])

  const value: AuthenticationContextValue = {
    configured: true,
    isLoading,
    currentUser,
    error,
    signIn: async () => {
      try {
        await instance.loginRedirect({ scopes: [apiScope] })
      } catch (error) {
        setError(error instanceof Error ? error.message : 'Sign-in could not be started.')
      }
    },
    signOut: async () => {
      if (!canLeaveWorkspace()) return
      setSelectedClinicId(null)
      await instance.logoutRedirect({ account: account ?? undefined })
    },
    refreshCurrentUser,
    selectClinic: async (clinicId) => {
      if (!canLeaveWorkspace()) return
      setSelectedClinicId(clinicId)
      await refreshCurrentUser()
    },
  }

  return <AuthenticationContext.Provider value={value}>{children}</AuthenticationContext.Provider>
}

export function useAuthentication() {
  const value = useContext(AuthenticationContext)
  if (!value) throw new Error('AuthenticationProvider is required.')
  return value
}