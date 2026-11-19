import type { AuthTokens, CurrentUser } from '../lib/auth/types'
import { jsonResponse, noContentResponse, problemResponse } from './responses'

/*
 * In-memory stand-in for the auth endpoints with the refresh semantics of the real API (AuthService.RefreshAsync),
 * as measured against it on 2026-11-13:
 * - each session accepts only its latest refresh token, and every refresh rotates it;
 * - presenting a token that was already used or signed out revokes every session of the user, the successor of
 *   that token included, and answers 401;
 * - a new login still works after that.
 *
 * It models sequential reuse, the pessimistic case: with 5 ms or more between two refreshes with the same token,
 * the real API revoked every session in 30 of 30 runs. It does not model the 0 ms race, where 26 of 30 runs took
 * the benign path (the loser gets a 401 and nothing is revoked).
 */

type Endpoint = 'login' | 'refresh' | 'logout' | 'me'

// A thrown error stands for a request that never got an answer; a Response is what the server answered.
type Failure = () => Error | Response

interface RefreshRecord {
  userId: string
  state: 'active' | 'revoked' | 'expired'
}

export const ANA: CurrentUser & { password: string } = {
  id: 'user-ana',
  name: 'Ana Agent',
  email: 'ana@example.com',
  role: 'Agent',
  password: 'ana-password',
}

export const ANA_CREDENTIALS = { email: ANA.email, password: ANA.password }

const MAX_REFRESH_TOKEN_LENGTH = 128

export type FakeAuthServer = ReturnType<typeof createFakeAuthServer>

export function createFakeAuthServer() {
  const refreshTokens = new Map<string, RefreshRecord>()
  const accessTokens = new Map<string, string>()
  const held = new Set<Endpoint>()
  const waiting = new Map<Endpoint, Array<() => void>>()
  let issued = 0
  let loginFailure: Failure | null = null
  let refreshFailure: Failure | null = null
  let meFailure: Failure | null = null
  let logoutFailure: Failure | null = null
  let loseNextRefreshResponse = false

  const counters = { refreshCalls: 0, meCalls: 0, logoutCalls: 0, massRevocations: 0 }
  const observed = {
    sentRefreshTokens: [] as string[],
    sentLogoutTokens: [] as unknown[],
    refreshSignals: [] as Array<AbortSignal | null | undefined>,
  }
  const hooks = {
    beforeRefresh: null as (() => void) | null,
    onLogout: null as (() => void) | null,
  }

  function issue(userId: string): AuthTokens {
    issued += 1
    const tokens: AuthTokens = {
      accessToken: `access-${issued}`,
      accessTokenExpiresAt: '2026-11-13T18:40:00+00:00',
      refreshToken: `refresh-${issued}`,
      refreshTokenExpiresAt: '2026-11-20T18:25:00+00:00',
    }
    accessTokens.set(tokens.accessToken, userId)
    refreshTokens.set(tokens.refreshToken, { userId, state: 'active' })
    return tokens
  }

  function revokeEverySession(userId: string): void {
    for (const record of refreshTokens.values()) {
      if (record.userId === userId && record.state === 'active') {
        record.state = 'revoked'
      }
    }
    counters.massRevocations += 1
  }

  async function gate(endpoint: Endpoint): Promise<void> {
    if (held.has(endpoint)) {
      await new Promise<void>((resolve) => {
        waiting.set(endpoint, [...(waiting.get(endpoint) ?? []), resolve])
      })
    }
  }

  function fail(failure: Failure): Response {
    const answer = failure()
    if (answer instanceof Response) {
      return answer
    }
    throw answer
  }

  const invalidRefreshToken = () => problemResponse(401, { detail: 'The refresh token is invalid or expired.' })

  const isMalformed = (token: unknown): boolean =>
    typeof token !== 'string' || token.length === 0 || token.length > MAX_REFRESH_TOKEN_LENGTH

  async function login(body: Record<string, unknown>): Promise<Response> {
    await gate('login')
    if (loginFailure) {
      return fail(loginFailure)
    }
    if (body.email !== ANA.email || body.password !== ANA.password) {
      return problemResponse(401, { detail: 'Invalid email or password.' })
    }
    return jsonResponse(200, issue(ANA.id))
  }

  async function refresh(body: Record<string, unknown>, signal: AbortSignal | null | undefined): Promise<Response> {
    counters.refreshCalls += 1
    observed.refreshSignals.push(signal)
    hooks.beforeRefresh?.()
    await gate('refresh')
    if (refreshFailure) {
      return fail(refreshFailure)
    }

    const token = body.refreshToken
    if (isMalformed(token)) {
      return problemResponse(400, { errors: { refreshToken: ['Invalid.'] } })
    }
    observed.sentRefreshTokens.push(token as string)

    const record = refreshTokens.get(token as string)
    if (!record || record.state === 'expired') {
      return invalidRefreshToken()
    }
    if (record.state === 'revoked') {
      revokeEverySession(record.userId)
      return invalidRefreshToken()
    }

    record.state = 'revoked'
    const tokens = issue(record.userId)
    if (loseNextRefreshResponse) {
      // The server rotated the token, but the answer never reached the client.
      loseNextRefreshResponse = false
      throw new TypeError('Failed to fetch')
    }
    return jsonResponse(200, tokens)
  }

  async function logout(body: Record<string, unknown>): Promise<Response> {
    counters.logoutCalls += 1
    observed.sentLogoutTokens.push(body.refreshToken)
    hooks.onLogout?.()
    await gate('logout')
    if (logoutFailure) {
      return fail(logoutFailure)
    }
    const record = refreshTokens.get(body.refreshToken as string)
    if (record?.state === 'active') {
      record.state = 'revoked'
    }
    return noContentResponse()
  }

  async function me(bearer: string | undefined): Promise<Response> {
    counters.meCalls += 1
    await gate('me')
    if (meFailure) {
      return fail(meFailure)
    }
    if (!bearer || accessTokens.get(bearer) !== ANA.id) {
      return problemResponse(401)
    }
    return jsonResponse(200, { id: ANA.id, name: ANA.name, email: ANA.email, role: ANA.role })
  }

  async function fetch(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
    const body = typeof init?.body === 'string' ? (JSON.parse(init.body) as Record<string, unknown>) : {}
    const authorization = (init?.headers as Record<string, string> | undefined)?.Authorization
    switch (String(input)) {
      case '/api/auth/login':
        return login(body)
      case '/api/auth/refresh':
        return refresh(body, init?.signal)
      case '/api/auth/logout':
        return logout(body)
      case '/api/auth/me':
        return me(authorization?.replace(/^Bearer /, ''))
      default:
        return problemResponse(404)
    }
  }

  return {
    fetch,
    counters,
    observed,
    hooks,
    /** Issues a session without HTTP, to seed storage. */
    signIn: () => issue(ANA.id),
    issuedTokenCount: () => issued,
    /** Forgets the calls made so far, so a test counts only those after its setup. */
    resetObservations() {
      Object.assign(counters, { refreshCalls: 0, meCalls: 0, logoutCalls: 0, massRevocations: 0 })
      observed.sentRefreshTokens.length = 0
      observed.sentLogoutTokens.length = 0
      observed.refreshSignals.length = 0
    },
    isActive: (refreshToken: string) => refreshTokens.get(refreshToken)?.state === 'active',
    activeSessionCount: () => [...refreshTokens.values()].filter((record) => record.state === 'active').length,
    /** Ends a token the way its 7-day lifetime would: a 401 with no revocation. */
    expireRefreshToken(refreshToken: string) {
      const record = refreshTokens.get(refreshToken)
      if (record) {
        record.state = 'expired'
      }
    },
    setLoginFailure(failure: Failure | null) {
      loginFailure = failure
    },
    setRefreshFailure(failure: Failure | null) {
      refreshFailure = failure
    },
    setMeFailure(failure: Failure | null) {
      meFailure = failure
    },
    setLogoutFailure(failure: Failure | null) {
      logoutFailure = failure
    },
    loseNextRefreshResponse() {
      loseNextRefreshResponse = true
    },
    /** Requests to a held endpoint wait, already counted, until it is released. */
    hold(endpoint: Endpoint) {
      held.add(endpoint)
    },
    release(endpoint: Endpoint) {
      held.delete(endpoint)
      for (const resolve of waiting.get(endpoint) ?? []) {
        resolve()
      }
      waiting.delete(endpoint)
    },
    pendingCount: (endpoint: Endpoint) => waiting.get(endpoint)?.length ?? 0,
  }
}
