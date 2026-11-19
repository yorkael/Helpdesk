import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { SessionContext } from '../app/session/sessionContext'
import { createFakeSession, userWithRole } from '../test/fakeSession'
import { HomePage } from './HomePage'

describe('HomePage', () => {
  it('shows who is signed in and with which role', () => {
    const fake = createFakeSession('authenticated', userWithRole('Agent'))

    render(
      <SessionContext value={fake.session}>
        <HomePage />
      </SessionContext>,
    )

    expect(screen.getByText('Signed in as Agent User (Agent)')).toBeInTheDocument()
  })
})
