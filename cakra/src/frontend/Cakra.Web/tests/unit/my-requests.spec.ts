import { describe, expect, it } from 'vitest'
import {
  formatShortId,
  isRequestBlocked,
  matchesRequestId,
  normalizeSearchQuery,
  resolveActiveBlockerCallout,
} from '@/views/MyRequestsView.vue'

describe('MyRequestsView - Short ID Formatting & Search Normalization (CR-031)', () => {
  describe('formatShortId', () => {
    it('formats a standard 36-character GUID to # followed by the first 8 characters', () => {
      const guid = 'd7e9b891-62d2-43bb-a579-37332cfa8a25'
      expect(formatShortId(guid)).toBe('#d7e9b891')
    })

    it('formats various valid GUID formats to exactly 8 characters with hash prefix', () => {
      expect(formatShortId('00000000-0000-0000-0000-000000000000')).toBe('#00000000')
      expect(formatShortId('e2a4b6c8-1111-2222-3333-444455556666')).toBe('#e2a4b6c8')
      expect(formatShortId('ABCDEF01-2345-6789-abcd-ef0123456789')).toBe('#ABCDEF01')
    })

    it('handles shorter string lengths safely', () => {
      expect(formatShortId('abc')).toBe('#abc')
      expect(formatShortId('1234567')).toBe('#1234567')
      expect(formatShortId('12345678')).toBe('#12345678')
    })

    it('handles null safely by returning an empty string', () => {
      expect(formatShortId(null)).toBe('')
    })

    it('handles undefined safely by returning an empty string', () => {
      expect(formatShortId(undefined)).toBe('')
    })

    it('handles empty string safely by returning an empty string', () => {
      expect(formatShortId('')).toBe('')
    })
  })

  describe('normalizeSearchQuery', () => {
    it('strips leading # and trims whitespace', () => {
      expect(normalizeSearchQuery('#d7e9b891')).toBe('d7e9b891')
      expect(normalizeSearchQuery('  #d7e9b891  ')).toBe('d7e9b891')
      expect(normalizeSearchQuery('#  d7e9b891')).toBe('d7e9b891')
    })

    it('converts search input to lowercase', () => {
      expect(normalizeSearchQuery('#D7E9B891')).toBe('d7e9b891')
      expect(normalizeSearchQuery('D7E9B891')).toBe('d7e9b891')
    })

    it('preserves search strings without leading #', () => {
      expect(normalizeSearchQuery('d7e9b891')).toBe('d7e9b891')
      expect(normalizeSearchQuery('  incident ticket  ')).toBe('incident ticket')
    })

    it('returns empty string when query is only # or whitespace', () => {
      expect(normalizeSearchQuery('#')).toBe('')
      expect(normalizeSearchQuery('  #  ')).toBe('')
      expect(normalizeSearchQuery('')).toBe('')
      expect(normalizeSearchQuery('   ')).toBe('')
    })

    it('handles null and undefined safely', () => {
      expect(normalizeSearchQuery(null)).toBe('')
      expect(normalizeSearchQuery(undefined)).toBe('')
    })
  })

  describe('matchesRequestId', () => {
    const rawGuid = 'd7e9b891-62d2-43bb-a579-37332cfa8a25'

    it('matches raw GUID when searched with leading # and 8-character prefix', () => {
      expect(matchesRequestId(rawGuid, '#d7e9b891')).toBe(true)
    })

    it('matches raw GUID when searched without leading # and 8-character prefix', () => {
      expect(matchesRequestId(rawGuid, 'd7e9b891')).toBe(true)
    })

    it('matches raw GUID case-insensitively with leading #', () => {
      expect(matchesRequestId(rawGuid, '#D7E9B891')).toBe(true)
      expect(matchesRequestId('D7E9B891-62D2-43BB-A579-37332CFA8A25', '#d7e9b891')).toBe(true)
    })

    it('matches raw GUID with partial short query', () => {
      expect(matchesRequestId(rawGuid, '#d7e9')).toBe(true)
      expect(matchesRequestId(rawGuid, 'd7e9')).toBe(true)
    })

    it('matches raw GUID when full GUID is searched with or without #', () => {
      expect(matchesRequestId(rawGuid, `#${rawGuid}`)).toBe(true)
      expect(matchesRequestId(rawGuid, rawGuid)).toBe(true)
    })

    it('does not match an unrelated GUID', () => {
      expect(matchesRequestId(rawGuid, '#99999999')).toBe(false)
      expect(matchesRequestId(rawGuid, 'xyz')).toBe(false)
    })

    it('returns false when search query is empty or only #', () => {
      expect(matchesRequestId(rawGuid, '')).toBe(false)
      expect(matchesRequestId(rawGuid, '#')).toBe(false)
      expect(matchesRequestId(rawGuid, '   ')).toBe(false)
      expect(matchesRequestId(rawGuid, null)).toBe(false)
      expect(matchesRequestId(rawGuid, undefined)).toBe(false)
    })

    it('returns false when rawId is null or empty', () => {
      expect(matchesRequestId(null, '#d7e9b891')).toBe(false)
      expect(matchesRequestId('', '#d7e9b891')).toBe(false)
      expect(matchesRequestId(undefined, '#d7e9b891')).toBe(false)
    })
  })

  describe('Assigned Requests search filtering integration', () => {
    const mockRequests = [
      {
        id: 'd7e9b891-62d2-43bb-a579-37332cfa8a25',
        title: 'Fix login authentication redirect',
        description: 'Users get 404 when returning from OAuth provider',
      },
      {
        id: 'a1b2c3d4-0000-1111-2222-333344445555',
        title: 'Implement database backup cron',
        description: 'Nightly backup job to S3 bucket',
      },
      {
        id: 'f8e7d6c5-9999-8888-7777-666655554444',
        title: 'Update invoice template #12',
        description: 'Change tax computation label',
      },
    ]

    function filterRequests(items: typeof mockRequests, query: string) {
      if (!query.trim()) return items
      const q = query.trim().toLowerCase()
      const cleanQ = normalizeSearchQuery(q)
      return items.filter((req) => {
        return (
          (cleanQ ? (req.id ?? '').toLowerCase().includes(cleanQ) : false) ||
          (req.title ?? '').toLowerCase().includes(q) ||
          (req.description ?? '').toLowerCase().includes(q)
        )
      })
    }

    it('filters correctly when searching by #<8-chars> prefix', () => {
      const results = filterRequests(mockRequests, '#d7e9b891')
      expect(results).toHaveLength(1)
      expect(results[0].id).toBe('d7e9b891-62d2-43bb-a579-37332cfa8a25')
    })

    it('filters correctly when searching by raw 8-chars prefix without #', () => {
      const results = filterRequests(mockRequests, 'a1b2c3d4')
      expect(results).toHaveLength(1)
      expect(results[0].id).toBe('a1b2c3d4-0000-1111-2222-333344445555')
    })

    it('filters by title keyword even if title contains a #', () => {
      const results = filterRequests(mockRequests, '#12')
      expect(results).toHaveLength(1)
      expect(results[0].title).toContain('Update invoice template #12')
    })

    it('returns all items when search input is empty or whitespace', () => {
      expect(filterRequests(mockRequests, '')).toHaveLength(3)
      expect(filterRequests(mockRequests, '   ')).toHaveLength(3)
    })

    it('returns no items when search query does not match any ID or title', () => {
      expect(filterRequests(mockRequests, '#00000000')).toHaveLength(0)
    })
  })

  describe('Assigned Requests Blocker Badges & Callouts (CR-032 / SCR-REQ-004)', () => {
    describe('isRequestBlocked', () => {
      it('returns true when activeBlockersCount > 0', () => {
        expect(isRequestBlocked({ activeBlockersCount: 1 })).toBe(true)
        expect(isRequestBlocked({ activeBlockersCount: 3 })).toBe(true)
      })

      it('returns true when hasActiveBlockers is true', () => {
        expect(isRequestBlocked({ hasActiveBlockers: true })).toBe(true)
      })

      it('returns false when activeBlockersCount is 0 and hasActiveBlockers is false or undefined', () => {
        expect(isRequestBlocked({ activeBlockersCount: 0 })).toBe(false)
        expect(isRequestBlocked({ activeBlockersCount: 0, hasActiveBlockers: false })).toBe(false)
      })

      it('returns false when request is null, undefined, or has no blocker properties', () => {
        expect(isRequestBlocked(null)).toBe(false)
        expect(isRequestBlocked(undefined)).toBe(false)
        expect(isRequestBlocked({})).toBe(false)
      })
    })

    describe('resolveActiveBlockerCallout', () => {
      it('returns latest uncleared blocker reason when blockers list is present', () => {
        const req = {
          activeBlockersCount: 1,
          blockers: [
            { reason: 'Waiting for vendor credentials', isCleared: false },
          ],
        }
        expect(resolveActiveBlockerCallout(req)).toBe('Waiting for vendor credentials')
      })

      it('returns latest uncleared blocker reason ignoring previously cleared blockers', () => {
        const req = {
          activeBlockersCount: 1,
          blockers: [
            { reason: 'Initial blocker that was cleared', isCleared: true },
            { reason: 'Second blocker awaiting QA environment', isCleared: false },
          ],
        }
        expect(resolveActiveBlockerCallout(req)).toBe('Second blocker awaiting QA environment')
      })

      it('falls back to activeBlockersCount summary when no individual blockers are listed', () => {
        const req = {
          activeBlockersCount: 2,
        }
        expect(resolveActiveBlockerCallout(req)).toBe('2 active blocker(s) impeding request progress')
      })

      it('returns empty string when request is null or undefined', () => {
        expect(resolveActiveBlockerCallout(null)).toBe('')
        expect(resolveActiveBlockerCallout(undefined)).toBe('')
      })
    })
  })
})
