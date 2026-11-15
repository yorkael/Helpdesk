import { act, fireEvent, render, screen } from '@testing-library/react'
import userEvent, { type UserEvent } from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SessionContext } from '../app/session/sessionContext'
import { ApiError } from '../lib/api/ApiError'
import type { ProblemDetails } from '../lib/api/problem'
import { MalformedAuthResponseError } from '../lib/auth/MalformedAuthResponseError'
import type { SessionEndReason } from '../lib/auth/sessionStore'
import { createFakeSession } from '../test/fakeSession'
import { LoginPage } from './LoginPage'

function renderLogin(endReason: SessionEndReason | null = null) {
  const fake = createFakeSession('anonymous', null, endReason)
  const view = render(
    <SessionContext value={fake.session}>
      <LoginPage />
    </SessionContext>,
  )
  return { ...fake, unmount: view.unmount }
}

async function fillIn(user: UserEvent) {
  await user.type(screen.getByLabelText('Email'), 'ana@example.com')
  await user.type(screen.getByLabelText('Password'), 'ana-password')
}

const signInButton = () => screen.getByRole('button', { name: 'Sign in' })
const apiError = (status: number, problem: ProblemDetails = {}) => new ApiError(status, { status, ...problem })

describe('LoginPage', () => {
  it('lets password managers fill in the form', () => {
    renderLogin()

    expect(screen.getByLabelText('Email')).toHaveAttribute('autocomplete', 'username')
    expect(screen.getByLabelText('Password')).toHaveAttribute('autocomplete', 'current-password')
  })

  it('says why the user is here when the session expired', () => {
    renderLogin('expired')

    expect(screen.getByRole('status')).toHaveTextContent('Your session expired. Please sign in again.')
  })

  it('says nothing about an expiry after a deliberate sign-out', () => {
    renderLogin('signedOut')

    expect(screen.queryByText('Your session expired. Please sign in again.')).not.toBeInTheDocument()
  })

  it('uses an email field and a masked password field', () => {
    renderLogin()

    expect(screen.getByLabelText('Email')).toHaveAttribute('type', 'email')
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password')
  })

  it('checks for empty fields without calling the API and focuses the first one', async () => {
    const user = userEvent.setup()
    const { manager } = renderLogin()

    await user.click(signInButton())

    expect(screen.getByLabelText('Email')).toHaveAccessibleDescription('Enter your email.')
    expect(screen.getByLabelText('Password')).toHaveAccessibleDescription('Enter your password.')
    expect(screen.getByLabelText('Email')).toHaveFocus()
    expect(manager.login).not.toHaveBeenCalled()
  })

  it('says the email or password is wrong on a 401, whatever the API wrote', async () => {
    const user = userEvent.setup()
    const { manager } = renderLogin()
    manager.login.mockRejectedValue(apiError(401, { detail: 'The account is locked.' }))

    await fillIn(user)
    await user.click(signInButton())

    expect(await screen.findByRole('alert')).toHaveTextContent('Incorrect email or password.')
  })

  it('marks the field the API rejected and focuses it', async () => {
    const user = userEvent.setup()
    const { manager } = renderLogin()
    manager.login.mockRejectedValue(apiError(400, { errors: { password: ['Too short.'] } }))

    await fillIn(user)
    await user.click(signInButton())

    await vi.waitFor(() => expect(screen.getByLabelText('Password')).toHaveFocus())
    expect(screen.getByLabelText('Password')).toBeInvalid()
    expect(screen.getByLabelText('Password')).toHaveAccessibleDescription('Check your password.')
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('reports a validation error about something the form does not show as a general message', async () => {
    const user = userEvent.setup()
    const { manager } = renderLogin()
    manager.login.mockRejectedValue(apiError(400, { errors: { '$.email': ['The input was not valid.'] } }))

    await fillIn(user)
    await user.click(signInButton())

    expect(await screen.findByRole('alert')).toHaveTextContent('Check the form and try again.')
    expect(screen.getByLabelText('Email')).not.toBeInvalid()
    expect(screen.getByLabelText('Password')).not.toBeInvalid()
  })

  it('says the server could not be reached when the request never got an answer', async () => {
    const user = userEvent.setup()
    const { manager } = renderLogin()
    manager.login.mockRejectedValue(new TypeError('Failed to fetch'))

    await fillIn(user)
    await user.click(signInButton())

    expect(await screen.findByRole('alert')).toHaveTextContent("Couldn't reach the server. Try again.")
  })

  // The server answered, but with nothing the form can act on.
  it('says something went wrong on a server error, rate limiting or an answer without tokens', async () => {
    const failures = [
      { label: '503', error: apiError(503) },
      { label: '429', error: apiError(429) },
      { label: 'no tokens', error: new MalformedAuthResponseError('refreshToken') },
    ]

    for (const { label, error } of failures) {
      const user = userEvent.setup()
      const { manager, unmount } = renderLogin()
      manager.login.mockRejectedValue(error)

      await fillIn(user)
      await user.click(signInButton())

      // The label travels with the result, so a failure names the case that broke.
      await vi.waitFor(() =>
        expect({ label, alert: screen.queryByRole('alert')?.textContent ?? null }).toEqual({
          label,
          alert: 'Something went wrong. Try again later.',
        }),
      )
      unmount()
    }
  })

  it('shows the button as loading while signing in', async () => {
    const user = userEvent.setup()
    const { manager } = renderLogin()
    manager.login.mockReturnValue(new Promise(() => {}))

    await fillIn(user)
    await user.click(signInButton())

    expect(screen.getByRole('button', { name: 'Signing in…' })).toHaveAttribute('aria-busy', 'true')
  })

  it('sends one login when the form is submitted twice in a row', async () => {
    const user = userEvent.setup()
    const { manager } = renderLogin()
    manager.login.mockReturnValue(new Promise(() => {}))
    await fillIn(user)
    const form = signInButton().closest('form')!

    fireEvent.submit(form)
    fireEvent.submit(form)

    expect(manager.login).toHaveBeenCalledOnce()
  })

  // One test for two rules on purpose: announcing a repeated failure needs the second attempt to go out at all.
  it('after a 401, lets the user try again and announces a second 401 anew', async () => {
    const user = userEvent.setup()
    const { manager } = renderLogin()
    let rejectSecondAttempt!: (error: unknown) => void
    manager.login.mockRejectedValueOnce(apiError(401)).mockImplementationOnce(
      () =>
        new Promise((_, reject) => {
          rejectSecondAttempt = reject
        }),
    )
    await fillIn(user)
    await user.click(signInButton())
    expect(await screen.findByRole('alert')).toHaveTextContent('Incorrect email or password.')

    await user.click(signInButton())

    expect(manager.login).toHaveBeenCalledTimes(2)
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    await act(async () => rejectSecondAttempt(apiError(401)))
    expect(await screen.findByRole('alert')).toHaveTextContent('Incorrect email or password.')
  })
})
