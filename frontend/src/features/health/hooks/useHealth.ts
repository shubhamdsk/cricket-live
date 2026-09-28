import { useQuery } from '@tanstack/react-query'

import { getHealth } from '@/features/health/api/getHealth'

export function useHealth() {
  return useQuery({
    queryKey: ['health'],
    queryFn: ({ signal }) => getHealth(signal),
  })
}
