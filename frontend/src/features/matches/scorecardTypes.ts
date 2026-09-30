/**
 * Mirrors `ScorecardDto` and its parts. See docs/api.md.
 *
 * Kept apart from `types.ts` because the scorecard is a separate call from a separate upstream,
 * and nothing that renders a match list needs any of it.
 *
 * Several figures here are strings that look like numbers. That is deliberate and matches the
 * API: `overs` in cricket notation means `3.5` is five balls rather than half an over, and a
 * strike rate for a batter who has faced nothing has no honest numeric value. They are computed
 * upstream and displayed, never used in arithmetic.
 */

export interface BattingLine {
  name: string
  runs: number
  /** Balls faced. Whole deliveries, so arithmetic on it is safe. */
  balls: number
  fours: number
  sixes: number
  strikeRate: string
  /**
   * How they got out, in the source's own words: `c Smith b Jadeja`, `not out`, or empty for
   * someone who has not batted. Not parsed, because the wording is the information.
   */
  dismissal: string
  isCaptain: boolean
  isKeeper: boolean
}

export interface BowlingLine {
  name: string
  /** Cricket over notation. Never arithmetic. */
  overs: string
  maidens: number
  runs: number
  wickets: number
  economy: string
}

export interface Extras {
  byes: number
  legByes: number
  wides: number
  noBalls: number
  penalty: number
  total: number
}

export interface Wicket {
  batterName: string
  /** The team's score when this wicket fell, not the batter's. */
  runs: number
  wicketNumber: number
  over: number
}

export interface Partnership {
  firstBatterName: string
  firstBatterRuns: number
  secondBatterName: string
  secondBatterRuns: number
  runs: number
  balls: number
}

export interface InningsCard {
  inningsNumber: number
  battingTeamName: string
  battingTeamShortName: string
  runs: number
  wickets: number
  overs: number
  runRate: number
  isDeclared: boolean
  batting: BattingLine[]
  bowling: BowlingLine[]
  extras: Extras
  fallOfWickets: Wicket[]
  partnerships: Partnership[]
}

export interface Scorecard {
  matchId: string
  innings: InningsCard[]
  /** The result or current state as prose, such as "India won by 8 wkts". Displayed verbatim. */
  status: string
  isComplete: boolean
}
