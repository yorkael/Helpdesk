import type { ProblemDetails } from '../lib/api/problem'

export function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json; charset=utf-8' } })
}

// Content type as the API sends it for exception-handler errors, without a charset.
export function problemResponse(status: number, problem: ProblemDetails = {}): Response {
  return new Response(JSON.stringify({ status, ...problem }), {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

export function noContentResponse(): Response {
  return new Response(null, { status: 204 })
}

// Header names in a plain object as sendJson builds them.
export function requestHeaders(init: RequestInit | undefined): Record<string, string> {
  return (init?.headers ?? {}) as Record<string, string>
}
