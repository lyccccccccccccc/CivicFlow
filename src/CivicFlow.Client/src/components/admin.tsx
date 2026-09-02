/* eslint-disable react-refresh/only-export-components */
import type { ReactNode } from 'react'
import { Box, Button, Chip, Paper, Stack, Typography } from '@mui/material'
import { Link } from 'react-router-dom'
import { formatDateTime } from './formatting'

const roleLabels: Record<string, string> = { Resident: 'Resident', CaseOfficer: 'Service officer', TeamManager: 'Team manager', SystemAdministrator: 'System administrator' }
export const adminSections = ['users', 'categories', 'workflow'] as const
export type AdminSection = typeof adminSections[number]
export const resolveAdminSection = (params: URLSearchParams): AdminSection => {
  const requested = params.get('section') as AdminSection | null
  return requested && adminSections.includes(requested) ? requested : 'users'
}
export const updateAdminQuery = (params: URLSearchParams, key: string, value: string) => {
  const next = new URLSearchParams(params)
  if (value) next.set(key, value); else next.delete(key)
  if (key !== 'page') next.set('page', '1')
  return next
}
export const hasAuditFilters = (params: URLSearchParams) => [...params.keys()].some(key => !['page', 'pageSize'].includes(key))
export function RoleLabel({ role }: { role: string }) { return <Chip size="small" variant="outlined" label={roleLabels[role] ?? role} /> }
export const roleLabel = (role: string) => roleLabels[role] ?? role
export function AccountStatus({ active }: { active: boolean }) { return <Chip size="small" color={active ? 'success' : 'default'} label={active ? 'Active' : 'Disabled'} /> }
export function AdminUserCard({ user, self, actions }: { user: { firstName: string; lastName: string; email: string; roles: string[]; isActive: boolean; createdAtUtc: string }; self?: boolean; actions: ReactNode }) { return <Paper component="article" sx={{ p: 4 }}><Stack spacing={3}><Box><Typography variant="h6" sx={{ overflowWrap: 'anywhere' }}>{user.firstName} {user.lastName}{self && <Chip label="You" size="small" sx={{ ml: 1 }} />}</Typography><Typography color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>{user.email}</Typography></Box><Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}><RoleLabel role={user.roles[0] ?? 'Resident'} /><AccountStatus active={user.isActive} /></Stack><Typography variant="caption" color="text.secondary">Created {formatDateTime(user.createdAtUtc)}</Typography><Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>{actions}</Stack>{self && <Typography variant="caption" color="text.secondary">You cannot disable your own account or remove your administrator role.</Typography>}</Stack></Paper> }
export function CategorySlaCard({ category, actions }: { category: { name: string; description: string; firstResponseHours: number; resolutionHours: number; isActive: boolean }; actions: ReactNode }) { return <Paper component="article" sx={{ p: 4 }}><Stack spacing={3}><Box><Typography variant="h6" sx={{ overflowWrap: 'anywhere' }}>{category.name}</Typography><Typography color="text.secondary" sx={{ overflowWrap: 'anywhere' }}>{category.description}</Typography></Box><AccountStatus active={category.isActive} /><Typography><strong>First response:</strong> {duration(category.firstResponseHours)}</Typography><Typography><strong>Resolution:</strong> {duration(category.resolutionHours)}</Typography><Stack direction="row" spacing={1}>{actions}</Stack></Stack></Paper> }
export function AuditEventCard({ row }: { row: { action: string; message: string; createdAtUtc: string; serviceRequestId: string; userName?: string; userEmail?: string; referenceNumber?: string } }) { return <Paper component="article" sx={{ p: 4 }}><Stack spacing={2}><Stack direction="row" sx={{ justifyContent: 'space-between', gap: 2, flexWrap: 'wrap' }}><Typography variant="h6">{auditAction(row.action)}</Typography><Typography variant="caption" color="text.secondary">{formatDateTime(row.createdAtUtc)}</Typography></Stack><Typography><strong>Actor:</strong> {row.userName ?? 'System'}{row.userEmail ? ` — ${row.userEmail}` : ''}</Typography>{row.referenceNumber && <Button component={Link} to={`/cases/${row.serviceRequestId}`} sx={{ alignSelf: 'flex-start' }}>{row.referenceNumber}</Button>}<Typography sx={{ overflowWrap: 'anywhere' }}>{row.message}</Typography></Stack></Paper> }
export const auditAction = (value: string) => value.replace(/([A-Z])/g, ' $1').trim()
export const duration = (hours: number) => hours >= 24 && hours % 24 === 0 ? `${hours / 24} ${hours === 24 ? 'day' : 'days'} (${hours} hours)` : `${hours} ${hours === 1 ? 'hour' : 'hours'}`
export const workflowStatuses = [{ name: 'Submitted', description: 'Received and awaiting staff review.' }, { name: 'Triaged', description: 'Priority and service targets have been reviewed.' }, { name: 'Assigned', description: 'Allocated to a service officer.' }, { name: 'In progress', description: 'Service work is underway.' }, { name: 'Waiting for resident', description: 'More information is required from the resident.' }, { name: 'Resolved', description: 'Work is complete and a resolution has been recorded.' }, { name: 'Reopened', description: 'The resident has requested further work.' }, { name: 'Closed', description: 'The request lifecycle is complete.' }, { name: 'Rejected', description: 'The request is not proceeding and a reason is recorded.' }]
