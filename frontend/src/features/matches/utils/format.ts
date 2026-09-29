import type { InningsScore, TeamInnings } from '@/features/matches/types'

export function formatInnings(score: InningsScore): string {
  const wickets = score.wickets === 10 ? '' : `/${score.wickets}`
  return `${score.runs}${wickets} (${score.overs})`
}

/** Test sides can have two innings; join them the way scorecards do. */
export function formatTeamScore(side: TeamInnings): string | null {
  if (side.innings.length === 0) return null
  return side.innings.map(formatInnings).join(' & ')
}

/** "1st innings", "2nd innings" — cricket never says "innings 2". */
export function formatInningsLabel(number: number): string {
  const suffix =
    number % 100 >= 11 && number % 100 <= 13
      ? 'th'
      : ({ 1: 'st', 2: 'nd', 3: 'rd' }[number % 10] ?? 'th')

  return `${number}${suffix} innings`
}

const startTimeFormatter = new Intl.DateTimeFormat(undefined, {
  weekday: 'short',
  day: 'numeric',
  month: 'short',
  hour: 'numeric',
  minute: '2-digit',
})

export function formatStartTime(startTimeUtc: string): string {
  return startTimeFormatter.format(new Date(startTimeUtc))
}
