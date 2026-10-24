import type { ButtonHTMLAttributes, MouseEvent } from 'react'
import { cx } from '../lib/cx'

type ButtonVariant = 'primary' | 'secondary' | 'ghost'

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant
  loading?: boolean
  loadingLabel?: string
}

const BASE_CLASSES =
  'inline-grid h-10 place-items-center rounded-control px-4 text-sm font-semibold disabled:cursor-not-allowed disabled:opacity-60'

const VARIANT_CLASSES: Record<ButtonVariant, { base: string; hover: string }> = {
  primary: { base: 'bg-primary text-on-primary', hover: 'hover:bg-primary-hover' },
  secondary: { base: 'border border-line-strong bg-surface text-ink', hover: 'hover:bg-paper' },
  ghost: { base: 'text-primary', hover: 'hover:bg-primary-soft' },
}

// Pointer presses only: keyboard activation matches :focus-visible and is not animated.
const PRESS_CLASSES =
  'transition-[scale] duration-(--duration-press) ease-out motion-safe:[&:active:not(:focus-visible)]:scale-[0.97]'

export function Button({
  variant = 'primary',
  loading = false,
  loadingLabel = 'Working…',
  type = 'button',
  disabled,
  className,
  children,
  onClick,
  ...props
}: ButtonProps) {
  const interactive = !loading && !disabled

  // aria-disabled instead of disabled keeps the button focusable; canceling the click also cancels a submit.
  function handleClick(event: MouseEvent<HTMLButtonElement>) {
    if (loading) {
      event.preventDefault()
      return
    }
    onClick?.(event)
  }

  return (
    // After {...props}: loading decides aria-busy and aria-disabled, whatever the consumer passes.
    <button
      {...props}
      type={type}
      disabled={disabled}
      aria-busy={loading || undefined}
      aria-disabled={loading || undefined}
      onClick={handleClick}
      className={cx(
        BASE_CLASSES,
        VARIANT_CLASSES[variant].base,
        interactive && VARIANT_CLASSES[variant].hover,
        interactive && PRESS_CLASSES,
        loading && 'cursor-progress',
        className,
      )}
    >
      {/* Both labels share one grid cell, so the button keeps its width when it starts loading. */}
      <span aria-hidden={loading || undefined} className={cx('[grid-area:1/1]', loading && 'invisible')}>
        {children}
      </span>
      <span aria-hidden={!loading || undefined} className={cx('[grid-area:1/1]', !loading && 'invisible')}>
        {loadingLabel}
      </span>
    </button>
  )
}
