import type { ReactNode } from 'react'
import { cx } from '../lib/cx'

type EmptyStateProps = {
  title: string
  description?: string
  action?: ReactNode
  headingLevel?: 2 | 3
  className?: string
}

export function EmptyState({ title, description, action, headingLevel = 2, className }: EmptyStateProps) {
  const Heading = headingLevel === 3 ? 'h3' : 'h2'

  return (
    <div
      className={cx(
        'flex flex-col items-center gap-2 rounded-card border border-dashed border-line-strong bg-surface px-6 py-10 text-center',
        className,
      )}
    >
      <Heading className="text-lg text-ink">{title}</Heading>
      {description && <p className="max-w-prose text-sm text-muted">{description}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  )
}
