import { apiGet } from '@/services/apiClient'
import type { HealthStatus } from '@/features/health/types'

export function getHealth(signal?: AbortSignal): Promise<HealthStatus> {
  return apiGet<HealthStatus>('/api/health', signal)
}
