export type MatchStatus = 'live' | 'upcoming' | 'completed'

export type MatchFormat = 'T20' | 'ODI' | 'TEST'

export interface TeamSummary {
  id: string
  name: string
  shortName: string
}

export interface InningsScore {
  runs: number
  wickets: number
  overs: string
}

/** A side and every innings it has batted. Tests can hold two. */
export interface TeamInnings {
  team: TeamSummary
  innings: InningsScore[]
}

export interface Match {
  id: string
  slug: string
  status: MatchStatus
  format: MatchFormat
  seriesName: string
  matchTitle: string
  venue: string
  startTimeUtc: string
  home: TeamInnings
  away: TeamInnings
  /** Provider-authored line such as "India need 37 runs in 28 balls". */
  statusText: string
}

export interface BatterSummary {
  playerId: string
  name: string
  runs: number
  balls: number
  fours: number
  sixes: number
  isOnStrike: boolean
}

export interface BowlerSummary {
  playerId: string
  name: string
  overs: string
  maidens: number
  runs: number
  wickets: number
}

export interface MatchDetails extends Match {
  tossText: string | null
  summary: string | null
  currentBatters: BatterSummary[]
  currentBowler: BowlerSummary | null
}
