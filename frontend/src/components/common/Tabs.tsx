import { useRef, type KeyboardEvent, type ReactNode } from 'react'

import { focusRing } from '@/components/common/focusRing'
import { cn } from '@/utils/cn'

export interface TabItem {
  id: string
  label: string
}

interface TabsProps {
  items: TabItem[]
  activeId: string
  onChange: (id: string) => void
  label: string
}

export function Tabs({ items, activeId, onChange, label }: TabsProps) {
  const tabRefs = useRef(new Map<string, HTMLButtonElement>())

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const currentIndex = items.findIndex((item) => item.id === activeId)
    if (currentIndex === -1) return

    let nextIndex: number
    switch (event.key) {
      case 'ArrowRight':
        nextIndex = (currentIndex + 1) % items.length
        break
      case 'ArrowLeft':
        nextIndex = (currentIndex - 1 + items.length) % items.length
        break
      case 'Home':
        nextIndex = 0
        break
      case 'End':
        nextIndex = items.length - 1
        break
      default:
        return
    }

    event.preventDefault()
    const nextTab = items[nextIndex]
    onChange(nextTab.id)
    tabRefs.current.get(nextTab.id)?.focus()
  }

  return (
    <div
      role="tablist"
      aria-label={label}
      onKeyDown={handleKeyDown}
      className="flex w-full gap-1 rounded-card border border-line bg-surface p-1"
    >
      {items.map((item) => {
        const isActive = item.id === activeId

        return (
          <button
            key={item.id}
            ref={(node) => {
              if (node) tabRefs.current.set(item.id, node)
              else tabRefs.current.delete(item.id)
            }}
            role="tab"
            id={`tab-${item.id}`}
            aria-selected={isActive}
            aria-controls={`panel-${item.id}`}
            tabIndex={isActive ? 0 : -1}
            onClick={() => onChange(item.id)}
            className={cn(
              'min-h-10 flex-1 rounded-md px-2 text-xs font-medium transition-colors sm:text-sm',
              focusRing,
              isActive
                ? 'bg-brand-soft text-brand-strong'
                : 'text-ink-muted hover:bg-surface-muted hover:text-ink',
            )}
          >
            {item.label}
          </button>
        )
      })}
    </div>
  )
}

interface TabPanelProps {
  id: string
  children: ReactNode
}

export function TabPanel({ id, children }: TabPanelProps) {
  return (
    <div role="tabpanel" id={`panel-${id}`} aria-labelledby={`tab-${id}`} tabIndex={0}>
      {children}
    </div>
  )
}
