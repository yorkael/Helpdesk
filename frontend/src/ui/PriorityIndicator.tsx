import { cx } from '../lib/cx'
import { TICKET_PRIORITY_LABELS, type TicketPriority } from '../lib/ticket'

// Complete class names, so Tailwind finds them in the source.
const PRIORITY_STYLES: Record<TicketPriority, { level: number; text: string; bar: string }> = {
  Low: { level: 1, text: 'text-priority-low-text', bar: 'bg-priority-low-bar' },
  Medium: { level: 2, text: 'text-priority-medium-text', bar: 'bg-priority-medium-bar' },
  High: { level: 3, text: 'text-priority-high-text', bar: 'bg-priority-high-bar' },
  Urgent: { level: 4, text: 'text-priority-urgent-text', bar: 'bg-priority-urgent-bar' },
}

const BAR_HEIGHTS = ['h-1', 'h-2', 'h-3', 'h-4']

type PriorityIndicatorProps = {
  priority: TicketPriority
  className?: string
}

// The bars repeat the label visually; the text carries the meaning, so they are hidden from assistive technology.
export function PriorityIndicator({ priority, className }: PriorityIndicatorProps) {
  const { level, text, bar } = PRIORITY_STYLES[priority]

  return (
    <span
      data-priority={priority}
      data-level={level}
      className={cx('inline-flex items-center gap-1.5 text-sm font-medium', text, className)}
    >
      <span aria-hidden="true" className="inline-flex items-end gap-0.5">
        {BAR_HEIGHTS.map((height, index) => {
          const active = index < level
          return (
            <span
              key={height}
              data-active={active}
              className={cx('w-1 rounded-[1px]', height, active ? bar : 'bg-priority-bar-off')}
            />
          )
        })}
      </span>
      {TICKET_PRIORITY_LABELS[priority]}
    </span>
  )
}
