export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

const PROBLEM_JSON = 'application/problem+json'

// The API sometimes adds "; charset=utf-8", hence startsWith. Anything else, such as an HTML error page from a
// proxy or plain JSON, carries no problem details, and neither does a body cut short.
export async function readProblem(response: Response): Promise<ProblemDetails | null> {
  const contentType = response.headers.get('content-type') ?? ''
  if (!contentType.toLowerCase().startsWith(PROBLEM_JSON)) {
    return null
  }

  try {
    const body: unknown = await response.json()
    return typeof body === 'object' && body !== null && !Array.isArray(body) ? (body as ProblemDetails) : null
  } catch {
    return null
  }
}
