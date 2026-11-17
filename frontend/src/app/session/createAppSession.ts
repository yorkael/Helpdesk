import { createApiClient, type ApiClient } from '../../lib/api/apiClient'
import type { Fetch } from '../../lib/api/http'
import { createAuthApi } from '../../lib/auth/authApi'
import { createRefreshLock } from '../../lib/auth/refreshLock'
import { createRefreshTokenStorage } from '../../lib/auth/refreshTokenStorage'
import { createSessionManager, type SessionManager } from '../../lib/auth/sessionManager'
import { createSessionStore, type SessionStore } from '../../lib/auth/sessionStore'

// Long enough for a slow network, short enough that a hung refresh does not hold the lock for every tab.
const REFRESH_TIMEOUT_MS = 15_000

export interface AppSession {
  manager: SessionManager
  store: SessionStore
  api: ApiClient
}

// Only builds objects: no request, listener or timer starts here. StrictMode runs a useState initializer twice
// and discards one result, so anything started here would leak from the discarded session. Side effects go in
// effects with a cleanup, as SessionProvider does.
export function createAppSession(): AppSession {
  // Reads the global on every call instead of capturing it, so tests can stub fetch.
  const fetchFn: Fetch = (input, init) => globalThis.fetch(input, init)
  const store = createSessionStore()
  const manager = createSessionManager({
    authApi: createAuthApi(fetchFn),
    storage: createRefreshTokenStorage(() => window.localStorage),
    // Undefined outside secure contexts and in jsdom; the lock then falls back to one tab.
    lock: createRefreshLock(navigator.locks),
    store,
    createTimeoutSignal: () => AbortSignal.timeout(REFRESH_TIMEOUT_MS),
  })
  // Only the three calls the client needs, so it cannot sign in or out; each one asks the manager at call time.
  const api = createApiClient(fetchFn, {
    accessToken: () => manager.accessToken(),
    refresh: () => manager.refresh(),
    expire: () => manager.expire(),
  })
  return { manager, store, api }
}
