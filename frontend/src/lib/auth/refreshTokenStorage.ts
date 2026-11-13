export const REFRESH_TOKEN_KEY = 'helpdesk.refreshToken'

export interface RefreshTokenStorage {
  get(): string | null
  set(token: string): void
  remove(): void
}

// Blocked storage (privacy settings, a sandboxed frame) or a full quota must not break the app: a failed read means
// there is no saved session, and a failed write keeps the session in this tab's memory only.
function ignoringStorageErrors<T>(action: () => T, fallback: T): T {
  try {
    return action()
  } catch {
    return fallback
  }
}

// getStorage is a function because merely reading window.localStorage throws when storage is blocked.
export function createRefreshTokenStorage(getStorage: () => Storage): RefreshTokenStorage {
  return {
    get: () => ignoringStorageErrors(() => getStorage().getItem(REFRESH_TOKEN_KEY), null),
    set: (token) => ignoringStorageErrors(() => getStorage().setItem(REFRESH_TOKEN_KEY, token), undefined),
    remove: () => ignoringStorageErrors(() => getStorage().removeItem(REFRESH_TOKEN_KEY), undefined),
  }
}
