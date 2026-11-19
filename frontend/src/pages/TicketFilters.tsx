import { useQuery } from '@tanstack/react-query'
import type { FormEvent } from 'react'
import { useApiClient } from '../app/session/sessionContext'
import type { CurrentUser } from '../lib/auth/types'
import {
  isTicketPriority,
  isTicketStatus,
  TICKET_PRIORITIES,
  TICKET_PRIORITY_LABELS,
  TICKET_STATUSES,
  TICKET_STATUS_LABELS,
} from '../lib/ticket'
import type { TicketListParams } from '../lib/tickets/listParams'
import { Button } from '../ui/Button'
import { Field, Input, Select } from '../ui/Field'

// GET /api/categories and GET /api/agents (CategoryResponse and AgentResponse in Helpdesk.Application).
interface NamedOption {
  id: string
  name: string
}

const NO_FILTERS: TicketListParams = {
  page: 1,
  status: null,
  priority: null,
  assignedToId: null,
  categoryId: null,
  search: null,
}

type TicketFiltersProps = {
  params: TicketListParams
  viewer: CurrentUser
  onChange: (params: TicketListParams) => void
}

export function TicketFilters({ params, viewer, onChange }: TicketFiltersProps) {
  const api = useApiClient()
  const categories = useQuery({
    queryKey: ['categories'],
    queryFn: ({ signal }) => api.request<NamedOption[]>('/api/categories', { signal }),
  })
  // Only an admin may read the agents; an agent's only assignee filter is themselves.
  const agents = useQuery({
    queryKey: ['agents'],
    queryFn: ({ signal }) => api.request<NamedOption[]>('/api/agents', { signal }),
    enabled: viewer.role === 'Admin',
  })

  // Any change starts again from the first page: the old page number means nothing for another result.
  function apply(changes: Partial<TicketListParams>) {
    onChange({ ...params, ...changes, page: 1 })
  }

  function search(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const text = String(new FormData(event.currentTarget).get('search') ?? '').trim()
    apply({ search: text === '' ? null : text })
  }

  const categoryOptions = categories.data ?? []
  const agentOptions = agents.data ?? []
  const active = describeFilters(params, viewer, categoryOptions, agentOptions)

  return (
    <div className="flex flex-col gap-4">
      {/* Sent with Enter or the button, not while typing: one request per search, and no timer to wait for. */}
      {/* Keyed by the search in the address, so clearing the filters also empties the field. */}
      {/* oxlint-disable-next-line jsx-a11y/prefer-tag-over-role -- <search> is newer than role="search" in assistive technology, and jsdom does not expose it as a landmark either */}
      <form key={params.search ?? ''} role="search" aria-label="Tickets" onSubmit={search} className="flex items-end gap-2">
        <Field label="Search tickets" className="flex-1">
          <Input name="search" type="search" maxLength={200} defaultValue={params.search ?? ''} />
        </Field>
        <Button type="submit" variant="secondary">
          Search
        </Button>
      </form>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Field label="Status">
          <Select
            value={params.status ?? ''}
            onChange={(event) => apply({ status: isTicketStatus(event.target.value) ? event.target.value : null })}
          >
            <option value="">Any status</option>
            {TICKET_STATUSES.map((status) => (
              <option key={status} value={status}>
                {TICKET_STATUS_LABELS[status]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Priority">
          <Select
            value={params.priority ?? ''}
            onChange={(event) => apply({ priority: isTicketPriority(event.target.value) ? event.target.value : null })}
          >
            <option value="">Any priority</option>
            {TICKET_PRIORITIES.map((priority) => (
              <option key={priority} value={priority}>
                {TICKET_PRIORITY_LABELS[priority]}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Category" hint={categories.isError ? "Couldn't load the categories." : undefined}>
          <Select value={params.categoryId ?? ''} onChange={(event) => apply({ categoryId: event.target.value || null })}>
            <option value="">Any category</option>
            <NamedOptions options={categoryOptions} selected={params.categoryId} unknownLabel="Selected category" />
          </Select>
        </Field>
        {viewer.role === 'Admin' ? (
          <Field label="Assigned to" hint={agents.isError ? "Couldn't load the agents." : undefined}>
            <Select value={params.assignedToId ?? ''} onChange={(event) => apply({ assignedToId: event.target.value || null })}>
              {/* No "unassigned" choice: the API has no filter for it. */}
              <option value="">Anyone</option>
              <NamedOptions options={agentOptions} selected={params.assignedToId} unknownLabel="Selected agent" />
            </Select>
          </Field>
        ) : (
          <label className="flex items-center gap-2 self-end text-sm font-semibold text-ink sm:h-10">
            <input
              type="checkbox"
              checked={params.assignedToId === viewer.id}
              onChange={(event) => apply({ assignedToId: event.target.checked ? viewer.id : null })}
              className="size-4"
            />
            Assigned to me
          </label>
        )}
      </div>
      {active.length > 0 && (
        <div className="flex flex-wrap items-center gap-3 text-sm">
          <p className="text-ink">Filtered by {active.join(', ')}</p>
          <Button variant="ghost" onClick={() => onChange(NO_FILTERS)}>
            Clear filters
          </Button>
        </div>
      )}
    </div>
  )
}

// An id in the address that the list does not hold (it failed to load, or the item is gone) keeps its own
// option, so the select still shows that a filter is on.
function NamedOptions({ options, selected, unknownLabel }: { options: NamedOption[]; selected: string | null; unknownLabel: string }) {
  const known = options.some((option) => option.id === selected)
  return (
    <>
      {selected !== null && !known && <option value={selected}>{unknownLabel}</option>}
      {options.map((option) => (
        <option key={option.id} value={option.id}>
          {option.name}
        </option>
      ))}
    </>
  )
}

function describeFilters(
  params: TicketListParams,
  viewer: CurrentUser,
  categories: NamedOption[],
  agents: NamedOption[],
): string[] {
  const nameOf = (options: NamedOption[], id: string, fallback: string) =>
    options.find((option) => option.id === id)?.name ?? fallback
  const parts: string[] = []
  if (params.status) parts.push(`status ${TICKET_STATUS_LABELS[params.status]}`)
  if (params.priority) parts.push(`priority ${TICKET_PRIORITY_LABELS[params.priority]}`)
  if (params.categoryId) parts.push(`category ${nameOf(categories, params.categoryId, 'selected')}`)
  if (params.assignedToId) {
    parts.push(params.assignedToId === viewer.id ? 'assigned to me' : `assigned to ${nameOf(agents, params.assignedToId, 'the selected agent')}`)
  }
  if (params.search) parts.push(`search “${params.search}”`)
  return parts
}
