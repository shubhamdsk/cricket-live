import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { Skeleton } from '@/components/common/Skeleton'
import { TeamCard } from '@/features/teams/components/TeamCard'
import { useAllTeams } from '@/features/teams/hooks/useTeams'
import { usePageTitle } from '@/hooks/usePageTitle'

export function TeamsPage() {
  usePageTitle('Teams')

  const { data, isPending, isError, error, refetch } = useAllTeams()

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h1 className="text-xl font-semibold tracking-tight text-ink sm:text-2xl">Teams</h1>
        {/*
          The same caveat the series page carries, and for a stronger reason: there is no team
          directory upstream to draw on, so a side exists here only once it has played a match we
          recorded.
        */}
        <p className="text-sm text-ink-subtle">
          Every side appearing in a match recorded here. A team shows up once it has played one.
        </p>
      </div>

      {isPending && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-20 w-full rounded-xl" />
          ))}
        </div>
      )}

      {isError && (
        <ErrorState
          description={error.message}
          onRetry={() => {
            void refetch()
          }}
        />
      )}

      {data !== undefined &&
        (data.length === 0 ? (
          <EmptyState
            title="No teams yet"
            description="Teams appear as matches are recorded, so this fills in as cricket is played."
          />
        ) : (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {data.map((team) => (
              <TeamCard key={team.id} team={team} />
            ))}
          </div>
        ))}
    </div>
  )
}
