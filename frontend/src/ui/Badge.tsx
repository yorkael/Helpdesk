import type { ReactNode } from 'react'
import { cx } from '../lib/cx'

type BadgeTone = 'neutral' | 'primary' | 'internal'

const TONE_CLASSES: Record<BadgeTone, string> = {
  neutral: 'border-line-strong bg-paper text-muted',
  primary: 'border-transparent bg-primary-soft text-primary',
  internal: 'border-internal-line bg-internal-bg text-ink',
}

type BadgeProps = {
  tone?: BadgeTone
  className?: string
  children: ReactNode
}

export function Badge({ tone = 'neutral', className, children }: BadgeProps) {
  return (
    <span
      data-tone={tone}
      className={cx(
        'inline-flex items-center gap-1.5 rounded-tag border px-1.5 py-0.5 text-xs font-semibold',
        TONE_CLASSES[tone],
        className,
      )}
    >
      {tone === 'internal' && <span aria-hidden="true" data-dot className="size-1.5 rounded-full bg-internal-dot" />}
      {children}
    </span>
  )
}
