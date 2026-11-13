const LOCK_NAME = 'helpdesk-refresh'

export interface RefreshLock {
  run<T>(task: () => Promise<T>): Promise<T>
}

// Web Locks serialize the refresh across every tab of the origin. Without them (old browsers, jsdom) the fallback
// only serializes within this tab, so two tabs can present the same refresh token and the API then revokes every
// session of the user.
export function createRefreshLock(locks: LockManager | undefined): RefreshLock {
  if (locks) {
    return { run: (task) => locks.request(LOCK_NAME, task) }
  }

  let tail: Promise<unknown> = Promise.resolve()
  return {
    run(task) {
      const result = tail.then(task)
      // A failed task must not block the ones queued after it.
      tail = result.catch(() => undefined)
      return result
    },
  }
}
