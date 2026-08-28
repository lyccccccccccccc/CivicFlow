import { useEffect, useState } from 'react'
import { Alert, Grid, LinearProgress, Paper, Stack, Typography } from '@mui/material'
import { api, type DashboardData } from '../api/client'

export function DashboardPage() {
  const [data, setData] = useState<DashboardData | null>(null); const [error, setError] = useState('')
  useEffect(() => { api<DashboardData>('/dashboard').then(setData).catch(e => setError(e instanceof Error ? e.message : 'Unable to load dashboard')) }, [])
  if (error) return <Alert severity="error">{error}</Alert>; if (!data) return <LinearProgress />
  const metrics = [['Open cases', data.open], ['Overdue SLA', data.overdue], ['Unassigned', data.unassigned], ['Resolved (30 days)', data.resolvedLast30Days]] as const
  const max = Math.max(...data.byStatus.map(x => x.count), 1)
  return <Stack spacing={4}><div><Typography variant="h3">Operations dashboard</Typography><Typography color="text.secondary">Live workload and service-target overview.</Typography></div><Grid container spacing={2}>{metrics.map(([label, value]) => <Grid size={{ xs: 6, md: 3 }} key={label}><Paper sx={{ p: 3 }}><Typography color="text.secondary">{label}</Typography><Typography variant="h3" sx={{ fontWeight: 850 }}>{value}</Typography></Paper></Grid>)}</Grid><Paper sx={{ p: 3 }}><Typography variant="h5" sx={{ mb: 3, fontWeight: 800 }}>Cases by status</Typography><Stack spacing={2}>{data.byStatus.map(row => <div key={row.status}><Stack direction="row" sx={{ justifyContent: 'space-between' }}><Typography>{row.status}</Typography><Typography sx={{ fontWeight: 700 }}>{row.count}</Typography></Stack><LinearProgress variant="determinate" value={row.count / max * 100} sx={{ mt: 1, height: 8, borderRadius: 4 }} /></div>)}</Stack></Paper></Stack>
}
