import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { describe, expect, it } from 'vitest'
import { createFakeSession, userWithRole } from '../test/fakeSession'
import { AppLayout } from './AppLayout'
import { SessionContext } from './session/sessionContext'

function renderLayout(fake = createFakeSession('anonymous')) {
  const router = createMemoryRouter([{ path: '/', Component: AppLayout, children: [{ index: true, Component: () => <p>Page content</p> }] }])
  render(
    <SessionContext value={fake.session}>
      <RouterProvider router={router} />
    </SessionContext>,
  )
  return fake
}

describe('AppLayout', () => {
  it('starts with the skip link, so it is the first tab stop', async () => {
    renderLayout()

    expect((await screen.findAllByRole('link'))[0]).toHaveTextContent('Skip to content')
  })

  it('points the skip link at the main landmark', async () => {
    renderLayout()

    expect(await screen.findByRole('link', { name: 'Skip to content' })).toHaveAttribute('href', '#main')
    expect(screen.getByRole('main')).toHaveAttribute('id', 'main')
  })

  // focus:not-sr-only resets padding to 0 and outranks px-4 and py-2, so the link is hidden only while unfocused.
  it('hides the skip link only while it is not focused', async () => {
    renderLayout()
    const link = await screen.findByRole('link', { name: 'Skip to content' })

    expect(link).toHaveClass('not-focus:sr-only', 'px-4', 'py-2')
    expect(link).not.toHaveClass('focus:not-sr-only')
  })

  it('renders the current page inside the main landmark', async () => {
    renderLayout()

    expect(await screen.findByRole('main')).toHaveTextContent('Page content')
  })

  it('signs the user out from the header', async () => {
    const user = userEvent.setup()
    const { manager } = renderLayout(createFakeSession('authenticated', userWithRole('Agent')))

    await user.click(await screen.findByRole('button', { name: 'Sign out' }))

    expect(manager.logout).toHaveBeenCalledOnce()
  })

  it('offers no sign-out to a visitor who is not signed in', async () => {
    renderLayout()

    await screen.findByRole('main')
    expect(screen.queryByRole('button', { name: 'Sign out' })).not.toBeInTheDocument()
  })
})
