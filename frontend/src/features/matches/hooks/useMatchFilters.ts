import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router-dom'

import type { MatchFilterParams } from '@/services/endpoints'

export type StatusFilter = 'all' | 'live' | 'upcoming' | 'completed'

export interface MatchFilters {
  status: StatusFilter
  /** A calendar day as `YYYY-MM-DD`, read in the reader's own timezone. Empty means any day. */
  date: string
  series: string
}

export interface MatchFilterState {
  filters: MatchFilters
  /** What the API is asked for: the same thing, with the day resolved to a pair of instants. */
  params: MatchFilterParams
  isFiltered: boolean
  set: (change: Partial<MatchFilters>) => void
  clear: () => void
}

const statuses: readonly StatusFilter[] = ['all', 'live', 'upcoming', 'completed']

function readStatus(value: string | null): StatusFilter {
  // An unrecognised status in a hand-edited URL falls back to showing everything, which is a
  // better answer than an empty page or an error about a query string.
  return statuses.includes(value as StatusFilter) ? (value as StatusFilter) : 'all'
}

/**
 * Turns a calendar day into the instants that bound it.
 *
 * `new Date('2026-03-15')` is UTC midnight, but a reader picking the 15th means their own 15th,
 * so the parts are passed separately to get local midnight. The API takes instants precisely so
 * this conversion happens where the timezone is actually known.
 */
function dayToRange(date: string): { from?: string; to?: string } {
  const parts = date.split('-').map(Number)

  if (parts.length !== 3 || parts.some(Number.isNaN)) {
    return {}
  }

  const [year, month, day] = parts
  const from = new Date(year, month - 1, day)
  const to = new Date(year, month - 1, day + 1)

  return { from: from.toISOString(), to: to.toISOString() }
}

/**
 * Filter state held in the URL rather than in component state.
 *
 * A filtered view is then something you can link someone, reload, and reach with the back button.
 * Holding it in `useState` would make all three quietly stop working, and none of them announce
 * themselves as broken.
 */
export function useMatchFilters(): MatchFilterState {
  const [searchParams, setSearchParams] = useSearchParams()

  const filters = useMemo<MatchFilters>(
    () => ({
      status: readStatus(searchParams.get('status')),
      date: searchParams.get('date') ?? '',
      series: searchParams.get('series') ?? '',
    }),
    [searchParams],
  )

  const params = useMemo<MatchFilterParams>(
    () => ({
      status: filters.status === 'all' ? undefined : filters.status,
      series: filters.series || undefined,
      ...(filters.date ? dayToRange(filters.date) : {}),
    }),
    [filters],
  )

  const set = useCallback(
    (change: Partial<MatchFilters>) => {
      const next = { ...filters, ...change }

      const query = new URLSearchParams()

      // Only what is actually set, so the default view has a clean URL and two ways of expressing
      // "no filter" do not produce two different cache keys.
      if (next.status !== 'all') query.set('status', next.status)
      if (next.date) query.set('date', next.date)
      if (next.series) query.set('series', next.series)

      // Replace rather than push: adjusting a dropdown should not bury the previous page under a
      // dozen history entries that all have to be backed through.
      setSearchParams(query, { replace: true })
    },
    [filters, setSearchParams],
  )

  const clear = useCallback(() => {
    setSearchParams(new URLSearchParams(), { replace: true })
  }, [setSearchParams])

  return {
    filters,
    params,
    isFiltered: filters.status !== 'all' || filters.date !== '' || filters.series !== '',
    set,
    clear,
  }
}
