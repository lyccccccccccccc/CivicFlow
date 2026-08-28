import { useCallback, useEffect, useState } from 'react'
import { Alert, Box, Button, Chip, Divider, Grid, MenuItem, Paper, Stack, TextField, Typography } from '@mui/material'
import { useParams } from 'react-router-dom'
import { api, type CaseDetail } from '../api/client'
import { useAuth } from '../auth/AuthContext'

const nextStatuses = ['InProgress', 'WaitingForResident', 'Resolved', 'Closed', 'Reopened', 'Rejected']

export function CaseDetailPage() {
  const { id } = useParams(); const { user } = useAuth(); const resident = user?.roles.includes('Resident')
  const canAssign = user?.roles.some(x => x === 'TeamManager' || x === 'SystemAdministrator') ?? false
  const [detail, setDetail] = useState<CaseDetail | null>(null); const [message, setMessage] = useState(''); const [internal, setInternal] = useState(false); const [status, setStatus] = useState('InProgress'); const [error, setError] = useState('')
  const [officers, setOfficers] = useState<{ id: string; firstName: string; lastName: string }[]>([]); const [officerId, setOfficerId] = useState('')
  const load = useCallback(() => api<CaseDetail>(`/cases/${id}`).then(setDetail).catch(e => setError(e instanceof Error ? e.message : 'Unable to load request')), [id])
  useEffect(() => { void load() }, [load])
  useEffect(() => { if (canAssign) void api<{ id: string; firstName: string; lastName: string }[]>('/officers').then(setOfficers) }, [canAssign])
  const comment = async () => { if (!message.trim()) return; await api(`/cases/${id}/comments`, { method: 'POST', body: JSON.stringify({ message, internal }) }); setMessage(''); await load() }
  const changeStatus = async () => { await api(`/cases/${id}/status`, { method: 'POST', body: JSON.stringify({ status, note: '' }) }); await load() }
  const triage = async () => { await api(`/cases/${id}/triage`, { method: 'POST', body: JSON.stringify({ priority: 1 }) }); await load() }
  const assign = async () => { if (!officerId) return; await api(`/cases/${id}/assign`, { method: 'POST', body: JSON.stringify({ officerId }) }); await load() }
  if (error) return <Alert severity="error">{error}</Alert>; if (!detail) return <Typography>Loading request…</Typography>
  const item = detail.case
  return <Stack spacing={3}><Box><Typography variant="overline" color="primary">{item.referenceNumber}</Typography><Typography variant="h3">{item.title}</Typography><Stack direction="row" spacing={1} sx={{ mt: 1 }}><Chip label={item.status} color="primary" /><Chip label={`${item.priority} priority`} variant="outlined" /></Stack></Box>
    <Grid container spacing={3}><Grid size={{ xs: 12, md: 8 }}><Stack spacing={3}><Paper sx={{ p: 3 }}><Typography variant="h6" sx={{ fontWeight: 800 }}>Request details</Typography><Divider sx={{ my: 2 }} /><Typography sx={{ whiteSpace: 'pre-wrap' }}>{item.description}</Typography><Typography color="text.secondary" sx={{ mt: 2 }}><strong>Location:</strong> {item.address}</Typography><Typography color="text.secondary"><strong>Category:</strong> {detail.category.name}</Typography></Paper>
      <Paper sx={{ p: 3 }}><Typography variant="h6" sx={{ fontWeight: 800 }}>Activity history</Typography><Stack spacing={2} sx={{ mt: 2 }}>{detail.activities.map(a => <Box key={a.id} sx={{ pl: 2, borderLeft: '3px solid', borderColor: a.isPublic ? 'primary.main' : 'warning.main' }}><Stack direction="row" sx={{ justifyContent: 'space-between' }}><Typography sx={{ fontWeight: 700 }}>{a.type}{!a.isPublic && ' · Internal'}</Typography><Typography variant="caption" color="text.secondary">{new Date(a.createdAtUtc).toLocaleString()}</Typography></Stack><Typography color="text.secondary">{a.message}</Typography></Box>)}</Stack></Paper>
      <Paper sx={{ p: 3 }}><Typography variant="h6" sx={{ fontWeight: 800 }}>{resident ? 'Send an update' : 'Add a case note'}</Typography><TextField fullWidth multiline minRows={3} value={message} onChange={e => setMessage(e.target.value)} sx={{ my: 2 }} label="Message" />{!resident && <Button variant={internal ? 'contained' : 'outlined'} color="warning" onClick={() => setInternal(!internal)} sx={{ mr: 2 }}>{internal ? 'Internal note selected' : 'Make internal'}</Button>}<Button variant="contained" onClick={comment}>Add update</Button></Paper></Stack></Grid>
      <Grid size={{ xs: 12, md: 4 }}><Stack spacing={3}><Paper sx={{ p: 3 }}><Typography variant="h6" sx={{ fontWeight: 800 }}>Service target</Typography><Typography color="text.secondary" sx={{ mt: 1 }}>Resolution due</Typography><Typography variant="h5">{item.resolutionDueAtUtc ? new Date(item.resolutionDueAtUtc).toLocaleString() : 'Set during triage'}</Typography></Paper>
      {!resident && <Paper sx={{ p: 3 }}><Typography variant="h6" sx={{ fontWeight: 800 }}>Case actions</Typography>{item.status === 'Submitted' && <Button fullWidth variant="contained" sx={{ my: 2 }} onClick={triage}>Triage as high priority</Button>}{canAssign && <><TextField select fullWidth label="Assign officer" value={officerId} onChange={e => setOfficerId(e.target.value)} sx={{ mt: 2 }}>{officers.map(x => <MenuItem key={x.id} value={x.id}>{x.firstName} {x.lastName}</MenuItem>)}</TextField><Button fullWidth variant="outlined" onClick={assign} disabled={!officerId} sx={{ mt: 1 }}>Assign case</Button></>}<TextField select fullWidth label="Next status" value={status} onChange={e => setStatus(e.target.value)} sx={{ my: 2 }}>{nextStatuses.map(x => <MenuItem key={x} value={x}>{x}</MenuItem>)}</TextField><Button fullWidth variant="outlined" onClick={changeStatus}>Update status</Button></Paper>}
      {resident && item.status === 'Resolved' && <Button variant="outlined" color="warning" onClick={async () => { await api(`/cases/${id}/status`, { method: 'POST', body: JSON.stringify({ status: 'Reopened', note: '' }) }); await load() }}>Reopen request</Button>}</Stack></Grid></Grid>
  </Stack>
}
