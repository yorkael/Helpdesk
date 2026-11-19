// Values exactly as the API sends and accepts them (TicketStatus and TicketPriority in Helpdesk.Domain).
export const TICKET_STATUSES = ['Open', 'InProgress', 'WaitingOnCustomer', 'Resolved', 'Closed'] as const

export type TicketStatus = (typeof TICKET_STATUSES)[number]

export const TICKET_STATUS_LABELS: Record<TicketStatus, string> = {
  Open: 'Open',
  InProgress: 'In progress',
  WaitingOnCustomer: 'Waiting on customer',
  Resolved: 'Resolved',
  Closed: 'Closed',
}

export const TICKET_PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'] as const

export type TicketPriority = (typeof TICKET_PRIORITIES)[number]

export const TICKET_PRIORITY_LABELS: Record<TicketPriority, string> = {
  Low: 'Low',
  Medium: 'Medium',
  High: 'High',
  Urgent: 'Urgent',
}

// A newer API may send a value this build does not know; these let a screen show it as text instead of failing.
export function isTicketStatus(value: string): value is TicketStatus {
  return (TICKET_STATUSES as readonly string[]).includes(value)
}

export function isTicketPriority(value: string): value is TicketPriority {
  return (TICKET_PRIORITIES as readonly string[]).includes(value)
}
