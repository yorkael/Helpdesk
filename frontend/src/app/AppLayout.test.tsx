import { render, screen } from '@testing-library/react'
import { createMemoryRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { describe, expect, it } from 'vitest'
import { AppLayout } from './AppLayout'

function renderLayout() {
  const router = createMemoryRouter([{ path: '/', Component: AppLayout, children: [{ index: true, Component: () => <p>Page content</p> }] }])
  render(<RouterProvider router={router} />)
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

  it('renders the current page inside the main landmark', async () => {
    renderLayout()

    expect(await screen.findByRole('main')).toHaveTextContent('Page content')
  })
})
