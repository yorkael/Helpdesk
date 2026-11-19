import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { SessionContext } from '../app/session/sessionContext'
import { ApiError } from '../lib/api/ApiError'
import type { CurrentUser } from '../lib/auth/types'
import type { TicketListParams } from '../lib/tickets/listParams'
import { createFakeSession, userWithRole } from '../test/fakeSession'
import { TicketFilters } from './TicketFilters'

const BILLING = { id: '10000000-0000-4000-8000-000000000002', name: 'Billing' }
const CATEGORIES = [{ id: '10000000-0000-4000-8000-000000000001', name: 'Account' }, BILLING]
const ANA = { id: '30000000-0000-4000-8000-000000000001', name: 'Ana Agent' }
const BRUNO = { id: '30000000-0000-4000-8000-000000000002', name: 'Bruno Agent' }

const AGENT: CurrentUser = { ...userWithRole('Agent'), id: ANA.id }
const ADMIN: CurrentUser = { ...userWithRole('Admin'), id: '30000000-0000-4000-8000-000000000009' }

const NO_FILTERS: TicketListParams = { page: 1, status: null, priority: null, assignedToId: null, categoryId: null, search: null }

type Answers = { categories?: () => Promise<unknown>; agents?: () => Promise<unknown> }

// The API client is the fake session's; each list answers from here, and any other request fails the test.
function renderFilters(viewer: CurrentUser, params: TicketListParams = NO_FILTERS, answers: Answers = {}) {
  const fake = createFakeSession('authenticated', viewer)
  fake.request.mockImplementation(async (path) => {
    if (path === '/api/categories') return (answers.categories ?? (async () => CATEGORIES))()
    if (path === '/api/agents') return (answers.agents ?? (async () => [ANA, BRUNO]))()
    throw new Error(`Unexpected request to ${path}`)
  })
  const onChange = vi.fn<(params: TicketListParams) => void>()
  const { unmount } = render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <SessionContext value={fake.session}>
        <TicketFilters params={params} viewer={viewer} onChange={onChange} />
      </SessionContext>
    </QueryClientProvider>,
  )
  return { fake, onChange, unmount, user: userEvent.setup() }
}

const optionsOf = (select: HTMLElement) => within(select).getAllByRole('option').map((option) => option.textContent)
const lastChange = (onChange: ReturnType<typeof renderFilters>['onChange']) => onChange.mock.lastCall![0]

describe('TicketFilters', () => {
  it('applies a status at once and starts again from page 1', async () => {
    const { onChange, user } = renderFilters(AGENT, { ...NO_FILTERS, page: 3, priority: 'High' })

    await user.selectOptions(screen.getByLabelText('Status'), 'In progress')

    expect(lastChange(onChange)).toEqual({ ...NO_FILTERS, page: 1, priority: 'High', status: 'InProgress' })
  })

  it('applies a priority at once', async () => {
    const { onChange, user } = renderFilters(AGENT)

    await user.selectOptions(screen.getByLabelText('Priority'), 'Urgent')

    expect(lastChange(onChange).priority).toBe('Urgent')
  })

  it('searches on Enter, with the text trimmed', async () => {
    const { onChange, user } = renderFilters(AGENT)

    await user.type(within(screen.getByRole('search', { name: 'Tickets' })).getByLabelText('Search tickets'), '  jam  {Enter}')

    expect(lastChange(onChange).search).toBe('jam')
  })

  it('offers the categories from the API', async () => {
    const { onChange, user } = renderFilters(AGENT)
    const category = screen.getByLabelText('Category')

    await screen.findByRole('option', { name: 'Billing' })
    await user.selectOptions(category, 'Billing')

    expect({ options: optionsOf(category), chosen: lastChange(onChange).categoryId }).toEqual({
      options: ['Any category', 'Account', 'Billing'],
      chosen: BILLING.id,
    })
  })

  it('keeps showing a selected category that the list does not hold', () => {
    renderFilters(AGENT, { ...NO_FILTERS, categoryId: '10000000-0000-4000-8000-000000000099' })

    expect(screen.getByLabelText('Category')).toHaveDisplayValue('Selected category')
  })

  it('says when the categories could not load, and still offers the other filters', async () => {
    renderFilters(AGENT, NO_FILTERS, { categories: () => Promise.reject(new ApiError(500, null)) })

    await vi.waitFor(() => expect(screen.getByLabelText('Category')).toHaveAccessibleDescription("Couldn't load the categories."))
    expect(optionsOf(screen.getByLabelText('Status'))).toHaveLength(6)
  })

  it("never asks for the agents in an agent's list", async () => {
    const { fake } = renderFilters(AGENT)

    // Both lists would be asked for on the first render, so once one request is out, the other would be too.
    await vi.waitFor(() => expect(fake.request).toHaveBeenCalled())

    expect(fake.request.mock.calls.map(([path]) => path)).toEqual(['/api/categories'])
  })

  it('filters an agent\'s list by their own id with "Assigned to me"', async () => {
    const { onChange, user } = renderFilters(AGENT)

    await user.click(screen.getByRole('checkbox', { name: 'Assigned to me' }))

    expect(lastChange(onChange).assignedToId).toBe(AGENT.id)
  })

  it('lets an admin choose any agent, with no choice for unassigned tickets', async () => {
    const { onChange, user } = renderFilters(ADMIN)
    const assignee = screen.getByLabelText('Assigned to')

    await screen.findByRole('option', { name: 'Bruno Agent' })
    await user.selectOptions(assignee, 'Bruno Agent')

    expect({ options: optionsOf(assignee), chosen: lastChange(onChange).assignedToId }).toEqual({
      options: ['Anyone', 'Ana Agent', 'Bruno Agent'],
      chosen: BRUNO.id,
    })
  })

  it('says when the agents could not load', async () => {
    renderFilters(ADMIN, NO_FILTERS, { agents: () => Promise.reject(new ApiError(500, null)) })

    await vi.waitFor(() => expect(screen.getByLabelText('Assigned to')).toHaveAccessibleDescription("Couldn't load the agents."))
  })

  it('says which filters are on, in words, naming the category and the agent once their lists arrive', async () => {
    const filtersText = () => screen.getByText(/^Filtered by/).textContent
    const ownFilters = renderFilters(AGENT, { page: 2, status: 'Open', priority: 'High', categoryId: null, assignedToId: AGENT.id, search: 'jam' })
    const own = filtersText()
    ownFilters.unmount()

    renderFilters(ADMIN, { ...NO_FILTERS, categoryId: BILLING.id, assignedToId: BRUNO.id })

    // Waits on the text itself, so the select's options play no part.
    await vi.waitFor(() =>
      expect({ own, named: filtersText() }).toEqual({
        own: 'Filtered by status Open, priority High, assigned to me, search “jam”',
        named: 'Filtered by category Billing, assigned to Bruno Agent',
      }),
    )
  })

  it('clears every filter', async () => {
    const { onChange, user } = renderFilters(AGENT, { page: 2, status: 'Open', priority: null, categoryId: null, assignedToId: null, search: 'jam' })

    await user.click(screen.getByRole('button', { name: 'Clear filters' }))

    expect(lastChange(onChange)).toEqual(NO_FILTERS)
  })
})
