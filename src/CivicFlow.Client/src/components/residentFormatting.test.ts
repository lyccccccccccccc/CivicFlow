import { describe, expect, it } from 'vitest'
import { notificationGroupLabel, residentServiceMessage } from './residentFormatting'

describe('resident-friendly formatting', () => {
  it('uses natural workflow language without raw enum values', () => expect(residentServiceMessage({ status: 'WaitingForResident', slaState: 'OnTrack' })).toBe('The service team is waiting for your reply'))
  it('groups notifications by resident-friendly dates', () => { const now = new Date('2026-09-02T12:00:00Z'); expect(notificationGroupLabel('2026-09-02T01:00:00Z', now)).toBe('Today'); expect(notificationGroupLabel('2026-09-01T01:00:00Z', now)).toBe('Yesterday') })
})
