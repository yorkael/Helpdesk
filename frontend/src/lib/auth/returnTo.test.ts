import { describe, expect, it } from 'vitest'
import { safeReturnTo } from './returnTo'

const ORIGIN = 'https://helpdesk.test'

describe('safeReturnTo', () => {
  it('keeps a path on this origin, as the URL parser reads it', () => {
    expect(safeReturnTo('/tickets/42?x=1#c', ORIGIN)).toBe('/tickets/42?x=1#c')
    expect(safeReturnTo('/a/../tickets?x=1#h', ORIGIN)).toBe('/tickets?x=1#h')
  })

  it('rejects paths the URL parser turns into another origin', () => {
    for (const value of ['//evil.com', '/\\evil.com', '/\t/evil.com']) {
      expect(safeReturnTo(value, ORIGIN)).toBeNull()
    }
  })

  it('rejects anything that does not start with a slash', () => {
    for (const value of ['tickets', 'https://evil.com/x', 'javascript:alert(1)']) {
      expect(safeReturnTo(value, ORIGIN)).toBeNull()
    }
  })

  it('rejects the login page itself', () => {
    expect(safeReturnTo('/login', ORIGIN)).toBeNull()
    expect(safeReturnTo('/login?x=1', ORIGIN)).toBeNull()
  })

  it('rejects values that are not strings', () => {
    for (const value of [null, undefined, { from: '/tickets' }, 42]) {
      expect(safeReturnTo(value, ORIGIN)).toBeNull()
    }
  })

  it('rejects a path the URL parser cannot read', () => {
    expect(safeReturnTo('//', ORIGIN)).toBeNull()
  })
})
