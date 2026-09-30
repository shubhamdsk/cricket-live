import { Component, type ErrorInfo, type ReactNode } from 'react'

import { focusRing } from '@/components/common/focusRing'
import { cn } from '@/utils/cn'

interface Props {
  children: ReactNode
}

interface State {
  error: Error | null
}

/**
 * The last line of defence: a render error becomes a page you can read instead of a blank screen.
 *
 * A class component because this is the one thing hooks still cannot do — `componentDidCatch` has
 * no hook equivalent, and every "error boundary hook" is a wrapper around a class like this one.
 *
 * Note what this does *not* cover, because it is easy to over-trust: errors thrown in event
 * handlers, in promises, and in anything asynchronous never reach a boundary. Those are handled
 * where they happen — TanStack Query surfaces fetch failures as `isError`, which the pages render
 * as `ErrorState`. This catches the remaining case: a bug in rendering itself.
 *
 * There is no reset method, and none is needed: the container this sits inside is keyed on the
 * pathname, so navigating away remounts this instance and takes the error with it. An error on one
 * route therefore cannot follow the reader to the next.
 */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    // Logged rather than swallowed. There is no error reporting service wired up, so the console
    // is the only place this can go — but losing it entirely would make a white screen
    // undiagnosable, and pretending otherwise by adding a stub reporter would be worse.
    console.error('Unhandled render error', error, info.componentStack)
  }

  render() {
    if (this.state.error === null) {
      return this.props.children
    }

    return (
      <div className="mx-auto max-w-md py-12 text-center">
        <h1 className="text-lg font-semibold text-ink">Something went wrong on this page</h1>

        <p className="mt-2 text-sm text-ink-subtle">
          This is a fault in the site rather than in the cricket data. Reloading usually clears
          it.
        </p>

        {/*
          The message, not the stack. It is occasionally the one clue that makes a bug report
          useful, and it is already visible in the console to anyone who looks.
        */}
        <p className="mt-4 break-words rounded-card border border-line bg-surface-muted p-3 text-left text-xs text-ink-subtle">
          {this.state.error.message}
        </p>

        <button
          type="button"
          onClick={() => window.location.reload()}
          className={cn(
            'mt-6 inline-flex min-h-11 items-center rounded-card bg-brand px-4 text-sm font-medium text-white transition hover:bg-brand-strong',
            focusRing,
          )}
        >
          Reload the page
        </button>
      </div>
    )
  }
}
