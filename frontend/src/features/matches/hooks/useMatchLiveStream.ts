import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'

import { matchKeys } from '@/features/matches/hooks/useMatches'
import type { MatchDetails } from '@/features/matches/types'

const baseUrl = import.meta.env.VITE_API_BASE_URL

const FIRST_RETRY_MS = 1_000
const MAX_RETRY_MS = 30_000

export type LiveStreamStatus =
  /** Not streaming: the match is finished, or the tab is in the background. */
  | 'idle'
  | 'connecting'
  | 'live'
  | 'reconnecting'
  /** The server said this match can produce nothing further. */
  | 'ended'

export interface LiveStream {
  status: LiveStreamStatus
  lastUpdateAt: number | null
}

/**
 * Keeps one match up to date over Server-Sent Events, writing each frame straight into the query
 * cache so the page renders from a single source whether the data arrived by fetch or by stream.
 *
 * The stream closes whenever the tab is hidden. That is not a battery optimisation: an open
 * connection is what tells the server someone is watching, and the server only spends provider
 * calls while somebody is. A tab forgotten in the background would otherwise drain a daily
 * allowance of a hundred calls on its own.
 */
export function useMatchLiveStream(slug: string | undefined, enabled: boolean): LiveStream {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<LiveStreamStatus>('idle')
  const [lastUpdateAt, setLastUpdateAt] = useState<number | null>(null)

  const active = Boolean(slug) && enabled

  useEffect(() => {
    if (!active) {
      return
    }

    let source: EventSource | null = null
    let retryTimer: number | undefined
    let attempt = 0
    let finished = false

    function close() {
      source?.close()
      source = null
      window.clearTimeout(retryTimer)
    }

    function connect() {
      if (finished) return

      setStatus(attempt === 0 ? 'connecting' : 'reconnecting')
      source = new EventSource(`${baseUrl}/api/matches/${encodeURIComponent(slug!)}/stream`)

      source.onopen = () => {
        attempt = 0
        setStatus('live')
      }

      source.addEventListener('match', (event) => {
        const match = JSON.parse((event as MessageEvent<string>).data) as MatchDetails

        queryClient.setQueryData(matchKeys.details(slug!), match)
        setLastUpdateAt(Date.now())
      })

      source.addEventListener('end', () => {
        // The match is over. Reconnecting would only earn another 'end'.
        finished = true
        close()
        setStatus('ended')
      })

      source.onerror = () => {
        close()
        if (finished) return

        attempt += 1

        // Exponential with jitter, so a server coming back up is not hit by every client at once.
        const backoff = Math.min(FIRST_RETRY_MS * 2 ** (attempt - 1), MAX_RETRY_MS)
        const jitter = backoff * 0.25 * Math.random()

        setStatus('reconnecting')
        retryTimer = window.setTimeout(connect, backoff + jitter)
      }
    }

    function onVisibilityChange() {
      if (document.visibilityState === 'hidden') {
        close()
        setStatus('idle')
        return
      }

      if (!source && !finished) {
        attempt = 0
        connect()
      }
    }

    document.addEventListener('visibilitychange', onVisibilityChange)

    if (document.visibilityState === 'visible') {
      connect()
    }

    return () => {
      finished = true
      document.removeEventListener('visibilitychange', onVisibilityChange)
      close()
    }
  }, [slug, active, queryClient])

  // Derived rather than stored, so leaving the page does not need a state update to report that
  // nothing is streaming.
  return { status: active ? status : 'idle', lastUpdateAt }
}
