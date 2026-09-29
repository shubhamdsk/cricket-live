import type { Team, TeamDetails } from '@/features/teams/types'
import { apiGet } from '@/services/apiClient'
import { endpoints } from '@/services/endpoints'

export function getAllTeams(signal?: AbortSignal): Promise<Team[]> {
  return apiGet<Team[]>(endpoints.teams.all(), signal)
}

/** Takes the team slug. Unlike a match or a series there is no id to pass instead. */
export function getTeamDetails(slug: string, signal?: AbortSignal): Promise<TeamDetails> {
  return apiGet<TeamDetails>(endpoints.teams.details(slug), signal)
}
