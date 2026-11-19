// Used by the contrast test only; the app never imports it, so it is not part of the bundle.

// "--color-*: initial" removes the default Tailwind palette; it is not a token.
const PALETTE_RESET = /--color-\*\s*:\s*initial\s*;/

export function parseColorTokens(css: string): Map<string, string> {
  const theme = css.replace(/\/\*[\s\S]*?\*\//g, '').match(/@theme\s*\{([\s\S]*?)\n\}/)
  if (!theme) throw new Error('No @theme block found')
  const body = theme[1].replace(PALETTE_RESET, '')

  const tokens = new Map<string, string>()
  for (const [, name, rawValue] of body.matchAll(/--color-([\w-]+)\s*:\s*([^;\n]+);/g)) {
    const value = rawValue.trim()
    if (!/^#[0-9a-fA-F]{6}$/.test(value)) {
      throw new Error(`--color-${name} must be a 6-digit hex color, got "${value}"`)
    }
    if (tokens.has(name)) throw new Error(`--color-${name} is declared more than once`)
    tokens.set(name, value)
  }

  const declared = [...body.matchAll(/--color-([^\s:;]+)/g)].map(([, name]) => name)
  if (declared.length !== tokens.size) {
    const unparsed = declared.filter((name) => !tokens.has(name)).map((name) => `--color-${name}`)
    throw new Error(
      `Found ${declared.length} color declarations but parsed ${tokens.size}; could not parse ${unparsed.join(', ')}. ` +
        'Write each color as "--color-<name>: #rrggbb;" on its own line.',
    )
  }
  return tokens
}

function relativeLuminance(hex: string): number {
  const [red, green, blue] = [1, 3, 5].map((start) => {
    const channel = parseInt(hex.slice(start, start + 2), 16) / 255
    return channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * red + 0.7152 * green + 0.0722 * blue
}

export function contrastRatio(foreground: string, background: string): number {
  const [lighter, darker] = [relativeLuminance(foreground), relativeLuminance(background)].sort((a, b) => b - a)
  return (lighter + 0.05) / (darker + 0.05)
}
