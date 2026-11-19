import { ApiError } from './ApiError'
import { readResult, sendJson, type Fetch } from './http'

/** What the client needs from the session. The session manager implements it. */
export interface SessionPort {
  accessToken(): string | null
  /**
   * Shared by every caller while in flight. Resolves once settled; if the session ended, accessToken() is null
   * afterwards. Rejects on a network failure or timeout, leaving the session as it was.
   */
  refresh(): Promise<void>
  /** The API rejected a token it had just issued. */
  expire(): void
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
}

export interface ApiClient {
  request<T>(path: string, options?: RequestOptions): Promise<T>
}

export function createApiClient(fetchFn: Fetch, session: SessionPort): ApiClient {
  async function send<T>(path: string, method: string, body: string | undefined, signal: AbortSignal | undefined, retried: boolean): Promise<T> {
    const sentWith = session.accessToken()
    const response = await sendJson(fetchFn, path, { method, body, accessToken: sentWith, signal })

    // A 403 also lands here: the user is signed in but not allowed, so the session stays as it is.
    if (response.status !== 401 || sentWith === null) {
      return readResult<T>(response)
    }

    if (retried) {
      // The token from a refresh was rejected too. Refreshing again could loop forever.
      session.expire()
      throw await ApiError.fromResponse(response)
    }

    // Another request may have refreshed while this one was in flight; its new token is then enough.
    if (session.accessToken() === sentWith) {
      await session.refresh()
    }

    if (session.accessToken() === null) {
      throw await ApiError.fromResponse(response)
    }

    return send<T>(path, method, body, signal, true)
  }

  return {
    request: <T>(path: string, { method = 'GET', body, signal }: RequestOptions = {}) =>
      send<T>(path, method, body === undefined ? undefined : JSON.stringify(body), signal, false),
  }
}
