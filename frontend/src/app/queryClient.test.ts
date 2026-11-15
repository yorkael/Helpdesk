import { describe, expect, it } from 'vitest'
import { ApiError } from '../lib/api/ApiError'
import { createQueryClient, shouldRetry } from './queryClient'

const apiError = (status: number) => new ApiError(status, null)

describe('shouldRetry', () => {
  it('does not retry a client error that would answer the same way again', () => {
    for (const status of [400, 401, 403, 404, 409]) {
      // The status travels with the result, so a failure names the case that broke.
      expect({ status, retry: shouldRetry(0, apiError(status)) }).toEqual({ status, retry: false })
    }
  })

  it('treats a timeout or rate limiting like a server error', () => {
    for (const status of [408, 429]) {
      for (const failureCount of [0, 1, 2, 3]) {
        expect({ status, failureCount, retry: shouldRetry(failureCount, apiError(status)) }).toEqual({
          status,
          failureCount,
          retry: shouldRetry(failureCount, apiError(503)),
        })
      }
    }
  })

  it('retries a server error or a network failure', () => {
    const failures = [
      { label: '500', error: apiError(500) },
      { label: '503', error: apiError(503) },
      { label: 'network', error: new TypeError('Failed to fetch') },
    ]

    for (const { label, error } of failures) {
      expect({ label, retry: shouldRetry(0, error) }).toEqual({ label, retry: true })
    }
  })

  it('stops after the third retry', () => {
    expect(shouldRetry(3, apiError(503))).toBe(false)
  })
})

describe('createQueryClient', () => {
  it('uses this retry rule for every query', () => {
    expect(createQueryClient().getDefaultOptions().queries?.retry).toBe(shouldRetry)
  })
})
