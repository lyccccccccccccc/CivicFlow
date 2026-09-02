import { useCallback, useEffect, useRef, useState } from 'react'
import { Alert, Button, Divider, FormControlLabel, Stack, Switch, TextField, Typography } from '@mui/material'
import { api, apiDownload, uploadAttachment, type CaseAttachment } from '../api/client'
import { AttachmentListItem, SelectedFileItem } from './resident'
import { ConfirmActionDialog, SectionCard } from './ui'

const residentEditable = ['Submitted', 'Triaged', 'Assigned', 'InProgress', 'WaitingForResident', 'Reopened']

export function CaseAttachments({ caseId, status, resident, canEdit }: { caseId: string; status: string; resident: boolean; canEdit: boolean }) {
  const [items, setItems] = useState<CaseAttachment[]>([]); const [file, setFile] = useState<File>(); const [internal, setInternal] = useState(false)
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false); const [deleting, setDeleting] = useState<CaseAttachment>(); const [reason, setReason] = useState('')
  const uploadKey = useRef(crypto.randomUUID())
  const load = useCallback(() => api<CaseAttachment[]>(`/cases/${caseId}/attachments`).then(setItems).catch(e => setError(e instanceof Error ? e.message : 'Unable to load attachments.')), [caseId])
  useEffect(() => { void load() }, [load])
  const editable = canEdit && (!resident || residentEditable.includes(status))
  const choose = (selected?: File) => { setError(''); if (selected && selected.size > 10 * 1024 * 1024) { setError('Each attachment must be 10 MB or smaller.'); setFile(undefined); return } setFile(selected); uploadKey.current = crypto.randomUUID() }
  const upload = async () => { if (!file || !editable) return; setBusy(true); setError(''); try { await uploadAttachment(caseId, file, resident ? 'Public' : internal ? 'Internal' : 'Public', uploadKey.current); setFile(undefined); uploadKey.current = crypto.randomUUID(); await load() } catch (e) { setError(e instanceof Error ? e.message : 'Upload failed. You can retry this file.') } finally { setBusy(false) } }
  const remove = async () => { if (!deleting || reason.trim().length < 10) return; setBusy(true); setError(''); try { await api(`/cases/${caseId}/attachments/${deleting.id}`, { method: 'DELETE', body: JSON.stringify({ reason: reason.trim() }) }); setDeleting(undefined); setReason(''); await load() } catch (e) { setError(e instanceof Error ? e.message : 'Unable to delete attachment.') } finally { setBusy(false) } }
  return <SectionCard title="Attachments" description="JPG, PNG or PDF · 10 MB each · up to 5 files">
    {error && <Alert severity="error" sx={{ mt: 2 }}>{error}</Alert>}
    <Stack>{items.map(item => <AttachmentListItem key={item.id} item={item} metaPrefix={resident ? undefined : item.visibility} actions={<><Button size="small" onClick={() => void apiDownload(`/cases/${caseId}/attachments/${item.id}/content`, item.originalFileName)}>Download</Button>{editable && (!resident || item.uploadedByUserId) && <Button size="small" color="error" onClick={() => setDeleting(item)}>Delete</Button>}</>} />)}</Stack>
    {items.length === 0 && <Typography color="text.secondary" sx={{ my: 2 }}>No attachments.</Typography>}
    {editable ? <><Divider sx={{ my: 4 }} /><Stack spacing={3}><Button component="label" variant="outlined" disabled={busy || items.length >= 5}>Choose file<input hidden type="file" accept=".jpg,.jpeg,.png,.pdf,image/jpeg,image/png,application/pdf" onChange={e => choose(e.target.files?.[0])} /></Button>{file && <SelectedFileItem file={file} onRemove={() => choose(undefined)} />}{!resident && <FormControlLabel control={<Switch checked={internal} onChange={e => { setInternal(e.target.checked); uploadKey.current = crypto.randomUUID() }} />} label="Internal attachment (hidden from resident)" />}<Button variant="contained" disabled={busy || !file || items.length >= 5} onClick={() => void upload()}>{busy ? 'Uploading…' : 'Upload attachment'}</Button></Stack></> : <Alert severity="info" sx={{ mt: 3 }}>{resident && ['Resolved', 'Closed', 'Rejected'].includes(status) ? 'Reopen this request before adding or deleting attachments.' : 'Attachments cannot be changed for this case.'}</Alert>}
    <ConfirmActionDialog open={Boolean(deleting)} title="Delete attachment?" description="The file will be soft deleted and retained for 30 days. This action is audited." confirmLabel="Confirm delete" confirmColor="error" busy={busy} confirmDisabled={reason.trim().length < 10} onCancel={() => setDeleting(undefined)} onConfirm={() => void remove()}><TextField autoFocus fullWidth multiline minRows={3} label="Reason (10–500 characters)" value={reason} onChange={e => setReason(e.target.value)} slotProps={{ htmlInput: { maxLength: 500 } }} /></ConfirmActionDialog>
  </SectionCard>
}
