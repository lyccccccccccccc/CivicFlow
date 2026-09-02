import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { AuthProvider, useAuth } from './AuthContext'

const initial = { accessToken: 'test-access', refreshToken: 'test-refresh', expiresAt: '2026-09-03T00:00:00Z', user: { id: 'user-1', email: 'resident@example.test', firstName: 'Riley', lastName: 'Resident', roles: ['Resident'] } }

function ProfileNameHarness() {
  const { user, updateUser } = useAuth()
  return <><span>{user?.firstName} {user?.lastName}</span><button onClick={() => user && updateUser({ ...user, firstName: 'Updated' })}>Update name</button></>
}

describe('authenticated profile state', () => {
  beforeEach(() => localStorage.setItem('civicflow.auth', JSON.stringify(initial)))
  afterEach(() => localStorage.clear())

  it('updates the current user immediately and persists the refreshed profile', async () => {
    const user = userEvent.setup(); render(<AuthProvider><ProfileNameHarness /></AuthProvider>)
    await user.click(screen.getByRole('button', { name: 'Update name' }))
    expect(screen.getByText('Updated Resident')).toBeVisible()
    expect(JSON.parse(localStorage.getItem('civicflow.auth') ?? '{}').user.firstName).toBe('Updated')
  })
})
