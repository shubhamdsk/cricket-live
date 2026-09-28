import { useHealth } from '@/features/health/hooks/useHealth'

export function ApiStatus() {
  const { data, isPending, isError, error } = useHealth()

  if (isPending) {
    return (
      <p className="rounded-md border border-slate-200 bg-white px-4 py-3 text-sm text-slate-500">
        Checking API connection…
      </p>
    )
  }

  if (isError) {
    return (
      <p
        role="alert"
        className="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
      >
        API unreachable: {error.message}
      </p>
    )
  }

  return (
    <p className="rounded-md border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
      API {data.status} · {data.environment}
    </p>
  )
}
