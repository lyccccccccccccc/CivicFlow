import type { ReactNode } from 'react'
import { Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, Paper, Skeleton, Stack, Typography, type AlertColor, type ButtonProps, type ChipProps, type PaperProps, type SxProps, type Theme } from '@mui/material'
import ErrorOutlineRoundedIcon from '@mui/icons-material/ErrorOutlineRounded'
import InboxRoundedIcon from '@mui/icons-material/InboxRounded'
import { civicTokens } from '../theme'
import { formatStatus } from './formatting'

export function PageHeader({ title, description, eyebrow, actions }: { title: string; description?: string; eyebrow?: string; actions?: ReactNode }) {
  return <Stack direction={{ xs: 'column', sm: 'row' }} sx={{ justifyContent: 'space-between', alignItems: { sm: 'flex-start' }, gap: 4 }}>
    <Box>{eyebrow && <Typography variant="overline" color="primary.main" sx={{ fontWeight: 800 }}>{eyebrow}</Typography>}<Typography variant="h3" component="h1">{title}</Typography>{description && <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 760 }}>{description}</Typography>}</Box>
    {actions && <Box sx={{ flexShrink: 0 }}>{actions}</Box>}
  </Stack>
}

export function SectionCard({ title, description, children, sx, ...props }: PaperProps & { title?: string; description?: string }) {
  return <Paper {...props} sx={{ p: { xs: 4, sm: 6 }, borderRadius: `${civicTokens.radius.card}px`, ...sx }}>
    {(title || description) && <Box sx={{ mb: 4 }}>{title && <Typography variant="h6">{title}</Typography>}{description && <Typography color="text.secondary" variant="body2" sx={{ mt: .5 }}>{description}</Typography>}</Box>}{children}
  </Paper>
}

export function PageLoading({ label = 'Loading page' }: { label?: string }) {
  return <Stack role="status" aria-live="polite" aria-busy="true" spacing={3} sx={{ py: 6 }}><Typography color="text.secondary">{label}…</Typography><Skeleton variant="rounded" height={96} /><Skeleton variant="rounded" height={240} /></Stack>
}

export function TableSkeleton({ rows = 5, columns = 6, label = 'Loading table' }: { rows?: number; columns?: number; label?: string }) {
  return <Box role="status" aria-live="polite" aria-busy="true" aria-label={label} sx={{ p: 4 }}><Stack spacing={2}>{Array.from({ length: rows }, (_, row) => <Stack direction="row" spacing={2} key={row}>{Array.from({ length: columns }, (_, column) => <Skeleton key={column} height={32} sx={{ flex: 1 }} />)}</Stack>)}</Stack></Box>
}

export function EmptyState({ title, description, action }: { title: string; description?: string; action?: ReactNode }) {
  return <Stack role="status" sx={{ alignItems: 'center', textAlign: 'center', py: 8, px: 4 }} spacing={2}><InboxRoundedIcon color="disabled" sx={{ fontSize: 42 }} /><Typography variant="h6">{title}</Typography>{description && <Typography color="text.secondary" sx={{ maxWidth: 520 }}>{description}</Typography>}{action}</Stack>
}

export function ErrorState({ title = 'Something went wrong', message, retry }: { title?: string; message: string; retry?: () => void }) {
  return <Alert severity="error" icon={<ErrorOutlineRoundedIcon />} role="alert" action={retry && <Button color="inherit" onClick={retry}>Try again</Button>}><Typography sx={{ fontWeight: 800 }}>{title}</Typography>{message}</Alert>
}

const statusColors: Record<string, ChipProps['color']> = { Submitted: 'info', Triaged: 'info', Assigned: 'warning', InProgress: 'warning', WaitingForResident: 'warning', Resolved: 'success', Closed: 'default', Reopened: 'info', Rejected: 'error' }
export function StatusChip({ status, ...props }: { status: string } & Omit<ChipProps, 'label' | 'color'>) { return <Chip size="small" color={statusColors[status] ?? 'default'} label={formatStatus(status)} {...props} /> }

const priorityColors: Record<string, ChipProps['color']> = { Critical: 'error', High: 'warning', Medium: 'info', Low: 'default' }
export function PriorityChip({ priority, ...props }: { priority?: string } & Omit<ChipProps, 'label' | 'color'>) { return priority ? <Chip size="small" color={priorityColors[priority] ?? 'default'} label={priority} {...props} /> : null }

const slaColors: Record<string, ChipProps['color']> = { OnTrack: 'success', AtRisk: 'warning', Overdue: 'error', Breached: 'error', Complete: 'success' }
export function SlaStatus({ state, label, ...props }: { state: string; label?: string } & Omit<ChipProps, 'label' | 'color'>) { return <Chip size="small" color={slaColors[state] ?? 'default'} label={label ?? formatStatus(state)} {...props} /> }

export function ConfirmActionDialog({ open, title, description, confirmLabel = 'Confirm', confirmColor = 'warning', busy = false, confirmDisabled = false, onCancel, onConfirm, children }: { open: boolean; title: string; description?: string; confirmLabel?: string; confirmColor?: AlertColor; busy?: boolean; confirmDisabled?: boolean; onCancel: () => void; onConfirm: () => void; children?: ReactNode }) {
  return <Dialog open={open} onClose={() => { if (!busy) onCancel() }} aria-labelledby="confirm-action-title"><DialogTitle id="confirm-action-title">{title}</DialogTitle><DialogContent>{description && <Typography sx={{ mb: children ? 4 : 0 }}>{description}</Typography>}{children}</DialogContent><DialogActions><Button disabled={busy} onClick={onCancel}>Cancel</Button><Button color={confirmColor} variant="contained" disabled={busy || confirmDisabled} onClick={onConfirm}>{busy ? 'Working…' : confirmLabel}</Button></DialogActions></Dialog>
}

export function FormActions({ primaryLabel, primaryProps, secondaryLabel = 'Cancel', onSecondary, sx }: { primaryLabel: string; primaryProps?: ButtonProps; secondaryLabel?: string; onSecondary?: () => void; sx?: SxProps<Theme> }) {
  return <Stack direction={{ xs: 'column-reverse', sm: 'row' }} spacing={2} sx={{ justifyContent: 'flex-end', ...sx }}>{onSecondary && <Button onClick={onSecondary}>{secondaryLabel}</Button>}<Button variant="contained" {...primaryProps}>{primaryLabel}</Button></Stack>
}

export function SkipLink() { return <Box component="a" href="#main-content" sx={{ position: 'fixed', top: 2, left: 2, zIndex: theme => theme.zIndex.tooltip + 1, transform: 'translateY(-150%)', bgcolor: 'background.paper', color: 'primary.main', px: 4, py: 3, borderRadius: 2, fontWeight: 800, '&:focus': { transform: 'translateY(0)' } }}>Skip to main content</Box> }
