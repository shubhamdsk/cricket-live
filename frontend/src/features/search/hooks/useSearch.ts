import { useQuery } from '@tanstack/react-query'

import { search } from '@/features/search/api/searchApi'
import { useDebounced } from '@/hooks/useDebounced'

const MINUTE = 60_000

/** Long enough that typing a word is one request, short enough to feel immediate. */
const DEBOUNCE_MS = 250

/**
 * Matched on the server too. One character matches most of everything, which is a list with a
 * delay in front of it rather than a search result.
 */
export const MIN_QUERY_LENGTH = 2

export const searchKeys = {
  all: ['search'] as const,
  term: (term: string) => [...searchKeys.all, term] as const,
}

/**
 * Search results for a term the user is still typing.
 *
 * Returns the settled term alongside the query so a caller can tell "nothing found" from "still
 * catching up with what you typed" — they look identical otherwise, and one of them is not a
 * result worth reporting.
 */
export function useSearch(term: string) {
  const settled = useDebounced(term.trim(), DEBOUNCE_MS)
  const enabled = settled.length >= MIN_QUERY_LENGTH

  const query = useQuery({
    queryKey: searchKeys.term(settled),
    queryFn: ({ signal }) => search(settled, signal),
    enabled,
    // The searchable set is matches, teams and series, none of which change by the second.
    staleTime: 2 * MINUTE,
  })

  return { ...query, settled, enabled }
}
