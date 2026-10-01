import { MapPin } from 'lucide-react'
import { Link } from 'react-router-dom'

import { focusRing } from '@/components/common/focusRing'
import { MatchStatusBadge } from '@/components/match/MatchStatusBadge'
import { TeamScoreRow } from '@/components/match/TeamScoreRow'
import type { Match } from '@/features/matches/types'
import { cn } from '@/utils/cn'

export function FeaturedMatch({ match }: { match: Match }) {
  return (
    <Link
      to={`/match/${match.slug}`}
      className={cn(
        'glass block rounded-panel border border-brand-line bg-gradient-to-br from-brand-soft to-surface p-5 shadow-card transition duration-200 ease-out hover:-translate-y-0.5 hover:border-brand hover:shadow-lift active:translate-y-0 sm:p-6',
        focusRing,
      )}
    >
      <div className="flex flex-wrap items-center gap-3">
        <MatchStatusBadge status={match.status} />
        <p className="text-sm font-medium text-ink-muted">{match.matchTitle}</p>
      </div>

      <p className="mt-1 text-sm text-ink-subtle">{match.seriesName}</p>

      <div className="mt-4 space-y-2 text-lg">
        <TeamScoreRow side={match.home} />
        <TeamScoreRow side={match.away} />
      </div>

      <p className="mt-4 font-medium text-live">{match.statusText}</p>

      <p className="mt-3 flex items-center gap-1.5 text-sm text-ink-subtle">
        <MapPin className="size-4 shrink-0" aria-hidden />
        <span className="truncate">{match.venue}</span>
      </p>
    </Link>
  )
}
