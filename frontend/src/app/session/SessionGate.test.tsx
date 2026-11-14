import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import type { AppSession } from './createAppSession'
import { createFakeSession } from '../../test/fakeSession'
import { SessionContext } from './sessionContext'
import { SessionGate } from './SessionGate'

function renderGate(session: AppSession) {
  render(
    <SessionContext value={session}>
      <SessionGate>
        <p>Signed-in content</p>
      </SessionGate>
    </SessionContext>,
  )
}

describe('SessionGate', () => {
  it('shows only a loading state while the session is restoring', () => {
    renderGate(createFakeSession('restoring').session)

    expect(screen.getByRole('status')).toHaveTextContent('Restoring your session…')
    expect(screen.queryByText('Signed-in content')).not.toBeInTheDocument()
  })

  it('starts the restore over when the user retries after the server could not be reached', async () => {
    const user = userEvent.setup()
    const fake = createFakeSession('unreachable')
    renderGate(fake.session)
    expect(screen.getByRole('heading', { level: 1, name: "Can't reach the server" })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Retry' }))

    expect(fake.manager.retryRestore).toHaveBeenCalledOnce()
    expect(fake.manager.restore).not.toHaveBeenCalled()
  })

  it('shows the retry as loading while it runs', async () => {
    const user = userEvent.setup()
    const fake = createFakeSession('unreachable')
    // Neither settles, so the loading state cannot end whichever one the button calls.
    fake.manager.retryRestore.mockReturnValue(new Promise(() => {}))
    fake.manager.restore.mockReturnValue(new Promise(() => {}))
    renderGate(fake.session)

    await user.click(screen.getByRole('button', { name: 'Retry' }))

    expect(screen.getByRole('button', { name: 'Retrying…' })).toHaveAttribute('aria-busy', 'true')
  })
})
