import { describe, expect, it } from 'vitest'
import { TICKET_PRIORITIES, TICKET_STATUSES } from '../lib/ticket'
import { contrastRatio, parseColorTokens } from './colorTokens'
import tokensCss from './tokens.css?raw'

const TEXT_MINIMUM = 4.5
const NON_TEXT_MINIMUM = 3

function kebabCase(apiValue: string): string {
  return apiValue.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase()
}

type Pair = { foreground: string; background: string; minimum: number }

function pairs(foreground: string, backgrounds: string[], minimum = TEXT_MINIMUM): Pair[] {
  return backgrounds.map((background) => ({ foreground, background, minimum }))
}

const statusTokens = TICKET_STATUSES.map((status) => `status-${kebabCase(status)}`)
const priorityTokens = TICKET_PRIORITIES.map((priority) => `priority-${kebabCase(priority)}`)

// subtle on line is 4.14:1 and is deliberately not an allowed pair.
const textPairs: Pair[] = [
  ...pairs('ink', ['paper', 'surface', 'primary-soft', 'highlight', 'internal-bg']),
  ...pairs('muted', ['paper', 'surface', 'primary-soft', 'internal-bg']),
  ...pairs('subtle', ['paper', 'surface', 'primary-soft']),
  ...pairs('primary', ['paper', 'surface', 'primary-soft']),
  ...pairs('danger', ['paper', 'surface']),
  ...pairs('on-primary', ['primary', 'primary-hover']),
  ...statusTokens.map((token) => ({ foreground: `${token}-fg`, background: `${token}-bg`, minimum: TEXT_MINIMUM })),
  ...priorityTokens.flatMap((token) => pairs(`${token}-text`, ['paper', 'surface'])),
]

// Focus ring and control borders (WCAG 1.4.11).
const nonTextPairs: Pair[] = [
  ...pairs('primary', ['paper', 'surface'], NON_TEXT_MINIMUM),
  ...pairs('control-line', ['paper', 'surface'], NON_TEXT_MINIMUM),
]

function color(name: string): string {
  const value = parseColorTokens(tokensCss).get(name)
  if (!value) throw new Error(`Missing token --color-${name}`)
  return value
}

describe('color tokens', () => {
  it('are all parsed from tokens.css', () => {
    expect(parseColorTokens(tokensCss).size).toBeGreaterThan(0)
  })

  it('define the colors of every ticket status and priority', () => {
    const expected = [
      ...statusTokens.flatMap((token) => [`${token}-fg`, `${token}-bg`]),
      ...priorityTokens.flatMap((token) => [`${token}-text`, `${token}-bar`]),
    ]
    const declared = parseColorTokens(tokensCss)

    expect(expected.filter((name) => !declared.has(name)).map((name) => `--color-${name}`)).toEqual([])
  })

  it.each([...textPairs, ...nonTextPairs])('$foreground on $background reaches $minimum:1', ({ foreground, background, minimum }) => {
    expect(contrastRatio(color(foreground), color(background))).toBeGreaterThanOrEqual(minimum)
  })
})
