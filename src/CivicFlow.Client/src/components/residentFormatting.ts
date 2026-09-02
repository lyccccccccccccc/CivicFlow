import type { CaseItem } from '../api/client'
import { formatDate } from './formatting'

type ResidentStatus = Pick<CaseItem, 'status' | 'slaState'>
export function residentServiceMessage(item: ResidentStatus) {
  if (['Resolved', 'Closed'].includes(item.status)) return 'Service work complete'
  if (item.status === 'Rejected') return 'Request not proceeding'
  if (item.status === 'WaitingForResident') return 'The service team is waiting for your reply'
  if (item.slaState === 'Overdue') return 'Service target overdue'
  if (item.slaState === 'AtRisk') return 'Service target due soon'
  return 'Service target on track'
}

export function notificationGroupLabel(value: string, now = new Date()) {
  const date = new Date(value); const today = new Date(now.getFullYear(), now.getMonth(), now.getDate()); const target = new Date(date.getFullYear(), date.getMonth(), date.getDate()); const days = Math.round((today.getTime() - target.getTime()) / 86400000)
  if (days === 0) return 'Today'; if (days === 1) return 'Yesterday'; return formatDate(target)
}

export function formatBytes(value: number) { return value < 1024 * 1024 ? `${Math.max(1, Math.ceil(value / 1024))} KB` : `${(value / 1024 / 1024).toFixed(1)} MB` }
