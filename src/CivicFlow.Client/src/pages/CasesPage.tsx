import { useEffect, useState } from 'react'
import { Alert, Box, Button, Chip, CircularProgress, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material'
import AddRoundedIcon from '@mui/icons-material/AddRounded'
import { Link } from 'react-router-dom'
import { api, type CaseItem } from '../api/client'
import { useAuth } from '../auth/AuthContext'

const colours: Record<string, 'default' | 'info' | 'warning' | 'success' | 'error'> = { Submitted: 'info', Triaged: 'info', Assigned: 'warning', InProgress: 'warning', WaitingForResident: 'error', Resolved: 'success', Closed: 'default', Rejected: 'error' }

export function CasesPage() {
  const { user } = useAuth(); const resident = user?.roles.includes('Resident')
  const [items, setItems] = useState<CaseItem[]>([]); const [search, setSearch] = useState(''); const [loading, setLoading] = useState(true); const [error, setError] = useState('')
  useEffect(() => { api<CaseItem[]>('/cases').then(setItems).catch(e => setError(e instanceof Error ? e.message : 'Unable to load cases')).finally(() => setLoading(false)) }, [])
  const visible = items.filter(x => `${x.referenceNumber} ${x.title}`.toLowerCase().includes(search.toLowerCase()))
  return <Stack spacing={3}><Stack direction={{ xs: 'column', sm: 'row' }} sx={{ justifyContent: 'space-between', gap: 2 }}><Box><Typography variant="h3">{resident ? 'My requests' : 'Case queue'}</Typography><Typography color="text.secondary">{resident ? 'Track every update from submission to closure.' : 'Prioritise, assign and resolve community requests.'}</Typography></Box>{resident && <Button component={Link} to="/requests/new" variant="contained" startIcon={<AddRoundedIcon />}>New request</Button>}</Stack>
    <TextField label="Search by reference or title" value={search} onChange={e => setSearch(e.target.value)} sx={{ maxWidth: 440 }} />{error && <Alert severity="error">{error}</Alert>}
    {loading ? <CircularProgress /> : <Paper sx={{ overflowX: 'auto' }}><Table><TableHead><TableRow><TableCell>Reference</TableCell><TableCell>Request</TableCell><TableCell>Status</TableCell><TableCell>Priority</TableCell><TableCell>Submitted</TableCell></TableRow></TableHead><TableBody>{visible.map(item => <TableRow hover key={item.id} component={Link} to={`/cases/${item.id}`} sx={{ textDecoration: 'none', cursor: 'pointer' }}><TableCell><strong>{item.referenceNumber}</strong></TableCell><TableCell>{item.title}</TableCell><TableCell><Chip size="small" color={colours[item.status] ?? 'default'} label={item.status.replace(/([A-Z])/g, ' $1').trim()} /></TableCell><TableCell>{item.priority}</TableCell><TableCell>{new Date(item.submittedAtUtc).toLocaleDateString()}</TableCell></TableRow>)}{visible.length === 0 && <TableRow><TableCell colSpan={5}>No requests found.</TableCell></TableRow>}</TableBody></Table></Paper>}
  </Stack>
}
