import { useEffect, useState } from 'react'
import { Alert, Box, Divider, Stack, TextField, Typography } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { profileApi, type Profile } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { AccountStatus, RoleLabel } from '../components/admin'
import { PasswordField } from '../components/resident'
import { ErrorState, FormActions, PageHeader, PageLoading, SectionCard } from '../components/ui'
import { normaliseName, validateFullName, validateNewPassword } from '../components/profileValidation'

export function ProfilePage() {
  const { user, updateUser, logout } = useAuth(); const navigate = useNavigate()
  const [profile, setProfile] = useState<Profile | null>(null); const [fullName, setFullName] = useState(''); const [loading, setLoading] = useState(true)
  const [profileBusy, setProfileBusy] = useState(false); const [passwordBusy, setPasswordBusy] = useState(false); const [error, setError] = useState(''); const [notice, setNotice] = useState('')
  const [passwords, setPasswords] = useState({ current: '', next: '', confirm: '' })
  const applyProfile = (result: Profile) => { setProfile(result); setFullName(result.fullName) }
  const load = async () => { setLoading(true); setError(''); try { applyProfile(await profileApi.get()) } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load your profile.') } finally { setLoading(false) } }
  useEffect(() => { let active = true; void profileApi.get().then(result => { if (active) { setProfile(result); setFullName(result.fullName) } }).catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'Unable to load your profile.') }).finally(() => { if (active) setLoading(false) }); return () => { active = false } }, [])
  const nameError = validateFullName(fullName); const nameUnchanged = profile ? normaliseName(fullName) === profile.fullName : true
  const saveProfile = async () => { if (!profile || profileBusy || nameError) return; setProfileBusy(true); setError(''); try { const result = await profileApi.update(fullName, profile.version); setProfile(result); setFullName(result.fullName); if (user) updateUser({ ...user, firstName: result.firstName, lastName: result.lastName }); setNotice(nameUnchanged ? 'Your profile is already up to date.' : 'Your name has been updated.') } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to update your profile.') } finally { setProfileBusy(false) } }
  const passwordError = validateNewPassword(passwords.next); const passwordsMatch = passwords.next === passwords.confirm
  const changePassword = async () => { if (passwordBusy || !passwords.current || passwordError || !passwordsMatch) return; setPasswordBusy(true); setError(''); try { await profileApi.changePassword(passwords.current, passwords.next); logout(); navigate('/login?reason=password-changed', { replace: true }) } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to change your password.'); setPasswordBusy(false) } }
  if (loading) return <PageLoading label="Loading profile" />
  if (!profile) return <ErrorState title="Unable to load profile" message={error || 'Your profile is unavailable.'} retry={() => void load()} />
  return <Stack spacing={5}><PageHeader title="Profile & security" description="Review your account details, update your name or change your password." />
    {error && <ErrorState title="Account update failed" message={error} retry={() => setError('')} />}{notice && <Alert severity="success" onClose={() => setNotice('')} role="status">{notice}</Alert>}
    <SectionCard title="Profile" description="Your email, role and account status are managed by CivicFlow administrators."><Stack spacing={4} component="form" onSubmit={event => { event.preventDefault(); void saveProfile() }} aria-busy={profileBusy}>
      <TextField label="Full name" required value={fullName} onChange={event => setFullName(event.target.value)} error={Boolean(nameError)} helperText={nameError || `${normaliseName(fullName).length} / 150 characters`} slotProps={{ htmlInput: { maxLength: 150 } }} />
      <Box component="dl" sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: 'minmax(120px, 180px) 1fr' }, gap: 2, m: 0, '& dt': { color: 'text.secondary' }, '& dd': { m: 0, overflowWrap: 'anywhere' } }}><Typography component="dt">Email</Typography><Typography component="dd">{profile.email}</Typography><Typography component="dt">Role</Typography><Box component="dd"><Stack direction="row" spacing={1} useFlexGap sx={{ flexWrap: 'wrap' }}>{profile.roles.map(role => <RoleLabel role={role} key={role} />)}</Stack></Box><Typography component="dt">Status</Typography><Box component="dd"><AccountStatus active={profile.isActive} /></Box></Box>
      <FormActions primaryLabel={profileBusy ? 'Saving…' : 'Save profile'} primaryProps={{ type: 'submit', disabled: profileBusy || Boolean(nameError) }} />
    </Stack></SectionCard>
    <SectionCard title="Change password" description="Changing your password signs you out of every CivicFlow session."><Stack spacing={4} component="form" onSubmit={event => { event.preventDefault(); void changePassword() }} aria-busy={passwordBusy}>
      <PasswordField label="Current password" autoComplete="current-password" required value={passwords.current} onChange={event => setPasswords({ ...passwords, current: event.target.value })} />
      <Divider />
      <PasswordField label="New password" autoComplete="new-password" required value={passwords.next} onChange={event => setPasswords({ ...passwords, next: event.target.value })} error={Boolean(passwords.next && passwordError)} helperText={passwordError || 'At least 10 characters with uppercase, lowercase, a number and a symbol.'} />
      <PasswordField label="Confirm new password" autoComplete="new-password" required value={passwords.confirm} onChange={event => setPasswords({ ...passwords, confirm: event.target.value })} error={Boolean(passwords.confirm && !passwordsMatch)} helperText={passwords.confirm && !passwordsMatch ? 'Passwords do not match.' : ' '} />
      <FormActions primaryLabel={passwordBusy ? 'Changing password…' : 'Change password'} primaryProps={{ type: 'submit', disabled: passwordBusy || !passwords.current || Boolean(passwordError) || !passwordsMatch }} />
    </Stack></SectionCard>
  </Stack>
}
