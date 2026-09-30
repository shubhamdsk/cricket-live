import type { ReactNode } from 'react'

import { cn } from '@/utils/cn'

type BadgeTone = 'neutral' | 'live' | 'upcoming' | 'completed'

/**
 * `completed` reads the same as `neutral`, and that is the fix rather than an oversight.
 *
 * It used to be one step lighter, on `ink-subtle`. That colour is fine on white — 5.2:1 — but on
 * the muted badge background it falls to 4.34:1, and at 12px the floor is 4.5:1. Lighthouse
 * caught it. Darkening it to `ink-muted` is the only change that keeps a filled badge, so the two
 * tones now render identically. They are kept separate because the callers mean different things
 * by them, and because a future palette can pull them apart again without touching call sites.
 */
const toneClasses: Record<BadgeTone, string> = {
  neutral: 'border-line bg-surface-muted text-ink-muted',
  live: 'border-live-line bg-live-soft text-live',
  upcoming: 'border-upcoming-line bg-upcoming-soft text-upcoming',
  completed: 'border-line bg-surface-muted text-ink-muted',
}

interface BadgeProps {
  tone?: BadgeTone
  children: ReactNode
  className?: string
}

export function Badge({ tone = 'neutral', children, className }: BadgeProps) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-semibold tracking-wide uppercase',
        toneClasses[tone],
        className,
      )}
    >
      {tone === 'live' && (
        <span className="relative flex size-1.5">
          <span className="absolute inline-flex size-full animate-ping rounded-full bg-live opacity-75" />
          <span className="relative inline-flex size-1.5 rounded-full bg-live" />
        </span>
      )}
      {children}
    </span>
  )
}
