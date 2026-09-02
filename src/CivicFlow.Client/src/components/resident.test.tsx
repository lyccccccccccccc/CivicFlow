import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { ThemeProvider } from '@mui/material'
import axe from 'axe-core'
import { describe, expect, it, vi } from 'vitest'
import { PasswordField, ResidentRequestCard, NotificationGroup } from './resident'
import { civicTheme } from '../theme'

const wrapper = (children: React.ReactNode) => <ThemeProvider theme={civicTheme}><MemoryRouter>{children}</MemoryRouter></ThemeProvider>

describe('resident shared components', () => {
  it('provides password autocomplete and an accessible visibility control', async () => {
    const user = userEvent.setup(); render(wrapper(<PasswordField label="Password" autoComplete="current-password" />))
    const input = screen.getByLabelText('Password'); expect(input).toHaveAttribute('autocomplete', 'current-password'); expect(input).toHaveAttribute('type', 'password')
    await user.click(screen.getByRole('button', { name: 'Show password' })); expect(input).toHaveAttribute('type', 'text'); expect(screen.getByRole('button', { name: 'Hide password' })).toBeVisible()
  })

  it('projects only resident-safe fields on request cards', () => {
    const item = { id: 'case-1', referenceNumber: 'CF-20260902-DEMO01', title: 'Damaged footpath near library', status: 'InProgress', categoryName: 'Roads and footpaths', submittedAtUtc: '2026-09-02T00:00:00Z', slaState: 'OnTrack' }
    const { container } = render(wrapper(<ResidentRequestCard item={item} />)); expect(screen.getByText(item.referenceNumber)).toBeVisible(); expect(screen.getByText('Service target on track')).toBeVisible(); expect(container).not.toHaveTextContent(/priority|assigned officer|internal|first response/i)
  })

  it('expresses unread notifications with text and semantic structure', () => {
    render(wrapper(<NotificationGroup label="Today" pending={[]} onRead={vi.fn()} items={[{ id: 'notice-1', title: 'Information requested', message: 'Please reply with more detail.', createdAtUtc: '2026-09-02T01:00:00Z' }]} />))
    expect(screen.getByText('Unread')).toBeVisible(); expect(screen.getByRole('article')).toBeVisible(); expect(screen.getByRole('button', { name: 'Mark read' })).toBeEnabled()
  })

  it('has no serious or critical axe violations in the resident card', async () => {
    const { container } = render(wrapper(<ResidentRequestCard item={{ id: 'case-1', referenceNumber: 'CF-20260902-DEMO01', title: 'Damaged footpath near library', status: 'Submitted', categoryName: 'Roads and footpaths', submittedAtUtc: '2026-09-02T00:00:00Z', slaState: 'OnTrack' }} />))
    const results = await axe.run(container); expect(results.violations.filter(item => ['serious', 'critical'].includes(item.impact ?? ''))).toEqual([])
  })
})
