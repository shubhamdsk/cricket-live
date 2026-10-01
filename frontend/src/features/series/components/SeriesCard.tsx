import { Link } from 'react-router-dom'

import { focusRing } from '@/components/common/focusRing'
import { heldCount } from '@/features/series/held'
import type { Series } from '@/features/series/types'
import { cn } from '@/utils/cn'

const DAY = { day: 'numeric' } as const
const FULL = { day: 'numeric', month: 'short', year: 'numeric' } as const

/**
 * The span, written the short way when both ends share a month, and left open-ended whenever the
 * far end is unknown.
 *
 * The only end date we have is that of the last match we hold, which is the end of the series
 * only when we hold all of them. For anything still being played that date is somewhere in the
 * middle, and a closed range ending there would contradict the fixture list on the page this card
 * opens. Computing the real end would mean fetching every listed series' fixtures to render one
 * list, which is sixty-odd provider calls for a date.
 */
function span(series: Series): string {
  const from = new Date(series.startTimeUtc)

  const { lastMatchUtc, matchCount, totalMatchCount } = series
  const complete = totalMatchCount === null || matchCount >= totalMatchCount

  if (lastMatchUtc === null || !complete) {
    return `From ${from.toLocaleDateString(undefined, FULL)}`
  }

  const to = new Date(lastMatchUtc)

  const sameMonth = from.getFullYear() === to.getFullYear() && from.getMonth() === to.getMonth()

  if (from.getTime() === to.getTime()) {
    return from.toLocaleDateString(undefined, FULL)
  }

  return `${from.toLocaleDateString(undefined, sameMonth ? DAY : FULL)} – ${to.toLocaleDateString(undefined, FULL)}`
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

      <p className="mt-1 text-sm text-ink-subtle">{heldCount(series)}</p>
    </Link>
  )
}
