import { describe, expect, it } from 'vitest'
import { validateFullName, validateNewPassword } from '../components/profileValidation'

describe('profile validation', () => {
  it('validates trimmed full names', () => {
    expect(validateFullName('   ')).toMatch(/between 2 and 150/i)
    expect(validateFullName(' A ')).toMatch(/between 2 and 150/i)
    expect(validateFullName(' Riley   Resident ')).toBe('')
    expect(validateFullName('A'.repeat(151))).toMatch(/between 2 and 150/i)
  })

  it('uses the registration password policy', () => {
    expect(validateNewPassword('short')).toMatch(/at least 10/i)
    expect(validateNewPassword('alllowercase1!')).toMatch(/uppercase/i)
    expect(validateNewPassword('Valid-Password1!')).toBe('')
  })
})
