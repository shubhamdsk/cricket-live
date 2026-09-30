import { useState } from 'react'

import type { Team } from '@/features/teams/types'
import { apiUrl } from '@/services/apiClient'
import { cn } from '@/utils/cn'

/**
 * A team's crest, or its initials when there is no crest to show.
 *
 * Most sides reach us without one, so the fallback is the normal case rather than the error case.
 * The initials come from the abbreviation the provider gave — never invented from the name, since
 * a made-up three-letter code looks exactly as authoritative as a real one.
 */
export function TeamCrest({ team, className }: { team: Team; className?: string }) {
  // A crest sometimes 404s — the provider's address was dead, or ours could not fetch it — which
  // renders as a broken-image icon: worse than no crest, because it reads as our bug. Falling
  // back to the same initials the no-crest case uses means a dead URL is indistinguishable from
  // an absent one, which is how it should look.
  const [failed, setFailed] = useState(false)

  const shape = cn(
    'shrink-0 overflow-hidden rounded-full border border-line bg-surface-muted',
    className,
  )

  if (team.logoUrl !== null && !failed) {
    return (
      <img
        // A path on our API rather than a whole address, so it has to be resolved the same way a
        // fetch would be. The provider's terms forbid the browser asking them for this directly.
        src={apiUrl(team.logoUrl)}
        // Empty, with the name carried by the text beside every use of this. A crest repeating
        // the name it sits next to is noise to a screen reader.
        alt=""
        loading="lazy"
        decoding="async"
        onError={() => setFailed(true)}
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
