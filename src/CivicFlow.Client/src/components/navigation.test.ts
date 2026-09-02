import assert from 'node:assert/strict'
import test from 'node:test'
import { getNavigationItems } from './navigation.ts'

const labels = (roles: string[]) => getNavigationItems(roles).map(item => item.label)

test('resident navigation exposes only resident workflow entries', () => {
  assert.deepEqual(labels(['Resident']), ['My requests', 'Submit request'])
})

test('officer navigation excludes management entries', () => {
  assert.deepEqual(labels(['CaseOfficer']), ['Case queue', 'Dashboard'])
})

test('manager and administrator receive only their authorised entries', () => {
  assert.deepEqual(labels(['TeamManager']), ['Case queue', 'Dashboard', 'Audit log'])
  assert.deepEqual(labels(['SystemAdministrator']), ['Case queue', 'Dashboard', 'Admin', 'Audit log'])
})
