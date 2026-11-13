import { readProblem, type ProblemDetails } from './problem'

// The UI decides by status. The problem details only feed messages and field errors, never decisions.
export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null) {
    super(`Request failed with status ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  static async fromResponse(response: Response): Promise<ApiError> {
    return new ApiError(response.status, await readProblem(response))
  }
}
