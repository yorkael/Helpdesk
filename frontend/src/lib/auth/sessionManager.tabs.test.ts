import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createFakeAuthServer } from '../../test/fakeAuthServer'
import { createRestoredTab, createTab } from '../../test/sessionTab'
import { createRefreshLock, type RefreshLock } from './refreshLock'
import { REFRESH_TOKEN_KEY } from './refreshTokenStorage'

const storedToken = () => localStorage.getItem(REFRESH_TOKEN_KEY)

// Tabs share jsdom's localStorage. A lock shared between them stands for Web Locks across tabs.
describe('createSessionManager across tabs', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('stores the new token before the next tab can refresh, so nothing is revoked', async () => {
    const server = createFakeAuthServer()
    const lock = createRefreshLock(undefined)
    const tabA = await createRestoredTab(server, { lock })
    const tabB = createTab(server, { lock })
    server.hold('me')

    const refreshA = tabA.manager.refresh()
    await vi.waitFor(() => expect(server.pendingCount('me')).toBe(1))
    const refreshB = tabB.manager.refresh()
    await vi.waitFor(() => expect(server.counters.refreshCalls).toBe(2))
    server.release('me')
    await Promise.all([refreshA, refreshB])

    expect(server.counters.massRevocations).toBe(0)
    expect(tabA.store.getState().status).toBe('authenticated')
    expect(tabB.store.getState().status).toBe('authenticated')
  })

  it('reads the stored token only once it holds the lock, so a waiting tab sends the rotated one', async () => {
    const server = createFakeAuthServer()
    const lock = createRefreshLock(undefined)
    let letTabBIn!: () => void
    const tabBMayEnter = new Promise<void>((resolve) => {
      letTabBIn = resolve
    })
    // Tab B gets the lock after tab A but starts only once tab A has finished, /me included.
    const lockForTabB: RefreshLock = {
      run: (task) =>
        lock.run(async () => {
          await tabBMayEnter
          return task()
        }),
    }
    const tabA = await createRestoredTab(server, { lock })
    const tabB = createTab(server, { lock: lockForTabB })

    const refreshA = tabA.manager.refresh()
    const refreshB = tabB.manager.refresh()
    await refreshA
    letTabBIn()
    await refreshB

    expect(server.counters.massRevocations).toBe(0)
    expect(tabB.store.getState().status).toBe('authenticated')
  })

  it('signs out before calling the API, so another tab cannot refresh the signed-out session', async () => {
    const server = createFakeAuthServer()
    const lock = createRefreshLock(undefined)
    const tabA = await createRestoredTab(server, { lock })
    const tabB = createTab(server, { lock })
    const issuedBefore = server.issuedTokenCount()
    let storedWhenLogoutArrived: string | null | undefined
    server.hooks.onLogout = () => {
      storedWhenLogoutArrived = storedToken()
    }
    server.hold('logout')

    const logoutA = tabA.manager.logout()
    // Tab A is signed out locally right before it calls the API.
    await vi.waitFor(() => expect(tabA.store.getState().status).toBe('anonymous'))
    await tabB.manager.refresh()
    server.release('logout')
    await logoutA

    // That logout reaches the API at all is the logout test's job; here, if it arrives, storage is already empty.
    expect(storedWhenLogoutArrived ?? null).toBeNull()
    expect(server.counters.refreshCalls).toBe(0)
    expect(server.issuedTokenCount()).toBe(issuedBefore)
  })

  it('without a shared lock, retries once with a token another tab stored during the refresh', async () => {
    const server = createFakeAuthServer()
    const tabA = await createRestoredTab(server)
    server.expireRefreshToken(storedToken()!)
    server.hold('refresh')

    const refreshA = tabA.manager.refresh()
    await vi.waitFor(() => expect(server.pendingCount('refresh')).toBe(1))
    // Another tab signs in meanwhile, with a lock of its own.
    localStorage.setItem(REFRESH_TOKEN_KEY, server.signIn().refreshToken)
    server.release('refresh')
    await refreshA

    expect(tabA.store.getState().status).toBe('authenticated')
    expect(server.counters.massRevocations).toBe(0)
  })

  it('on expiry, leaves alone a token that another tab stored since', async () => {
    const server = createFakeAuthServer()
    const lock = createRefreshLock(undefined)
    const tabA = await createRestoredTab(server, { lock })
    const storedByOtherTab = server.signIn().refreshToken
    localStorage.setItem(REFRESH_TOKEN_KEY, storedByOtherTab)

    await tabA.manager.expire()

    expect(storedToken()).toBe(storedByOtherTab)
    expect(tabA.store.getState().status).toBe('anonymous')
  })
})
