import { createStore, type StoreApi } from 'zustand/vanilla'
import type { CurrentUser } from './types'

export type SessionStatus = 'restoring' | 'authenticated' | 'anonymous' | 'unreachable'

// endedElsewhere: another tab removed the saved token, by signing out or on an expiry.
export type SessionEndReason = 'expired' | 'signedOut' | 'endedElsewhere'

// No tokens here: whatever React can read, a component or devtools could leak.
export interface SessionState {
  status: SessionStatus
  user: CurrentUser | null
  endReason: SessionEndReason | null
}

export type SessionStore = StoreApi<SessionState>

export function createSessionStore(): SessionStore {
  return createStore<SessionState>()(() => ({ status: 'restoring', user: null, endReason: null }))
}
