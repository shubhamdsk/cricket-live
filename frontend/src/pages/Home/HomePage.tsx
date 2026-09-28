import { ApiStatus } from '@/features/health/components/ApiStatus'

export function HomePage() {
  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">Home</h1>
      <ApiStatus />
      <p className="text-slate-600">
        Featured, live, upcoming, and recent match sections arrive in Sprint 2.
      </p>
    </div>
  )
}
