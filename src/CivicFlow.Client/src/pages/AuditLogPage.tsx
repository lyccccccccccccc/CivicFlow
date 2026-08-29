import { useEffect, useState } from 'react'
import { Alert, MenuItem, Paper, Stack, Table, TableBody, TableCell, TableHead, TablePagination, TableRow, TextField, Typography } from '@mui/material'
import { useSearchParams } from 'react-router-dom'
import { api, type PagedResponse } from '../api/client'

type AdminUser = { id: string; firstName: string; lastName: string; email: string }
type AuditRow = { id: string; action: string; message: string; createdAtUtc: string; serviceRequestId: string; actorId: string; userName?: string; userEmail?: string; referenceNumber?: string }

export function AuditLogPage() {
  const [params, setParams] = useSearchParams(); const query = params.toString(); const [data, setData] = useState<PagedResponse<AuditRow> | null>(null); const [users, setUsers] = useState<AdminUser[]>([]); const [error, setError] = useState('')
  useEffect(() => { api<AdminUser[]>('/admin/users').then(setUsers) }, [])
  useEffect(() => { api<PagedResponse<AuditRow>>(`/admin/audit-logs${query ? `?${query}` : ''}`).then(setData).catch(e => setError(e instanceof Error ? e.message : 'Unable to load audit log')) }, [query])
  const update = (key: string, value: string) => { const next = new URLSearchParams(params); if (value) next.set(key, value); else next.delete(key); if (key !== 'page') next.set('page', '1'); setParams(next) }
  const page = Number(params.get('page') ?? '1'); const pageSize = Number(params.get('pageSize') ?? '25')
  return <Stack spacing={3}><div><Typography variant="h3">Audit log</Typography><Typography color="text.secondary">Read-only history of case, category and user access changes.</Typography></div>{error && <Alert severity="error">{error}</Alert>}
    <Paper sx={{ p: 2 }}><Stack direction={{ xs: 'column', md: 'row' }} spacing={2} useFlexGap sx={{ flexWrap: 'wrap' }}><TextField select size="small" label="User" value={params.get('userId') ?? ''} onChange={e => update('userId', e.target.value)} sx={{ minWidth: 190 }}><MenuItem value="">All users</MenuItem>{users.map(x => <MenuItem key={x.id} value={x.id}>{x.firstName} {x.lastName}</MenuItem>)}</TextField><TextField size="small" label="Action" value={params.get('action') ?? ''} onChange={e => update('action', e.target.value)} /><TextField size="small" label="Case reference or title" value={params.get('case') ?? ''} onChange={e => update('case', e.target.value)} /><TextField size="small" type="date" label="From" value={params.get('from')?.slice(0, 10) ?? ''} onChange={e => update('from', e.target.value)} slotProps={{ inputLabel: { shrink: true } }} /><TextField size="small" type="date" label="To" value={params.get('to')?.slice(0, 10) ?? ''} onChange={e => update('to', e.target.value)} slotProps={{ inputLabel: { shrink: true } }} /></Stack></Paper>
    <Paper sx={{ overflowX: 'auto' }}><Table><TableHead><TableRow><TableCell>Timestamp</TableCell><TableCell>User</TableCell><TableCell>Action</TableCell><TableCell>Case</TableCell><TableCell>Details</TableCell></TableRow></TableHead><TableBody>{data?.items.map(row => <TableRow key={row.id}><TableCell>{new Date(row.createdAtUtc).toLocaleString()}</TableCell><TableCell>{row.userName ?? 'System'}<Typography variant="caption" sx={{ display: 'block' }} color="text.secondary">{row.userEmail}</Typography></TableCell><TableCell>{format(row.action)}</TableCell><TableCell>{row.referenceNumber ?? '—'}</TableCell><TableCell>{row.message}</TableCell></TableRow>)}{data?.items.length === 0 && <TableRow><TableCell colSpan={5}>No audit events match the filters.</TableCell></TableRow>}</TableBody></Table><TablePagination component="div" count={data?.totalCount ?? 0} page={Math.max(0, page - 1)} rowsPerPage={pageSize} rowsPerPageOptions={[25, 50, 100]} onPageChange={(_, next) => update('page', String(next + 1))} onRowsPerPageChange={e => update('pageSize', e.target.value)} /></Paper>
  </Stack>
}
const format = (value: string) => value.replace(/([A-Z])/g, ' $1').trim()
