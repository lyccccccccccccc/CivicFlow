import { useState, type FormEvent, type ReactNode } from 'react'
import { Alert, Button, Paper, Stack, TextField, Typography } from '@mui/material'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function LoginPage() {
  const { login } = useAuth(); const navigate = useNavigate()
  const [email, setEmail] = useState('resident@civicflow.local'); const [password, setPassword] = useState('REDACTED_HISTORICAL_DEVELOPMENT_SECRET')
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  const submit = async (event: FormEvent) => { event.preventDefault(); setBusy(true); setError(''); try { await login(email, password); navigate('/home') } catch (e) { setError(e instanceof Error ? e.message : 'Sign in failed') } finally { setBusy(false) } }
  return <AuthCard title="Welcome back" subtitle="Sign in to manage or track requests."><Stack component="form" spacing={2} onSubmit={submit}>
    {error && <Alert severity="error">{error}</Alert>}<TextField label="Email" type="email" value={email} onChange={e => setEmail(e.target.value)} required autoFocus /><TextField label="Password" type="password" value={password} onChange={e => setPassword(e.target.value)} required />
    <Button type="submit" variant="contained" size="large" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</Button>
    <Typography variant="body2" sx={{ textAlign: 'center' }}>New resident? <Link to="/register">Create an account</Link></Typography>
    <Alert severity="info">Demo accounts use <strong>REDACTED_HISTORICAL_DEVELOPMENT_SECRET</strong>. Try resident@, officer@, manager@ or admin@civicflow.local.</Alert>
  </Stack></AuthCard>
}

export function RegisterPage() {
  const { register } = useAuth(); const navigate = useNavigate()
  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', password: '' }); const [error, setError] = useState(''); const [busy, setBusy] = useState(false)
  const submit = async (event: FormEvent) => { event.preventDefault(); setBusy(true); setError(''); try { await register(form); navigate('/requests') } catch (e) { setError(e instanceof Error ? e.message : 'Registration failed') } finally { setBusy(false) } }
  return <AuthCard title="Create resident account" subtitle="Submit and track community service requests."><Stack component="form" spacing={2} onSubmit={submit}>
    {error && <Alert severity="error">{error}</Alert>}<Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}><TextField label="First name" value={form.firstName} onChange={e => setForm({ ...form, firstName: e.target.value })} required /><TextField label="Last name" value={form.lastName} onChange={e => setForm({ ...form, lastName: e.target.value })} required /></Stack>
    <TextField label="Email" type="email" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} required /><TextField label="Password" type="password" helperText="At least 10 characters with upper/lowercase, number and symbol" value={form.password} onChange={e => setForm({ ...form, password: e.target.value })} required />
    <Button type="submit" variant="contained" size="large" disabled={busy}>Create account</Button><Typography variant="body2" sx={{ textAlign: 'center' }}>Already registered? <Link to="/login">Sign in</Link></Typography>
  </Stack></AuthCard>
}

function AuthCard({ title, subtitle, children }: { title: string; subtitle: string; children: ReactNode }) {
  return <Paper sx={{ maxWidth: 520, mx: 'auto', p: { xs: 3, sm: 5 } }}><Typography variant="h4" sx={{ fontWeight: 850 }}>{title}</Typography><Typography color="text.secondary" sx={{ mt: 1, mb: 3 }}>{subtitle}</Typography>{children}</Paper>
}
