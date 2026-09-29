import { Link } from 'react-router-dom'

import { focusRing } from '@/components/common/focusRing'
import type { SearchHit, SearchResults } from '@/features/search/types'
import { cn } from '@/utils/cn'

/**
 * The route each kind of hit leads to, alongside its heading.
 *
 * Kept as data rather than three near-identical blocks of markup, so adding a kind is one entry
 * and the groups cannot drift apart in styling.
 */
const groups = [
  { key: 'matches', heading: 'Matches', route: (id: string) => `/match/${id}` },
  { key: 'teams', heading: 'Teams', route: (id: string) => `/teams/${id}` },
  { key: 'series', heading: 'Series', route: (id: string) => `/series/${id}` },
] as const satisfies ReadonlyArray<{
  key: keyof Pick<SearchResults, 'matches' | 'teams' | 'series'>
  heading: string
  route: (id: string) => string
}>

function Row({ hit, to, onNavigate }: { hit: SearchHit; to: string; onNavigate?: () => void }) {
  return (
    <Link
      to={to}
      onClick={onNavigate}
      className={cn('block rounded-lg px-3 py-2 transition hover:bg-surface-muted', focusRing)}
    >
      <span className="block truncate text-sm font-medium text-ink">{hit.title}</span>
      {hit.subtitle !== '' && (
        <span className="block truncate text-xs text-ink-subtle">{hit.subtitle}</span>
      )}
    </Link>
  )
}

/**
 * Every non-empty group, headed and linked.
 *
 * An empty group renders nothing at all rather than a heading over blank space: "no teams matched"
 * is not information the reader asked for, and three such headings would bury the group that did
 * match.
 */
export function SearchResultGroups({
  results,
  onNavigate,
}: {
  results: SearchResults
  onNavigate?: () => void
}) {
  return (
    <div className="space-y-4">
      {groups.map((group) => {
        const hits = results[group.key]

        if (hits.length === 0) {
          return null
        }

        return (
          <section key={group.key} className="space-y-1">
            <h3 className="px-3 text-xs font-semibold uppercase tracking-wide text-ink-subtle">
              {group.heading}
            </h3>
            <ul>
              {hits.map((hit) => (
                <li key={hit.id}>
                  <Row hit={hit} to={group.route(hit.id)} onNavigate={onNavigate} />
                </li>
              ))}
            </ul>
          </section>
        )
      })}
    </div>
  )
}
