import type { Team } from '@/features/teams/types'
import { cn } from '@/utils/cn'

/**
 * A team's crest, or its initials when there is no crest to show.
 *
 * Most sides reach us without one, so the fallback is the normal case rather than the error case.
 * The initials come from the abbreviation the provider gave — never invented from the name, since
 * a made-up three-letter code looks exactly as authoritative as a real one.
 */
export function TeamCrest({ team, className }: { team: Team; className?: string }) {
  const shape = cn(
    'shrink-0 overflow-hidden rounded-full border border-line bg-surface-muted',
    className,
  )

  if (team.logoUrl !== null) {
    return (
      <img
        src={team.logoUrl}
        // Empty, with the name carried by the text beside every use of this. A crest repeating
        // the name it sits next to is noise to a screen reader.
        alt=""
        loading="lazy"
        className={cn(shape, 'object-contain')}
      />
    )
  }

  return (
    <span
      aria-hidden="true"
      className={cn(
        shape,
        'flex items-center justify-center text-xs font-semibold uppercase text-ink-subtle',
      )}
    >
      {team.shortName.slice(0, 3)}
    </span>
  )
}
