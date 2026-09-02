import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Paper, Snackbar, Stack } from '@mui/material'
import { api } from '../api/client'
import { EmptyState, ErrorState, PageHeader, TableSkeleton } from '../components/ui'
import { NotificationGroup, type NotificationView } from '../components/resident'
import { notificationGroupLabel } from '../components/residentFormatting'

export function NotificationsPage() {
  const [items, setItems] = useState<NotificationView[]>([]); const [error, setError] = useState(''); const [notice, setNotice] = useState(''); const [pending, setPending] = useState<string[]>([]); const [loading, setLoading] = useState(true)
  const inFlight = useRef(new Set<string>()); const load = useCallback(() => api<NotificationView[]>('/notifications').then(setItems), [])
  useEffect(() => { void load().catch(error => setError(error instanceof Error ? error.message : 'Unable to load notifications')).finally(() => setLoading(false)) }, [load])
  const groups = useMemo(() => { const result = new Map<string, NotificationView[]>(); for (const item of items) { const label = notificationGroupLabel(item.createdAtUtc); result.set(label, [...(result.get(label) ?? []), item]) } return [...result.entries()] }, [items])
  const read = async (id: string) => { if (inFlight.current.has(id)) return; inFlight.current.add(id); setPending([...inFlight.current]); setError(''); try { await api(`/notifications/${id}/read`, { method: 'POST' }); await load(); setNotice('Notification marked as read.') } catch (error) { setError(error instanceof Error ? error.message : 'Unable to mark notification read') } finally { inFlight.current.delete(id); setPending([...inFlight.current]) } }
  const retry = () => { setLoading(true); setError(''); void load().catch(error => setError(error instanceof Error ? error.message : 'Unable to load notifications')).finally(() => setLoading(false)) }
  return <Stack spacing={6} aria-busy={loading}><PageHeader title="Notifications" description="Public case messages and workflow updates that need your attention." />{error && <ErrorState title="Unable to load notifications" message={error} retry={retry} />}{loading ? <Paper><TableSkeleton rows={4} columns={2} label="Loading notifications" /></Paper> : groups.length ? groups.map(([label, group]) => <NotificationGroup key={label} label={label} items={group} pending={pending} onRead={id => void read(id)} />) : !error && <Paper><EmptyState title="You’re all caught up" description="New public case updates will appear here." /></Paper>}<Snackbar open={Boolean(notice)} autoHideDuration={3500} onClose={() => setNotice('')} message={notice} /></Stack>
}
