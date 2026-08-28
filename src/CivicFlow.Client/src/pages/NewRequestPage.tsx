import { useEffect, useState, type FormEvent } from 'react'
import { Alert, Button, MenuItem, Paper, Stack, TextField, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { api, type Category } from '../api/client'

export function NewRequestPage() {
  const navigate = useNavigate(); const [categories, setCategories] = useState<Category[]>([])
  const [form, setForm] = useState({ categoryId: '', title: '', description: '', address: '' }); const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  useEffect(() => { api<Category[]>('/categories').then(setCategories).catch(() => setError('Unable to load service categories.')) }, [])
  const submit = async (event: FormEvent) => { event.preventDefault(); setBusy(true); setError(''); try { const created = await api<{ id: string }>('/cases', { method: 'POST', body: JSON.stringify(form) }); navigate(`/cases/${created.id}`) } catch (e) { setError(e instanceof Error ? e.message : 'Unable to submit request') } finally { setBusy(false) } }
  return <Paper sx={{ maxWidth: 760, mx: 'auto', p: { xs: 3, md: 5 } }}><Typography variant="h3">Submit a service request</Typography><Typography color="text.secondary" sx={{ mb: 4 }}>Provide clear details so the right team can respond quickly.</Typography>
    <Stack component="form" spacing={3} onSubmit={submit}>{error && <Alert severity="error">{error}</Alert>}
      <TextField select label="Service category" value={form.categoryId} onChange={e => setForm({ ...form, categoryId: e.target.value })} required>{categories.map(c => <MenuItem key={c.id} value={c.id}><Stack><span>{c.name}</span><Typography variant="caption" color="text.secondary">{c.description}</Typography></Stack></MenuItem>)}</TextField>
      <TextField label="Short title" value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} slotProps={{ htmlInput: { maxLength: 160 } }} required />
      <TextField label="What happened?" value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} multiline minRows={5} helperText="Include what you observed, when it occurred and any safety risk." required />
      <TextField label="Location or street address" value={form.address} onChange={e => setForm({ ...form, address: e.target.value })} required />
      <Alert severity="info">Do not include sensitive personal information. Attachments are planned for the next release.</Alert>
      <Stack direction="row" spacing={2} sx={{ justifyContent: 'flex-end' }}><Button onClick={() => navigate(-1)}>Cancel</Button><Button type="submit" variant="contained" size="large" disabled={busy}>{busy ? 'Submitting…' : 'Submit request'}</Button></Stack>
    </Stack></Paper>
}
