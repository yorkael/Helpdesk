import { describe, expect, it } from 'vitest'
import { createAuthApi } from '../lib/auth/authApi'
import { ANA_CREDENTIALS, createFakeAuthServer } from './fakeAuthServer'

// Checks the fake against what the real API answered in the captures, so the session tests rest on real behavior.
describe('createFakeAuthServer', () => {
  it('rotates the refresh token on every refresh', async () => {
    const server = createFakeAuthServer()
    const api = createAuthApi(server.fetch)
    const first = await api.login(ANA_CREDENTIALS)

    const second = await api.refresh(first.refreshToken)

    expect(second.refreshToken).not.toBe(first.refreshToken)
    expect(server.isActive(first.refreshToken)).toBe(false)
    expect(server.isActive(second.refreshToken)).toBe(true)
  })

  it('revokes every session of the user, the successor included, when a used token comes back', async () => {
    const server = createFakeAuthServer()
    const api = createAuthApi(server.fetch)
    const session = await api.login(ANA_CREDENTIALS)
    const otherSession = await api.login(ANA_CREDENTIALS)
    const successor = await api.refresh(session.refreshToken)

    await expect(api.refresh(session.refreshToken)).rejects.toMatchObject({ status: 401 })

    expect(server.counters.massRevocations).toBe(1)
    await expect(api.refresh(successor.refreshToken)).rejects.toMatchObject({ status: 401 })
    await expect(api.refresh(otherSession.refreshToken)).rejects.toMatchObject({ status: 401 })
    await expect(api.login(ANA_CREDENTIALS)).resolves.toMatchObject({ refreshToken: expect.any(String) })
  })

  it('treats a refresh after logout as reuse', async () => {
    const server = createFakeAuthServer()
    const api = createAuthApi(server.fetch)
    const session = await api.login(ANA_CREDENTIALS)
    const otherSession = await api.login(ANA_CREDENTIALS)

    await api.logout(session.refreshToken)
    await expect(api.refresh(session.refreshToken)).rejects.toMatchObject({ status: 401 })

    expect(server.isActive(otherSession.refreshToken)).toBe(false)
  })

  it('answers an expired token with a 401 and revokes nothing', async () => {
    const server = createFakeAuthServer()
    const api = createAuthApi(server.fetch)
    const session = await api.login(ANA_CREDENTIALS)
    const otherSession = await api.login(ANA_CREDENTIALS)
    server.expireRefreshToken(session.refreshToken)

    await expect(api.refresh(session.refreshToken)).rejects.toMatchObject({ status: 401 })

    expect(server.counters.massRevocations).toBe(0)
    expect(server.isActive(otherSession.refreshToken)).toBe(true)
  })

  it('rejects malformed refresh tokens with a 400, as the API validator does', async () => {
    const api = createAuthApi(createFakeAuthServer().fetch)

    await expect(api.refresh('x'.repeat(129))).rejects.toMatchObject({ status: 400 })
  })
})
