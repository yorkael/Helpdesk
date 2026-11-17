import { TICKET_PRIORITIES, TICKET_STATUSES, type TicketPriority, type TicketStatus } from '../ticket'

/** Fixed: nothing asks to change it, so it stays out of the address. */
export const TICKET_PAGE_SIZE = 20

// The limits of GET /api/tickets (ListTicketsRequestValidator and TicketLimits in Helpdesk.Application).
const SEARCH_MAX_LENGTH = 200
// The API skips (page - 1) * pageSize rows as a 32-bit int and answers 400 for a page past that.
const MAX_PAGE = Math.floor(2_147_483_647 / TICKET_PAGE_SIZE) + 1
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

export interface TicketListParams {
  page: number
  status: TicketStatus | null
  priority: TicketPriority | null
  assignedToId: string | null
  categoryId: string | null
  search: string | null
}

const FILTER_KEYS = ['status', 'priority', 'assignedToId', 'categoryId', 'search'] as const

/**
 * Reads the list's state from the address with the API's own rules. A value the API would reject is dropped
 * rather than sent, so an address typed by hand shows a list instead of a 400. A repeated parameter counts with
 * its first value, as in the API.
 */
export function readTicketListParams(query: URLSearchParams): TicketListParams {
  return {
    page: readPage(query.get('page')),
    status: readName(TICKET_STATUSES, query.get('status')),
    priority: readName(TICKET_PRIORITIES, query.get('priority')),
    assignedToId: readGuid(query.get('assignedToId')),
    categoryId: readGuid(query.get('categoryId')),
    search: readSearch(query.get('search')),
  }
}

/** The address for these params: page 1 and the filters not set are left out. */
export function writeTicketListParams(params: TicketListParams): URLSearchParams {
  const query = new URLSearchParams()
  if (params.page !== 1) {
    query.set('page', String(params.page))
  }
  for (const key of FILTER_KEYS) {
    const value = params[key]
    if (value !== null) query.set(key, value)
  }
  return query
}

/** The request for one page of the list. The page size is sent so the pages always match TICKET_PAGE_SIZE. */
export function ticketListPath(params: TicketListParams): string {
  const query = writeTicketListParams(params)
  query.set('pageSize', String(TICKET_PAGE_SIZE))
  return `/api/tickets?${query.toString()}`
}

function readPage(value: string | null): number {
  if (value === null || !/^\d+$/.test(value)) {
    return 1
  }
  const page = Number(value)
  return page >= 1 && page <= MAX_PAGE ? page : 1
}

// Exact names only: the API compares them case-sensitively.
function readName<T extends string>(allowed: readonly T[], value: string | null): T | null {
  return allowed.find((name) => name === value) ?? null
}

function readGuid(value: string | null): string | null {
  return value !== null && GUID.test(value) ? value : null
}

// Trimmed as the API trims it, and measured after trimming because the trimmed value is what is sent.
function readSearch(value: string | null): string | null {
  const search = value?.trim() ?? ''
  return search !== '' && search.length <= SEARCH_MAX_LENGTH ? search : null
}
