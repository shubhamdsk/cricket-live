import { Link } from 'react-router-dom'

import { focusRing } from '@/components/common/focusRing'
import { usePageMeta } from '@/hooks/usePageMeta'
import { cn } from '@/utils/cn'

export function NotFoundPage() {
  // Not indexed, and this one matters more than it looks. A static host answers every unknown path
  // with this page and a 200, so without the instruction a crawler would read every typo and dead
  // link as a real page with real content.
  usePageMeta('Page not found', {
    description: 'This page does not exist.',
    noindex: true,
  })

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
