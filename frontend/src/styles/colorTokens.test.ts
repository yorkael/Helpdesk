import { describe, expect, it } from 'vitest'
import { contrastRatio, parseColorTokens } from './colorTokens'

describe('parseColorTokens', () => {
  it('reads the tokens of an @theme block, ignoring comments and the palette reset', () => {
    const tokens = parseColorTokens('@theme {\n  --color-*: initial;\n  /* --color-old: #000000; */\n  --color-ink: #1A1830;\n}')

    expect([...tokens]).toEqual([['ink', '#1A1830']])
  })

  it('rejects a color that is not a 6-digit hex', () => {
    expect(() => parseColorTokens('@theme {\n  --color-ink: rgb(26 24 48);\n}')).toThrow(
      '--color-ink must be a 6-digit hex color, got "rgb(26 24 48)"',
    )
  })

  it('rejects a duplicated token', () => {
    expect(() => parseColorTokens('@theme {\n  --color-ink: #1A1830;\n  --color-ink: #000000;\n}')).toThrow(
      '--color-ink is declared more than once',
    )
  })

  it('rejects a declaration it cannot parse, such as one without a semicolon', () => {
    expect(() => parseColorTokens('@theme {\n  --color-ink: #1A1830\n}')).toThrow(
      'Found 1 color declarations but parsed 0; could not parse --color-ink.',
    )
  })

  it('rejects css without an @theme block', () => {
    expect(() => parseColorTokens(':root {\n  --color-ink: #1A1830;\n}')).toThrow('No @theme block found')
  })
})

describe('contrastRatio', () => {
  it('matches the WCAG reference values', () => {
    expect(contrastRatio('#000000', '#FFFFFF')).toBeCloseTo(21, 2)
    expect(contrastRatio('#777777', '#FFFFFF')).toBeCloseTo(4.48, 2)
  })
})
