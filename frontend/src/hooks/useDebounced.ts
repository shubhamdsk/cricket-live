import { useEffect, useState } from 'react'

/**
 * The value as it was `delay` milliseconds ago, once it stops changing.
 *
 * Debouncing the value rather than the request is deliberate: a query key derived from a settled
 * value means TanStack Query handles the rest — caching, deduplication and cancelling a request
 * whose key is now stale — instead of this hook owning timers and abort signals as well.
 */
export function useDebounced<T>(value: T, delay: number): T {
  const [settled, setSettled] = useState(value)

  useEffect(() => {
    const timer = setTimeout(() => setSettled(value), delay)

    // Cleanup on every change is what makes it a debounce rather than a throttle: a keystroke
    // arriving before the timer fires cancels it and starts again.
    return () => clearTimeout(timer)
  }, [value, delay])

  return settled
}
