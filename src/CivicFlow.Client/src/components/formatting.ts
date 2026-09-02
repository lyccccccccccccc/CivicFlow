const labels: Record<string, string> = { WaitingForResident: 'Waiting for resident', InProgress: 'In progress', OnTrack: 'On track', AtRisk: 'At risk', NoSla: 'No SLA' }
export const formatStatus = (value: string) => labels[value] ?? value
