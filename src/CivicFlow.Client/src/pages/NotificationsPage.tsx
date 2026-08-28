import { useCallback, useEffect, useState } from 'react'
import { Button, Paper, Stack, Typography } from '@mui/material'
import { Link } from 'react-router-dom'
import { api } from '../api/client'

type Notification = { id: string; serviceRequestId?: string; title: string; message: string; readAtUtc?: string; createdAtUtc: string }

export function NotificationsPage() {
  const [items, setItems] = useState<Notification[]>([])
  const load = useCallback(() => api<Notification[]>('/notifications').then(setItems), [])
  useEffect(() => { void load() }, [load])
  const read = async (id: string) => { await api(`/notifications/${id}/read`, { method: 'POST' }); await load() }
  return <Stack spacing={3}><div><Typography variant="h3">Notifications</Typography><Typography color="text.secondary">Case assignments and resident-visible workflow updates.</Typography></div>{items.map(item => <Paper key={item.id} sx={{ p: 3, bgcolor: item.readAtUtc ? 'background.paper' : '#eff6ff' }}><Stack direction={{ xs: 'column', sm: 'row' }} sx={{ justifyContent: 'space-between', gap: 2 }}><div><Typography sx={{ fontWeight: 800 }}>{item.title}</Typography><Typography color="text.secondary">{item.message}</Typography><Typography variant="caption" color="text.secondary">{new Date(item.createdAtUtc).toLocaleString()}</Typography></div><Stack direction="row" spacing={1}>{item.serviceRequestId && <Button component={Link} to={`/cases/${item.serviceRequestId}`}>Open</Button>}{!item.readAtUtc && <Button onClick={() => read(item.id)}>Mark read</Button>}</Stack></Stack></Paper>)}{items.length === 0 && <Paper sx={{ p: 4 }}><Typography>No notifications yet.</Typography></Paper>}</Stack>
}
