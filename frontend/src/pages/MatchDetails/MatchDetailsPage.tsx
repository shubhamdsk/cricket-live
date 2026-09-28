import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'

import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { Skeleton } from '@/components/common/Skeleton'
import { TabPanel, Tabs, type TabItem } from '@/components/common/Tabs'
import { focusRing } from '@/components/common/focusRing'
import { MatchHeader } from '@/components/match/MatchHeader'
import { CurrentPlayers } from '@/features/matches/components/CurrentPlayers'
import { useMatchDetails } from '@/features/matches/hooks/useMatches'
import type { MatchDetails } from '@/features/matches/types'
import { ApiError } from '@/services/apiClient'
import { cn } from '@/utils/cn'

const tabs: TabItem[] = [
  { id: 'summary', label: 'Summary' },
  { id: 'scorecard', label: 'Scorecard' },
  { id: 'commentary', label: 'Commentary' },
  { id: 'stats', label: 'Stats' },
]

export function MatchDetailsPage() {
  const { slug } = useParams<{ slug: string }>()
  const { data, isPending, isError, error, refetch } = useMatchDetails(slug)
  const [activeTab, setActiveTab] = useState('summary')

  if (isPending) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-64 w-full rounded-xl" />
        <Skeleton className="h-12 w-full rounded-lg" />
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
            description="This match may have been removed, or the link may be incorrect."
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

      <Tabs
        items={tabs}
        activeId={activeTab}
        onChange={setActiveTab}
        label="Match information"
      />

      {activeTab === 'summary' && (
        <TabPanel id="summary">
          <SummaryTab match={data} />
        </TabPanel>
      )}

      {activeTab === 'scorecard' && (
        <TabPanel id="scorecard">
          <EmptyState title="Scorecard" description="Full scorecards arrive in Sprint 6." />
        </TabPanel>
      )}

      {activeTab === 'commentary' && (
        <TabPanel id="commentary">
          <EmptyState
            title="Commentary"
            description="Ball-by-ball commentary arrives in Sprint 6."
          />
        </TabPanel>
      )}

      {activeTab === 'stats' && (
        <TabPanel id="stats">
          <EmptyState title="Statistics" description="Match statistics arrive in Sprint 6." />
        </TabPanel>
      )}
    </div>
  )
}

function SummaryTab({ match }: { match: MatchDetails }) {
  const hasLivePlayers = match.currentBatters.length > 0 || match.currentBowler !== null

  return (
    <div className="space-y-4">
      {match.summary && (
        <p className="rounded-card border border-line bg-surface p-4 text-sm leading-relaxed text-ink-muted">
          {match.summary}
        </p>
      )}

      {hasLivePlayers ? (
        <CurrentPlayers batters={match.currentBatters} bowler={match.currentBowler} />
      ) : (
        <EmptyState
          title="No players at the crease"
          description="Batting and bowling figures appear once the match is under way."
        />
      )}
    </div>
  )
}
