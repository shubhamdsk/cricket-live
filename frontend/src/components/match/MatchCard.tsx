import { Link } from 'react-router-dom'

import { focusRing } from '@/components/common/focusRing'
import { MatchStatusBadge } from '@/components/match/MatchStatusBadge'
import { TeamScoreRow } from '@/components/match/TeamScoreRow'
import type { Match } from '@/features/matches/types'
import { formatStartTime } from '@/features/matches/utils/format'
import { cn } from '@/utils/cn'

export function MatchCard({ match }: { match: Match }) {
  const isUpcoming = match.status === 'upcoming'

  return (
    <Link
      to={`/match/${match.slug}`}
      className={cn(
        'glass flex flex-col gap-3 rounded-card border border-line bg-surface p-4 shadow-card transition duration-200 ease-out hover:-translate-y-0.5 hover:border-brand-line hover:bg-brand-soft hover:shadow-lift active:translate-y-0',
        focusRing,
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-ink">{match.matchTitle}</p>
          <p className="truncate text-xs text-ink-subtle">{match.seriesName}</p>
        </div>
        <MatchStatusBadge status={match.status} />
      </div>

      <div className="space-y-1.5">
        <TeamScoreRow side={match.home} isDimmed={isUpcoming} />
        <TeamScoreRow side={match.away} isDimmed={isUpcoming} />
      </div>

      <p
        className={cn(
          'text-sm',
          match.status === 'live' ? 'font-medium text-live' : 'text-ink-muted',
        )}
      >
        {isUpcoming ? formatStartTime(match.startTimeUtc) : match.statusText}
      </p>
    </Link>
  )
}
