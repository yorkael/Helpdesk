import { describe, expect, it, vi } from 'vitest'
import { jsonResponse, noContentResponse, problemResponse, requestHeaders } from '../../test/responses'
import { createAuthApi } from './authApi'
import type { AuthTokens } from './types'

const tokens: AuthTokens = {
  accessToken: 'access-1',
  accessTokenExpiresAt: '2026-11-13T18:25:39.53+00:00',
  refreshToken: 'refresh-1',
  refreshTokenExpiresAt: '2026-11-20T18:24:39.53+00:00',
}

const sentBody = (fetchFn: ReturnType<typeof vi.fn<typeof fetch>>) => JSON.parse(String(fetchFn.mock.calls[0]?.[1]?.body))

describe('createAuthApi', () => {
  it('logs in with the credentials and returns the tokens', async () => {
    const fetchFn = vi.fn<typeof fetch>(async () => jsonResponse(200, tokens))

    await expect(createAuthApi(fetchFn).login({ email: 'ana@example.com', password: 'secret-password' })).resolves.toMatchObject({
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
    })
    expect(fetchFn.mock.calls[0]?.[0]).toBe('/api/auth/login')
    expect(fetchFn.mock.calls[0]?.[1]?.method).toBe('POST')
    expect(sentBody(fetchFn)).toEqual({ email: 'ana@example.com', password: 'secret-password' })
  })

  it('reports rejected credentials as an ApiError with status 401', async () => {
    const fetchFn = vi.fn<typeof fetch>(async () => problemResponse(401, { detail: 'Invalid email or password.' }))

    await expect(createAuthApi(fetchFn).login({ email: 'ana@example.com', password: 'wrong' })).rejects.toMatchObject({ status: 401 })
  })

  it('refreshes with the refresh token in the body and passes the abort signal on', async () => {
    const fetchFn = vi.fn<typeof fetch>(async () => jsonResponse(200, tokens))
    const controller = new AbortController()

    await createAuthApi(fetchFn).refresh('refresh-0', controller.signal)

    expect(fetchFn.mock.calls[0]?.[0]).toBe('/api/auth/refresh')
    expect(sentBody(fetchFn)).toEqual({ refreshToken: 'refresh-0' })
    expect(fetchFn.mock.calls[0]?.[1]?.signal).toBe(controller.signal)
  })

  // Each call checks its answer; what counts as a usable token is requireTokens.test's job.
  describe('a 200 without usable tokens', () => {
    const withoutField = (field: string) => {
      const body: Record<string, unknown> = { ...tokens }
      delete body[field]
      return vi.fn<typeof fetch>(async () => jsonResponse(200, body))
    }
    const settle = (promise: Promise<unknown>) =>
      promise.then(
        () => 'resolved',
        () => 'rejected',
      )

    it('is rejected on login when a token is missing', async () => {
      for (const field of ['accessToken', 'refreshToken']) {
        const settled = await settle(createAuthApi(withoutField(field)).login({ email: 'ana@example.com', password: 'secret-password' }))
        // The field travels with the result, so a failure names the case that broke.
        expect({ field, settled }).toEqual({ field, settled: 'rejected' })
      }
    })

    it('is rejected on refresh when a token is missing', async () => {
      for (const field of ['accessToken', 'refreshToken']) {
        const settled = await settle(createAuthApi(withoutField(field)).refresh('refresh-0'))
        expect({ field, settled }).toEqual({ field, settled: 'rejected' })
      }
    })
  })

  it('logs out with the refresh token in the body', async () => {
    const fetchFn = vi.fn<typeof fetch>(async () => noContentResponse())

    await expect(createAuthApi(fetchFn).logout('refresh-1')).resolves.toBeUndefined()
    expect(fetchFn.mock.calls[0]?.[0]).toBe('/api/auth/logout')
    expect(sentBody(fetchFn)).toEqual({ refreshToken: 'refresh-1' })
  })

  it('asks for the current user with the access token', async () => {
    const user = { id: 'u1', name: 'Ana', email: 'ana@example.com', role: 'Agent' }
    const fetchFn = vi.fn<typeof fetch>(async () => jsonResponse(200, user))

    await expect(createAuthApi(fetchFn).me('access-1')).resolves.toEqual(user)
    expect(fetchFn.mock.calls[0]?.[0]).toBe('/api/auth/me')
    expect(requestHeaders(fetchFn.mock.calls[0]?.[1]).Authorization).toBe('Bearer access-1')
  })
})
