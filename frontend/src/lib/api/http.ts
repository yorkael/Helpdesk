import { ApiError } from './ApiError'

export type Fetch = typeof fetch

export interface JsonRequest {
  method: string
  // Already serialized, so a retry can send exactly the same body again; a stream could only be read once.
  body?: string
  accessToken?: string | null
  signal?: AbortSignal
}

export function sendJson(fetchFn: Fetch, path: string, request: JsonRequest): Promise<Response> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (request.body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  if (request.accessToken) {
    headers.Authorization = `Bearer ${request.accessToken}`
  }

  return fetchFn(path, { method: request.method, headers, body: request.body, signal: request.signal })
}

export async function readResult<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw await ApiError.fromResponse(response)
  }
  if (response.status === 204) {
    return undefined as T
  }
  return (await response.json()) as T
}
