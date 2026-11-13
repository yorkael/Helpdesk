import { ApiError } from '../api/ApiError'
import type { SessionPort } from '../api/apiClient'
import type { AuthApi } from './authApi'
import type { RefreshLock } from './refreshLock'
import type { RefreshTokenStorage } from './refreshTokenStorage'
import type { SessionEndReason, SessionStore } from './sessionStore'
import type { AuthTokens, Credentials } from './types'

// RefreshTokenMaxLength in the API's validators: a longer stored value can only be corrupt.
const MAX_REFRESH_TOKEN_LENGTH = 128

export interface SessionManagerDependencies {
  authApi: AuthApi
  storage: RefreshTokenStorage
  lock: RefreshLock
  store: SessionStore
  /** In the app, a 15-second AbortSignal.timeout, so a hung refresh cannot hold the lock forever. */
  createTimeoutSignal: () => AbortSignal
}

export interface SessionManager extends SessionPort {
  expire(): Promise<void>
  /** Runs once per manager: later calls share the first one, so a double mount cannot refresh twice. */
  restore(): Promise<void>
  /**
   * Starts the restore over, only on the user's request. If the failed attempt timed out after the server had
   * already rotated the token, this presents a used token and the API revokes every session of the user.
   */
  retryRestore(): Promise<void>
  login(credentials: Credentials): Promise<void>
  logout(): Promise<void>
  onEnded(listener: (reason: SessionEndReason) => void): () => void
}

const isUnauthorized = (error: unknown) => error instanceof ApiError && error.status === 401

const isWellFormed = (token: string) => token.length > 0 && token.length <= MAX_REFRESH_TOKEN_LENGTH

export function createSessionManager({
  authApi,
  storage,
  lock,
  store,
  createTimeoutSignal,
}: SessionManagerDependencies): SessionManager {
  // The access token lives only in this closure, never in the store or in React state.
  let accessToken: string | null = null
  // The refresh token this tab stored last, so expire() never deletes one that another tab stored since.
  let knownRefreshToken: string | null = null
  let inflight: Promise<void> | null = null
  let restoring: Promise<void> | null = null
  const listeners = new Set<(reason: SessionEndReason) => void>()

  function clearSession(reason: SessionEndReason): void {
    accessToken = null
    knownRefreshToken = null
    store.setState({ status: 'anonymous', user: null, endReason: reason })
    for (const listener of listeners) {
      listener(reason)
    }
  }

  async function exchange(sent: string): Promise<AuthTokens> {
    const tokens = await authApi.refresh(sent, createTimeoutSignal())
    // Saved before the lock is released: whoever holds it next, in this tab or another, must read the new token.
    storage.set(tokens.refreshToken)
    knownRefreshToken = tokens.refreshToken
    return tokens
  }

  // The API revokes every session of the user when it sees a refresh token twice, so the token is read and
  // replaced only while holding the lock that every tab shares. Resolves with null when the session is over.
  async function rotateInsideLock(): Promise<AuthTokens | null> {
    const stored = storage.get()
    if (stored === null || !isWellFormed(stored)) {
      storage.remove()
      return null
    }

    let sent = stored
    // One more try only if a different token appeared in storage meanwhile: another tab signed in without the
    // shared lock (the fallback without Web Locks). Never for the token that was just rejected.
    for (let retriesLeft = 1; ; retriesLeft--) {
      try {
        return await exchange(sent)
      } catch (error) {
        if (!isUnauthorized(error)) throw error
      }

      const current = storage.get()
      if (retriesLeft === 0 || current === null || current === sent || !isWellFormed(current)) {
        if (current === sent) {
          storage.remove()
        }
        return null
      }
      sent = current
    }
  }

  async function loadUser(token: string): Promise<void> {
    try {
      const user = await authApi.me(token)
      store.setState({ status: 'authenticated', user, endReason: null })
    } catch (error) {
      if (isUnauthorized(error)) {
        await expire()
      } else if (store.getState().status !== 'authenticated') {
        // Nothing on screen yet. A session already on screen keeps its user: its new access token is valid.
        store.setState({ status: 'unreachable' })
      }
    }
  }

  // Any failure other than a 401 propagates and is never retried here: the server may already have rotated the
  // token, and presenting the old one again would revoke every session of the user.
  async function runRefresh(): Promise<void> {
    const tokens = await lock.run(rotateInsideLock)
    if (tokens === null) {
      clearSession('expired')
      return
    }
    accessToken = tokens.accessToken
    await loadUser(tokens.accessToken)
  }

  function refresh(): Promise<void> {
    inflight ??= runRefresh().finally(() => {
      inflight = null
    })
    return inflight
  }

  async function expire(): Promise<void> {
    const known = knownRefreshToken
    clearSession('expired')
    await lock.run(async () => {
      if (known !== null && storage.get() === known) {
        storage.remove()
      }
    })
  }

  async function runRestore(): Promise<void> {
    if (storage.get() === null) {
      store.setState({ status: 'anonymous', user: null, endReason: null })
      return
    }
    try {
      await refresh()
    } catch {
      store.setState({ status: 'unreachable' })
    }
  }

  function restore(): Promise<void> {
    restoring ??= runRestore()
    return restoring
  }

  async function login(credentials: Credentials): Promise<void> {
    const loginTokens = await authApi.login(credentials)
    await lock.run(async () => storage.set(loginTokens.refreshToken))
    knownRefreshToken = loginTokens.refreshToken
    accessToken = loginTokens.accessToken
    await loadUser(loginTokens.accessToken)
  }

  // The local session ends first, inside the lock, so no tab can present the token after this point. The API
  // call is best effort: the user is signed out here even when it fails.
  async function logout(): Promise<void> {
    const signedOutToken = await lock.run(async () => {
      const token = storage.get()
      storage.remove()
      return token
    })
    clearSession('signedOut')
    if (signedOutToken !== null) {
      await authApi.logout(signedOutToken).catch(() => undefined)
    }
  }

  return {
    accessToken: () => accessToken,
    refresh,
    expire,
    restore,
    retryRestore() {
      restoring = null
      return restore()
    },
    login,
    logout,
    onEnded(listener) {
      listeners.add(listener)
      return () => {
        listeners.delete(listener)
      }
    },
  }
}
