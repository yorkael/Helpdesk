import { beforeEach, describe, expect, it } from 'vitest'
import { createRefreshTokenStorage, REFRESH_TOKEN_KEY } from './refreshTokenStorage'

// A Storage whose every method throws, as a full quota or blocked site data does.
function throwingStorage(error: DOMException): Storage {
  const fail = () => {
    throw error
  }
  return { length: 0, clear: fail, getItem: fail, key: fail, removeItem: fail, setItem: fail }
}

describe('createRefreshTokenStorage', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('keeps the refresh token in localStorage under its own key', () => {
    const storage = createRefreshTokenStorage(() => localStorage)

    storage.set('refresh-1')
    expect(localStorage.getItem(REFRESH_TOKEN_KEY)).toBe('refresh-1')
    expect(storage.get()).toBe('refresh-1')

    storage.remove()
    expect(storage.get()).toBeNull()
  })

  it('reads no token when storage is blocked', () => {
    const storage = createRefreshTokenStorage(() => {
      throw new DOMException('Access denied', 'SecurityError')
    })

    expect(storage.get()).toBeNull()
  })

  it('does not throw when storage is full', () => {
    const storage = createRefreshTokenStorage(() => throwingStorage(new DOMException('Quota exceeded', 'QuotaExceededError')))

    expect(() => storage.set('refresh-1')).not.toThrow()
  })

  it('does not throw when storage refuses to remove the token', () => {
    const storage = createRefreshTokenStorage(() => throwingStorage(new DOMException('Access denied', 'SecurityError')))

    expect(() => storage.remove()).not.toThrow()
  })
})
