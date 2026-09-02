import { describe, expect, it } from 'vitest'
import { getNavigationItems } from './navigation'

const labels = (roles: string[]) => getNavigationItems(roles).map(item => item.label)

describe('role-projected navigation', () => {
  it('exposes only resident workflow entries', () => expect(labels(['Resident'])).toEqual(['My requests', 'Submit request']))
  it('excludes management entries for officers', () => expect(labels(['CaseOfficer'])).toEqual(['Case queue', 'Dashboard']))
  it('adds only authorised management entries', () => {
    expect(labels(['TeamManager'])).toEqual(['Case queue', 'Dashboard', 'Audit log'])
    expect(labels(['SystemAdministrator'])).toEqual(['Case queue', 'Dashboard', 'Admin', 'Audit log'])
  })
})
