import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { FormEvent } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { Button } from './Button'

function submitHandler() {
  return vi.fn<(event: FormEvent) => void>((event) => event.preventDefault())
}

describe('Button', () => {
  it('calls onClick once when clicked', async () => {
    const onClick = vi.fn<() => void>()
    render(<Button onClick={onClick}>Save</Button>)

    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    expect(onClick).toHaveBeenCalledTimes(1)
  })

  it('submits its form when type is submit', async () => {
    const onSubmit = submitHandler()
    render(
      <form onSubmit={onSubmit}>
        <Button type="submit">Send</Button>
      </form>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'Send' }))

    expect(onSubmit).toHaveBeenCalledTimes(1)
  })

  it('exposes the loading state through aria-busy, aria-disabled and its accessible name', async () => {
    render(
      <Button loading loadingLabel="Saving…">
        Save
      </Button>,
    )
    const button = screen.getByRole('button', { name: 'Saving…' })

    expect(button).toHaveAttribute('aria-busy', 'true')
    expect(button).toHaveAttribute('aria-disabled', 'true')
    expect(button).not.toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Save' })).not.toBeInTheDocument()

    await userEvent.tab()
    expect(button).toHaveFocus()
  })

  it('has hover and press styles only while it is not loading', () => {
    const { rerender } = render(<Button>Save</Button>)
    const button = screen.getByRole('button')

    expect(button).toHaveClass('hover:bg-primary-hover', 'motion-safe:[&:active:not(:focus-visible)]:scale-[0.97]')

    rerender(<Button loading>Save</Button>)

    expect(button).not.toHaveClass('hover:bg-primary-hover')
    expect(button).not.toHaveClass('motion-safe:[&:active:not(:focus-visible)]:scale-[0.97]')
  })

  it('defaults to type="button", so it does not submit its form', async () => {
    const onSubmit = submitHandler()
    render(
      <form onSubmit={onSubmit}>
        <Button>Cancel</Button>
      </form>,
    )
    const button = screen.getByRole('button', { name: 'Cancel' })

    await userEvent.click(button)

    expect(button).toHaveAttribute('type', 'button')
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('does not submit its form while loading, by click or by Enter in a field', async () => {
    const onSubmit = submitHandler()
    render(
      <form onSubmit={onSubmit}>
        <input aria-label="Title" />
        <Button type="submit" loading loadingLabel="Sending…">
          Send
        </Button>
      </form>,
    )

    await userEvent.click(screen.getByRole('button', { name: 'Sending…' }))
    await userEvent.type(screen.getByRole('textbox', { name: 'Title' }), '{Enter}')

    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('does not call onClick while loading', async () => {
    const onClick = vi.fn<() => void>()
    render(
      <Button loading onClick={onClick}>
        Save
      </Button>,
    )

    await userEvent.click(screen.getByRole('button'))

    expect(onClick).not.toHaveBeenCalled()
  })

  it('has no aria-busy or aria-disabled while it is not loading', () => {
    render(<Button>Save</Button>)
    const button = screen.getByRole('button', { name: 'Save' })

    expect(button).not.toHaveAttribute('aria-busy')
    expect(button).not.toHaveAttribute('aria-disabled')
  })

  it('when disabled, ignores clicks, drops hover and press styles and is skipped by Tab', async () => {
    const onClick = vi.fn<() => void>()
    render(
      <Button disabled onClick={onClick}>
        Save
      </Button>,
    )
    const button = screen.getByRole('button', { name: 'Save' })

    await userEvent.click(button)
    await userEvent.tab()

    expect(onClick).not.toHaveBeenCalled()
    expect(button).not.toHaveClass('hover:bg-primary-hover')
    expect(button).not.toHaveClass('motion-safe:[&:active:not(:focus-visible)]:scale-[0.97]')
    expect(button).not.toHaveFocus()
  })

  it('overrides the aria-busy and aria-disabled a consumer passes', () => {
    const { rerender } = render(
      <Button aria-busy="true" aria-disabled="true">
        Save
      </Button>,
    )
    const button = screen.getByRole('button')

    expect(button).not.toHaveAttribute('aria-busy')
    expect(button).not.toHaveAttribute('aria-disabled')

    rerender(
      <Button loading aria-busy="false" aria-disabled="false">
        Save
      </Button>,
    )

    expect(button).toHaveAttribute('aria-busy', 'true')
    expect(button).toHaveAttribute('aria-disabled', 'true')
  })
})
