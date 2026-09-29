import { useQuery } from '@tanstack/react-query'

import { getAllTeams, getTeamDetails } from '@/features/teams/api/teamsApi'

const MINUTE = 60_000

export const teamKeys = {
  all: ['teams'] as const,
  list: () => [...teamKeys.all, 'list'] as const,
  details: (slug: string) => [...teamKeys.all, 'details', slug] as const,
}

/**
 * Which teams we hold a match for changes as slowly as the fixture list does, so this is held
 * for a long time. The scores inside are refetched by the pages that actually show them.
 */
export function useAllTeams() {
  return useQuery({
    queryKey: teamKeys.list(),
    queryFn: ({ signal }) => getAllTeams(signal),
    staleTime: 30 * MINUTE,
  })
}

/** Refetched while the team has a match in progress, and left alone once it does not. */
export function useTeamDetails(slug: string | undefined) {
  return useQuery({
    queryKey: teamKeys.details(slug ?? ''),
    queryFn: ({ signal }) => getTeamDetails(slug!, signal),
    enabled: Boolean(slug),
    staleTime: 5 * MINUTE,
    refetchInterval: (query) => (query.state.data?.team.isActive ? 5 * MINUTE : false),
  })
}
