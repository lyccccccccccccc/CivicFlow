import { lazy, Suspense, useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
import { Alert, Button, CircularProgress, MenuItem, Stack, TextField, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { api, uploadAttachment, type Category } from '../api/client'
import type { MapPoint } from '../components/MapPicker'
import { CharacterCounter, FormSection, InlineNotice, SelectedFileItem } from '../components/resident'
import { FormActions, PageHeader, PageLoading } from '../components/ui'

const MapPicker = lazy(() => import('../components/MapPicker').then(module => ({ default: module.MapPicker })))
type CreatedCase = { id: string; referenceNumber: string }

export function NewRequestPage() {
  const navigate = useNavigate(); const submitting = useRef(false); const [categories, setCategories] = useState<Category[]>([]); const [categoriesLoading, setCategoriesLoading] = useState(true); const [categoryError, setCategoryError] = useState('')
  const [form, setForm] = useState({ categoryId: '', title: '', description: '', address: '' }); const [error, setError] = useState(''); const [errors, setErrors] = useState<Record<string, string>>({}); const [busy, setBusy] = useState(false)
  const [location, setLocation] = useState<MapPoint>(); const [files, setFiles] = useState<File[]>([]); const [created, setCreated] = useState<CreatedCase>()
  const loadCategories = useCallback(() => { setCategoriesLoading(true); setCategoryError(''); return api<Category[]>('/categories').then(setCategories).catch(() => setCategoryError('Service categories could not be loaded.')).finally(() => setCategoriesLoading(false)) }, [])
  useEffect(() => { void api<Category[]>('/categories').then(setCategories).catch(() => setCategoryError('Service categories could not be loaded.')).finally(() => setCategoriesLoading(false)) }, [])

  const uploadFiles = async (createdCase: CreatedCase) => { const failed: File[] = []; for (const file of files) { try { await uploadAttachment(createdCase.id, file, 'Public', crypto.randomUUID()) } catch { failed.push(file) } } setFiles(failed); if (failed.length) { setCreated(createdCase); throw new Error(`${failed.length} attachment${failed.length === 1 ? '' : 's'} could not be uploaded. Your request is safe and the failed files can be retried.`) } navigate(`/cases/${createdCase.id}`, { state: { notice: `Request ${createdCase.referenceNumber} submitted successfully.` } }) }
  const submit = async (event: FormEvent) => { event.preventDefault(); if (submitting.current || created) return; const validation = validate(form); setErrors(validation); if (Object.keys(validation).length) return; submitting.current = true; setBusy(true); setError(''); try { const response = await api<CreatedCase>('/cases', { method: 'POST', body: JSON.stringify({ ...form, title: form.title.trim(), description: form.description.trim(), address: form.address.trim(), ...location }) }); await uploadFiles(response) } catch (e) { setError(e instanceof Error ? e.message : 'Unable to submit request') } finally { submitting.current = false; setBusy(false) } }
  const chooseFiles = (selected: FileList | null) => { const incoming = Array.from(selected ?? []); const combined = [...files, ...incoming].slice(0, 5); const invalid = combined.find(file => file.size > 10 * 1024 * 1024); if (invalid) { setError(`${invalid.name} exceeds the 10 MB limit.`); return } setFiles(combined); setError('') }
  const retry = async () => { if (!created || submitting.current) return; submitting.current = true; setBusy(true); setError(''); try { await uploadFiles(created) } catch (e) { setError(e instanceof Error ? e.message : 'Attachment retry failed.') } finally { submitting.current = false; setBusy(false) } }

  return <Stack spacing={6} sx={{ maxWidth: 900, mx: 'auto' }}><PageHeader title="Submit a service request" description="Tell us what happened, where it is and anything that will help the service team respond." />
    {error && (created ? <InlineNotice severity="warning" title={`Request ${created.referenceNumber} was created successfully`}>{error}</InlineNotice> : <Alert severity="error" role="alert">{error}</Alert>)}
    <Stack component="form" spacing={5} onSubmit={submit} noValidate aria-busy={busy}>
      <FormSection title="Request details" description="Choose the service and describe the issue in clear, specific language.">
        {categoryError && <InlineNotice severity="error" title="Categories unavailable"><Stack direction={{ xs: 'column', sm: 'row' }} sx={{ alignItems: { sm: 'center' }, gap: 2 }}><span>{categoryError}</span><Button color="inherit" onClick={() => void loadCategories()}>Try again</Button></Stack></InlineNotice>}
        {categoriesLoading ? <Stack role="status" direction="row" spacing={2} sx={{ alignItems: 'center' }}><CircularProgress size={22} /><Typography>Loading service categories…</Typography></Stack> : <TextField select label="Service category" value={form.categoryId} onChange={e => setForm({ ...form, categoryId: e.target.value })} error={Boolean(errors.categoryId)} helperText={errors.categoryId || 'Select the service that best matches your request.'} required disabled={Boolean(created)}>{categories.map(category => <MenuItem key={category.id} value={category.id}><Stack><span>{category.name}</span><Typography variant="caption" color="text.secondary">{category.description}</Typography></Stack></MenuItem>)}</TextField>}
        <TextField label="Short title" value={form.title} onChange={e => setForm({ ...form, title: e.target.value })} slotProps={{ htmlInput: { maxLength: 150, 'aria-describedby': 'title-count' } }} error={Boolean(errors.title)} helperText={<Stack direction="row" sx={{ justifyContent: 'space-between' }}><span>{errors.title ?? 'Summarise the issue in a few words.'}</span><CharacterCounter id="title-count" current={form.title.length} max={150} /></Stack>} required disabled={Boolean(created)} />
        <TextField label="What happened?" value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} multiline minRows={6} slotProps={{ htmlInput: { maxLength: 2000, 'aria-describedby': 'description-count' } }} error={Boolean(errors.description)} helperText={<Stack direction="row" sx={{ justifyContent: 'space-between', gap: 2 }}><span>{errors.description ?? 'Include what you observed and any immediate safety concern.'}</span><CharacterCounter id="description-count" current={form.description.length} max={2000} /></Stack>} required disabled={Boolean(created)} />
      </FormSection>

      <FormSection title="Location" description="A written address is required. The optional map pin helps the service team identify the exact position.">
        <TextField label="Location or street address" value={form.address} onChange={e => setForm({ ...form, address: e.target.value })} slotProps={{ htmlInput: { maxLength: 300 } }} error={Boolean(errors.address)} helperText={errors.address ?? 'Enter a street address, park, facility or recognisable location.'} required disabled={Boolean(created)} />
        <Suspense fallback={<PageLoading label="Loading map" />}><MapPicker value={location} onChange={setLocation} /></Suspense>
      </FormSection>

      <FormSection title="Attachments" description="Add up to five JPG, PNG or PDF files. Each file can be up to 10 MB.">
        <Button component="label" variant="outlined" disabled={busy || files.length >= 5 || Boolean(created)}>Choose files<input hidden multiple type="file" accept=".jpg,.jpeg,.png,.pdf,image/jpeg,image/png,application/pdf" onChange={e => { chooseFiles(e.target.files); e.target.value = '' }} /></Button>
        {files.length > 0 && <Stack spacing={2}>{files.map((file, index) => <SelectedFileItem key={`${file.name}-${file.lastModified}-${index}`} file={file} onRemove={() => setFiles(current => current.filter((_, itemIndex) => itemIndex !== index))} />)}</Stack>}
        <InlineNotice>Files upload securely after the request is created. If one file fails, the request and any successful uploads remain safe.</InlineNotice>
      </FormSection>

      <FormSection title="Review and submit" description="Check the details above before creating your request.">
        <Stack spacing={1}><Typography><strong>Category:</strong> {categories.find(category => category.id === form.categoryId)?.name ?? 'Not selected'}</Typography><Typography><strong>Location:</strong> {form.address.trim() || 'Not entered'}</Typography><Typography><strong>Attachments:</strong> {files.length}</Typography></Stack>
        {created ? <FormActions secondaryLabel="Open request" onSecondary={() => navigate(`/cases/${created.id}`)} primaryLabel={busy ? 'Retrying attachments…' : 'Retry failed attachments'} primaryProps={{ disabled: busy || files.length === 0, onClick: () => void retry() }} /> : <FormActions onSecondary={() => navigate(-1)} primaryLabel={busy ? 'Creating your request…' : 'Submit request'} primaryProps={{ type: 'submit', size: 'large', disabled: busy || categoriesLoading || Boolean(categoryError) }} />}
      </FormSection>
    </Stack>
  </Stack>
}

function validate(form: { categoryId: string; title: string; description: string; address: string }) { const errors: Record<string, string> = {}; if (!form.categoryId) errors.categoryId = 'Select a service category.'; check(errors, 'title', 'Title', form.title, 5, 150); check(errors, 'description', 'Description', form.description, 20, 2000); check(errors, 'address', 'Location', form.address, 5, 300); return errors }
function check(errors: Record<string, string>, field: string, label: string, value: string, min: number, max: number) { const length = value.trim().length; if (length < min || length > max) errors[field] = `${label} must be ${min}–${max} characters after trimming.` }
