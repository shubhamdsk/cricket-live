import { MatchListSection } from '@/features/matches/components/MatchListSection'
import { useLiveMatches } from '@/features/matches/hooks/useMatches'
import { usePageMeta } from '@/hooks/usePageMeta'

export function LivePage() {
  usePageMeta('Live matches', {
    description:
      'Every cricket match in play right now, with scores, overs and the state of the game updating as it happens.',
  })

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
