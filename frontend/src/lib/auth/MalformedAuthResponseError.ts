// Not an ApiError and without a status on purpose: nothing can read it as a 401 and end the session over it.
export class MalformedAuthResponseError extends Error {
  readonly field: string

  constructor(field: string) {
    super(`The auth response has no usable ${field}`)
    this.name = 'MalformedAuthResponseError'
    this.field = field
  }
}
