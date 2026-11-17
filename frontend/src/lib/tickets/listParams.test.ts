import { describe, expect, it } from 'vitest'
import { readTicketListParams, ticketListPath, writeTicketListParams, type TicketListParams } from './listParams'

const AGENT_ID = '0d7a3c52-6f1e-4b8a-9c2d-3e4f5a6b7c8d'
const CATEGORY_ID = 'A1B2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D'

const read = (query: string) => readTicketListParams(new URLSearchParams(query))

const defaults: TicketListParams = {
  page: 1,
  status: null,
  priority: null,
  assignedToId: null,
  categoryId: null,
  search: null,
}

describe('readTicketListParams', () => {
  it('starts on page 1 with no filters when the address has none', () => {
    expect(read('')).toEqual(defaults)
  })

  it('reads every filter the API accepts', () => {
    const query = `page=3&status=WaitingOnCustomer&priority=Urgent&assignedToId=${AGENT_ID}&categoryId=${CATEGORY_ID}&search=printer`

    expect(read(query)).toEqual({
      page: 3,
      status: 'WaitingOnCustomer',
      priority: 'Urgent',
      assignedToId: AGENT_ID,
      categoryId: CATEGORY_ID,
      search: 'printer',
    })
  })

  it('takes the first value of a repeated parameter, as the API does', () => {
    expect(read('status=Closed&status=Open').status).toBe('Closed')
  })

  it('ignores a page that is not a positive integer', () => {
    const pages = ['0', '-2', '1.5', '2e1', 'abc', '', ' 2']

    expect(pages.map((page) => ({ page, read: read(`page=${encodeURIComponent(page)}`).page }))).toEqual(
      pages.map((page) => ({ page, read: 1 })),
    )
  })

  it('drops a page the API would reject as too large', () => {
    // The API skips (page - 1) * 20 rows as a 32-bit int: 107374183 is the last page it accepts.
    const pages = ['107374183', '107374184', '99999999999999999999']

    expect(pages.map((page) => ({ page, read: read(`page=${page}`).page }))).toEqual([
      { page: '107374183', read: 107374183 },
      { page: '107374184', read: 1 },
      { page: '99999999999999999999', read: 1 },
    ])
  })

  it('drops a status or priority the API would reject', () => {
    const cases = ['status=open', 'status=IN_PROGRESS', 'status=1', 'status=', 'priority=urgent', 'priority=Critical']

    expect(cases.map((query) => ({ query, ...pick(read(query), 'status', 'priority') }))).toEqual(
      cases.map((query) => ({ query, status: null, priority: null })),
    )
  })

  it('drops ids that are not GUIDs', () => {
    const ids = ['nope', '42', '0d7a3c526f1e4b8a9c2d3e4f5a6b7c8d', `{${AGENT_ID}}`, '']

    expect(
      ids.map((id) => ({ id, ...pick(read(`assignedToId=${id}&categoryId=${id}`), 'assignedToId', 'categoryId') })),
    ).toEqual(ids.map((id) => ({ id, assignedToId: null, categoryId: null })))
  })

  it('trims the search and drops one that is only whitespace', () => {
    const longest = 'a'.repeat(200)

    // The limit applies to the trimmed text, because that is what is sent.
    expect({
      padded: read('search=%20%20jam%20').search,
      blank: read('search=%20%20%20').search,
      longestPadded: read(`search=%20${longest}%20`).search,
    }).toEqual({ padded: 'jam', blank: null, longestPadded: longest })
  })

  it('drops a search longer than the API accepts', () => {
    const lengths = [200, 201, 1000]

    expect(lengths.map((length) => ({ length, kept: read(`search=${'a'.repeat(length)}`).search !== null }))).toEqual([
      { length: 200, kept: true },
      { length: 201, kept: false },
      { length: 1000, kept: false },
    ])
  })
})

describe('writing the params', () => {
  it('leaves page 1 and the filters not set out of the address and the request', () => {
    // The page size is left to the test below, so this one only sees what is left out.
    const requestKeys = [...requestQuery(ticketListPath(defaults)).keys()].filter((key) => key !== 'pageSize')

    expect({ address: writeTicketListParams(defaults).toString(), requestKeys }).toEqual({ address: '', requestKeys: [] })
  })

  it('asks the API for the page and the filters, 20 tickets per page', () => {
    const params: TicketListParams = {
      page: 2,
      status: 'Open',
      priority: 'High',
      assignedToId: AGENT_ID,
      categoryId: CATEGORY_ID,
      search: '100% done',
    }
    const path = ticketListPath(params)

    expect(path.startsWith('/api/tickets?')).toBe(true)
    expect(Object.fromEntries(requestQuery(path))).toEqual({
      page: '2',
      status: 'Open',
      priority: 'High',
      assignedToId: AGENT_ID,
      categoryId: CATEGORY_ID,
      search: '100% done',
      pageSize: '20',
    })
  })

  it('writes an address that reads back as the same params', () => {
    const params: TicketListParams = { ...defaults, page: 4, status: 'Resolved', search: 'a&b=c' }

    expect(read(writeTicketListParams(params).toString())).toEqual(params)
  })
})

function requestQuery(path: string): URLSearchParams {
  return new URLSearchParams(path.slice(path.indexOf('?') + 1))
}

function pick<K extends keyof TicketListParams>(params: TicketListParams, ...keys: K[]): Pick<TicketListParams, K> {
  return Object.fromEntries(keys.map((key) => [key, params[key]])) as Pick<TicketListParams, K>
}
