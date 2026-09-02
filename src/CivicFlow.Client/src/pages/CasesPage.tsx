import { useEffect, useMemo, useState } from 'react'
import { Button, MenuItem, Paper, Stack, Table, TableBody, TableCell, TableHead, TablePagination, TableRow, TableSortLabel, TextField } from '@mui/material'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import { Link, useSearchParams } from 'react-router-dom'
import { api, type CaseItem, type Category, type Officer, type PagedResponse } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { EmptyState, ErrorState, PageHeader, PriorityChip, SlaStatus, StatusChip, TableSkeleton } from '../components/ui'
import { ActiveFilterSummary, ResidentRequestCard, ResponsiveDataView } from '../components/resident'
import { formatDateTime } from '../components/formatting'

const priorities = ['Low', 'Medium', 'High', 'Critical']
const statuses = ['Submitted', 'Triaged', 'Assigned', 'InProgress', 'WaitingForResident', 'Resolved', 'Closed', 'Reopened', 'Rejected']
const slaStates = ['OnTrack', 'AtRisk', 'Overdue', 'NoSla', 'Complete']

export function CasesPage() {
  const { user } = useAuth(); const resident = user?.roles.includes('Resident'); const officer = user?.roles.includes('CaseOfficer')
  const canAssign = user?.roles.some(x => x === 'TeamManager' || x === 'SystemAdministrator') ?? false
  const [params, setParams] = useSearchParams(); const queryString = params.toString()
  const [result, setResult] = useState<PagedResponse<CaseItem> | null>(null); const [categories, setCategories] = useState<Category[]>([]); const [officers, setOfficers] = useState<Officer[]>([])
  const [loading, setLoading] = useState(true); const [error, setError] = useState('')
  const page = Number(params.get('page') ?? '1'); const pageSize = Number(params.get('pageSize') ?? '20')

  useEffect(() => {
    if (officer && !params.has('mine')) { const next = new URLSearchParams(params); next.set('mine', 'true'); setParams(next, { replace: true }) }
  }, [officer, params, setParams])
  useEffect(() => { api<Category[]>(resident ? '/categories' : '/categories?includeInactive=true').then(setCategories) }, [resident])
  useEffect(() => { if (canAssign) api<Officer[]>('/officers').then(setOfficers) }, [canAssign])
  useEffect(() => {
    const query = new URLSearchParams(queryString); if (!query.has('page')) query.set('page', '1'); if (!query.has('pageSize')) query.set('pageSize', '20')
    api<PagedResponse<CaseItem>>(`/cases?${query}`).then(setResult).catch(e => setError(e instanceof Error ? e.message : 'Unable to load cases')).finally(() => setLoading(false))
  }, [queryString])

  const update = (key: string, value: string) => { const next = new URLSearchParams(params); if (value) next.set(key, value); else next.delete(key); if (key !== 'page') next.set('page', '1'); setParams(next) }
  const sort = (key: string) => { const next = new URLSearchParams(params); const current = params.get('sortBy'); const direction = current === key && params.get('sortDirection') === 'asc' ? 'desc' : 'asc'; next.set('sortBy', key); next.set('sortDirection', direction); next.set('page', '1'); setParams(next) }
  const clear = () => { const next = new URLSearchParams(); if (officer) next.set('mine', 'true'); setParams(next) }
  const heading = resident ? 'My requests' : officer ? 'My cases' : 'Case queue'
  const quickViews = useMemo(() => officer ? [['Today', 'today'], ['Overdue', 'overdue'], ['Waiting for resident', 'waiting'], ['Recently updated', 'recent']] : [], [officer])
  const sortDirection = params.get('sortDirection') === 'asc' ? 'asc' : 'desc'

  return <Stack spacing={3}>
    <PageHeader title={heading} description={resident ? 'Track every update from submission to closure.' : 'Filter, prioritise and manage operational work.'} actions={resident && <Button component={Link} to="/requests/new" variant="contained" startIcon={<AddRoundedIcon />}>New request</Button>} />
    {!resident && quickViews.length > 0 && <Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>{quickViews.map(([label, value]) => <Button key={value} variant={params.get('quickView') === value ? 'contained' : 'outlined'} onClick={() => update('quickView', params.get('quickView') === value ? '' : value)}>{label}</Button>)}</Stack>}
    <Paper sx={{ p: 2 }}><Stack direction={{ xs: 'column', md: 'row' }} spacing={2} useFlexGap sx={{ flexWrap: 'wrap' }}>
      <TextField size="small" label="Search reference, title or address" value={params.get('search') ?? ''} onChange={e => update('search', e.target.value)} sx={{ minWidth: 280, flexGrow: 1 }} />
      {!resident && <TextField select size="small" label="Priority" value={params.get('priority') ?? ''} onChange={e => update('priority', e.target.value)} sx={{ minWidth: 130 }}><MenuItem value="">All</MenuItem>{priorities.map(x => <MenuItem key={x} value={x}>{x}</MenuItem>)}</TextField>}
      <TextField select size="small" label="Status" value={params.get('status') ?? ''} onChange={e => update('status', e.target.value)} sx={{ minWidth: 170 }}><MenuItem value="">All</MenuItem>{statuses.map(x => <MenuItem key={x} value={x}>{format(x)}</MenuItem>)}</TextField>
      {!resident && <TextField select size="small" label="Category" value={params.get('categoryId') ?? ''} onChange={e => update('categoryId', e.target.value)} sx={{ minWidth: 170 }}><MenuItem value="">All</MenuItem>{categories.map(x => <MenuItem key={x.id} value={x.id}>{x.name}</MenuItem>)}</TextField>}
      {canAssign && <TextField select size="small" label="Officer" value={params.get('officerId') ?? ''} onChange={e => update('officerId', e.target.value)} sx={{ minWidth: 170 }}><MenuItem value="">All</MenuItem>{officers.map(x => <MenuItem key={x.id} value={x.id}>{x.firstName} {x.lastName}</MenuItem>)}</TextField>}
      {!resident && <TextField select size="small" label="SLA state" value={params.get('slaState') ?? ''} onChange={e => update('slaState', e.target.value)} sx={{ minWidth: 140 }}><MenuItem value="">All</MenuItem>{slaStates.map(x => <MenuItem key={x} value={x}>{format(x)}</MenuItem>)}</TextField>}
      {!resident && <TextField size="small" type="date" label="Due from" value={params.get('dueFrom')?.slice(0, 10) ?? ''} onChange={e => update('dueFrom', e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />}
      {!resident && <TextField size="small" type="date" label="Due to" value={params.get('dueTo')?.slice(0, 10) ?? ''} onChange={e => update('dueTo', e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />}
      {(params.get('search') || params.get('status') || (!resident && [...params.keys()].some(key => !['page', 'pageSize', 'mine'].includes(key)))) && <Button onClick={clear}>Clear filters</Button>}
    </Stack></Paper>
    {resident && <ActiveFilterSummary search={params.get('search') ?? undefined} status={params.get('status') ?? undefined} onClear={clear} />}
    {error && <ErrorState title="Unable to load requests" message={error} retry={() => window.location.reload()} />}
    {loading ? <Paper><TableSkeleton columns={resident ? 6 : 9} label={resident ? 'Loading your requests' : 'Loading cases'} /></Paper> : <><ResponsiveDataView desktop={<Paper sx={{ overflowX: 'auto' }} aria-busy="false"><Table size={resident ? 'medium' : 'small'}><TableHead><TableRow>
      <Sortable label="Reference" name="reference" active={params.get('sortBy')} direction={sortDirection} onSort={sort} />
      <Sortable label="Title" name="title" active={params.get('sortBy')} direction={sortDirection} onSort={sort} />
      <Sortable label="Category" name="category" active={params.get('sortBy')} direction={sortDirection} onSort={sort} />
      {!resident && <Sortable label="Priority" name="priority" active={params.get('sortBy')} direction={sortDirection} onSort={sort} />}
      <Sortable label="Status" name="status" active={params.get('sortBy')} direction={sortDirection} onSort={sort} />
      {!resident && <Sortable label="Assigned officer" name="officer" active={params.get('sortBy')} direction={sortDirection} onSort={sort} />}
      <Sortable label="Submitted" name="submitted" active={params.get('sortBy')} direction={sortDirection} onSort={sort} />
      {!resident && <Sortable label="SLA due" name="due" active={params.get('sortBy')} direction={sortDirection} onSort={sort} />}
      <TableCell>{resident ? 'Service target' : 'SLA state'}</TableCell>
    </TableRow></TableHead><TableBody>{result?.items.map(item => <TableRow hover key={item.id} sx={{ '&:focus-within': { outline: '2px solid', outlineColor: 'primary.main', outlineOffset: -2 } }}>
      <TableCell><Button component={Link} to={`/cases/${item.id}`} variant="text" size="small" sx={{ minHeight: 36, p: 1, fontWeight: 850, whiteSpace: 'nowrap' }}>{item.referenceNumber}</Button></TableCell><TableCell sx={{ minWidth: 190 }}>{item.title}</TableCell><TableCell>{item.categoryName}</TableCell>{!resident && <TableCell><PriorityChip priority={item.priority} /></TableCell>}<TableCell><StatusChip status={item.status} /></TableCell>{!resident && <TableCell>{item.assignedOfficerName ?? 'Unassigned'}</TableCell>}<TableCell>{formatDate(item.submittedAtUtc)}</TableCell>{!resident && <TableCell>{item.resolutionDueAtUtc ? formatDate(item.resolutionDueAtUtc) : 'Not set'}</TableCell>}<TableCell><SlaStatus state={item.slaState} label={resident ? residentTarget(item.status, item.slaState) : undefined} /></TableCell>
    </TableRow>)}{result?.items.length === 0 && <TableRow><TableCell colSpan={resident ? 6 : 9}><EmptyState title={resident ? 'No requests found' : 'No matching cases'} description={resident ? 'Submit a service request or change the current filters.' : 'Try changing or clearing the current filters.'} action={resident && <Button component={Link} to="/requests/new" variant="contained">Submit request</Button>} /></TableCell></TableRow>}</TableBody></Table></Paper>} mobile={<Stack spacing={4}>{result?.items.map(item => <ResidentRequestCard key={item.id} item={item} />)}{result?.items.length === 0 && <Paper><EmptyState title="No requests found" description="Submit a service request or change the current filters." action={<Button component={Link} to="/requests/new" variant="contained">Submit request</Button>} /></Paper>}</Stack>} />
      <Paper sx={{ mt: 3 }}>
      <TablePagination component="div" count={result?.totalCount ?? 0} page={Math.max(0, page - 1)} rowsPerPage={pageSize} rowsPerPageOptions={[10, 20, 50]} onPageChange={(_, next) => update('page', String(next + 1))} onRowsPerPageChange={e => update('pageSize', e.target.value)} />
      </Paper></>}
  </Stack>
}

function Sortable({ label, name, active, direction, onSort }: { label: string; name: string; active: string | null; direction: 'asc' | 'desc'; onSort: (name: string) => void }) {
  return <TableCell sortDirection={active === name ? direction : false}><TableSortLabel active={active === name} direction={active === name ? direction : 'asc'} onClick={() => onSort(name)}>{label}</TableSortLabel></TableCell>
}
const format = (value: string) => ({ WaitingForResident: 'Waiting for resident', InProgress: 'In progress', OnTrack: 'On track', AtRisk: 'At risk', NoSla: 'No SLA' }[value] ?? value)
const formatDate = formatDateTime
const residentTarget = (status: string, state: string) => ['Resolved', 'Closed'].includes(status) ? 'Complete' : state === 'Overdue' ? 'Overdue' : state === 'AtRisk' ? 'Due soon' : 'On track'
