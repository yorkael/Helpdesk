import { describe, expect, it, vi } from 'vitest'
import { jsonResponse, problemResponse, requestHeaders } from '../../test/responses'
import { ApiError } from './ApiError'
import { createApiClient, type SessionPort } from './apiClient'

// A session whose refresh swaps in the next token; null as the next token means the refresh ended the session.
function fakeSession(initialToken: string | null, nextToken: string | null = 'token-2') {
  let token = initialToken
  const session = {
    accessToken: () => token,
    refresh: vi.fn<SessionPort['refresh']>(async () => {
      token = nextToken
    }),
    expire: vi.fn<SessionPort['expire']>(() => {
      token = null
    }),
    replaceToken: (value: string | null) => {
      token = value
    },
  }
  return session satisfies SessionPort
}

// Guards the tests against an endless retry loop: it throws instead of answering a sixth call.
function fetchAnswering(...responses: Array<() => Response>) {
  return vi.fn<typeof fetch>(async () => {
    const next = responses.shift()
    if (!next) {
      throw new Error('Unexpected extra request')
    }
    return next()
  })
}

const sentToken = (fetchFn: ReturnType<typeof fetchAnswering>, call: number) =>
  requestHeaders(fetchFn.mock.calls[call]?.[1]).Authorization

describe('createApiClient', () => {
  it('sends the access token and returns the JSON body', async () => {
    const fetchFn = fetchAnswering(() => jsonResponse(200, [{ id: 'c1', name: 'Hardware' }]))
    const client = createApiClient(fetchFn, fakeSession('token-1'))

    await expect(client.request('/api/categories')).resolves.toEqual([{ id: 'c1', name: 'Hardware' }])
    expect(fetchFn).toHaveBeenCalledWith('/api/categories', expect.objectContaining({ method: 'GET' }))
    expect(sentToken(fetchFn, 0)).toBe('Bearer token-1')
  })

  it('refreshes once after a 401 and retries the request with the new token', async () => {
    const fetchFn = fetchAnswering(() => problemResponse(401), () => jsonResponse(200, { ok: true }))
    const session = fakeSession('token-1')
    const client = createApiClient(fetchFn, session)

    await expect(client.request('/api/tickets')).resolves.toEqual({ ok: true })
    expect(session.refresh).toHaveBeenCalledTimes(1)
    expect(sentToken(fetchFn, 1)).toBe('Bearer token-2')
  })

  it('sends the same body again when it retries a POST', async () => {
    const fetchFn = fetchAnswering(() => problemResponse(401), () => jsonResponse(201, { id: 't1' }))
    const client = createApiClient(fetchFn, fakeSession('token-1'))

    await client.request('/api/tickets', { method: 'POST', body: { title: 'Printer jam' } })

    expect(fetchFn).toHaveBeenCalledTimes(2)
    expect(fetchFn.mock.calls[1]?.[1]?.body).toBe('{"title":"Printer jam"}')
    expect(fetchFn.mock.calls[1]?.[1]?.method).toBe('POST')
  })

  it('expires the session when the retried request is rejected too, without refreshing again', async () => {
    const fetchFn = fetchAnswering(...Array.from({ length: 5 }, () => () => problemResponse(401)))
    const session = fakeSession('token-1')
    const client = createApiClient(fetchFn, session)

    await expect(client.request('/api/tickets')).rejects.toMatchObject({ status: 401 })
    expect(session.refresh).toHaveBeenCalledTimes(1)
    expect(fetchFn).toHaveBeenCalledTimes(2)
    expect(session.expire).toHaveBeenCalledTimes(1)
  })

  it('retries without refreshing when another request already replaced the token', async () => {
    const session = fakeSession('token-1')
    const fetchFn = fetchAnswering(
      () => {
        session.replaceToken('token-from-another-request')
        return problemResponse(401)
      },
      () => jsonResponse(200, { ok: true }),
    )
    const client = createApiClient(fetchFn, session)

    await expect(client.request('/api/tickets')).resolves.toEqual({ ok: true })
    expect(session.refresh).not.toHaveBeenCalled()
    expect(sentToken(fetchFn, 1)).toBe('Bearer token-from-another-request')
  })

  it('fails with the 401 and does not retry when the refresh ended the session', async () => {
    const fetchFn = fetchAnswering(() => problemResponse(401), () => jsonResponse(200, { ok: true }))
    const session = fakeSession('token-1', null)
    const client = createApiClient(fetchFn, session)

    await expect(client.request('/api/tickets')).rejects.toMatchObject({ status: 401 })
    expect(fetchFn).toHaveBeenCalledTimes(1)
  })

  it('lets a network failure during the refresh through, without retrying or ending the session', async () => {
    const networkError = new TypeError('Failed to fetch')
    const fetchFn = fetchAnswering(() => problemResponse(401), () => jsonResponse(200, { ok: true }))
    const session = fakeSession('token-1')
    session.refresh.mockRejectedValueOnce(networkError)
    const client = createApiClient(fetchFn, session)

    await expect(client.request('/api/tickets')).rejects.toBe(networkError)
    expect(session.refresh).toHaveBeenCalledTimes(1)
    expect(session.expire).not.toHaveBeenCalled()
    expect(fetchFn).toHaveBeenCalledTimes(1)
  })

  it('does not refresh after a 401 to a request sent without a token', async () => {
    const fetchFn = fetchAnswering(() => problemResponse(401))
    const session = fakeSession(null)
    const client = createApiClient(fetchFn, session)

    await expect(client.request('/api/tickets')).rejects.toMatchObject({ status: 401 })
    expect(session.refresh).not.toHaveBeenCalled()
  })

  it('reports a 403 as an ApiError and leaves the session alone', async () => {
    const fetchFn = fetchAnswering(() => problemResponse(403))
    const session = fakeSession('token-1')
    const client = createApiClient(fetchFn, session)

    const error = await client.request('/api/agents').catch((caught: unknown) => caught)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(403)
    expect(session.refresh).not.toHaveBeenCalled()
    expect(session.expire).not.toHaveBeenCalled()
  })
})
