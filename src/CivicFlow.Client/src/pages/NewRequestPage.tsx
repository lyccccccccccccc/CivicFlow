import { lazy, Suspense, useEffect, useState, type FormEvent } from 'react'
import { Alert, Button, MenuItem, Paper, Stack, TextField, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { api, uploadAttachment, type Category } from '../api/client'
import type { MapPoint } from '../components/MapPicker'
import { FormActions, PageLoading } from '../components/ui'

const MapPicker = lazy(() => import('../components/MapPicker').then(module => ({ default: module.MapPicker })))

export function NewRequestPage() {
  const navigate = useNavigate(); const [categories, setCategories] = useState<Category[]>([])
  const [form, setForm] = useState({ categoryId: '', title: '', description: '', address: '' }); const [error, setError] = useState(''); const [errors, setErrors] = useState<Record<string, string>>({}); const [busy, setBusy] = useState(false)
  const [location, setLocation] = useState<MapPoint>()
  const [files, setFiles] = useState<File[]>([]); const [createdCaseId, setCreatedCaseId] = useState('')
  useEffect(() => { api<Category[]>('/categories').then(setCategories).catch(() => setError('Unable to load service categories.')) }, [])
  const uploadFiles = async (caseId: string) => { const failed: File[] = []; for (const file of files) { try { await uploadAttachment(caseId, file, 'Public', crypto.randomUUID()) } catch { failed.push(file) } } setFiles(failed); if (failed.length) { setCreatedCaseId(caseId); throw new Error(`Request created, but ${failed.length} attachment${failed.length === 1 ? '' : 's'} failed. Retry below or open the request.`) } navigate(`/cases/${caseId}`) }
  const submit = async (event: FormEvent) => { event.preventDefault(); const validation = validate(form); setErrors(validation); if (Object.keys(validation).length) return; setBusy(true); setError(''); try { const created = await api<{ id: string }>('/cases', { method: 'POST', body: JSON.stringify({ ...form, title: form.title.trim(), description: form.description.trim(), address: form.address.trim(), ...location }) }); await uploadFiles(created.id) } catch (e) { setError(e instanceof Error ? e.message : 'Unable to submit request') } finally { setBusy(false) } }
  const chooseFiles = (selected: FileList | null) => { const next = Array.from(selected ?? []).slice(0, 5); const invalid = next.find(file => file.size > 10 * 1024 * 1024); if (invalid) { setError(`${invalid.name} exceeds the 10 MB limit.`); return } setFiles(next); setError('') }
  const retry = async () => { if (!createdCaseId) return; setBusy(true); setError(''); try { await uploadFiles(createdCaseId) } catch (e) { setError(e instanceof Error ? e.message : 'Attachment retry failed.') } finally { setBusy(false) } }
  return <Paper sx={{ maxWidth: 760, mx: 'auto', p: { xs: 3, md: 5 } }}><Typography variant="h3">Submit a service request</Typography><Typography color="text.secondary" sx={{ mb: 4 }}>Provide clear details so the right team can respond quickly.</Typography>
    <Stack component="form" spacing={3} onSubmit={submit} noValidate>{error && <Alert severity="error">{error}</Alert>}
      <TextField select label="Service category" value={form.categoryId} onChange={e => setForm({ ...form, categoryId: e.target.value })} error={Boolean(errors.categoryId)} helperText={errors.categoryId} required>{categories.map(c => <MenuItem key={c.id} value={c.id}><Stack><span>{c.name}</span><Typography variant="caption" color="text.secondary">{c.description}</Typography></Stack></MenuItem>)}</TextField>
      <TextField label="Short title" value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} slotProps={{ htmlInput: { maxLength: 150 } }} error={Boolean(errors.title)} helperText={errors.title ?? '5–150 characters'} required />
      <TextField label="What happened?" value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} multiline minRows={5} slotProps={{ htmlInput: { maxLength: 2000 } }} error={Boolean(errors.description)} helperText={errors.description ?? '20–2000 characters. Include what you observed and any safety risk.'} required />
      <TextField label="Location or street address" value={form.address} onChange={e => setForm({ ...form, address: e.target.value })} slotProps={{ htmlInput: { maxLength: 300 } }} error={Boolean(errors.address)} helperText={errors.address ?? '5–300 characters'} required />
      <Suspense fallback={<PageLoading label="Loading map" />}><MapPicker value={location} onChange={setLocation} /></Suspense>
      <Stack spacing={1}><Typography sx={{ fontWeight: 700 }}>Photos or PDF (optional)</Typography><Button component="label" variant="outlined">Choose up to 5 files<input hidden multiple type="file" accept=".jpg,.jpeg,.png,.pdf,image/jpeg,image/png,application/pdf" onChange={e => chooseFiles(e.target.files)} /></Button>{files.map(file => <Typography key={`${file.name}-${file.lastModified}`} variant="body2">{file.name} · {(file.size / 1024 / 1024).toFixed(1)} MB</Typography>)}</Stack>
      <Alert severity="info">The request is created first, then attachments upload individually. A failed file can be retried without losing the request.</Alert>
      {createdCaseId ? <FormActions secondaryLabel="Open request" onSecondary={() => navigate(`/cases/${createdCaseId}`)} primaryLabel={busy ? 'Retrying…' : 'Retry attachments'} primaryProps={{ disabled: busy || files.length === 0, onClick: () => void retry() }} /> : <FormActions onSecondary={() => navigate(-1)} primaryLabel={busy ? 'Submitting…' : 'Submit request'} primaryProps={{ type: 'submit', size: 'large', disabled: busy }} />}
    </Stack></Paper>
}

function validate(form: { categoryId: string; title: string; description: string; address: string }) {
  const errors: Record<string, string> = {}
  if (!form.categoryId) errors.categoryId = 'Select a service category.'
  check(errors, 'title', 'Title', form.title, 5, 150); check(errors, 'description', 'Description', form.description, 20, 2000); check(errors, 'address', 'Location', form.address, 5, 300)
  return errors
}
function check(errors: Record<string, string>, field: string, label: string, value: string, min: number, max: number) { const length = value.trim().length; if (length < min || length > max) errors[field] = `${label} must be ${min}–${max} characters after trimming.` }
