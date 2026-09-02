import { act, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useDebouncedValue } from './useDebouncedValue'

describe('useDebouncedValue', () => {
  afterEach(() => vi.useRealTimers())

  it('publishes only the latest value after the debounce interval', () => {
    vi.useFakeTimers()
    const { result, rerender } = renderHook(({ value }) => useDebouncedValue(value, 300), { initialProps: { value: 'first' } })
    rerender({ value: 'second' }); rerender({ value: 'latest' })
    act(() => vi.advanceTimersByTime(299)); expect(result.current).toBe('first')
    act(() => vi.advanceTimersByTime(1)); expect(result.current).toBe('latest')
  })
})
