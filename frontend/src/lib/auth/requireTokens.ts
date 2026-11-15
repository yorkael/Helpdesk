import { MalformedAuthResponseError } from './MalformedAuthResponseError'
import type { SessionTokens } from './types'

function requireToken(fields: Record<string, unknown>, field: keyof SessionTokens): string {
  const value = fields[field]
  if (typeof value !== 'string' || value === '') {
    throw new MalformedAuthResponseError(field)
  }
  return value
}

// Checked before anyone stores anything, so a 200 without a token can never save "undefined" as the refresh token.
export function requireTokens(body: unknown): SessionTokens {
  const fields: Record<string, unknown> = typeof body === 'object' && body !== null ? { ...body } : {}
  return { accessToken: requireToken(fields, 'accessToken'), refreshToken: requireToken(fields, 'refreshToken') }
}
