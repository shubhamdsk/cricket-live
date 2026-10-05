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

/**
 * How long ago the API last managed to read the score provider.
 *
 * Relative rather than a clock time, because the question a reader has about stale data is "how
 * out of date is this", and "3 hours ago" answers it where "05:12" needs them to do the subtraction
 * themselves. Rounded coarsely for the same reason: nobody deciding whether to trust a score cares
 * about the difference between 3h 10m and 3h 25m.
 */
export function formatAsOf(asOfUtc: string): string {
  const timestamp = new Date(asOfUtc).getTime()
  if (Number.isNaN(timestamp)) return 'recently'

  const minutes = Math.round((Date.now() - timestamp) / 60_000)

  if (minutes < 2) return 'moments ago'
  if (minutes < 60) return `${minutes} minutes ago`

  const hours = Math.round(minutes / 60)
  return hours === 1 ? 'about an hour ago' : `about ${hours} hours ago`
}
