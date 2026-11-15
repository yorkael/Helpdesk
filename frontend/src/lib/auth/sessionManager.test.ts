import { beforeEach, describe, expect, it } from 'vitest'
import { ANA, ANA_CREDENTIALS, createFakeAuthServer } from '../../test/fakeAuthServer'
import { problemResponse } from '../../test/responses'
import { createRestoredTab, createTab } from '../../test/sessionTab'
import type { AuthApi } from './authApi'
import { MalformedAuthResponseError } from './MalformedAuthResponseError'
import { REFRESH_TOKEN_KEY } from './refreshTokenStorage'

const storedToken = () => localStorage.getItem(REFRESH_TOKEN_KEY)
const seedStoredToken = (token: string) => localStorage.setItem(REFRESH_TOKEN_KEY, token)
const anaAsUser = { id: ANA.id, name: ANA.name, email: ANA.email, role: ANA.role }

describe('createSessionManager', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  describe('refresh', () => {
    it('shares one refresh among simultaneous callers', async () => {
      const server = createFakeAuthServer()
      const { manager } = await createRestoredTab(server)

      await Promise.all([manager.refresh(), manager.refresh(), manager.refresh()])

      expect(server.counters.refreshCalls).toBe(1)
    })

    it('calls the server again after a failed refresh', async () => {
      const server = createFakeAuthServer()
      // A pass-through lock, so only the manager's own in-flight promise is under test.
      const { manager, store } = await createRestoredTab(server, { lock: { run: (task) => task() } })
      server.setRefreshFailure(() => problemResponse(503))
      await expect(manager.refresh()).rejects.toMatchObject({ status: 503 })
      server.setRefreshFailure(null)
      const callsBefore = server.counters.refreshCalls

      await manager.refresh()

      expect(server.counters.refreshCalls).toBe(callsBefore + 1)
      expect(store.getState().status).toBe('authenticated')
    })

    it('keeps the session through a network failure, a timeout or a server error', async () => {
      const server = createFakeAuthServer()
      const { manager, store, ended } = await createRestoredTab(server)
      const tokenBefore = storedToken()
      const failures = [
        () => new TypeError('Failed to fetch'),
        () => new DOMException('The operation timed out.', 'TimeoutError'),
        () => problemResponse(503),
      ]

      for (const failure of failures) {
        server.setRefreshFailure(failure)
        await expect(manager.refresh()).rejects.toBeDefined()
        expect(storedToken()).toBe(tokenBefore)
        expect(store.getState().status).toBe('authenticated')
      }
      expect(ended).not.toHaveBeenCalled()
    })

    it('ends the session when the API rejects the refresh token', async () => {
      const server = createFakeAuthServer()
      const { manager, store, ended } = await createRestoredTab(server)
      server.expireRefreshToken(storedToken()!)

      await manager.refresh()

      expect(storedToken()).toBeNull()
      expect(manager.accessToken()).toBeNull()
      expect(store.getState()).toMatchObject({ status: 'anonymous', user: null, endReason: 'expired' })
      expect(ended).toHaveBeenCalledExactlyOnceWith('expired')
    })

    it('never repeats a refresh whose answer was lost, so the used token is not presented again', async () => {
      const server = createFakeAuthServer()
      const { manager } = await createRestoredTab(server)
      server.loseNextRefreshResponse()

      await manager.refresh().catch(() => undefined)

      expect(server.counters.refreshCalls).toBe(1)
      expect(server.counters.massRevocations).toBe(0)
    })

    it('tries a token that changed in storage at most once more', async () => {
      const server = createFakeAuthServer()
      const { manager } = createTab(server)
      seedStoredToken('unknown-0')
      let changes = 0
      server.hooks.beforeRefresh = () => {
        if (changes < 5) {
          changes += 1
          seedStoredToken(`unknown-${changes}`)
        }
      }

      await manager.refresh()

      expect(server.counters.refreshCalls).toBeLessThanOrEqual(2)
    })

    it('discards a malformed stored token without calling the API', async () => {
      const server = createFakeAuthServer()
      const { manager } = createTab(server)
      seedStoredToken('x'.repeat(129))

      await manager.refresh()

      expect(server.counters.refreshCalls).toBe(0)
      expect(storedToken()).toBeNull()
    })

    it('sends the refresh with the timeout signal', async () => {
      const server = createFakeAuthServer()
      const timeout = new AbortController()
      const { manager } = createTab(server, { createTimeoutSignal: () => timeout.signal })
      seedStoredToken(server.signIn().refreshToken)

      await manager.refresh()

      expect(server.observed.refreshSignals).toEqual([timeout.signal])
    })

    it('resolves with the new tokens in place when only /me fails on the network', async () => {
      const server = createFakeAuthServer()
      const { manager } = createTab(server)
      const seeded = server.signIn().refreshToken
      seedStoredToken(seeded)
      server.setMeFailure(() => new TypeError('Failed to fetch'))

      await expect(manager.refresh()).resolves.toBeUndefined()

      expect(manager.accessToken()).not.toBeNull()
      expect(storedToken()).not.toBe(seeded)
      expect(server.isActive(storedToken()!)).toBe(true)
    })

    it('keeps a session already on screen when only /me fails on the network', async () => {
      const server = createFakeAuthServer()
      const { manager, store } = await createRestoredTab(server)
      const userBefore = store.getState().user
      server.setMeFailure(() => new TypeError('Failed to fetch'))

      await manager.refresh()

      expect(store.getState().status).toBe('authenticated')
      expect(store.getState().user).toBe(userBefore)
    })

    it('ends the session when /me rejects the new access token', async () => {
      const server = createFakeAuthServer()
      const { manager, store } = await createRestoredTab(server)
      server.setMeFailure(() => problemResponse(401))

      await manager.refresh()

      expect(store.getState()).toMatchObject({ status: 'anonymous', endReason: 'expired' })
    })

    it('removes its own stored token when it expires the session', async () => {
      const server = createFakeAuthServer()
      const { manager } = await createRestoredTab(server)

      await manager.expire()

      expect(storedToken()).toBeNull()
    })
  })

  describe('restore', () => {
    it('restores once, however many times it is called', async () => {
      const server = createFakeAuthServer()
      const { manager } = createTab(server)
      seedStoredToken(server.signIn().refreshToken)

      await manager.restore()
      await manager.restore()

      expect(server.counters.refreshCalls).toBe(1)
      expect(server.counters.meCalls).toBe(1)
    })

    it('without a saved token, signs out quietly and calls nothing', async () => {
      const server = createFakeAuthServer()
      const { manager, store } = createTab(server)

      await manager.restore()

      expect(store.getState()).toEqual({ status: 'anonymous', user: null, endReason: null })
      expect(server.counters.refreshCalls).toBe(0)
    })

    it('stays unreachable without rejecting when the refresh fails with a server error, rate limiting or a malformed answer', async () => {
      const failures: Array<{ label: string; failure?: () => Response; authApi?: Partial<AuthApi> }> = [
        { label: '503', failure: () => problemResponse(503) },
        { label: '429', failure: () => problemResponse(429) },
        {
          label: 'malformed JSON',
          failure: () => new Response('{', { status: 200, headers: { 'Content-Type': 'application/json' } }),
        },
        {
          // The client's own check is authApi.test's job; here only what the manager does with its error.
          label: 'no tokens',
          authApi: { refresh: async () => Promise.reject(new MalformedAuthResponseError('refreshToken')) },
        },
      ]

      for (const { label, failure, authApi } of failures) {
        localStorage.clear()
        const server = createFakeAuthServer()
        const { manager, store, ended } = createTab(server, { authApi })
        const seeded = server.signIn().refreshToken
        seedStoredToken(seeded)
        server.setRefreshFailure(failure ?? null)

        const settled = await manager.restore().then(
          () => 'resolved',
          () => 'rejected',
        )

        // The label travels with the values, so a failure's diff names the case that broke.
        expect({ label, settled, status: store.getState().status, stored: storedToken(), ended: ended.mock.calls.length }).toEqual({
          label,
          settled: 'resolved',
          status: 'unreachable',
          stored: seeded,
          ended: 0,
        })
      }
    })

    it('stays unreachable without rejecting when /me fails with a server error', async () => {
      const server = createFakeAuthServer()
      const { manager, store } = createTab(server)
      seedStoredToken(server.signIn().refreshToken)
      server.setMeFailure(() => problemResponse(500))

      await expect(manager.restore()).resolves.toBeUndefined()

      expect(store.getState().status).toBe('unreachable')
    })

    it('starts over on retry with the token saved by the attempt that could not load the user', async () => {
      const server = createFakeAuthServer()
      const { manager, store } = createTab(server)
      seedStoredToken(server.signIn().refreshToken)
      server.setMeFailure(() => new TypeError('Failed to fetch'))
      await manager.restore()
      const savedByFirstAttempt = storedToken()
      server.setMeFailure(null)

      await manager.retryRestore()

      expect(server.observed.sentRefreshTokens.at(-1)).toBe(savedByFirstAttempt)
      expect(server.counters.massRevocations).toBe(0)
      expect(store.getState().status).toBe('authenticated')
    })
  })

  describe('login', () => {
    it('saves the refresh token, so reloading the page restores the session', async () => {
      const server = createFakeAuthServer()
      const { manager } = createTab(server)
      await manager.login(ANA_CREDENTIALS)

      const reloaded = createTab(server)
      await reloaded.manager.restore()

      expect(reloaded.store.getState().status).toBe('authenticated')
    })

    it('saves nothing and keeps the state when the login is rejected, cannot reach the API or answers without tokens', async () => {
      const server = createFakeAuthServer()
      const { manager, store } = createTab(server)
      await manager.restore()
      const stateBefore = store.getState()

      await expect(manager.login({ email: ANA.email, password: 'wrong-password' })).rejects.toMatchObject({ status: 401 })
      server.setLoginFailure(() => new TypeError('Failed to fetch'))
      await expect(manager.login(ANA_CREDENTIALS)).rejects.toBeInstanceOf(TypeError)

      expect(storedToken()).toBeNull()
      expect(store.getState()).toBe(stateBefore)

      const noTokens = createTab(createFakeAuthServer(), {
        authApi: { login: async () => Promise.reject(new MalformedAuthResponseError('refreshToken')) },
      })
      await noTokens.manager.restore()
      const noTokensBefore = noTokens.store.getState()
      await expect(noTokens.manager.login(ANA_CREDENTIALS)).rejects.toBeInstanceOf(MalformedAuthResponseError)

      expect(storedToken()).toBeNull()
      expect(noTokens.store.getState()).toBe(noTokensBefore)
    })
  })

  describe('user and token handling', () => {
    it('takes the user from /me after a restore and after a login', async () => {
      const server = createFakeAuthServer()
      const restored = createTab(server)
      seedStoredToken(server.signIn().refreshToken)
      await restored.manager.restore()
      const signedIn = createTab(server)
      await signedIn.manager.login(ANA_CREDENTIALS)

      expect(restored.store.getState().user).toEqual(anaAsUser)
      expect(signedIn.store.getState().user).toEqual(anaAsUser)
    })

    it('keeps tokens out of the store and only the refresh token in localStorage', async () => {
      const server = createFakeAuthServer()
      const { manager, store } = await createRestoredTab(server)
      await manager.refresh()
      const serialized = JSON.stringify(store.getState())

      expect(Object.keys(store.getState())).toEqual(['status', 'user', 'endReason'])
      for (const token of [manager.accessToken()!, storedToken()!, ...server.observed.sentRefreshTokens]) {
        expect(serialized).not.toContain(token)
      }
      expect(Array.from({ length: localStorage.length }, (_, index) => localStorage.key(index))).toEqual([REFRESH_TOKEN_KEY])
    })
  })

  describe('a session ended in another tab', () => {
    it('ends this tab with its own reason and forgets the access token', async () => {
      const server = createFakeAuthServer()
      const { manager, store, ended } = await createRestoredTab(server)

      manager.endedInAnotherTab()

      expect(store.getState()).toMatchObject({ status: 'anonymous', user: null, endReason: 'endedElsewhere' })
      expect(manager.accessToken()).toBeNull()
      expect(ended).toHaveBeenCalledExactlyOnceWith('endedElsewhere')
    })

    it('leaves a tab that was not signed in as it was', async () => {
      const server = createFakeAuthServer()
      const { manager, store, ended } = createTab(server)
      // Set by hand, so this does not depend on how restore() reaches the anonymous state.
      store.setState({ status: 'anonymous', user: null, endReason: null })

      manager.endedInAnotherTab()

      expect(store.getState()).toEqual({ status: 'anonymous', user: null, endReason: null })
      expect(ended).not.toHaveBeenCalled()
    })
  })

  describe('logout', () => {
    it('sends the stored refresh token to the API, which revokes the session', async () => {
      const server = createFakeAuthServer()
      const { manager } = await createRestoredTab(server)
      const tokenBefore = storedToken()!

      await manager.logout()

      expect(server.observed.sentLogoutTokens).toEqual([tokenBefore])
      expect(server.isActive(tokenBefore)).toBe(false)
      expect(server.activeSessionCount()).toBe(0)
    })

    it('does nothing when repeated, as after a double click on Sign out', async () => {
      const server = createFakeAuthServer()
      const { manager, ended } = await createRestoredTab(server)

      await manager.logout()
      const sentAfterFirst = [...server.observed.sentLogoutTokens]
      await manager.logout()

      expect(ended).toHaveBeenCalledOnce()
      expect(server.observed.sentLogoutTokens).toEqual(sentAfterFirst)
    })

    it('still ends a session kept only in memory, with no saved token to send', async () => {
      const server = createFakeAuthServer()
      const { manager, store, ended } = await createRestoredTab(server)
      // As when storage refused the token: the session is on screen, nothing is saved.
      localStorage.removeItem(REFRESH_TOKEN_KEY)

      await manager.logout()

      expect(store.getState()).toMatchObject({ status: 'anonymous', endReason: 'signedOut' })
      expect(ended).toHaveBeenCalledExactlyOnceWith('signedOut')
      expect(server.observed.sentLogoutTokens).toEqual([])
    })

    it('signs out locally even when the API cannot be reached', async () => {
      const server = createFakeAuthServer()
      const { manager, store, ended } = await createRestoredTab(server)
      server.setLogoutFailure(() => new TypeError('Failed to fetch'))

      await expect(manager.logout()).resolves.toBeUndefined()

      expect(storedToken()).toBeNull()
      expect(manager.accessToken()).toBeNull()
      expect(store.getState()).toMatchObject({ status: 'anonymous', endReason: 'signedOut' })
      expect(ended).toHaveBeenCalledExactlyOnceWith('signedOut')
    })
  })
})
