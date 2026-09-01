import { useEffect, useState, type FormEvent } from 'react'
import { Alert, Button, MenuItem, Paper, Stack, TextField, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { api, type Category } from '../api/client'
import { MapPicker, type MapPoint } from '../components/MapPicker'

export function NewRequestPage() {
  const navigate = useNavigate(); const [categories, setCategories] = useState<Category[]>([])
  const [form, setForm] = useState({ categoryId: '', title: '', description: '', address: '' }); const [error, setError] = useState(''); const [errors, setErrors] = useState<Record<string, string>>({}); const [busy, setBusy] = useState(false)
  const [location, setLocation] = useState<MapPoint>()
  useEffect(() => { api<Category[]>('/categories').then(setCategories).catch(() => setError('Unable to load service categories.')) }, [])
  const submit = async (event: FormEvent) => { event.preventDefault(); const validation = validate(form); setErrors(validation); if (Object.keys(validation).length) return; setBusy(true); setError(''); try { const created = await api<{ id: string }>('/cases', { method: 'POST', body: JSON.stringify({ ...form, title: form.title.trim(), description: form.description.trim(), address: form.address.trim(), ...location }) }); navigate(`/cases/${created.id}`) } catch (e) { setError(e instanceof Error ? e.message : 'Unable to submit request') } finally { setBusy(false) } }
  return <Paper sx={{ maxWidth: 760, mx: 'auto', p: { xs: 3, md: 5 } }}><Typography variant="h3">Submit a service request</Typography><Typography color="text.secondary" sx={{ mb: 4 }}>Provide clear details so the right team can respond quickly.</Typography>
    <Stack component="form" spacing={3} onSubmit={submit} noValidate>{error && <Alert severity="error">{error}</Alert>}
      <TextField select label="Service category" value={form.categoryId} onChange={e => setForm({ ...form, categoryId: e.target.value })} error={Boolean(errors.categoryId)} helperText={errors.categoryId} required>{categories.map(c => <MenuItem key={c.id} value={c.id}><Stack><span>{c.name}</span><Typography variant="caption" color="text.secondary">{c.description}</Typography></Stack></MenuItem>)}</TextField>
      <TextField label="Short title" value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} slotProps={{ htmlInput: { maxLength: 150 } }} error={Boolean(errors.title)} helperText={errors.title ?? '5–150 characters'} required />
      <TextField label="What happened?" value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} multiline minRows={5} slotProps={{ htmlInput: { maxLength: 2000 } }} error={Boolean(errors.description)} helperText={errors.description ?? '20–2000 characters. Include what you observed and any safety risk.'} required />
      <TextField label="Location or street address" value={form.address} onChange={e => setForm({ ...form, address: e.target.value })} slotProps={{ htmlInput: { maxLength: 300 } }} error={Boolean(errors.address)} helperText={errors.address ?? '5–300 characters'} required />
      <MapPicker value={location} onChange={setLocation} />
      <Alert severity="info">Do not include sensitive personal information. You can add photos or a PDF after the request is created.</Alert>
      <Stack direction="row" spacing={2} sx={{ justifyContent: 'flex-end' }}><Button onClick={() => navigate(-1)}>Cancel</Button><Button type="submit" variant="contained" size="large" disabled={busy}>{busy ? 'Submitting…' : 'Submit request'}</Button></Stack>
    </Stack></Paper>
}

function validate(form: { categoryId: string; title: string; description: string; address: string }) {
  const errors: Record<string, string> = {}
  if (!form.categoryId) errors.categoryId = 'Select a service category.'
  check(errors, 'title', 'Title', form.title, 5, 150); check(errors, 'description', 'Description', form.description, 20, 2000); check(errors, 'address', 'Location', form.address, 5, 300)
  return errors
}
function check(errors: Record<string, string>, field: string, label: string, value: string, min: number, max: number) { const length = value.trim().length; if (length < min || length > max) errors[field] = `${label} must be ${min}–${max} characters after trimming.` }
