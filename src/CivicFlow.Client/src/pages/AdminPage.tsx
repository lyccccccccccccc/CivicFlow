import { useEffect, useState, type FormEvent } from 'react'
import { Alert, Button, Grid, MenuItem, Paper, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material'
import { api } from '../api/client'

type AdminUser = { id: string; firstName: string; lastName: string; email: string; isActive: boolean; createdAtUtc: string; roles: string[] }
const roles = ['Resident', 'CaseOfficer', 'TeamManager', 'SystemAdministrator']

export function AdminPage() {
  const [users, setUsers] = useState<AdminUser[]>([]); const [message, setMessage] = useState('')
  const [form, setForm] = useState({ name: '', description: '', firstResponseHours: 8, resolutionHours: 72 })
  const loadUsers = () => api<AdminUser[]>('/admin/users').then(setUsers)
  useEffect(() => { void loadUsers() }, [])
  const changeRole = async (id: string, role: string) => { await api(`/admin/users/${id}/role`, { method: 'PUT', body: JSON.stringify({ role }) }); await loadUsers() }
  const toggleActive = async (user: AdminUser) => { await api(`/admin/users/${user.id}/active`, { method: 'PUT', body: JSON.stringify({ isActive: !user.isActive }) }); await loadUsers() }
  const submit = async (event: FormEvent) => { event.preventDefault(); await api('/admin/categories', { method: 'POST', body: JSON.stringify(form) }); setMessage('Service category created.'); setForm({ name: '', description: '', firstResponseHours: 8, resolutionHours: 72 }) }
  return <Stack spacing={4}><div><Typography variant="h3">System administration</Typography><Typography color="text.secondary">Manage reference data and review user access.</Typography></div>{message && <Alert severity="success">{message}</Alert>}
    <Grid container spacing={3}><Grid size={{ xs: 12, md: 5 }}><Paper sx={{ p: 3 }}><Typography variant="h5" sx={{ fontWeight: 800, mb: 2 }}>New service category</Typography><Stack component="form" spacing={2} onSubmit={submit}><TextField label="Name" required value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} /><TextField label="Description" required multiline value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} /><TextField label="First response hours" type="number" value={form.firstResponseHours} onChange={e => setForm({ ...form, firstResponseHours: Number(e.target.value) })} /><TextField label="Resolution hours" type="number" value={form.resolutionHours} onChange={e => setForm({ ...form, resolutionHours: Number(e.target.value) })} /><Button type="submit" variant="contained">Create category</Button></Stack></Paper></Grid>
      <Grid size={{ xs: 12, md: 7 }}><Paper sx={{ overflowX: 'auto' }}><Table><TableHead><TableRow><TableCell>User</TableCell><TableCell>Role</TableCell><TableCell>Status</TableCell></TableRow></TableHead><TableBody>{users.map(user => <TableRow key={user.id}><TableCell>{user.firstName} {user.lastName}<Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>{user.email}</Typography></TableCell><TableCell><TextField select size="small" value={user.roles[0] ?? 'Resident'} onChange={e => changeRole(user.id, e.target.value)}>{roles.map(role => <MenuItem value={role} key={role}>{role}</MenuItem>)}</TextField></TableCell><TableCell><Button color={user.isActive ? 'success' : 'error'} onClick={() => toggleActive(user)}>{user.isActive ? 'Active' : 'Disabled'}</Button></TableCell></TableRow>)}</TableBody></Table></Paper></Grid></Grid>
  </Stack>
}
