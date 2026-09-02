import { useState, type ReactNode } from 'react'
import { Alert, Box, Button, Chip, IconButton, InputAdornment, Paper, Stack, TextField, Typography, type AlertColor, type TextFieldProps } from '@mui/material'
import AttachFileRoundedIcon from '@mui/icons-material/AttachFileRounded'
import CloseRoundedIcon from '@mui/icons-material/CloseRounded'
import RemoveRedEyeRoundedIcon from '@mui/icons-material/RemoveRedEyeRounded'
import VisibilityOffRoundedIcon from '@mui/icons-material/VisibilityOffRounded'
import { Link } from 'react-router-dom'
import type { CaseAttachment, CaseItem } from '../api/client'
import { StatusChip } from './ui'
import { formatBytes, residentServiceMessage } from './residentFormatting'

export function PasswordField({ label = 'Password', autoComplete, helperText, ...props }: Omit<TextFieldProps, 'type'> & { autoComplete: 'current-password' | 'new-password' }) {
  const [visible, setVisible] = useState(false)
  return <TextField {...props} label={label} type={visible ? 'text' : 'password'} autoComplete={autoComplete} helperText={helperText} slotProps={{ input: { endAdornment: <InputAdornment position="end"><IconButton edge="end" aria-label={visible ? 'Hide password' : 'Show password'} onClick={() => setVisible(value => !value)}><>{visible ? <VisibilityOffRoundedIcon /> : <RemoveRedEyeRoundedIcon />}</></IconButton></InputAdornment> } }} />
}

export function CharacterCounter({ current, max, id }: { current: number; max: number; id?: string }) {
  const remaining = max - current
  return <Typography component="span" id={id} variant="caption" color={remaining < 0 ? 'error.main' : 'text.secondary'} aria-live="polite">{current.toLocaleString()} / {max.toLocaleString()} characters</Typography>
}

export function InlineNotice({ severity = 'info', title, children }: { severity?: AlertColor; title?: string; children: ReactNode }) {
  return <Alert severity={severity} role="status">{title && <Typography sx={{ fontWeight: 800 }}>{title}</Typography>}{children}</Alert>
}

export function FormSection({ title, description, children }: { title: string; description?: string; children: ReactNode }) {
  return <Paper component="section" sx={{ p: { xs: 4, sm: 6 } }}><Typography component="h2" variant="h5">{title}</Typography>{description && <Typography color="text.secondary" sx={{ mt: 1, mb: 5 }}>{description}</Typography>}<Stack spacing={4} sx={{ mt: description ? 0 : 4 }}>{children}</Stack></Paper>
}

export function ResponsiveDataView({ desktop, mobile, busy = false }: { desktop: ReactNode; mobile: ReactNode; busy?: boolean }) {
  return <Box aria-busy={busy}><Box sx={{ display: { xs: 'none', md: 'block' } }}>{desktop}</Box><Box sx={{ display: { xs: 'block', md: 'none' } }}>{mobile}</Box></Box>
}

export function ActiveFilterSummary({ search, status, onClear }: { search?: string; status?: string; onClear: () => void }) {
  const active = [search && `Search: ${search}`, status && `Status: ${friendly(status)}`].filter(Boolean) as string[]
  if (!active.length) return null
  return <Stack direction="row" spacing={2} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }} aria-label="Active filters"><Typography variant="body2" color="text.secondary">Active filters</Typography>{active.map(value => <Chip key={value} label={value} size="small" />)}<Button size="small" onClick={onClear}>Clear all</Button></Stack>
}

type ResidentCardItem = Pick<CaseItem, 'id' | 'referenceNumber' | 'title' | 'status' | 'categoryName' | 'submittedAtUtc' | 'slaState'>
export function ResidentRequestCard({ item }: { item: ResidentCardItem }) {
  return <Paper component="article" sx={{ p: 5 }}><Stack spacing={3}>
    <Stack direction="row" sx={{ justifyContent: 'space-between', gap: 2, alignItems: 'flex-start' }}><Box><Typography variant="overline" color="primary.main" sx={{ fontWeight: 850 }}>{item.referenceNumber}</Typography><Typography component="h2" variant="h6">{item.title}</Typography></Box><StatusChip status={item.status} /></Stack>
    <Stack spacing={1}><Typography variant="body2"><strong>Category:</strong> {item.categoryName}</Typography><Typography variant="body2" color="text.secondary">Submitted {new Date(item.submittedAtUtc).toLocaleDateString([], { dateStyle: 'medium' })}</Typography></Stack>
    <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', gap: 2 }}><Typography variant="body2" sx={{ fontWeight: 700 }}>{residentServiceMessage(item)}</Typography><Button component={Link} to={`/cases/${item.id}`} variant="outlined">View request</Button></Stack>
  </Stack></Paper>
}

export function AttachmentListItem({ item, actions, metaPrefix }: { item: Pick<CaseAttachment, 'originalFileName' | 'sizeBytes' | 'uploadedAtUtc'>; actions: ReactNode; metaPrefix?: string }) {
  return <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3} sx={{ py: 3, borderBottom: '1px solid', borderColor: 'divider', alignItems: { sm: 'center' }, justifyContent: 'space-between' }}><Stack direction="row" spacing={2} sx={{ minWidth: 0 }}><AttachFileRoundedIcon color="action" /><Box sx={{ minWidth: 0 }}><Typography title={item.originalFileName} sx={{ fontWeight: 700, overflowWrap: 'anywhere' }}>{item.originalFileName}</Typography><Typography variant="caption" color="text.secondary">{metaPrefix && `${metaPrefix} · `}{formatBytes(item.sizeBytes)} · {new Date(item.uploadedAtUtc).toLocaleString()}</Typography></Box></Stack><Stack direction="row" spacing={1} sx={{ flexShrink: 0, flexWrap: 'wrap' }}>{actions}</Stack></Stack>
}

export function SelectedFileItem({ file, onRemove }: { file: File; onRemove: () => void }) {
  return <Stack direction="row" spacing={2} sx={{ alignItems: 'center', justifyContent: 'space-between', p: 3, bgcolor: 'background.default', borderRadius: 2 }}><Box sx={{ minWidth: 0 }}><Typography sx={{ fontWeight: 700, overflowWrap: 'anywhere' }}>{file.name}</Typography><Typography variant="caption" color="text.secondary">{formatBytes(file.size)}</Typography></Box><IconButton aria-label={`Remove ${file.name}`} onClick={onRemove}><CloseRoundedIcon /></IconButton></Stack>
}

export function TimelineItem({ title, message, meta, publicEntry = true }: { title: string; message: string; meta: string; publicEntry?: boolean }) {
  return <Box component="article" sx={{ position: 'relative', pl: 7, pb: 5, '&::before': { content: '""', position: 'absolute', left: 7, top: 8, bottom: 0, width: 2, bgcolor: 'divider' }, '&::after': { content: '""', position: 'absolute', left: 2, top: 7, width: 12, height: 12, borderRadius: '50%', bgcolor: publicEntry ? 'primary.main' : 'warning.main' } }}><Typography sx={{ fontWeight: 800 }}>{title}</Typography><Typography sx={{ mt: 1, whiteSpace: 'pre-wrap' }}>{message}</Typography><Typography variant="caption" color="text.secondary">{meta}</Typography></Box>
}

export type NotificationView = { id: string; serviceRequestId?: string; title: string; message: string; readAtUtc?: string; createdAtUtc: string }
export function NotificationGroup({ label, items, pending, onRead }: { label: string; items: NotificationView[]; pending: string[]; onRead: (id: string) => void }) {
  return <Stack component="section" spacing={3} aria-labelledby={`notification-${slug(label)}`}><Typography id={`notification-${slug(label)}`} component="h2" variant="h5">{label}</Typography>{items.map(item => <Paper component="article" key={item.id} sx={{ p: { xs: 4, sm: 5 }, borderLeft: '4px solid', borderLeftColor: item.readAtUtc ? 'divider' : 'primary.main', bgcolor: item.readAtUtc ? 'background.paper' : '#f5faff' }}><Stack direction={{ xs: 'column', sm: 'row' }} sx={{ justifyContent: 'space-between', gap: 4 }}><Box>{!item.readAtUtc && <Chip label="Unread" size="small" color="info" sx={{ mb: 2 }} />}<Typography sx={{ fontWeight: item.readAtUtc ? 700 : 850 }}>{item.title}</Typography><Typography color="text.secondary" sx={{ mt: 1 }}>{item.message}</Typography><Typography variant="caption" color="text.secondary">{new Date(item.createdAtUtc).toLocaleString()}</Typography></Box><Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>{item.serviceRequestId && <Button component={Link} to={`/cases/${item.serviceRequestId}`}>Open request</Button>}{!item.readAtUtc && <Button disabled={pending.includes(item.id)} onClick={() => onRead(item.id)}>{pending.includes(item.id) ? 'Saving…' : 'Mark read'}</Button>}</Stack></Stack></Paper>)}</Stack>
}

const friendly = (value: string) => ({ WaitingForResident: 'Waiting for resident', InProgress: 'In progress' }[value] ?? value)
const slug = (value: string) => value.toLowerCase().replace(/[^a-z0-9]+/g, '-')
