import { Link } from 'react-router-dom'

import { focusRing } from '@/components/common/focusRing'
import type { Series } from '@/features/series/types'
import { cn } from '@/utils/cn'

/** The span of what we hold, written the short way when both ends share a month. */
function span(series: Series): string {
  const from = new Date(series.startTimeUtc)
  const to = new Date(series.lastMatchUtc)

  const sameMonth = from.getFullYear() === to.getFullYear() && from.getMonth() === to.getMonth()

  const day = { day: 'numeric' } as const
  const full = { day: 'numeric', month: 'short', year: 'numeric' } as const

  if (from.getTime() === to.getTime()) {
    return from.toLocaleDateString(undefined, full)
  }

  return `${from.toLocaleDateString(undefined, sameMonth ? day : full)} – ${to.toLocaleDateString(undefined, full)}`
}

export function SeriesCard({ series }: { series: Series }) {
  return (
    <Link
      to={`/series/${series.slug}`}
      className={cn(
        'block rounded-xl border border-line bg-surface p-4 transition hover:border-brand/40 hover:shadow-sm',
        focusRing,
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <h3 className="text-base font-semibold leading-snug text-ink">{series.name}</h3>
        {series.isOngoing && (
          <span className="shrink-0 rounded-full bg-live/10 px-2 py-0.5 text-xs font-medium text-live">
            Ongoing
          </span>
        )}
      </div>

      <p className="mt-2 text-sm text-ink-subtle">{span(series)}</p>

      {/*
        "held", not "matches". We show what we have, and for a long tournament that is a fraction
        of what it played, because our history starts when this site did.
      */}
      <p className="mt-1 text-sm text-ink-subtle">
        {series.matchCount} {series.matchCount === 1 ? 'match' : 'matches'} held
      </p>
    </Link>
  )
}
