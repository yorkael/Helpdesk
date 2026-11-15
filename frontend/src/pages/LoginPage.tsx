import { useRef, useState, type FormEvent } from 'react'
import { useSessionManager, useSessionState } from '../app/session/sessionContext'
import { ApiError } from '../lib/api/ApiError'
import { Button } from '../ui/Button'
import { Field, Input } from '../ui/Field'

type FieldName = 'email' | 'password'
type FieldErrors = Partial<Record<FieldName, string>>

const FIELD_ORDER: readonly FieldName[] = ['email', 'password']

// Our own wording: the API's validation messages are written for developers.
const FIELD_ERRORS_FROM_API: Record<FieldName, string> = {
  email: 'Enter a valid email address.',
  password: 'Check your password.',
}

const MESSAGES = {
  rejected: 'Incorrect email or password.',
  invalid: 'Check the form and try again.',
  unreachable: "Couldn't reach the server. Try again.",
  unexpected: 'Something went wrong. Try again later.',
}

interface Failure {
  fieldErrors: FieldErrors
  message: string | null
}

const isFieldName = (key: string): key is FieldName => key === 'email' || key === 'password'

function validate(email: string, password: string): FieldErrors {
  const errors: FieldErrors = {}
  if (email === '') {
    errors.email = 'Enter your email.'
  }
  if (password === '') {
    errors.password = 'Enter your password.'
  }
  return errors
}

// By status only: the problem's detail never decides what the user reads.
function describeFailure(error: unknown): Failure {
  if (error instanceof ApiError && error.status === 401) {
    return { fieldErrors: {}, message: MESSAGES.rejected }
  }
  if (error instanceof ApiError && error.status === 400) {
    const fieldErrors: FieldErrors = {}
    let unmapped = false
    for (const key of Object.keys(error.problem?.errors ?? {})) {
      if (isFieldName(key)) {
        fieldErrors[key] = FIELD_ERRORS_FROM_API[key]
      } else {
        unmapped = true
      }
    }
    const message = unmapped || Object.keys(fieldErrors).length === 0 ? MESSAGES.invalid : null
    return { fieldErrors, message }
  }
  if (error instanceof TypeError) {
    return { fieldErrors: {}, message: MESSAGES.unreachable }
  }
  return { fieldErrors: {}, message: MESSAGES.unexpected }
}

function focusFirstInvalid(form: HTMLFormElement, errors: FieldErrors): void {
  const first = FIELD_ORDER.find((name) => errors[name] !== undefined)
  const control = first === undefined ? null : form.elements.namedItem(first)
  if (control instanceof HTMLElement) {
    control.focus()
  }
}

// On success there is nothing to do here: the session becomes authenticated and RedirectIfAuthenticated leaves.
export function LoginPage() {
  const manager = useSessionManager()
  const expired = useSessionState((state) => state.endReason === 'expired')
  // A ref, not state: two submits in the same tick both read the state from before either of them.
  const submitting = useRef(false)
  const [pending, setPending] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [message, setMessage] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (submitting.current) return
    const form = event.currentTarget
    const data = new FormData(form)
    const email = String(data.get('email') ?? '').trim()
    const password = String(data.get('password') ?? '')

    const localErrors = validate(email, password)
    if (Object.keys(localErrors).length > 0) {
      setFieldErrors(localErrors)
      setMessage(null)
      focusFirstInvalid(form, localErrors)
      return
    }

    submitting.current = true
    setPending(true)
    // Cleared before each attempt, so the same failure twice is announced twice.
    setFieldErrors({})
    setMessage(null)
    try {
      await manager.login({ email, password })
    } catch (error) {
      const failure = describeFailure(error)
      setFieldErrors(failure.fieldErrors)
      setMessage(failure.message)
      focusFirstInvalid(form, failure.fieldErrors)
    } finally {
      submitting.current = false
      setPending(false)
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-sm flex-col gap-6 py-12">
      <h1 className="text-3xl text-ink">Sign in</h1>
      {/* A status, not an alert: it explains why the user is here, it does not report a failed action. */}
      {expired && (
        // oxlint-disable-next-line jsx-a11y/prefer-tag-over-role -- <output> is not exposed as a live region as reliably as an explicit role="status"
        <p role="status" className="text-sm text-muted">
          Your session expired. Please sign in again.
        </p>
      )}
      <form noValidate onSubmit={(event) => void handleSubmit(event)} className="flex flex-col gap-4">
        <Field label="Email" error={fieldErrors.email}>
          <Input name="email" type="email" autoComplete="username" />
        </Field>
        <Field label="Password" error={fieldErrors.password}>
          <Input name="password" type="password" autoComplete="current-password" />
        </Field>
        {message && (
          <p role="alert" className="text-sm font-medium text-danger">
            {message}
          </p>
        )}
        <Button type="submit" loading={pending} loadingLabel="Signing in…">
          Sign in
        </Button>
      </form>
    </div>
  )
}
