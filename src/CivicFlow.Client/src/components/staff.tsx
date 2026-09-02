import type { ReactNode } from 'react'
import { Box, Button, LinearProgress, Paper, Stack, Typography } from '@mui/material'
import { Link } from 'react-router-dom'
import type { CaseItem, ChartRow } from '../api/client'
import { formatDateTime } from './formatting'
import { EmptyState, PriorityChip, SlaStatus, StatusChip } from './ui'

export function StaffCaseCard({ item, showOfficer = true }: { item: CaseItem; showOfficer?: boolean }) {
  return <Paper component="article" sx={{ p: 4 }}><Stack spacing={3}>
    <Stack direction="row" spacing={2} sx={{ justifyContent: 'space-between', alignItems: 'flex-start' }}><Box sx={{ minWidth: 0 }}><Typography variant="overline" color="primary.main" sx={{ fontWeight: 850, overflowWrap: 'anywhere' }}>{item.referenceNumber}</Typography><Typography component="h2" variant="h6" title={item.title} sx={{ overflowWrap: 'anywhere' }}>{item.title}</Typography></Box><StatusChip status={item.status} /></Stack>
    <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}><PriorityChip priority={item.priority} /><SlaStatus state={item.slaState} /></Stack>
    <Box><Typography variant="body2"><strong>Category:</strong> {item.categoryName}</Typography>{showOfficer && <Typography variant="body2"><strong>Assigned officer:</strong> {item.assignedOfficerName ?? 'Not assigned'}</Typography>}<Typography variant="body2" color="text.secondary">{item.updatedAtUtc ? 'Updated' : 'Submitted'} {formatDateTime(item.updatedAtUtc ?? item.submittedAtUtc)}</Typography></Box>
    <Button component={Link} to={`/cases/${item.id}`} variant="outlined" sx={{ alignSelf: 'flex-start', minHeight: 44 }}>Open request</Button>
  </Stack></Paper>
}

export function DashboardKpiCard({ label, value, description, to }: { label: string; value: number; description: string; to: string }) {
  return <Paper component={Link} to={to} aria-label={`${label}: ${value}. ${description}`} sx={{ p: 3, height: '100%', display: 'block', color: 'inherit', textDecoration: 'none', '&:hover': { borderColor: 'primary.main', boxShadow: 2 }, '&:focus-visible': { outline: '3px solid', outlineColor: 'primary.main', outlineOffset: 2 } }}><Typography color="text.secondary" sx={{ fontWeight: 700 }}>{label}</Typography><Typography variant="h3" sx={{ fontWeight: 850 }}>{value}</Typography><Typography variant="caption" color="text.secondary">{description}</Typography></Paper>
}

export function SlaWorkItemCard({ item }: { item: Pick<CaseItem, 'id' | 'referenceNumber' | 'title' | 'priority' | 'status' | 'assignedOfficerName' | 'slaState' | 'nextSlaDueAtUtc' | 'nextSlaTarget'> }) {
  return <Paper component="article" sx={{ p: 4 }}><Stack spacing={3}><Box><Typography variant="overline" color="primary.main" sx={{ fontWeight: 850 }}>{item.referenceNumber}</Typography><Typography component="h3" variant="h6" sx={{ overflowWrap: 'anywhere' }}>{item.title}</Typography></Box><Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}><StatusChip status={item.status} /><PriorityChip priority={item.priority} /><SlaStatus state={item.slaState} /></Stack><Typography variant="body2"><strong>{item.nextSlaTarget ?? 'Next target'}:</strong> {item.nextSlaDueAtUtc ? formatDateTime(item.nextSlaDueAtUtc) : 'Not set'}</Typography><Typography variant="body2"><strong>Assigned officer:</strong> {item.assignedOfficerName ?? 'Not assigned'}</Typography><Button component={Link} to={`/cases/${item.id}`} variant="outlined" sx={{ alignSelf: 'flex-start', minHeight: 44 }}>Open request</Button></Stack></Paper>
}

export function StaffWorkspaceSection({ title, visibility, children }: { title: string; visibility?: 'public' | 'internal'; children: ReactNode }) {
  const description = visibility === 'public' ? 'Visible to the resident' : visibility === 'internal' ? 'Not visible to the resident' : undefined
  return <Paper component="section" sx={{ p: { xs: 4, sm: 5 } }}><Typography component="h2" variant="h6">{title}</Typography>{description && <Typography variant="body2" color={visibility === 'internal' ? 'warning.dark' : 'text.secondary'} sx={{ fontWeight: 700, mt: .5 }}>{description}</Typography>}<Box sx={{ mt: 3 }}>{children}</Box></Paper>
}

export function CompactChart({ title, description, rows }: { title: string; description: string; rows: ChartRow[] }) {
  const total = rows.reduce((sum, row) => sum + row.count, 0); const max = Math.max(...rows.map(row => row.count), 1)
  if (!rows.length) return <Paper sx={{ p: 4 }}><EmptyState title={`No ${title.toLowerCase()} data`} description="No data matches the current filters." /></Paper>
  return <Paper sx={{ p: 4, height: '100%' }} aria-label={`${title}. ${rows.map(row => `${row.label}: ${row.count}`).join(', ')}`}><Typography variant="h5">{title}</Typography><Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>{description}</Typography><Stack spacing={2}>{rows.map(row => { const share = total ? Math.round(row.count / total * 100) : 0; return <Box key={row.label}><Stack direction="row" sx={{ justifyContent: 'space-between', gap: 2 }}><Typography>{row.label}</Typography><Typography sx={{ fontWeight: 800 }}>{row.count} · {share}%</Typography></Stack><LinearProgress variant="determinate" value={row.count / max * 100} aria-label={`${row.label}: ${row.count}, ${share}%`} sx={{ mt: 1, height: 8, borderRadius: 4 }} /></Box> })}</Stack></Paper>
}
