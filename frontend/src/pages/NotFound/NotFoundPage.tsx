import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">Page not found</h1>
      <Link to="/" className="text-emerald-700 underline underline-offset-4">
        Back to home
      </Link>
    </div>
  )
}
