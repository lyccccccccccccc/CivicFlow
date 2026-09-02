import { Box, Button, Chip, Grid, Paper, Stack, Typography } from '@mui/material'
import ArrowForwardRoundedIcon from '@mui/icons-material/ArrowForwardRounded'
import CheckCircleRoundedIcon from '@mui/icons-material/CheckCircleRounded'
import ForumRoundedIcon from '@mui/icons-material/ForumRounded'
import SearchRoundedIcon from '@mui/icons-material/SearchRounded'
import SendRoundedIcon from '@mui/icons-material/SendRounded'
import ShieldRoundedIcon from '@mui/icons-material/ShieldRounded'
import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

const values = [
  { icon: <SendRoundedIcon />, title: 'Submit', text: 'Give the service team the details, location and supporting files they need.' },
  { icon: <SearchRoundedIcon />, title: 'Track', text: 'Follow each meaningful milestone from submission through resolution.' },
  { icon: <ForumRoundedIcon />, title: 'Communicate', text: 'Reply securely when the service team requests more information.' },
]
const steps = ['Create your request', 'The service team reviews and assigns it', 'Track progress and respond', 'Review the resolution']

export function LandingPage() {
  const { user } = useAuth(); const home = user?.roles.includes('Resident') ? '/requests' : '/cases'
  return <Stack spacing={{ xs: 12, md: 18 }} sx={{ py: { xs: 5, md: 12 } }}>
    <Grid container spacing={{ xs: 8, md: 12 }} sx={{ alignItems: 'center' }}><Grid size={{ xs: 12, md: 7 }}><Stack spacing={5}>
      <Chip label="COMMUNITY SERVICE REQUESTS" color="secondary" sx={{ alignSelf: 'flex-start', fontWeight: 850 }} />
      <Box><Typography component="h1" variant="h1" sx={{ maxWidth: 820 }}>A clearer way to request local services.</Typography><Typography component="p" variant="h6" color="text.secondary" sx={{ maxWidth: 680, mt: 4, lineHeight: 1.65 }}>Report an issue once, understand what happens next, and keep every public update in one secure place.</Typography></Box>
      {user ? <Button component={Link} to={home} variant="contained" size="large" endIcon={<ArrowForwardRoundedIcon />} sx={{ alignSelf: { sm: 'flex-start' } }}>Open CivicFlow</Button> : <Stack direction={{ xs: 'column', sm: 'row' }} spacing={3}><Button component={Link} to="/register" variant="contained" size="large" endIcon={<ArrowForwardRoundedIcon />}>Create an account</Button><Button component={Link} to="/login" variant="outlined" size="large">Sign in to track a request</Button></Stack>}
      <Typography variant="caption" color="text.secondary">Independent portfolio prototype. Not affiliated with any government organisation.</Typography>
    </Stack></Grid><Grid size={{ xs: 12, md: 5 }}><Paper sx={{ p: { xs: 5, md: 7 }, bgcolor: 'primary.dark', color: 'primary.contrastText', border: 0 }}><ShieldRoundedIcon sx={{ fontSize: 42 }} /><Typography component="h2" variant="h4" sx={{ mt: 4 }}>Designed for accountable service</Typography><Typography sx={{ mt: 3, color: 'rgba(255,255,255,.82)' }}>Your request is protected by role-based access. Residents see the public conversation and service progress, while internal operational notes remain private.</Typography><Stack spacing={2.5} sx={{ mt: 6 }}>{['Secure access', 'Transparent milestones', 'Auditable decisions', 'Accessible on any device'].map(point => <Stack direction="row" spacing={2} key={point}><CheckCircleRoundedIcon fontSize="small" /><Typography>{point}</Typography></Stack>)}</Stack></Paper></Grid></Grid>
    <Box component="section" aria-labelledby="landing-value-title"><Typography id="landing-value-title" component="h2" variant="h3" sx={{ maxWidth: 720 }}>Everything residents need to stay informed</Typography><Grid container spacing={5} sx={{ mt: 4 }}>{values.map(value => <Grid size={{ xs: 12, md: 4 }} key={value.title}><Paper sx={{ height: '100%', p: 6 }}><Box sx={{ color: 'primary.main' }}>{value.icon}</Box><Typography component="h3" variant="h5" sx={{ mt: 3 }}>{value.title}</Typography><Typography color="text.secondary" sx={{ mt: 2 }}>{value.text}</Typography></Paper></Grid>)}</Grid></Box>
    <Paper component="section" aria-labelledby="landing-process-title" sx={{ p: { xs: 6, md: 10 } }}><Typography id="landing-process-title" component="h2" variant="h3">How it works</Typography><Grid container spacing={5} sx={{ mt: 4 }}>{steps.map((step, index) => <Grid size={{ xs: 12, sm: 6, lg: 3 }} key={step}><Typography variant="overline" color="primary.main" sx={{ fontWeight: 850 }}>STEP {index + 1}</Typography><Typography component="h3" variant="h6" sx={{ mt: 1 }}>{step}</Typography></Grid>)}</Grid></Paper>
  </Stack>
}
