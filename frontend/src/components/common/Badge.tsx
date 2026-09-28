import type { ReactNode } from 'react'

import { cn } from '@/utils/cn'

type BadgeTone = 'neutral' | 'live' | 'upcoming' | 'completed'

const toneClasses: Record<BadgeTone, string> = {
  neutral: 'border-line bg-surface-muted text-ink-muted',
  live: 'border-live-line bg-live-soft text-live',
  upcoming: 'border-upcoming-line bg-upcoming-soft text-upcoming',
  completed: 'border-line bg-surface-muted text-ink-subtle',
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
