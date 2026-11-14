const LOGIN_PATH = '/login'

// The return path travels in history state, which another site cannot set, and is still checked: after signing
// in, only a path on this origin is followed, and never the login page itself.
export function safeReturnTo(value: unknown, origin: string): string | null {
  if (typeof value !== 'string' || !value.startsWith('/')) {
    return null
  }

  // The parser drops tabs and newlines and reads a backslash as a slash, so "/\t/evil.com" and "/\evil.com" both
  // become "//evil.com", another origin. Only the parsed URL decides, and only what it parsed is returned.
  let url: URL
  try {
    url = new URL(value, origin)
  } catch {
    return null
  }
  if (url.origin !== origin || url.pathname === LOGIN_PATH) {
    return null
  }
  return url.pathname + url.search + url.hash
}
