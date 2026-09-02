export type NavigationItem = { label: string; to: string }

export function getNavigationItems(roles: string[]): NavigationItem[] {
  const resident = roles.includes('Resident')
  const admin = roles.includes('SystemAdministrator')
  const manager = roles.includes('TeamManager')
  if (resident) return [{ label: 'My requests', to: '/requests' }, { label: 'Submit request', to: '/requests/new' }]
  const items: NavigationItem[] = [{ label: 'Case queue', to: '/cases' }, { label: 'Dashboard', to: '/dashboard' }]
  if (admin) items.push({ label: 'Admin', to: '/admin' })
  if (admin || manager) items.push({ label: 'Audit log', to: '/admin/audit-log' })
  return items
}
