import { describe, expect, it } from 'vitest'
import viewContent from '@/views/RequestDetailView.vue?raw'
import {
  ACTIVE_BLOCKER_GUARD_MESSAGE,
  checkHasActiveBlockers,
  filterActiveBlockers,
  filterClearedBlockers,
  isCompleteRequestDisabled,
  isStartWorkDisabled,
  resolveActiveBlockersCount,
  resolveCompleteRequestTooltip,
  resolveStartWorkTooltip,
} from '@/views/RequestDetailView.vue'
import RequestDetailView from '@/views/RequestDetailView.vue'
import type { RequestBlocker } from '@/api/requests'

describe('SCR-REQ-003: Request Detail Blockers & Action Invariant Guards (CR-032)', () => {
  const sampleActiveBlocker: RequestBlocker = {
    id: 'blocker-1',
    requestId: 'req-100',
    reason: 'Waiting for vendor firewall clearance',
    createdByPersonId: 'person-1',
    createdByName: 'John Doe',
    createdAtUtc: '2026-10-15T09:00:00Z',
    isCleared: false,
    clearedAtUtc: null,
    clearedByPersonId: null,
    clearedByName: null,
    clearanceReason: null,
  }

  const sampleClearedBlocker: RequestBlocker = {
    id: 'blocker-2',
    requestId: 'req-100',
    reason: 'Database port blocked by network policy',
    createdByPersonId: 'person-2',
    createdByName: 'Jane Smith',
    createdAtUtc: '2026-10-14T08:00:00Z',
    isCleared: true,
    clearedAtUtc: '2026-10-14T11:30:00Z',
    clearedByPersonId: 'person-3',
    clearedByName: 'Admin Bob',
    clearanceReason: 'Port 1433 opened in security group rule',
  }

  describe('Component Definition', () => {
    it('exports a valid Vue component for RequestDetailView', () => {
      expect(RequestDetailView).toBeDefined()
      expect(typeof RequestDetailView).toBe('object')
    })
  })

  describe('Active & Cleared Blockers Filtering', () => {
    it('filterActiveBlockers returns only uncleared blockers', () => {
      const blockers: RequestBlocker[] = [sampleActiveBlocker, sampleClearedBlocker]
      const active = filterActiveBlockers(blockers)
      expect(active).toHaveLength(1)
      expect(active[0].id).toBe('blocker-1')
      expect(active[0].isCleared).toBe(false)
    })

    it('filterActiveBlockers handles null or undefined blockers array safely', () => {
      expect(filterActiveBlockers(null)).toEqual([])
      expect(filterActiveBlockers(undefined)).toEqual([])
      expect(filterActiveBlockers([])).toEqual([])
    })

    it('filterClearedBlockers returns only cleared blockers', () => {
      const blockers: RequestBlocker[] = [sampleActiveBlocker, sampleClearedBlocker]
      const cleared = filterClearedBlockers(blockers)
      expect(cleared).toHaveLength(1)
      expect(cleared[0].id).toBe('blocker-2')
      expect(cleared[0].isCleared).toBe(true)
      expect(cleared[0].clearanceReason).toBe('Port 1433 opened in security group rule')
    })

    it('filterClearedBlockers handles null or undefined blockers array safely', () => {
      expect(filterClearedBlockers(null)).toEqual([])
      expect(filterClearedBlockers(undefined)).toEqual([])
      expect(filterClearedBlockers([])).toEqual([])
    })
  })

  describe('Active Blockers Count & Existence Checks', () => {
    it('resolveActiveBlockersCount returns explicit activeBlockersCount when provided', () => {
      expect(resolveActiveBlockersCount(3, [])).toBe(3)
      expect(resolveActiveBlockersCount(0, [sampleActiveBlocker])).toBe(0)
    })

    it('resolveActiveBlockersCount calculates count from blockers when activeBlockersCount is null or undefined', () => {
      expect(resolveActiveBlockersCount(null, [sampleActiveBlocker, sampleClearedBlocker])).toBe(1)
      expect(resolveActiveBlockersCount(undefined, [sampleActiveBlocker, sampleActiveBlocker])).toBe(2)
      expect(resolveActiveBlockersCount(undefined, [sampleClearedBlocker])).toBe(0)
      expect(resolveActiveBlockersCount(null, null)).toBe(0)
    })

    it('checkHasActiveBlockers returns true when count is positive', () => {
      expect(checkHasActiveBlockers(2, [])).toBe(true)
      expect(checkHasActiveBlockers(null, [sampleActiveBlocker])).toBe(true)
    })

    it('checkHasActiveBlockers returns false when count is zero or no blockers exist', () => {
      expect(checkHasActiveBlockers(0, [])).toBe(false)
      expect(checkHasActiveBlockers(null, [sampleClearedBlocker])).toBe(false)
      expect(checkHasActiveBlockers(null, null)).toBe(false)
    })
  })

  describe('Lifecycle Action Invariant Guards Disabling (Architecture §4 TD-008)', () => {
    describe('isStartWorkDisabled', () => {
      it('disables Start/Resume Work button whenever hasActiveBlockers is true, even for assigned owner', () => {
        expect(isStartWorkDisabled(true, false, true)).toBe(true)
      })

      it('disables Start/Resume Work button when user is not assigned owner, even without blockers', () => {
        expect(isStartWorkDisabled(false, false, false)).toBe(true)
      })

      it('disables Start/Resume Work button while submitting an action', () => {
        expect(isStartWorkDisabled(true, true, false)).toBe(true)
      })

      it('enables Start/Resume Work button only when assigned owner, not submitting, and no active blockers', () => {
        expect(isStartWorkDisabled(true, false, false)).toBe(false)
      })
    })

    describe('isCompleteRequestDisabled', () => {
      it('disables Complete Request button whenever hasActiveBlockers is true, even with all subtasks complete', () => {
        expect(isCompleteRequestDisabled(false, false, true)).toBe(true)
      })

      it('disables Complete Request button when unfinished subtasks remain, even without blockers', () => {
        expect(isCompleteRequestDisabled(false, true, false)).toBe(true)
      })

      it('disables Complete Request button when both active blockers and unfinished subtasks exist', () => {
        expect(isCompleteRequestDisabled(false, true, true)).toBe(true)
      })

      it('disables Complete Request button while submitting an action', () => {
        expect(isCompleteRequestDisabled(true, false, false)).toBe(true)
      })

      it('enables Complete Request button only when no active blockers, no unfinished subtasks, and not submitting', () => {
        expect(isCompleteRequestDisabled(false, false, false)).toBe(false)
      })
    })
  })

  describe('Invariant Guard Tooltip & Alert Text (CR-032 TD-008)', () => {
    it('ACTIVE_BLOCKER_GUARD_MESSAGE matches exact required text', () => {
      expect(ACTIVE_BLOCKER_GUARD_MESSAGE).toBe(
        'Cannot resume work or complete request while active blockers remain.',
      )
    })

    describe('resolveStartWorkTooltip', () => {
      it('returns blocker guard message when active blockers exist', () => {
        expect(resolveStartWorkTooltip(true, true, false)).toBe(ACTIVE_BLOCKER_GUARD_MESSAGE)
        expect(resolveStartWorkTooltip(true, true, true)).toBe(ACTIVE_BLOCKER_GUARD_MESSAGE)
        expect(resolveStartWorkTooltip(true, false, true)).toBe(ACTIVE_BLOCKER_GUARD_MESSAGE)
      })

      it('returns non-owner message when not assigned owner and no blockers exist', () => {
        expect(resolveStartWorkTooltip(false, false, false)).toBe(
          'Only the assigned owner can start work on this request.',
        )
        expect(resolveStartWorkTooltip(false, false, true)).toBe(
          'Only the assigned owner can resume work on this request.',
        )
      })

      it('returns standard action tooltip when eligible', () => {
        expect(resolveStartWorkTooltip(false, true, false)).toBe('Start work')
        expect(resolveStartWorkTooltip(false, true, true)).toBe('Resume work')
      })
    })

    describe('resolveCompleteRequestTooltip', () => {
      it('returns blocker guard message when active blockers exist', () => {
        expect(resolveCompleteRequestTooltip(true, false)).toBe(ACTIVE_BLOCKER_GUARD_MESSAGE)
        expect(resolveCompleteRequestTooltip(true, true)).toBe(ACTIVE_BLOCKER_GUARD_MESSAGE)
      })

      it('returns subtasks requirement message when unfinished subtasks exist and no blockers', () => {
        expect(resolveCompleteRequestTooltip(false, true)).toBe(
          'All sub-tasks must be completed or removed before completing the request',
        )
      })

      it('returns standard complete tooltip when eligible', () => {
        expect(resolveCompleteRequestTooltip(false, false)).toBe('Complete request')
      })
    })
  })

  describe('Template & Modal Structural Verification (Architecture §4 TD-008, SCR-REQ-003)', () => {
    describe('Blockers Card Template Rendering', () => {
      it('includes the data-testid="request-blockers-card" container', () => {
        expect(viewContent).toContain('data-testid="request-blockers-card"')
      })

      it('includes the data-testid="blockers-count-badge" badge', () => {
        expect(viewContent).toContain('data-testid="blockers-count-badge"')
      })

      it('includes the data-testid="add-blocker-btn" button', () => {
        expect(viewContent).toContain('data-testid="add-blocker-btn"')
      })

      it('includes the data-testid="clear-blocker-btn" button', () => {
        expect(viewContent).toContain('data-testid="clear-blocker-btn"')
      })

      it('includes active blockers list and metadata hooks', () => {
        expect(viewContent).toContain('data-testid="active-blockers-list"')
        expect(viewContent).toContain('data-testid="active-blocker-item"')
        expect(viewContent).toContain('data-testid="blocker-reason"')
        expect(viewContent).toContain('data-testid="blocker-reporter"')
        expect(viewContent).toContain('data-testid="blocker-timestamp"')
      })

      it('includes cleared blockers history and collapsible accordion hooks', () => {
        expect(viewContent).toContain('data-testid="cleared-blockers-history"')
        expect(viewContent).toContain('data-testid="toggle-cleared-blockers-btn"')
        expect(viewContent).toContain('data-testid="cleared-blockers-count"')
        expect(viewContent).toContain('data-testid="cleared-blockers-list"')
        expect(viewContent).toContain('data-testid="cleared-blocker-reason"')
        expect(viewContent).toContain('data-testid="cleared-blocker-reporter"')
        expect(viewContent).toContain('data-testid="cleared-blocker-resolver"')
        expect(viewContent).toContain('data-testid="cleared-blocker-notes"')
        expect(viewContent).toContain('data-testid="cleared-blocker-resolved-at"')
      })
    })

    describe('Add Blocker Modal Dialog', () => {
      it('includes data-testid="add-blocker-modal" dialog container', () => {
        expect(viewContent).toContain('data-testid="add-blocker-modal"')
      })

      it('includes required textarea with data-testid="add-blocker-reason-input" and maxlength 1000', () => {
        expect(viewContent).toContain('data-testid="add-blocker-reason-input"')
        expect(viewContent).toContain('maxlength="1000"')
      })

      it('includes submit button with data-testid="submit-add-blocker-btn"', () => {
        expect(viewContent).toContain('data-testid="submit-add-blocker-btn"')
      })

      it('includes cancel and close buttons', () => {
        expect(viewContent).toContain('data-testid="cancel-add-blocker-btn"')
        expect(viewContent).toContain('data-testid="close-add-blocker-modal-btn"')
      })
    })

    describe('Clear Blocker Modal Dialog', () => {
      it('includes data-testid="clear-blocker-modal" dialog container', () => {
        expect(viewContent).toContain('data-testid="clear-blocker-modal"')
      })

      it('includes optional clearance reason textarea with data-testid="clear-blocker-reason-input"', () => {
        expect(viewContent).toContain('data-testid="clear-blocker-reason-input"')
      })

      it('includes confirm button with data-testid="confirm-clear-blocker-btn"', () => {
        expect(viewContent).toContain('data-testid="confirm-clear-blocker-btn"')
      })

      it('includes cancel and close buttons', () => {
        expect(viewContent).toContain('data-testid="cancel-clear-blocker-btn"')
        expect(viewContent).toContain('data-testid="close-clear-blocker-modal-btn"')
      })
    })

    describe('Disabled Button Guards & Warning Alert', () => {
      it('includes active blockers warning banner data-testid="active-blockers-warning"', () => {
        expect(viewContent).toContain('data-testid="active-blockers-warning"')
      })

      it('guards Start / Resume Work button with hasActiveBlockers check in :disabled binding', () => {
        expect(viewContent).toContain('data-testid="start-work-action-button"')
        expect(viewContent).toContain(':disabled="!isAssignedOwner || isSubmittingAction || hasActiveBlockers"')
      })

      it('guards Complete Request button with hasActiveBlockers check in :disabled binding', () => {
        expect(viewContent).toContain('data-testid="complete-request-action-button"')
        expect(viewContent).toContain(':disabled="isSubmittingAction || hasUnfinishedSubTasks || hasActiveBlockers"')
      })
    })
  })
})

