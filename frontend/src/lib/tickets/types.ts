// GET /api/tickets as the API sends it (TicketListItem and PagedResponse in Helpdesk.Application).

export interface TicketListItem {
  id: string
  title: string
  // Plain strings, not TicketStatus and TicketPriority: the API may know values this build does not.
  status: string
  priority: string
  categoryId: string
  categoryName: string
  createdById: string
  createdByName: string
  assignedToId: string | null
  assignedToName: string | null
  /** ISO 8601, in UTC. */
  createdAt: string
}

export interface PagedResponse<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}
