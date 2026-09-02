import { useState } from 'react'
import { AppBar, Badge, Box, Button, Container, Divider, Drawer, IconButton, List, ListItemButton, ListItemText, Menu, MenuItem, Stack, Toolbar, Typography } from '@mui/material'
import CloseRoundedIcon from '@mui/icons-material/CloseRounded'
import MenuRoundedIcon from '@mui/icons-material/MenuRounded'
import NotificationsNoneRoundedIcon from '@mui/icons-material/NotificationsNoneRounded'
import LogoutRoundedIcon from '@mui/icons-material/LogoutRounded'
import AccountCircleRoundedIcon from '@mui/icons-material/AccountCircleRounded'
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { civicTokens } from '../theme'
import { SkipLink } from './ui'
import { getNavigationItems } from './navigation'

export function AppShell() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [mobileOpen, setMobileOpen] = useState(false)
  const [accountAnchor, setAccountAnchor] = useState<HTMLElement | null>(null)
  const navigation = user ? getNavigationItems(user.roles) : []
  const signOut = () => { setMobileOpen(false); setAccountAnchor(null); logout(); navigate('/') }
  return <>
    <SkipLink />
    <AppBar position="sticky" elevation={0} sx={{ borderBottom: '1px solid', borderColor: 'rgba(255,255,255,.18)' }}>
      <Toolbar sx={{ maxWidth: civicTokens.layout.maxWidth, width: '100%', mx: 'auto', px: { xs: 4, sm: 6 } }}>
        {user && <IconButton color="inherit" aria-label="Open main menu" aria-controls="mobile-navigation" aria-expanded={mobileOpen} onClick={() => setMobileOpen(true)} sx={{ display: { md: 'none' }, mr: 2 }}><MenuRoundedIcon /></IconButton>}
        <Typography component={Link} to="/" variant="h6" sx={{ color: 'inherit', textDecoration: 'none', fontWeight: 900 }}>CivicFlow</Typography>
        <Stack component="nav" aria-label="Main navigation" direction="row" spacing={1} sx={{ ml: 4, flexGrow: 1, display: { xs: 'none', md: 'flex' } }}>
          {navigation.map(item => <Button key={item.to} color="inherit" component={Link} to={item.to} aria-current={location.pathname === item.to ? 'page' : undefined} sx={{ bgcolor: location.pathname === item.to ? 'rgba(255,255,255,.13)' : undefined }}>{item.label}</Button>)}
        </Stack>
        <Box sx={{ flexGrow: { xs: 1, md: 0 } }} />
        {user ? <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <IconButton color="inherit" aria-label="Notifications" component={Link} to="/notifications"><Badge variant="dot" color="warning"><NotificationsNoneRoundedIcon /></Badge></IconButton>
          <Button color="inherit" startIcon={<AccountCircleRoundedIcon />} aria-controls={accountAnchor ? 'account-menu' : undefined} aria-haspopup="menu" aria-expanded={accountAnchor ? 'true' : undefined} onClick={event => setAccountAnchor(event.currentTarget)} sx={{ display: { xs: 'none', md: 'inline-flex' } }}>{user.firstName} {user.lastName}</Button>
        </Stack> : <Button color="inherit" component={Link} to="/login">Sign in</Button>}
      </Toolbar>
    </AppBar>
    <Menu id="account-menu" anchorEl={accountAnchor} open={Boolean(accountAnchor)} onClose={() => setAccountAnchor(null)} slotProps={{ list: { 'aria-label': 'Account menu' } }}><MenuItem component={Link} to="/profile" onClick={() => setAccountAnchor(null)}>Profile &amp; security</MenuItem><MenuItem onClick={signOut}>Sign out</MenuItem></Menu>
    <Drawer id="mobile-navigation" anchor="left" open={mobileOpen} onClose={() => setMobileOpen(false)} ModalProps={{ keepMounted: true }} slotProps={{ paper: { sx: { width: 'min(88vw, 340px)' } } }}>
      <Stack sx={{ height: '100%' }}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', px: 5, py: 4 }}><Typography variant="h6">Navigation</Typography><IconButton aria-label="Close main menu" onClick={() => setMobileOpen(false)}><CloseRoundedIcon /></IconButton></Stack>
        <Divider />
        <List component="nav" aria-label="Mobile navigation" sx={{ px: 2, py: 3, flexGrow: 1 }}>
          {navigation.map(item => <ListItemButton key={item.to} component={Link} to={item.to} selected={location.pathname === item.to} onClick={() => setMobileOpen(false)} sx={{ minHeight: 48, borderRadius: 2 }}><ListItemText primary={item.label} /></ListItemButton>)}
          <ListItemButton component={Link} to="/notifications" selected={location.pathname === '/notifications'} onClick={() => setMobileOpen(false)} sx={{ minHeight: 48, borderRadius: 2 }}><ListItemText primary="Notifications" /></ListItemButton>
          <ListItemButton component={Link} to="/profile" selected={location.pathname === '/profile'} onClick={() => setMobileOpen(false)} sx={{ minHeight: 48, borderRadius: 2 }}><ListItemText primary="Profile & security" /></ListItemButton>
        </List>
        <Divider />
        {user && <Button startIcon={<LogoutRoundedIcon />} onClick={signOut} sx={{ m: 4, justifyContent: 'flex-start' }}>Sign out</Button>}
      </Stack>
    </Drawer>
    <Box component="main" id="main-content" tabIndex={-1} sx={{ minHeight: 'calc(100vh - 64px)', py: { xs: 6, md: 10 }, outline: 'none' }}>
      <Container maxWidth={false} sx={{ maxWidth: civicTokens.layout.maxWidth, px: { xs: 4, sm: 6, lg: 8 } }}><Outlet /></Container>
    </Box>
  </>
}
