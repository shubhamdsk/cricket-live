import { Link } from 'react-router-dom'

import { focusRing } from '@/components/common/focusRing'
import { TeamCrest } from '@/features/teams/components/TeamCrest'
import type { Team } from '@/features/teams/types'
import { cn } from '@/utils/cn'

export function TeamCard({ team }: { team: Team }) {
  return (
    <Link
      to={`/teams/${team.id}`}
      className={cn(
        'glass flex items-center gap-3 rounded-card border border-line bg-surface p-4 shadow-card transition hover:-translate-y-0.5 hover:border-brand-line hover:shadow-lift',
        focusRing,
      )}
    >
      <TeamCrest team={team} className="size-10" />

      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <h3 className="truncate text-base font-semibold leading-snug text-ink">
            {team.name}
          </h3>
          {team.isActive && (
            <span className="shrink-0 rounded-full bg-live/10 px-2 py-0.5 text-xs font-medium text-live">
              Playing
            </span>
          )}
        </div>

        {/* "held", as everywhere: this counts our records, not the team's career. */}
        <p className="mt-0.5 text-sm text-ink-subtle">
          {team.matchCount} {team.matchCount === 1 ? 'match' : 'matches'} held
        </p>
      </div>
    </Link>
  )
}
