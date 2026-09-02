const labels: Record<string, string> = { WaitingForResident: 'Waiting for resident', InProgress: 'In progress', OnTrack: 'On track', AtRisk: 'At risk', NoSla: 'No SLA' }
export const formatStatus = (value: string) => labels[value] ?? value

const dateFormatter = new Intl.DateTimeFormat('en-AU', { day: 'numeric', month: 'short', year: 'numeric' })
const timeFormatter = new Intl.DateTimeFormat('en-AU', { hour: 'numeric', minute: '2-digit', hour12: true })
export function formatDate(value: string | Date) { const date = value instanceof Date ? value : new Date(value); const parts = Object.fromEntries(dateFormatter.formatToParts(date).map(part => [part.type, part.value])); return `${parts.day} ${parts.month.slice(0, 3)} ${parts.year}` }
export function formatDateTime(value: string | Date) { const date = value instanceof Date ? value : new Date(value); return `${formatDate(date)}, ${timeFormatter.format(date).replace(/\s+/g, ' ').toLowerCase()}` }
