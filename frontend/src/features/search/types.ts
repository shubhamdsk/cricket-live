/**
 * Mirrors the search DTOs returned by our API. See docs/api.md.
 *
 * Grouped by kind rather than one ranked list, because a team and a match are not more or less
 * relevant than one another and interleaving them would need a scoring rule invented for it.
 *
 * Players are not a group here. No source available to us links a player to a match, and the
 * provider's player index holds a name and a country and nothing else — so a player result would
 * lead to a page with nothing on it. See docs/decisions.md.
 */

export interface SearchHit {
  /** The slug or id this kind of thing is addressed by. */
  id: string
  title: string
  /** Empty rather than absent when there is no useful second line. */
  subtitle: string
}

export interface SearchResults {
  /** The term as the API read it, trimmed. */
  query: string
  matches: SearchHit[]
  teams: SearchHit[]
  series: SearchHit[]
  total: number
}
