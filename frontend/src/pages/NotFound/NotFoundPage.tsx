import { Link } from 'react-router-dom'

import { focusRing } from '@/components/common/focusRing'
import { cn } from '@/utils/cn'

export function NotFoundPage() {
  return (
    <div className="space-y-4">
      <h1 className="text-xl font-semibold tracking-tight text-ink sm:text-2xl">
        Page not found
      </h1>
      <Link
        to="/"
        className={cn(
          'rounded-md text-sm font-medium text-brand-strong underline underline-offset-4',
          focusRing,
        )}
      >
        Back to home
      </Link>
    </div>
  )
}
