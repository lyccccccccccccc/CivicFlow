import { Box, Button, Chip, Grid, Paper, Stack, Typography } from '@mui/material'
import ArrowForwardRoundedIcon from '@mui/icons-material/ArrowForwardRounded'
import CheckCircleRoundedIcon from '@mui/icons-material/CheckCircleRounded'
import { Link } from 'react-router-dom'

const points = ['Transparent case tracking', 'SLA-driven service teams', 'Secure role-based access', 'Complete activity history']

export function LandingPage() {
  return <Grid container spacing={6} sx={{ py: { xs: 4, md: 10 }, alignItems: 'center' }}>
    <Grid size={{ xs: 12, md: 7 }}><Stack spacing={3}>
      <Chip label="COMMUNITY SERVICE REQUESTS" color="secondary" sx={{ alignSelf: 'flex-start', fontWeight: 800 }} />
      <Typography variant="h1" sx={{ fontSize: { xs: 48, md: 72 }, lineHeight: 1.02 }}>Clear requests.<br />Accountable outcomes.</Typography>
      <Typography component="p" variant="h6" color="text.secondary" sx={{ maxWidth: 680, lineHeight: 1.6 }}>Report local issues, follow every update, and help service teams deliver timely community outcomes.</Typography>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
        <Button component={Link} to="/register" variant="contained" size="large" endIcon={<ArrowForwardRoundedIcon />}>Submit a request</Button>
        <Button component={Link} to="/login" variant="outlined" size="large">Track an existing request</Button>
      </Stack>
      <Typography variant="caption" color="text.secondary">Independent portfolio prototype. Not affiliated with any government organisation.</Typography>
    </Stack></Grid>
    <Grid size={{ xs: 12, md: 5 }}><Paper sx={{ p: 4, borderTop: '6px solid', borderColor: 'secondary.main' }}>
      <Typography component="h2" variant="h5" sx={{ fontWeight: 800 }} gutterBottom>Built for trustworthy service</Typography>
      <Typography color="text.secondary" sx={{ mb: 3 }}>One shared view from initial report to verified resolution.</Typography>
      <Stack spacing={2}>{points.map(point => <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }} key={point}><CheckCircleRoundedIcon color="primary" /><Typography>{point}</Typography></Stack>)}</Stack>
      <Box sx={{ mt: 4, p: 2, bgcolor: 'primary.50', borderRadius: 2 }}><Typography variant="body2"><strong>Accessible by design</strong><br />Keyboard-friendly controls, readable contrast and responsive layouts.</Typography></Box>
    </Paper></Grid>
  </Grid>
}
