import { useSearchParams } from 'react-router-dom'

import { EmptyState } from '@/components/common/EmptyState'
import { ErrorState } from '@/components/common/ErrorState'
import { Skeleton } from '@/components/common/Skeleton'
import { SearchResultGroups } from '@/features/search/components/SearchResultGroups'
import { MIN_QUERY_LENGTH, useSearch } from '@/features/search/hooks/useSearch'
import { usePageMeta } from '@/hooks/usePageMeta'

/**
 * The full results page, whose term lives in the URL.
 *
 * Same reasoning as the match filters in D-019: a set of results worth reading is worth sharing
 * and worth surviving a reload, and state held only in a component does neither.
 */
export function SearchPage() {
  const [params, setParams] = useSearchParams()
  const term = params.get('q') ?? ''

  // The term, so a browser history entry says which search it was rather than nine reading "Search".
  //
  // Not indexed. The results are a rearrangement of pages that already exist, so every distinct
  // term anyone searched would otherwise become a thin page competing with the real ones. `follow`
  // rather than `nofollow`, because the links out of here do lead somewhere worth crawling.
  usePageMeta(term === '' ? 'Search' : `Search: ${term}`, {
    description: 'Search cricket matches, teams and series.',
    noindex: true,
  })

  const { data, isFetching, isError, error, refetch, enabled } = useSearch(term)

  return (
    <div className="space-y-6">
      <div className="space-y-1">
        <h1 className="text-xl font-semibold tracking-tight text-ink sm:text-2xl">Search</h1>
        {/*
          Stated up front, because the absence of players is the first thing a reader will wonder
          about. No source available to us links a player to a match.
        */}
        <p className="text-sm text-ink-subtle">
          Matches, teams and series recorded here. Players are not searchable — no source we
          have connects a player to a match.
        </p>
      </div>

      <label className="sr-only" htmlFor="search-page-input">
        Search matches, teams and series
      </label>
      <input
        id="search-page-input"
        type="search"
        value={term}
        placeholder="Search matches, teams and series"
        autoComplete="off"
        onChange={(event) => {
          // `replace`, so typing does not fill the back button with one entry per keystroke.
          setParams(event.target.value === '' ? {} : { q: event.target.value }, {
            replace: true,
          })
        }}
        className="glass h-11 w-full rounded-card border border-line bg-surface px-4 text-sm text-ink placeholder:text-ink-subtle focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand"
      />

      {!enabled && (
        <EmptyState
          title="Type to search"
          description={`At least ${MIN_QUERY_LENGTH} characters — a single letter matches almost everything.`}
        />
      )}

      {enabled && isError && (
        <ErrorState
          description={error.message}
          onRetry={() => {
            void refetch()
          }}
        />
      )}

      {enabled && !isError && data === undefined && (
        <div className="space-y-3">
          {Array.from({ length: 4 }, (_, index) => (
            <Skeleton key={index} className="h-12 w-full rounded-lg" />
          ))}
        </div>
      )}

      {enabled && data !== undefined && (
        <>
          <p aria-live="polite" className="text-sm text-ink-subtle">
            {isFetching
              ? 'Searching…'
              : `${data.total} ${data.total === 1 ? 'result' : 'results'} for “${data.query}”`}
          </p>

          {data.total === 0 && !isFetching ? (
            <EmptyState
              title="Nothing matched"
              description="Only matches recorded here are searchable, so something played before this site started will not appear."
            />
          ) : (
            <SearchResultGroups results={data} />
          )}
        </>
      )}
    </div>
  )
}
