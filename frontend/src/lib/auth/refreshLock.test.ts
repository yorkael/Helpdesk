import { describe, expect, it, vi } from 'vitest'
import { createRefreshLock } from './refreshLock'

describe('createRefreshLock', () => {
  it('uses Web Locks under one name when the browser has them', async () => {
    const request = vi.fn<(name: string, task: () => Promise<string>) => Promise<string>>((_name, task) => task())
    const lock = createRefreshLock({ request } as unknown as LockManager)

    await expect(lock.run(async () => 'done')).resolves.toBe('done')
    expect(request).toHaveBeenCalledWith('helpdesk-refresh', expect.any(Function))
  })

  it('without Web Locks, runs one task at a time within the tab', async () => {
    const lock = createRefreshLock(undefined)
    const events: string[] = []
    let finishFirst!: () => void

    const first = lock.run(async () => {
      events.push('first started')
      await new Promise<void>((resolve) => {
        finishFirst = resolve
      })
      events.push('first finished')
    })
    const second = lock.run(async () => {
      events.push('second started')
    })
    await vi.waitFor(() => expect(events).toContain('first started'))
    await Promise.resolve()

    expect(events).toEqual(['first started'])
    finishFirst()
    await Promise.all([first, second])
    expect(events).toEqual(['first started', 'first finished', 'second started'])
  })

  it('without Web Locks, a failed task does not block the next one', async () => {
    const lock = createRefreshLock(undefined)

    const failed = lock.run(async () => {
      throw new Error('Refresh failed')
    })
    const next = lock.run(async () => 'next ran')

    await expect(failed).rejects.toThrow('Refresh failed')
    await expect(next).resolves.toBe('next ran')
  })
})
