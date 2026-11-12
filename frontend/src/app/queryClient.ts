import { QueryClient } from '@tanstack/react-query'

// Library defaults until there are queries to tune retries and stale times for.
export function createQueryClient(): QueryClient {
  return new QueryClient()
}
