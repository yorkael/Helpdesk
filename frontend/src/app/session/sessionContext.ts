import { createContext, useContext } from 'react'
import { useStore } from 'zustand'
import type { ApiClient } from '../../lib/api/apiClient'
import type { SessionManager } from '../../lib/auth/sessionManager'
import type { SessionState } from '../../lib/auth/sessionStore'
import type { AppSession } from './createAppSession'

export const SessionContext = createContext<AppSession | null>(null)

function useAppSession(): AppSession {
  const session = useContext(SessionContext)
  if (session === null) {
    throw new Error('Session hooks need a SessionProvider above them.')
  }
  return session
}

export function useSessionState<T>(selector: (state: SessionState) => T): T {
  return useStore(useAppSession().store, selector)
}

export function useSessionManager(): SessionManager {
  return useAppSession().manager
}

export function useApiClient(): ApiClient {
  return useAppSession().api
}
