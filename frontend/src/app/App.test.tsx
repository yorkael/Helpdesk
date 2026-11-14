import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { SLOW_RENDER_WAIT_MS, SLOW_TEST_TIMEOUT_MS } from '../test/timeouts'
import { App } from './App'

describe('App', () => {
  it('renders the home page through the browser router', { timeout: SLOW_TEST_TIMEOUT_MS }, async () => {
    render(<App />)

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Helpdesk' }, { timeout: SLOW_RENDER_WAIT_MS }),
    ).toBeInTheDocument()
  })
})
