import type { LiveStream } from '@/features/matches/hooks/useMatchLiveStream'
import { cn } from '@/utils/cn'

/**
 * Says how fresh the score is and how it got here. The wording matters as much as the dot: the
 * provider's free data runs several minutes behind the ground, so nothing here may suggest a
 * ball-by-ball feed. A clock time is a fact the reader can judge for themselves; "Live" on its own
 * would be a promise we cannot keep.
 */
export function LiveStreamIndicator({ status, lastUpdateAt }: LiveStream) {
  if (status === 'idle' || status === 'ended') {
    return null
  }

  const connected = status === 'live'

  return (
    <p
      className={cn(
        'flex items-center gap-1.5 text-xs',
        connected ? 'text-ink-subtle' : 'text-ink-muted',
      )}
      title="Scores refresh about every five minutes. Free cricket data runs a few minutes behind play."
      aria-live="polite"
    >
      <span
        className={cn(
          'size-1.5 shrink-0 rounded-full',
          connected ? 'bg-live' : 'animate-pulse bg-line-strong',
        )}
        aria-hidden
      />
      {connected ? <Freshness at={lastUpdateAt} /> : 'Reconnecting…'}
    </p>
  )
}

/**
 * An absolute time rather than "2 min ago". A relative label has to be re-rendered on a timer to
 * stay true, and running a clock to animate a counter for data that moves every five minutes is
 * work the reader never asked for. This also keeps the component pure.
 */
function Freshness({ at }: { at: number | null }) {
  if (at === null) {
    return <>Waiting for the next update</>
  }

  return <>Updated {timeFormatter.format(at)}</>
}

const timeFormatter = new Intl.DateTimeFormat(undefined, {
  hour: 'numeric',
  minute: '2-digit',
})
