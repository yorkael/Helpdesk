import { describe, expect, it } from 'vitest'
import { cx } from './cx'

describe('cx', () => {
  it('joins the class names it is given, skipping false, null, undefined and empty strings', () => {
    expect(cx('base', false, 'active', null, undefined, '', 'last')).toBe('base active last')
  })

  it('returns an empty string when there is nothing to join', () => {
    expect(cx(false, undefined)).toBe('')
  })
})
