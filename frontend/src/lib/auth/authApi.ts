import { readResult, sendJson, type Fetch } from '../api/http'
import { requireTokens } from './requireTokens'
import type { Credentials, CurrentUser, SessionTokens } from './types'

export interface AuthApi {
  login(credentials: Credentials): Promise<SessionTokens>
  refresh(refreshToken: string, signal?: AbortSignal): Promise<SessionTokens>
  logout(refreshToken: string): Promise<void>
  me(accessToken: string): Promise<CurrentUser>
}

// Plain calls with no session logic and no retries: the session manager decides what each failure means.
export function createAuthApi(fetchFn: Fetch): AuthApi {
  const post = async <T>(path: string, body: object, signal?: AbortSignal) =>
    readResult<T>(await sendJson(fetchFn, path, { method: 'POST', body: JSON.stringify(body), signal }))

  return {
    login: async (credentials) => requireTokens(await post<unknown>('/api/auth/login', credentials)),
    refresh: async (refreshToken, signal) => requireTokens(await post<unknown>('/api/auth/refresh', { refreshToken }, signal)),
    logout: (refreshToken) => post<void>('/api/auth/logout', { refreshToken }),
    me: async (accessToken) => readResult<CurrentUser>(await sendJson(fetchFn, '/api/auth/me', { method: 'GET', accessToken })),
  }
}
