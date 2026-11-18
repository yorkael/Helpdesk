import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { SessionContext } from '../app/session/sessionContext'
import { ApiError } from '../lib/api/ApiError'
import type { PagedResponse, TicketListItem } from '../lib/tickets/types'
import { createFakeSession, userWithRole } from '../test/fakeSession'
import { TicketListPage } from './TicketListPage'

function ticket(number: number, overrides: Partial<TicketListItem> = {}): TicketListItem {
  return {
    id: `00000000-0000-4000-8000-${String(number).padStart(12, '0')}`,
    title: `Ticket ${number}`,
    status: 'Open',
    priority: 'Medium',
    categoryId: '10000000-0000-4000-8000-000000000001',
    categoryName: 'Billing',
    createdById: '20000000-0000-4000-8000-000000000001',
    createdByName: 'Carla Client',
    assignedToId: null,
    assignedToName: null,
    createdAt: '2026-11-16T22:00:54.037244+00:00',
    ...overrides,
  }
}

function pageOf(items: TicketListItem[], page = 1, totalCount = items.length): PagedResponse<TicketListItem> {
  return { items, page, pageSize: 20, totalCount }
}

const tickets = (from: number, to: number) => Array.from({ length: to - from + 1 }, (_, index) => ticket(from + index))

// A component test: the API client is the fake session's, and nothing retries, so an error shows at once.
function renderListAt(path: string, answer: (request: FakeRequest) => void) {
  const fake = createFakeSession('authenticated', userWithRole('Agent'))
  // Set before the first render, which already sends the request.
  answer(fake.request)
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter([{ path: '/tickets', Component: TicketListPage }], { initialEntries: [path] })
  render(
    <QueryClientProvider client={queryClient}>
      <SessionContext value={fake.session}>
        <RouterProvider router={router} />
      </SessionContext>
    </QueryClientProvider>,
  )
  return { fake, router }
}

type FakeRequest = ReturnType<typeof createFakeSession>['request']

const cellsOf = (row: HTMLElement) => within(row).getAllByRole('cell').map((cell) => cell.textContent)

describe('TicketListPage', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('asks the API for the page and the filters in the address', async () => {
    const { fake } = renderListAt('/tickets?page=2&status=Open', (request) => request.mockResolvedValue(pageOf([], 2, 26)))

    await vi.waitFor(() => expect(fake.request).toHaveBeenCalled())

    expect(fake.request.mock.calls.map(([path]) => path)).toEqual(['/api/tickets?page=2&status=Open&pageSize=20'])
  })

  it("shows each ticket's title, status, priority, category, creator, assignee and creation date", async () => {
    renderListAt('/tickets', (request) =>
      request.mockResolvedValue(
        pageOf([ticket(1, { status: 'InProgress', priority: 'Urgent', assignedToId: 'a', assignedToName: 'Ana Agent' })]),
      ),
    )

    const row = (await screen.findByText('Ticket 1')).closest('tr')!

    expect(cellsOf(row).slice(0, 6)).toEqual(['Ticket 1', 'In progress', 'Urgent', 'Billing', 'Carla Client', 'Ana Agent'])
    // The date is checked by its machine-readable value; the text depends on the locale and the time zone.
    expect(within(row).getByRole('time')).toHaveAttribute('dateTime', '2026-11-16T22:00:54.037244+00:00')
  })

  it('says a ticket nobody has taken is unassigned', async () => {
    renderListAt('/tickets', (request) => request.mockResolvedValue(pageOf([ticket(1)])))

    const row = (await screen.findByText('Ticket 1')).closest('tr')!

    expect(cellsOf(row)[5]).toBe('Unassigned')
  })

  it('shows a status this build does not know as the API sent it', async () => {
    renderListAt('/tickets', (request) => request.mockResolvedValue(pageOf([ticket(1, { status: 'Escalated' })])))

    const row = (await screen.findByText('Ticket 1')).closest('tr')!

    expect(cellsOf(row)[1]).toBe('Escalated')
  })

  it('shows a priority this build does not know as the API sent it', async () => {
    renderListAt('/tickets', (request) => request.mockResolvedValue(pageOf([ticket(1, { priority: 'Critical' })])))

    const row = (await screen.findByText('Ticket 1')).closest('tr')!

    expect(cellsOf(row)[2]).toBe('Critical')
  })

  it('waits before showing a skeleton, so a fast answer never flashes one', () => {
    vi.useFakeTimers()
    renderListAt('/tickets', (request) => request.mockReturnValue(new Promise(() => undefined)))

    expect({
      status: screen.getByRole('status').textContent,
      skeleton: screen.queryByTestId('ticket-list-skeleton'),
    }).toEqual({ status: 'Loading tickets…', skeleton: null })
  })

  it('shows a skeleton once the first page has taken 250 ms', () => {
    vi.useFakeTimers()
    renderListAt('/tickets', (request) => request.mockReturnValue(new Promise(() => undefined)))

    act(() => {
      vi.advanceTimersByTime(250)
    })

    expect(screen.getByTestId('ticket-list-skeleton')).toBeInTheDocument()
  })

  it('announces which tickets it shows', async () => {
    renderListAt('/tickets?page=2', (request) => request.mockResolvedValue(pageOf(tickets(21, 26), 2, 26)))

    await screen.findByText('Ticket 21')

    expect(screen.getByRole('status')).toHaveTextContent('Showing 21–26 of 26 tickets')
  })

  it('says when there are no tickets', async () => {
    renderListAt('/tickets', (request) => request.mockResolvedValue(pageOf([])))

    expect(await screen.findByRole('heading', { name: 'No tickets to show.' })).toBeInTheDocument()
  })

  it('offers the first page, with the same filters, past the last page', async () => {
    renderListAt('/tickets?page=99&priority=High', (request) => request.mockResolvedValue(pageOf([], 99, 26)))

    expect(await screen.findByRole('link', { name: 'Go to the first page' })).toHaveAttribute('href', '/tickets?priority=High')
  })

  it('lets the user try again after an error', async () => {
    const user = userEvent.setup()
    renderListAt('/tickets', (request) => request.mockRejectedValueOnce(new ApiError(500, null)).mockResolvedValueOnce(pageOf([ticket(1)])))

    await user.click(await screen.findByRole('button', { name: 'Retry' }))

    expect(await screen.findByText('Ticket 1')).toBeInTheDocument()
  })

  it('says a 403 is not allowed, without a retry and keeping the session', async () => {
    const { fake } = renderListAt('/tickets', (request) => request.mockRejectedValue(new ApiError(403, null)))

    expect(await screen.findByRole('heading', { name: "You don't have permission to do this." })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Retry' })).not.toBeInTheDocument()
    expect(fake.manager.expire).not.toHaveBeenCalled()
    expect(fake.manager.logout).not.toHaveBeenCalled()
  })

  it('links to the previous and the next page, keeping the filters', async () => {
    renderListAt('/tickets?page=2&status=Open', (request) => request.mockResolvedValue(pageOf(tickets(21, 40), 2, 50)))

    const pages = await screen.findByRole('navigation', { name: 'Pages' })

    expect({
      previous: within(pages).getByRole('link', { name: 'Previous page' }).getAttribute('href'),
      next: within(pages).getByRole('link', { name: 'Next page' }).getAttribute('href'),
      position: within(pages).getByText(/^Page /).textContent,
    }).toEqual({ previous: '/tickets?status=Open', next: '/tickets?page=3&status=Open', position: 'Page 2 of 3' })
  })

  it('keeps the rows of the page it leaves, marked busy, until the next one arrives', async () => {
    const user = userEvent.setup()
    renderListAt('/tickets', (request) => request.mockResolvedValueOnce(pageOf(tickets(1, 20), 1, 26)).mockReturnValueOnce(new Promise(() => undefined)))

    await user.click(await screen.findByRole('link', { name: 'Next page' }))

    expect(screen.getByText('Ticket 1')).toBeInTheDocument()
    expect(screen.getByRole('table').parentElement).toHaveAttribute('aria-busy', 'true')
  })

  it('moves focus to the results after a page change', async () => {
    const user = userEvent.setup()
    renderListAt('/tickets', (request) => request.mockResolvedValueOnce(pageOf(tickets(1, 20), 1, 26)).mockResolvedValueOnce(pageOf(tickets(21, 26), 2, 26)))

    await user.click(await screen.findByRole('link', { name: 'Next page' }))
    await screen.findByText('Ticket 21')

    expect(screen.getByRole('heading', { name: 'Results' })).toHaveFocus()
  })

  it('rewrites an address it cannot use, without a new history entry', async () => {
    const { router } = renderListAt('/tickets?page=abc&status=open&search=%20jam%20', (request) =>
      request.mockResolvedValue(pageOf([ticket(1)])),
    )

    await screen.findByText('Ticket 1')

    expect({ search: router.state.location.search, action: router.state.historyAction }).toEqual({
      search: '?search=jam',
      action: 'REPLACE',
    })
  })
})
