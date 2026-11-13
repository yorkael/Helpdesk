import { describe, expect, it, vi } from 'vitest'
import { jsonResponse, noContentResponse, problemResponse, requestHeaders } from '../../test/responses'
import { ApiError } from './ApiError'
import { readResult, sendJson } from './http'

describe('sendJson', () => {
  it('sends a JSON body with its content type and the bearer token', async () => {
    const fetchFn = vi.fn<typeof fetch>(async () => noContentResponse())

    await sendJson(fetchFn, '/api/tickets', { method: 'POST', body: '{"title":"x"}', accessToken: 'token-1' })

    const init = fetchFn.mock.calls[0]?.[1]
    expect(init?.body).toBe('{"title":"x"}')
    expect(requestHeaders(init)).toEqual({
      Accept: 'application/json',
      'Content-Type': 'application/json',
      Authorization: 'Bearer token-1',
    })
  })

  it('sends neither a content type without a body nor an Authorization header without a token', async () => {
    const fetchFn = vi.fn<typeof fetch>(async () => noContentResponse())

    await sendJson(fetchFn, '/api/categories', { method: 'GET', accessToken: null })

    expect(requestHeaders(fetchFn.mock.calls[0]?.[1])).toEqual({ Accept: 'application/json' })
  })

  it('passes the abort signal on to fetch', async () => {
    const fetchFn = vi.fn<typeof fetch>(async () => noContentResponse())
    const controller = new AbortController()

    await sendJson(fetchFn, '/api/auth/refresh', { method: 'POST', signal: controller.signal })

    expect(fetchFn.mock.calls[0]?.[1]?.signal).toBe(controller.signal)
  })
})

describe('readResult', () => {
  it('returns the JSON body of a successful response', async () => {
    await expect(readResult(jsonResponse(200, { id: 't1' }))).resolves.toEqual({ id: 't1' })
  })

  it('returns undefined for 204 No Content', async () => {
    await expect(readResult(noContentResponse())).resolves.toBeUndefined()
  })

  it('throws an ApiError for an error response', async () => {
    const error = await readResult(problemResponse(409, { detail: 'Conflict' })).catch((caught: unknown) => caught)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 409, problem: { detail: 'Conflict' } })
  })
})
