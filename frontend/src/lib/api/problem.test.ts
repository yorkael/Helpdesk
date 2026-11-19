import { describe, expect, it } from 'vitest'
import { readProblem } from './problem'

const response = (body: string, contentType: string) => new Response(body, { status: 400, headers: { 'Content-Type': contentType } })

describe('readProblem', () => {
  it('reads problem+json bodies', async () => {
    const problem = await readProblem(response('{"status":409,"detail":"Conflict"}', 'application/problem+json'))

    expect(problem).toEqual({ status: 409, detail: 'Conflict' })
  })

  it('reads problem+json with a charset, as the API sends for malformed request bodies', async () => {
    const body = '{"status":400,"errors":{"$.email":["The input was not valid."]}}'

    const problem = await readProblem(response(body, 'application/problem+json; charset=utf-8'))

    expect(problem?.errors).toEqual({ '$.email': ['The input was not valid.'] })
  })

  it('ignores JSON bodies that are not problem+json', async () => {
    expect(await readProblem(response('{"detail":"From a proxy"}', 'application/json'))).toBeNull()
  })

  it('returns null for a problem+json body that is cut short', async () => {
    expect(await readProblem(response('{"status":', 'application/problem+json'))).toBeNull()
  })

  it('returns null for an HTML error page', async () => {
    expect(await readProblem(response('<html>Bad gateway</html>', 'text/html'))).toBeNull()
  })
})
