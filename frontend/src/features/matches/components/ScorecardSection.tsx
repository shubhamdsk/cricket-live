import { useId, useState } from 'react'

import { Button } from '@/components/common/Button'
import { Card } from '@/components/common/Card'
import { Skeleton } from '@/components/common/Skeleton'
import { InningsCardView } from '@/features/matches/components/InningsCardView'
import { useMatchScorecard } from '@/features/matches/hooks/useMatches'

/**
 * The scorecard, behind a button that has to be pressed.
 *
 * Every other section on this page loads with the page. This one does not, and the reason is the
 * allowance behind it: two hundred requests a month rather than a day. Opening it on page load
 * would mean every visit to any match page spends one, and the month would be gone in an
 * afternoon. A press is a reader saying they actually want it.
 *
 * Whether a card exists cannot be known without asking, so unlike the other optional sections on
 * this page it cannot hide itself in advance. It says plainly that there is none rather than
 * showing an empty table, which is the spirit of D-013 within what one request can tell us.
 */
export function ScorecardSection({ slug }: { slug: string | undefined }) {
  const [requested, setRequested] = useState(false)
  const panelId = useId()

  const { data, isFetching, isError, error } = useMatchScorecard(slug, requested)

  return (
    <Card className="p-4 sm:p-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-base font-semibold tracking-tight text-ink">Scorecard</h2>

        {!requested && (
          <Button
            variant="secondary"
            size="sm"
            aria-expanded={false}
            aria-controls={panelId}
            onClick={() => setRequested(true)}
          >
            Show scorecard
          </Button>
        )}
      </div>

      {!requested && (
        <p className="mt-2 text-sm text-ink-muted">
          The full card comes from a second source with a small request allowance, so it is
          fetched only when asked for.
        </p>
      )}

      <div id={panelId}>
        {requested && isFetching && (
          <div className="mt-4 space-y-3" role="status" aria-busy="true">
            <span className="sr-only">Loading the scorecard</span>
            <Skeleton className="h-6 w-40 rounded-md" />
            <Skeleton className="h-48 w-full rounded-md" />
          </div>
        )}

        {requested && !isFetching && isError && (
          // No retry button. A failure has already spent a request, and the usual cause is that
          // the allowance is gone — which pressing again cannot fix and does make worse.
          <p className="mt-2 text-sm text-ink-muted">{error.message}</p>
        )}

        {requested && !isFetching && !isError && data === null && (
          <p className="mt-2 text-sm text-ink-muted">
            No scorecard is available for this match.
          </p>
        )}

        {data != null && (
          <div className="mt-4 space-y-8">
            {data.innings.map((innings) => (
              <InningsCardView key={innings.inningsNumber} innings={innings} />
            ))}

            {/* The source's own sentence, shown last because it is the conclusion of everything
                above it. Verbatim, like every other status line in this app. */}
            {data.status && (
              <p className="border-t border-line pt-4 text-sm font-medium text-ink">
                {data.status}
              </p>
            )}
          </div>
        )}
      </div>
    </Card>
  )
}
