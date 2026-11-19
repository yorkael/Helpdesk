import { vi } from 'vitest'
import { createAuthApi, type AuthApi } from '../lib/auth/authApi'
import { createRefreshLock, type RefreshLock } from '../lib/auth/refreshLock'
import { createRefreshTokenStorage, REFRESH_TOKEN_KEY } from '../lib/auth/refreshTokenStorage'
import { createSessionManager } from '../lib/auth/sessionManager'
import { createSessionStore, type SessionEndReason } from '../lib/auth/sessionStore'
import type { FakeAuthServer } from './fakeAuthServer'

interface TabOptions {
  /** Share one lock between tabs to model Web Locks; give each tab its own to model the fallback. */
  lock?: RefreshLock
  createTimeoutSignal?: () => AbortSignal
  /** Replaces calls of the real API client, so a manager rule can be tested without the client's own checks. */
  authApi?: Partial<AuthApi>
}

// One browser tab: its own manager, store and memory, sharing jsdom's localStorage with every other tab.
export function createTab(server: FakeAuthServer, { lock, createTimeoutSignal, authApi }: TabOptions = {}) {
  const store = createSessionStore()
  const manager = createSessionManager({
    authApi: { ...createAuthApi(server.fetch), ...authApi },
    storage: createRefreshTokenStorage(() => localStorage),
    lock: lock ?? createRefreshLock(undefined),
    store,
    createTimeoutSignal: createTimeoutSignal ?? (() => new AbortController().signal),
  })
  const ended = vi.fn<(reason: SessionEndReason) => void>()
  manager.onEnded(ended)
  return { manager, store, ended }
}

// A tab that loaded with a saved session, without going through login(), so tests of other rules do not depend
// on login storing the token. The server's observations start after the restore.
export async function createRestoredTab(server: FakeAuthServer, options: TabOptions = {}) {
  localStorage.setItem(REFRESH_TOKEN_KEY, server.signIn().refreshToken)
  const tab = createTab(server, options)
  await tab.manager.restore()
  server.resetObservations()
  return tab
}
