import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { ThemeProvider } from '@mui/material'
import axe from 'axe-core'
import { describe, expect, it } from 'vitest'
import { civicTheme } from '../theme'
import { DashboardKpiCard, SlaWorkItemCard, StaffCaseCard, StaffWorkspaceSection } from './staff'

const wrap = (node: React.ReactNode) => <ThemeProvider theme={civicTheme}><MemoryRouter>{node}</MemoryRouter></ThemeProvider>
const item = { id: 'case-1', referenceNumber: 'CF-20260902-STAFF1', title: 'Streetlight outage near community hall', description: '', address: '', serviceCategoryId: 'cat', categoryName: 'Street lighting', status: 'InProgress', priority: 'High', assignedOfficerId: 'officer', assignedOfficerName: 'Casey Officer', submittedAtUtc: '2026-09-02T04:13:00Z', updatedAtUtc: '2026-09-02T05:13:00Z', firstResponseSlaState: 'OnTrack', resolutionSlaState: 'AtRisk', slaState: 'AtRisk' }

describe('staff experience components', () => {
  it('shows operational fields and a real request link on the staff card', () => { render(wrap(<StaffCaseCard item={item} />)); expect(screen.getByText('Casey Officer')).toBeVisible(); expect(screen.getByText('High')).toBeVisible(); expect(screen.getByRole('link', { name: 'Open request' })).toHaveAttribute('href', '/cases/case-1') })
  it('maps KPI cards to an accessible queue link', () => { render(wrap(<DashboardKpiCard label="Overdue" value={3} description="Cases with an overdue target" to="/cases?slaState=Overdue" />)); expect(screen.getByRole('link', { name: /Overdue: 3/ })).toHaveAttribute('href', '/cases?slaState=Overdue') })
  it('shows SLA work details without relying on colour', () => { render(wrap(<SlaWorkItemCard item={{ ...item, nextSlaTarget: 'Resolution', nextSlaDueAtUtc: item.updatedAtUtc }} />)); expect(screen.getByText('At risk')).toBeVisible(); expect(screen.getByText(/Assigned officer/)).toBeVisible() })
  it('states public and internal visibility in text', () => { render(wrap(<><StaffWorkspaceSection title="Public conversation" visibility="public">Public</StaffWorkspaceSection><StaffWorkspaceSection title="Internal notes" visibility="internal">Internal</StaffWorkspaceSection></>)); expect(screen.getByText('Visible to the resident')).toBeVisible(); expect(screen.getByText('Not visible to the resident')).toBeVisible() })
  it('has no serious or critical axe violations', async () => { const { container } = render(wrap(<StaffCaseCard item={item} />)); const results = await axe.run(container); expect(results.violations.filter(result => ['serious', 'critical'].includes(result.impact ?? ''))).toEqual([]) })
})
