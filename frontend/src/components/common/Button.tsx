import type { ButtonHTMLAttributes } from 'react'

import { focusRing } from '@/components/common/focusRing'
import { cn } from '@/utils/cn'

type ButtonVariant = 'primary' | 'secondary' | 'ghost'
type ButtonSize = 'sm' | 'md'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant
  size?: ButtonSize
}

/*
 * `text-on-brand` and `hover:bg-brand-hover` are tokens of their own rather than reuses of
 * `surface` and `brand-strong`, which is what they were. Both reuses happened to be true only in
 * the light theme: `surface` became translucent, which made the label on a green button almost
 * invisible, and `brand-strong` became a light accent, which made the hover state fill with a
 * colour that white text cannot sit on.
 */
const variantClasses: Record<ButtonVariant, string> = {
  primary: 'bg-brand text-on-brand hover:bg-brand-hover',
  secondary: 'border border-line-strong bg-surface text-ink-muted hover:bg-surface-muted',
  ghost: 'text-ink-muted hover:bg-surface-muted hover:text-ink',
}

const sizeClasses: Record<ButtonSize, string> = {
  sm: 'min-h-9 px-3 text-sm',
  md: 'min-h-11 px-4 text-sm sm:min-h-10',
}

export function Button({
  variant = 'primary',
  size = 'md',
  className,
  type = 'button',
  ...props
}: ButtonProps) {
  return (
    <button
      type={type}
      className={cn(
        'inline-flex items-center justify-center gap-2 rounded-full font-medium transition duration-150 ease-out active:scale-[0.98] disabled:cursor-not-allowed disabled:opacity-60 disabled:active:scale-100',
        focusRing,
        variantClasses[variant],
        sizeClasses[size],
        className,
      )}
      {...props}
    />
  )
}
