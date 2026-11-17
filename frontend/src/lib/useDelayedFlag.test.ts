import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useDelayedFlag } from './useDelayedFlag'

const renderFlag = (active: boolean) =>
  renderHook(({ active: isActive }: { active: boolean }) => useDelayedFlag(isActive), { initialProps: { active } })

const advance = (ms: number) =>
  act(() => {
    vi.advanceTimersByTime(ms)
  })

describe('useDelayedFlag', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('stays off while there is no work', () => {
    const { result } = renderFlag(false)
    advance(1000)

    expect(result.current).toBe(false)
  })

  it('turns on only once the work has lasted 250 ms', () => {
    const { result } = renderFlag(true)
    advance(249)
    const before = result.current
    advance(1)

    expect({ before, after: result.current }).toEqual({ before: false, after: true })
  })

  // Every render is recorded: the last value alone would hide one render that still showed the flag.
  it('turns off in the first render after the work ends', () => {
    const renders: { active: boolean; flag: boolean }[] = []
    const { rerender } = renderHook(
      ({ active }: { active: boolean }) => {
        const flag = useDelayedFlag(active)
        renders.push({ active, flag })
        return flag
      },
      { initialProps: { active: true } },
    )
    advance(250)
    rerender({ active: false })
    const afterTheWork = renders.filter((render) => !render.active)

    expect(afterTheWork).toEqual(afterTheWork.map(() => ({ active: false, flag: false })))
  })

  // Its own delay, so this test checks the restart and not the default length.
  it('makes the next piece of work wait the whole delay again', () => {
    const { result, rerender } = renderHook(({ active }: { active: boolean }) => useDelayedFlag(active, 100), {
      initialProps: { active: true },
    })
    advance(100)
    rerender({ active: false })
    rerender({ active: true })
    const atStart = result.current
    advance(99)
    const before = result.current
    advance(1)

    expect({ atStart, before, after: result.current }).toEqual({ atStart: false, before: false, after: true })
  })
})
