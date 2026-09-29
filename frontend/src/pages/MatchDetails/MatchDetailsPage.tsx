import { Link, useParams } from 'react-router-dom'

import { Card } from '@/components/common/Card'
import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { Skeleton } from '@/components/common/Skeleton'
import { focusRing } from '@/components/common/focusRing'
import { MatchHeader } from '@/components/match/MatchHeader'
import { LiveStreamIndicator } from '@/features/matches/components/LiveStreamIndicator'
import { useMatchDetails } from '@/features/matches/hooks/useMatches'
import { useMatchLiveStream } from '@/features/matches/hooks/useMatchLiveStream'
import type { MatchDetails, TeamInnings } from '@/features/matches/types'
import {
  formatInnings,
  formatInningsLabel,
  formatStartTime,
} from '@/features/matches/utils/format'
import { ApiError } from '@/services/apiClient'
import { cn } from '@/utils/cn'

const formatLabels: Record<MatchDetails['format'], string> = {
  T20: 'Twenty20',
  ODI: 'One Day International',
  TEST: 'Test',
  OTHER: 'Other format',
}

export function MatchDetailsPage() {
  const { slug } = useParams<{ slug: string }>()
  const { data, isPending, isError, error, refetch } = useMatchDetails(slug)

  // Only a match in progress has anything to stream, and holding a connection open for one that
  // does not would keep the server's poller awake for no reason.
  const stream = useMatchLiveStream(slug, data?.status === 'live')

  if (isPending) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-64 w-full rounded-xl" />
        <Skeleton className="h-40 w-full rounded-xl" />
      </div>
    )
  }

  if (isError) {
    if (error instanceof ApiError && error.status === 404) {
      return (
        <div className="space-y-4">
          <EmptyState
            title="Match not found"
            description="This match may have dropped out of our data provider's window, or the link may be incorrect."
          />
          <Link
            to="/"
            className={cn(
              'rounded-md text-sm font-medium text-brand-strong hover:underline',
              focusRing,
            )}
          >
            Back to home
          </Link>
        </div>
      )
    }

    return (
      <ErrorState
        description={error.message}
        onRetry={() => {
          void refetch()
        }}
      />
    )
  }

  return (
    <div className="space-y-4">
      <MatchHeader match={data} />
      <LiveStreamIndicator {...stream} />
      <InningsBreakdown match={data} />
      <MatchInformation match={data} />
    </div>
  )
}

function InningsBreakdown({ match }: { match: MatchDetails }) {
  const sides = [match.home, match.away]
  const hasAnyInnings = sides.some((side) => side.innings.length > 0)

  if (!hasAnyInnings) {
    return (
      <EmptyState
        title="No innings yet"
        description="Scores appear here once the first ball is bowled."
      />
    )
  }

  return (
    <Card className="p-4 sm:p-6">
      <h2 className="text-base font-semibold tracking-tight text-ink">Innings</h2>

      <dl className="mt-3 divide-y divide-line">{sides.flatMap((side) => renderSide(side))}</dl>
    </Card>
  )
}

function renderSide(side: TeamInnings) {
  return side.innings.map((innings) => (
    <div
      key={`${side.team.id}-${innings.number}`}
      className="flex items-baseline justify-between gap-4 py-2.5"
    >
      <dt className="min-w-0 text-sm text-ink-muted">
        <span className="font-medium text-ink">{side.team.name}</span>{' '}
        <span className="whitespace-nowrap">{formatInningsLabel(innings.number)}</span>
      </dt>
      <dd className="score-figures shrink-0 font-medium text-ink">{formatInnings(innings)}</dd>
    </div>
  ))
}

function MatchInformation({ match }: { match: MatchDetails }) {
  const rows: Array<[string, string]> = [
    ['Format', formatLabels[match.format]],
    ['Series', match.seriesName || 'Not published'],
    ['Venue', match.venue || 'Not published'],
    ['Start', formatStartTime(match.startTimeUtc)],
  ]

  return (
    <Card className="p-4 sm:p-6">
      <h2 className="text-base font-semibold tracking-tight text-ink">Match information</h2>

      <dl className="mt-3 grid gap-x-6 gap-y-2.5 sm:grid-cols-2">
        {rows.map(([label, value]) => (
          <div key={label} className="flex flex-col">
            <dt className="text-xs uppercase tracking-wide text-ink-subtle">{label}</dt>
            <dd className="text-sm text-ink">{value}</dd>
          </div>
        ))}
      </dl>
    </Card>
  )
}
