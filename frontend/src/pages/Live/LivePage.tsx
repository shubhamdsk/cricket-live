import { MatchListSection } from '@/features/matches/components/MatchListSection'
import { useLiveMatches } from '@/features/matches/hooks/useMatches'
import { usePageTitle } from '@/hooks/usePageTitle'

export function LivePage() {
  usePageTitle('Live matches')

  const liveQuery = useLiveMatches()

  return (
    <div className="space-y-6">
      <h1 className="text-xl font-semibold tracking-tight text-ink sm:text-2xl">
        Live matches
      </h1>

      <MatchListSection
        title="Live now"
        query={liveQuery}
        emptyTitle="No live matches"
        emptyDescription="Nothing is in play at the moment. Upcoming fixtures are on the home page."
        skeletonCount={4}
      />
    </div>
  )
}
