import type { ReactNode } from 'react'
import { cx } from '../lib/cx'

// Decorative: LoadingRegion announces the loading state once for the whole group.
export function Skeleton({ className }: { className?: string }) {
  return (
    <div aria-hidden="true" className={cx('animate-pulse rounded-control bg-line motion-reduce:animate-none', className)} />
  )
}

type LoadingRegionProps = {
  label?: string
  className?: string
  children: ReactNode
}

export function LoadingRegion({ label = 'Loading…', className, children }: LoadingRegionProps) {
  return (
    // oxlint-disable-next-line jsx-a11y/prefer-tag-over-role -- <output> is not exposed as a live region as reliably as an explicit role="status"
    <div role="status" className={className}>
      <span className="sr-only">{label}</span>
      {children}
    </div>
  )
}
