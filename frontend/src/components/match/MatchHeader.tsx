import { MapPin } from 'lucide-react'

import { Card } from '@/components/common/Card'
import { MatchStatusBadge } from '@/components/match/MatchStatusBadge'
import { TeamScoreRow } from '@/components/match/TeamScoreRow'
import type { MatchDetails } from '@/features/matches/types'
import { formatStartTime } from '@/features/matches/utils/format'
import { cn } from '@/utils/cn'

export function MatchHeader({ match }: { match: MatchDetails }) {
  const isUpcoming = match.status === 'upcoming'

  return (
    <Card className="space-y-4 p-4 sm:p-6">
      <div className="space-y-1">
        <div className="flex flex-wrap items-center gap-3">
          <MatchStatusBadge status={match.status} />
          <h1 className="text-lg font-semibold tracking-tight text-ink sm:text-xl">
            {match.home.team.shortName} vs {match.away.team.shortName}
          </h1>
        </div>
        <p className="text-sm text-ink-muted">
          {match.matchTitle} · {match.seriesName}
        </p>
      </div>

      <div className="space-y-2 text-lg">
        <TeamScoreRow side={match.home} isDimmed={isUpcoming} />
        <TeamScoreRow side={match.away} isDimmed={isUpcoming} />
      </div>

      <p
        className={cn('font-medium', match.status === 'live' ? 'text-live' : 'text-ink-muted')}
      >
        {isUpcoming ? formatStartTime(match.startTimeUtc) : match.statusText}
      </p>

      <div className="space-y-1 border-t border-line pt-3 text-sm text-ink-subtle">
        <p className="flex items-center gap-1.5">
          <MapPin className="size-4 shrink-0" aria-hidden />
          <span>{match.venue || 'Venue not published'}</span>
        </p>
      </div>
    </Card>
  )
}
