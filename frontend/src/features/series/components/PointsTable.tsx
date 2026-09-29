import type { Standing } from '@/features/series/types'

/**
 * A points table exactly as its source published it.
 *
 * Nothing here is computed. Points rules differ by competition — the County Championship alone
 * awards bonus points for batting and bowling — so a derived table would be a guess presented as
 * a standing. Every number is read or the row is not shown at all.
 */
export function PointsTable({ standings }: { standings: Standing[] }) {
  // Absence means we could not get a table, never that the series has none. The caller omits the
  // whole section in that case rather than rendering an empty one.
  if (standings.length === 0) {
    return null
  }

  const groups = [...new Set(standings.map((row) => row.group))]

  // Only some competitions have ties, and a column of zeroes is noise on the ones that do not.
  const showTied = standings.some((row) => row.tied > 0)

  return (
    <section className="space-y-3">
      <h2 className="text-lg font-semibold tracking-tight text-ink">Points table</h2>

      {groups.map((group) => (
        <div key={group} className="overflow-hidden rounded-xl border border-line bg-surface">
          {group && (
            <div className="border-b border-line bg-surface-sunken px-4 py-2 text-xs font-medium uppercase tracking-wide text-ink-subtle">
              {group}
            </div>
          )}

          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <caption className="sr-only">
                {group ? `${group} points table` : 'Points table'}
              </caption>
              <thead>
                <tr className="border-b border-line text-xs uppercase tracking-wide text-ink-subtle">
                  <th scope="col" className="px-4 py-2 text-left font-medium">
                    Team
                  </th>
                  <th scope="col" className="px-2 py-2 text-right font-medium">
                    <abbr title="Played">P</abbr>
                  </th>
                  <th scope="col" className="px-2 py-2 text-right font-medium">
                    <abbr title="Won">W</abbr>
                  </th>
                  <th scope="col" className="px-2 py-2 text-right font-medium">
                    <abbr title="Lost">L</abbr>
                  </th>
                  {showTied && (
                    <th scope="col" className="px-2 py-2 text-right font-medium">
                      <abbr title="Tied">T</abbr>
                    </th>
                  )}
                  <th scope="col" className="px-2 py-2 text-right font-medium">
                    <abbr title="No result">NR</abbr>
                  </th>
                  <th scope="col" className="px-2 py-2 text-right font-medium">
                    <abbr title="Points">Pts</abbr>
                  </th>
                  <th scope="col" className="px-4 py-2 text-right font-medium">
                    <abbr title="Net run rate">NRR</abbr>
                  </th>
                </tr>
              </thead>
              <tbody>
                {standings
                  .filter((row) => row.group === group)
                  .map((row) => (
                    <tr key={row.teamName} className="border-b border-line/60 last:border-0">
                      <th scope="row" className="px-4 py-2 text-left font-medium text-ink">
                        {row.teamName}
                      </th>
                      <td className="px-2 py-2 text-right tabular-nums text-ink-subtle">
                        {row.played}
                      </td>
                      <td className="px-2 py-2 text-right tabular-nums text-ink-subtle">
                        {row.won}
                      </td>
                      <td className="px-2 py-2 text-right tabular-nums text-ink-subtle">
                        {row.lost}
                      </td>
                      {showTied && (
                        <td className="px-2 py-2 text-right tabular-nums text-ink-subtle">
                          {row.tied}
                        </td>
                      )}
                      <td className="px-2 py-2 text-right tabular-nums text-ink-subtle">
                        {row.noResult}
                      </td>
                      <td className="px-2 py-2 text-right font-semibold tabular-nums text-ink">
                        {row.points}
                      </td>
                      <td className="px-4 py-2 text-right tabular-nums text-ink-subtle">
                        {row.netRunRate}
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        </div>
      ))}
    </section>
  )
}
