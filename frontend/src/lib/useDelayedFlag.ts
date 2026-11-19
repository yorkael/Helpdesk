import { useEffect, useState } from 'react'

// Under a quarter of a second a response reads as instant, so a loading state shown sooner only flickers.
export const LOADING_INDICATOR_DELAY_MS = 250

/** True once `active` has stayed true for the delay; false again as soon as it turns false. */
export function useDelayedFlag(active: boolean, delayMs = LOADING_INDICATOR_DELAY_MS): boolean {
  const [elapsed, setElapsed] = useState(false)

  useEffect(() => {
    if (!active) {
      return
    }
    const timer = setTimeout(() => setElapsed(true), delayMs)
    // Reset in the cleanup, so the next piece of work waits the whole delay again.
    return () => {
      clearTimeout(timer)
      setElapsed(false)
    }
  }, [active, delayMs])

  // Checked here too: the cleanup only runs after this render, which would otherwise still show the flag.
  return active && elapsed
}
