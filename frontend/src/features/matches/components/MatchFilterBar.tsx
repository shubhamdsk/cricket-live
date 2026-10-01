import { X } from 'lucide-react'

import { Button } from '@/components/common/Button'
import { focusRing } from '@/components/common/focusRing'
import type { MatchFilterState, StatusFilter } from '@/features/matches/hooks/useMatchFilters'
import { cn } from '@/utils/cn'

interface MatchFilterBarProps {
  state: MatchFilterState
  series: string[]
  isSeriesPending: boolean
}

const statusLabels: Record<StatusFilter, string> = {
  all: 'All',
  live: 'Live',
  upcoming: 'Upcoming',
  completed: 'Completed',
}

const fieldClasses = cn(
  'glass min-h-11 w-full rounded-lg border border-line-strong bg-surface px-3 text-sm text-ink',
  'disabled:cursor-not-allowed disabled:opacity-60 sm:min-h-10',
  focusRing,
)

export function MatchFilterBar({ state, series, isSeriesPending }: MatchFilterBarProps) {
  const { filters, isFiltered, set, clear } = state

  // A shared link can name a series that has since aged out of both the window and the archive.
  // Keeping it as an option shows the reader what they are filtered to, rather than a blank
  // dropdown above an empty list with no explanation.
  const options =
    filters.series && !series.includes(filters.series) ? [filters.series, ...series] : series

  return (
    <section className="glass space-y-3 rounded-card border border-line bg-surface p-4 shadow-card">
      <h2 className="sr-only">Filter matches</h2>

      {/*
        A radio group rather than a fourth dropdown: there are four options, they are the primary
        way people narrow this page, and one tap beats open-scan-tap.
      */}
      <div role="radiogroup" aria-label="Status" className="flex flex-wrap gap-2">
        {(Object.keys(statusLabels) as StatusFilter[]).map((status) => (
          <button
            key={status}
            type="button"
            role="radio"
            aria-checked={filters.status === status}
            onClick={() => set({ status })}
            className={cn(
              'min-h-9 rounded-full px-3.5 text-sm font-medium transition duration-150',
              focusRing,
              filters.status === status
                ? 'bg-brand text-on-brand'
                : 'border border-line-strong text-ink-muted hover:bg-surface-muted',
            )}
          >
            {statusLabels[status]}
          </button>
        ))}
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <div className="space-y-1">
          <label htmlFor="filter-date" className="block text-xs font-medium text-ink-muted">
            Date
          </label>
          <input
            id="filter-date"
            type="date"
            value={filters.date}
            onChange={(event) => set({ date: event.target.value })}
            className={fieldClasses}
          />
        </div>

        <div className="space-y-1">
          <label htmlFor="filter-series" className="block text-xs font-medium text-ink-muted">
            Series
          </label>
          {/*
            Every option comes from a match that exists, so there is no selection here that
            returns nothing. While the list is loading the control is disabled rather than empty,
            because an empty dropdown reads as "no series" rather than "not yet".
          */}
          <select
            id="filter-series"
            value={filters.series}
            disabled={isSeriesPending}
            onChange={(event) => set({ series: event.target.value })}
            className={fieldClasses}
          >
            <option value="">{isSeriesPending ? 'Loading series…' : 'All series'}</option>
            {options.map((name) => (
              <option key={name} value={name}>
                {name}
              </option>
            ))}
          </select>
        </div>
      </div>

      {isFiltered && (
        <Button variant="ghost" size="sm" onClick={clear} className="px-2">
          <X className="size-4" aria-hidden />
          Clear filters
        </Button>
      )}
    </section>
  )
}
