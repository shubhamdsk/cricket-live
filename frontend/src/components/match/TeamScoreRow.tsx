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
          'flex min-w-0 items-center gap-2 text-sm font-medium sm:text-base',
          isDimmed ? 'text-ink-muted' : 'text-ink',
        )}
      >
        <TeamCrest side={side} />
        <span className="truncate sm:hidden">{side.team.shortName}</span>
        <span className="hidden truncate sm:inline">{side.team.name}</span>
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

/**
 * The provider only holds crests for international sides, so most domestic teams render nothing
 * here. The row must read the same either way, which is why there is no placeholder shape.
 */
function TeamCrest({ side }: { side: TeamInnings }) {
  if (!side.team.logoUrl) return null

  return (
    <img
      src={side.team.logoUrl}
      alt=""
      loading="lazy"
      // Decoded off the main thread, and sized in the markup as well as in CSS so a list of cards
      // does not reflow as crests arrive one by one.
      decoding="async"
      width={20}
      height={20}
      // The crest is served by the provider's CDN, which has no reason to be told which page of
      // ours the reader is on.
      referrerPolicy="no-referrer"
      className="size-5 shrink-0 self-center rounded-full object-cover"
    />
  )
}
