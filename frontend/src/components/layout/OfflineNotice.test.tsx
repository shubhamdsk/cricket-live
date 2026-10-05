import { act, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'

import { OfflineNotice } from '@/components/layout/OfflineNotice'

function goOffline(offline: boolean) {
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(!offline)
  act(() => {
    window.dispatchEvent(new Event(offline ? 'offline' : 'online'))
  })
}

describe('OfflineNotice', () => {
  it('says nothing while online', () => {
    render(<OfflineNotice />)

    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('appears when the connection drops and goes when it returns', () => {
    render(<OfflineNotice />)

    goOffline(true)
    expect(screen.getByRole('status')).toHaveTextContent('You’re offline')

    goOffline(false)
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })
})
