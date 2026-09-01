import { AppBar, Badge, Box, Button, Container, IconButton, Stack, Toolbar, Typography } from '@mui/material'
import NotificationsNoneRoundedIcon from '@mui/icons-material/NotificationsNoneRounded'
import { Link, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function AppShell() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const resident = user?.roles.includes('Resident')
  const admin = user?.roles.includes('SystemAdministrator')
  return <>
    <AppBar position="sticky" elevation={0} sx={{ borderBottom: '1px solid', borderColor: 'rgba(255,255,255,.15)' }}>
      <Toolbar><Typography component={Link} to="/" variant="h6" color="inherit" sx={{ textDecoration: 'none', fontWeight: 900 }}>CivicFlow</Typography>
        <Stack direction="row" spacing={1} sx={{ ml: 4, flexGrow: 1, display: { xs: 'none', md: 'flex' } }}>
          {user && <Button color="inherit" component={Link} to={resident ? '/requests' : '/cases'}>{resident ? 'My requests' : 'Case queue'}</Button>}
          {resident && <Button color="inherit" component={Link} to="/requests/new">Submit request</Button>}
          {user && !resident && <Button color="inherit" component={Link} to="/dashboard">Dashboard</Button>}
          {admin && <Button color="inherit" component={Link} to="/admin">Admin</Button>}
          {(admin || user?.roles.includes('TeamManager')) && <Button color="inherit" component={Link} to="/admin/audit-log">Audit log</Button>}
        </Stack>
        {user ? <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <IconButton color="inherit" aria-label="Notifications" component={Link} to="/notifications"><Badge variant="dot" color="warning"><NotificationsNoneRoundedIcon /></Badge></IconButton>
          <Typography variant="body2" sx={{ display: { xs: 'none', sm: 'block' } }}>{user.firstName} {user.lastName}</Typography>
          <Button color="inherit" onClick={() => { logout(); navigate('/') }}>Sign out</Button>
        </Stack> : <Button color="inherit" component={Link} to="/login">Sign in</Button>}
      </Toolbar>
    </AppBar>
    <Box component="main" sx={{ minHeight: 'calc(100vh - 64px)', py: { xs: 3, md: 5 } }}><Container maxWidth="lg"><Outlet /></Container></Box>
  </>
}
