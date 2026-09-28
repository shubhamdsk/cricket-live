import { Badge } from '@/components/common/Badge'
import type { MatchStatus } from '@/features/matches/types'

const statusLabels: Record<MatchStatus, string> = {
  live: 'Live',
  upcoming: 'Upcoming',
  completed: 'Result',
}

export function MatchStatusBadge({ status }: { status: MatchStatus }) {
  return <Badge tone={status}>{statusLabels[status]}</Badge>
}
