import { QueryClient } from '@tanstack/react-query'
import { ApiError } from '../lib/api/ApiError'

// The library's default number of attempts after the first one.
const MAX_RETRIES = 3

// Timeouts and rate limiting may pass on their own; any other 4xx (401, 403, 404, a validation error) answers the
// same way every time, and a 401 already had its one refresh and retry in the API client.
const TRANSIENT_CLIENT_ERRORS = new Set([408, 429])

export function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500 && !TRANSIENT_CLIENT_ERRORS.has(error.status)) {
    return false
  }
  return failureCount < MAX_RETRIES
}

export function createQueryClient(): QueryClient {
  return new QueryClient({ defaultOptions: { queries: { retry: shouldRetry } } })
}
