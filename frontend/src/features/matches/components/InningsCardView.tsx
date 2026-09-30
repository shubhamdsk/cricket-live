import type { InningsCard } from '@/features/matches/scorecardTypes'
import { cn } from '@/utils/cn'

/**
 * One innings, batting then bowling, with the tail of it underneath.
 *
 * Real tables rather than a grid of divs. A scorecard is tabular data in the strictest sense —
 * every cell is a figure about a player — and a screen reader reading it as a table can announce
 * "Kohli, runs 82" instead of leaving a listener to count columns.
 *
 * Numeric cells are right-aligned and use the tabular figures the rest of the app uses for scores,
 * so the columns line up as the numbers change width.
 */
export function InningsCardView({ innings }: { innings: InningsCard }) {
  const heading = `${innings.battingTeamName} innings`

  return (
    <section aria-label={heading} className="space-y-4">
      <header className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
        <h3 className="text-sm font-semibold tracking-tight text-ink">
          {innings.battingTeamName}
          {innings.isDeclared && <span className="text-ink-subtle"> (declared)</span>}
        </h3>

        <p className="score-figures text-sm font-semibold text-ink">
          {innings.runs}/{innings.wickets}
          <span className="font-normal text-ink-subtle">
            {' '}
            ({innings.overs} ov, RR {innings.runRate})
          </span>
        </p>
      </header>

      <BattingTable innings={innings} />
      <BowlingTable innings={innings} />
      <FallOfWickets innings={innings} />
    </section>
  )
}

const cellPadding = 'px-2 py-1.5 first:pl-0 last:pr-0'
const headCell = cn(cellPadding, 'text-xs font-medium uppercase tracking-wide text-ink-subtle')
const numberCell = cn(
  cellPadding,
  'score-figures text-right text-sm text-ink-muted tabular-nums',
)

function BattingTable({ innings }: { innings: InningsCard }) {
  // A batting card with nobody on it is a card the source could not fill, not an innings nobody
  // batted in. Saying nothing is more honest than an empty table with headers.
  if (innings.batting.length === 0) {
    return null
  }

  return (
    <div className="-mx-2 overflow-x-auto px-2">
      <table className="w-full min-w-[26rem] border-collapse text-left">
        <caption className="sr-only">{innings.battingTeamName} batting</caption>

        <thead>
          <tr className="border-b border-line">
            <th scope="col" className={headCell}>
              Batter
            </th>
            <th scope="col" className={cn(headCell, 'text-right')}>
              R
            </th>
            <th scope="col" className={cn(headCell, 'text-right')}>
              B
            </th>
            <th scope="col" className={cn(headCell, 'text-right')}>
              4s
            </th>
            <th scope="col" className={cn(headCell, 'text-right')}>
              6s
            </th>
            <th scope="col" className={cn(headCell, 'text-right')}>
              SR
            </th>
          </tr>
        </thead>

        <tbody className="divide-y divide-line">
          {innings.batting.map((batter) => (
            <tr key={batter.name}>
              <th scope="row" className={cn(cellPadding, 'text-sm font-normal')}>
                <span className="font-medium text-ink">
                  {batter.name}
                  {/* The source marks these with a suffix and so do we, because everyone who
                      reads scorecards already knows what they mean. */}
                  {batter.isCaptain && <span className="text-ink-subtle"> (c)</span>}
                  {batter.isKeeper && <span className="text-ink-subtle"> (wk)</span>}
                </span>
                {batter.dismissal && (
                  <span className="block text-xs text-ink-subtle">{batter.dismissal}</span>
                )}
              </th>
              <td className={cn(numberCell, 'font-semibold text-ink')}>{batter.runs}</td>
              <td className={numberCell}>{batter.balls}</td>
              <td className={numberCell}>{batter.fours}</td>
              <td className={numberCell}>{batter.sixes}</td>
              <td className={numberCell}>{batter.strikeRate}</td>
            </tr>
          ))}
        </tbody>

        <tfoot>
          <tr className="border-t border-line">
            <th scope="row" className={cn(cellPadding, 'text-sm font-medium text-ink-muted')}>
              Extras
            </th>
            <td className={cn(numberCell, 'font-medium text-ink')}>{innings.extras.total}</td>
            <td colSpan={4} className={cn(cellPadding, 'text-right text-xs text-ink-subtle')}>
              {describeExtras(innings)}
            </td>
          </tr>
        </tfoot>
      </table>
    </div>
  )
}

/**
 * The breakdown in the conventional shorthand, listing only what there was.
 *
 * `b 4, lb 2, w 7` reads better than five entries where three are zero, and an innings with no
 * extras at all gets nothing rather than a row of noughts.
 */
function describeExtras({ extras }: InningsCard): string {
  const parts: string[] = []

  if (extras.byes > 0) parts.push(`b ${extras.byes}`)
  if (extras.legByes > 0) parts.push(`lb ${extras.legByes}`)
  if (extras.wides > 0) parts.push(`w ${extras.wides}`)
  if (extras.noBalls > 0) parts.push(`nb ${extras.noBalls}`)
  if (extras.penalty > 0) parts.push(`p ${extras.penalty}`)

  return parts.join(', ')
}

function BowlingTable({ innings }: { innings: InningsCard }) {
  if (innings.bowling.length === 0) {
    return null
  }

  return (
    <div className="-mx-2 overflow-x-auto px-2">
      <table className="w-full min-w-[24rem] border-collapse text-left">
        <caption className="sr-only">Bowling to {innings.battingTeamName}</caption>

        <thead>
          <tr className="border-b border-line">
            <th scope="col" className={headCell}>
              Bowler
            </th>
            <th scope="col" className={cn(headCell, 'text-right')}>
              O
            </th>
            <th scope="col" className={cn(headCell, 'text-right')}>
              M
            </th>
            <th scope="col" className={cn(headCell, 'text-right')}>
              R
            </th>
            <th scope="col" className={cn(headCell, 'text-right')}>
              W
            </th>
            <th scope="col" className={cn(headCell, 'text-right')}>
              Econ
            </th>
          </tr>
        </thead>

        <tbody className="divide-y divide-line">
          {innings.bowling.map((bowler) => (
            <tr key={bowler.name}>
              <th scope="row" className={cn(cellPadding, 'text-sm font-medium text-ink')}>
                {bowler.name}
              </th>
              <td className={numberCell}>{bowler.overs}</td>
              <td className={numberCell}>{bowler.maidens}</td>
              <td className={numberCell}>{bowler.runs}</td>
              <td className={cn(numberCell, 'font-semibold text-ink')}>{bowler.wickets}</td>
              <td className={numberCell}>{bowler.economy}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/**
 * How the innings came apart, in the one-line form scorecards have always used.
 *
 * Prose rather than a table because that is how it is read: a sequence, not a set of rows anyone
 * scans down a column of.
 */
function FallOfWickets({ innings }: { innings: InningsCard }) {
  if (innings.fallOfWickets.length === 0) {
    return null
  }

  return (
    <p className="text-xs leading-relaxed text-ink-subtle">
      <span className="font-medium text-ink-muted">Fall of wickets: </span>
      {innings.fallOfWickets
        .map(
          (wicket) =>
            `${wicket.runs}-${wicket.wicketNumber} (${wicket.batterName}, ${wicket.over} ov)`,
        )
        .join(', ')}
    </p>
  )
}
