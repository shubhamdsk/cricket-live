import type { TeamInnings } from '@/features/matches/types'
import { formatTeamScore } from '@/features/matches/utils/format'
import { cn } from '@/utils/cn'

interface TeamScoreRowProps {
  side: TeamInnings
  isDimmed?: boolean
}

export function TeamScoreRow({ side, isDimmed = false }: TeamScoreRowProps) {
  const score = formatTeamScore(side)

  return (
    <div className="flex items-baseline justify-between gap-3">
      <span
        className={cn(
          'truncate text-sm font-medium sm:text-base',
          isDimmed ? 'text-ink-muted' : 'text-ink',
        )}
      >
        <span className="sm:hidden">{side.team.shortName}</span>
        <span className="hidden sm:inline">{side.team.name}</span>
      </span>

      {score ? (
        <span
          className={cn(
            'score-figures shrink-0 text-sm font-semibold sm:text-base',
            isDimmed ? 'text-ink-muted' : 'text-ink',
          )}
        >
          {score}
        </span>
      ) : (
        <span className="shrink-0 text-sm text-ink-subtle">Yet to bat</span>
      )}
    </div>
  )
}
