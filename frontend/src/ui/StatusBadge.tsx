import { cx } from '../lib/cx'
import { TICKET_STATUS_LABELS, type TicketStatus } from '../lib/ticket'

// Complete class names, so Tailwind finds them in the source.
const STATUS_CLASSES: Record<TicketStatus, string> = {
  Open: 'bg-status-open-bg text-status-open-fg',
  InProgress: 'bg-status-in-progress-bg text-status-in-progress-fg',
  WaitingOnCustomer: 'bg-status-waiting-on-customer-bg text-status-waiting-on-customer-fg',
  Resolved: 'bg-status-resolved-bg text-status-resolved-fg',
  Closed: 'bg-status-closed-bg text-status-closed-fg',
}

type StatusBadgeProps = {
  status: TicketStatus
  className?: string
}

export function StatusBadge({ status, className }: StatusBadgeProps) {
  return (
    <span
      data-status={status}
      className={cx(
        'inline-flex items-center rounded-tag border border-current/25 px-1.5 py-0.5 text-xs font-semibold tracking-wider uppercase',
        STATUS_CLASSES[status],
        className,
      )}
    >
      {TICKET_STATUS_LABELS[status]}
    </span>
  )
}
