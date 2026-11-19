import { describe, expect, it } from 'vitest'
import { problemResponse } from '../../test/responses'
import { ApiError } from './ApiError'

describe('ApiError.fromResponse', () => {
  it('keeps the HTTP status and the problem details', async () => {
    const error = await ApiError.fromResponse(problemResponse(404, { detail: 'The ticket was not found.' }))

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(404)
    expect(error.problem?.detail).toBe('The ticket was not found.')
  })

  it('takes the status from the response, not the body, when there are no problem details', async () => {
    const error = await ApiError.fromResponse(new Response('<html>Bad gateway</html>', { status: 502, headers: { 'Content-Type': 'text/html' } }))

    expect(error.status).toBe(502)
    expect(error.problem).toBeNull()
  })

  it('does not put the problem detail in its message', async () => {
    const error = await ApiError.fromResponse(problemResponse(403, { detail: 'Agents can only assign tickets to themselves.' }))

    expect(error.message).toBe('Request failed with status 403')
  })
})
