import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { Link, useSearchParams } from 'react-router'
import { useApiClient } from '../app/session/sessionContext'
import { ApiError } from '../lib/api/ApiError'
import { isTicketPriority, isTicketStatus } from '../lib/ticket'
import {
  readTicketListParams,
  ticketListPath,
  TICKET_PAGE_SIZE,
  writeTicketListParams,
  type TicketListParams,
} from '../lib/tickets/listParams'
import type { PagedResponse, TicketListItem } from '../lib/tickets/types'
import { useDelayedFlag } from '../lib/useDelayedFlag'
import { Button } from '../ui/Button'
import { EmptyState } from '../ui/EmptyState'
import { PriorityIndicator } from '../ui/PriorityIndicator'
import { Skeleton } from '../ui/Skeleton'
import { StatusBadge } from '../ui/StatusBadge'

const SKELETON_ROWS = 5

const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' })

// The address is the only state of the list, so a reload, a shared link and Back all show the same page.
export function TicketListPage() {
  const api = useApiClient()
  const [searchParams, setSearchParams] = useSearchParams()
  const params = readTicketListParams(searchParams)
  const normalized = writeTicketListParams(params).toString()

  // A value the API would reject was already dropped from params; the address is rewritten to match, without
  // a new history entry, so Back does not return to the address that could not be used.
  useEffect(() => {
    if (normalized !== searchParams.toString()) {
      setSearchParams(normalized, { replace: true })
    }
  }, [normalized, searchParams, setSearchParams])

  const tickets = useQuery({
    queryKey: ['tickets', params],
    queryFn: ({ signal }) => api.request<PagedResponse<TicketListItem>>(ticketListPath(params), { signal }),
    // The rows of the page left stay on screen, marked busy, until the next page arrives.
    placeholderData: keepPreviousData,
  })
  const showSkeleton = useDelayedFlag(tickets.isPending)

  // After a page change, focus goes to the results, so a keyboard user does not tab through the page again.
  const resultsHeading = useRef<HTMLHeadingElement>(null)
  const shownPage = useRef(params.page)
  useEffect(() => {
    if (shownPage.current !== params.page) {
      shownPage.current = params.page
      resultsHeading.current?.focus()
    }
  }, [params.page])

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-3xl text-ink">Tickets</h1>
      <section aria-labelledby="ticket-results" className="flex flex-col gap-4">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 id="ticket-results" ref={resultsHeading} tabIndex={-1} className="text-lg text-ink focus:outline-none">
            Results
          </h2>
          {/* oxlint-disable-next-line jsx-a11y/prefer-tag-over-role -- <output> is not exposed as a live region as reliably as an explicit role="status" */}
          <p role="status" className="text-sm text-muted">
            {summaryOf(tickets.data, tickets.isPending)}
          </p>
        </div>
        {tickets.isPending ? (
          showSkeleton && <TicketListSkeleton />
        ) : tickets.isError ? (
          <TicketListError error={tickets.error} retrying={tickets.isFetching} onRetry={() => void tickets.refetch()} />
        ) : (
          <TicketResults response={tickets.data} params={params} busy={tickets.isPlaceholderData} />
        )}
      </section>
    </div>
  )
}

function summaryOf(response: PagedResponse<TicketListItem> | undefined, pending: boolean): string {
  if (pending) {
    return 'Loading tickets…'
  }
  if (!response || response.items.length === 0) {
    return ''
  }
  const first = (response.page - 1) * response.pageSize + 1
  const last = first + response.items.length - 1
  return `Showing ${first}–${last} of ${response.totalCount} tickets`
}

function TicketListSkeleton() {
  return (
    <div data-testid="ticket-list-skeleton" className="flex flex-col gap-3">
      {Array.from({ length: SKELETON_ROWS }, (_, row) => (
        <Skeleton key={row} className="h-10 w-full" />
      ))}
    </div>
  )
}

function TicketListError({ error, retrying, onRetry }: { error: Error; retrying: boolean; onRetry: () => void }) {
  // Defensive: the API lets every signed-in role list tickets, so it does not answer 403 here today.
  if (error instanceof ApiError && error.status === 403) {
    return <EmptyState title="You don't have permission to do this." />
  }
  return (
    <EmptyState
      title="Couldn't load tickets."
      description="Check your connection and try again."
      action={
        <Button loading={retrying} loadingLabel="Retrying…" onClick={onRetry}>
          Retry
        </Button>
      }
    />
  )
}

function TicketResults({
  response,
  params,
  busy,
}: {
  response: PagedResponse<TicketListItem>
  params: TicketListParams
  busy: boolean
}) {
  if (response.items.length === 0) {
    return response.totalCount === 0 ? (
      <EmptyState title="No tickets to show." />
    ) : (
      <EmptyState
        title="This page has no tickets."
        action={<Link to={{ search: writeTicketListParams({ ...params, page: 1 }).toString() }}>Go to the first page</Link>}
      />
    )
  }

  const pageCount = Math.ceil(response.totalCount / TICKET_PAGE_SIZE)
  return (
    <>
      {/* Its own horizontal scroll, so a narrow screen never scrolls the whole page sideways. */}
      <div aria-busy={busy || undefined} className="overflow-x-auto rounded-card border border-line bg-surface">
        <table className="w-full text-left text-sm">
          <thead className="border-b border-line text-xs tracking-wider text-muted uppercase">
            <tr>
              <th scope="col" className="px-4 py-3 font-semibold">Title</th>
              <th scope="col" className="px-4 py-3 font-semibold">Status</th>
              <th scope="col" className="px-4 py-3 font-semibold">Priority</th>
              <th scope="col" className="hidden px-4 py-3 font-semibold md:table-cell">Category</th>
              <th scope="col" className="hidden px-4 py-3 font-semibold lg:table-cell">Created by</th>
              <th scope="col" className="hidden px-4 py-3 font-semibold md:table-cell">Assigned to</th>
              <th scope="col" className="hidden px-4 py-3 font-semibold sm:table-cell">Created</th>
            </tr>
          </thead>
          <tbody>
            {response.items.map((ticket) => (
              <tr key={ticket.id} className="border-b border-line last:border-b-0">
                <td className="px-4 py-3 font-medium text-ink">{ticket.title}</td>
                <td className="px-4 py-3">
                  {isTicketStatus(ticket.status) ? <StatusBadge status={ticket.status} /> : ticket.status}
                </td>
                <td className="px-4 py-3">
                  {isTicketPriority(ticket.priority) ? <PriorityIndicator priority={ticket.priority} /> : ticket.priority}
                </td>
                <td className="hidden px-4 py-3 text-ink md:table-cell">{ticket.categoryName}</td>
                <td className="hidden px-4 py-3 text-ink lg:table-cell">{ticket.createdByName}</td>
                <td className="hidden px-4 py-3 text-ink md:table-cell">{ticket.assignedToName ?? 'Unassigned'}</td>
                <td className="hidden px-4 py-3 whitespace-nowrap text-muted sm:table-cell">
                  <time dateTime={ticket.createdAt}>{dateFormat.format(new Date(ticket.createdAt))}</time>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Pagination page={params.page} pageCount={pageCount} params={params} />
    </>
  )
}

function Pagination({ page, pageCount, params }: { page: number; pageCount: number; params: TicketListParams }) {
  if (pageCount <= 1) {
    return null
  }
  const linkTo = (target: number) => ({ search: writeTicketListParams({ ...params, page: target }).toString() })

  return (
    <nav aria-label="Pages" className="flex items-center justify-between gap-4 text-sm">
      {page > 1 ? (
        <Link to={linkTo(page - 1)} className="font-semibold text-primary">
          Previous page
        </Link>
      ) : (
        <span />
      )}
      <span className="text-muted">
        Page {page} of {pageCount}
      </span>
      {page < pageCount ? (
        <Link to={linkTo(page + 1)} className="font-semibold text-primary">
          Next page
        </Link>
      ) : (
        <span />
      )}
    </nav>
  )
}
