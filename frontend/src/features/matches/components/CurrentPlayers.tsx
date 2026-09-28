import { Card } from '@/components/common/Card'
import type { BatterSummary, BowlerSummary } from '@/features/matches/types'

interface CurrentPlayersProps {
  batters: BatterSummary[]
  bowler: BowlerSummary | null
}

const headerCellClasses = 'pb-2 text-right font-medium'
const rowNameClasses = 'max-w-0 truncate py-2 pr-2 text-left font-normal text-ink'

export function CurrentPlayers({ batters, bowler }: CurrentPlayersProps) {
  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <Card className="p-4">
        <h3 className="text-sm font-semibold text-ink">Batting</h3>

        <table className="mt-3 w-full text-sm">
          <thead>
            <tr className="text-left text-xs text-ink-subtle">
              <th scope="col" className="pb-2 font-medium">
                Batter
              </th>
              <th scope="col" className={headerCellClasses}>
                R
              </th>
              <th scope="col" className={headerCellClasses}>
                B
              </th>
              <th scope="col" className={headerCellClasses}>
                4s
              </th>
              <th scope="col" className={headerCellClasses}>
                6s
              </th>
            </tr>
          </thead>
          <tbody className="score-figures">
            {batters.map((batter) => (
              <tr key={batter.playerId} className="border-t border-line">
                <th scope="row" className={rowNameClasses}>
                  {batter.name}
                  {batter.isOnStrike && <span aria-label=" on strike"> *</span>}
                </th>
                <td className="py-2 text-right font-semibold">{batter.runs}</td>
                <td className="py-2 text-right text-ink-muted">{batter.balls}</td>
                <td className="py-2 text-right text-ink-muted">{batter.fours}</td>
                <td className="py-2 text-right text-ink-muted">{batter.sixes}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>

      <Card className="p-4">
        <h3 className="text-sm font-semibold text-ink">Bowling</h3>

        {bowler ? (
          <table className="mt-3 w-full text-sm">
            <thead>
              <tr className="text-left text-xs text-ink-subtle">
                <th scope="col" className="pb-2 font-medium">
                  Bowler
                </th>
                <th scope="col" className={headerCellClasses}>
                  O
                </th>
                <th scope="col" className={headerCellClasses}>
                  M
                </th>
                <th scope="col" className={headerCellClasses}>
                  R
                </th>
                <th scope="col" className={headerCellClasses}>
                  W
                </th>
              </tr>
            </thead>
            <tbody className="score-figures">
              <tr className="border-t border-line">
                <th scope="row" className={rowNameClasses}>
                  {bowler.name}
                </th>
                <td className="py-2 text-right text-ink-muted">{bowler.overs}</td>
                <td className="py-2 text-right text-ink-muted">{bowler.maidens}</td>
                <td className="py-2 text-right text-ink-muted">{bowler.runs}</td>
                <td className="py-2 text-right font-semibold">{bowler.wickets}</td>
              </tr>
            </tbody>
          </table>
        ) : (
          <p className="mt-3 text-sm text-ink-muted">No bowler is currently in action.</p>
        )}
      </Card>
    </div>
  )
}
