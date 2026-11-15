import { describe, expect, it } from 'vitest'
import { ApiError } from '../api/ApiError'
import { MalformedAuthResponseError } from './MalformedAuthResponseError'
import { requireTokens } from './requireTokens'

const tokens = {
  accessToken: 'access-1',
  accessTokenExpiresAt: '2026-11-13T18:25:39.53+00:00',
  refreshToken: 'refresh-1',
  refreshTokenExpiresAt: '2026-11-20T18:25:39.53+00:00',
}

const outcome = (body: unknown) => {
  try {
    requireTokens(body)
    return 'accepted'
  } catch {
    return 'rejected'
  }
}

describe('requireTokens', () => {
  it('rejects a token that is empty or not a string', () => {
    for (const refreshToken of ['', 42]) {
      // The value travels with the result, so a failure names the case that broke.
      expect({ refreshToken, outcome: outcome({ ...tokens, refreshToken }) }).toEqual({ refreshToken, outcome: 'rejected' })
    }
  })

  it('rejects with its own error, which has no status that could pass for a 401', () => {
    let error: unknown
    try {
      requireTokens({ accessToken: 'access-1' })
    } catch (caught) {
      error = caught
    }

    expect(error).toBeInstanceOf(MalformedAuthResponseError)
    expect(error).not.toBeInstanceOf(ApiError)
    expect(error).not.toHaveProperty('status')
  })
})
