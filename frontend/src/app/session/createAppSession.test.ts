import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createApiClient, type ApiClient } from '../../lib/api/apiClient'
import { createAppSession } from './createAppSession'

// The client is replaced at its boundary: these tests check what createAppSession hands it, not how it handles
// a 401, which apiClient.test covers.
vi.mock('../../lib/api/apiClient', () => ({ createApiClient: vi.fn<typeof createApiClient>() }))

const createApiClientMock = vi.mocked(createApiClient)

function createWithPort() {
  const client: ApiClient = { request: () => Promise.reject(new Error('Not used in these tests')) }
  createApiClientMock.mockReturnValue(client)
  const session = createAppSession()
  const port = createApiClientMock.mock.calls[0]![1]
  return { session, port, client }
}

describe('createAppSession', () => {
  beforeEach(() => {
    createApiClientMock.mockReset()
    localStorage.clear()
  })

  it('gives the session the API client built for it', () => {
    const { session, client } = createWithPort()

    expect(createApiClientMock).toHaveBeenCalledOnce()
    expect(session.api).toBe(client)
  })

  // Each spy is set after the session is built, so the port passes only if it asks the manager on every call.
  it("reads the manager's access token each time the client asks", () => {
    const { session, port } = createWithPort()
    vi.spyOn(session.manager, 'accessToken').mockReturnValueOnce('access-1').mockReturnValueOnce('access-2')

    expect([port.accessToken(), port.accessToken()]).toEqual(['access-1', 'access-2'])
  })

  it('refreshes through the manager', async () => {
    const { session, port } = createWithPort()
    const refresh = vi.spyOn(session.manager, 'refresh').mockResolvedValue(undefined)

    await port.refresh()

    expect(refresh).toHaveBeenCalledOnce()
  })

  it('expires the session through the manager', () => {
    const { session, port } = createWithPort()
    const expire = vi.spyOn(session.manager, 'expire').mockResolvedValue(undefined)

    port.expire()

    expect(expire).toHaveBeenCalledOnce()
  })
})
