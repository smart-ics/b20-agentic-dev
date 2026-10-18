import { describe, expect, it } from 'vitest'
import viewContent from '@/views/WorkPackageView.vue?raw'
import {
  canUserReopenWorkPackage,
  hasUnfinishedWorkPackageRequests,
  countUnfinishedWorkPackageRequests,
  isWorkPackageDeadlineExpired,
} from '@/views/WorkPackageView.vue'
import WorkPackageView from '@/views/WorkPackageView.vue'

describe('SCR-WP-001: Work Package Re-Open Capability (CR-035)', () => {
  describe('Component Definition', () => {
    it('exports a valid Vue component for WorkPackageView', () => {
      expect(WorkPackageView).toBeDefined()
      expect(typeof WorkPackageView).toBe('object')
    })
  })

  describe('Authorization Gate: canUserReopenWorkPackage', () => {
    const closedWp = {
      id: 'wp-100',
      status: 'CLOSED',
      ownerPersonId: 'owner-person-123',
    }

    it('returns false when work package is null or undefined', () => {
      expect(canUserReopenWorkPackage(null, ['Administrator'], 'owner-person-123')).toBe(false)
      expect(canUserReopenWorkPackage(null, [], null)).toBe(false)
    })

    it('returns false when package status is DRAFT or ACTIVE (only CLOSED can be re-opened)', () => {
      expect(canUserReopenWorkPackage({ status: 'DRAFT', ownerPersonId: 'p-1' }, ['Administrator'], 'p-1')).toBe(false)
      expect(canUserReopenWorkPackage({ status: 'ACTIVE', ownerPersonId: 'p-1' }, ['Management'], 'p-1')).toBe(false)
      expect(canUserReopenWorkPackage({ status: 'active', ownerPersonId: 'p-1' }, ['Administrator'], 'p-1')).toBe(false)
    })

    it('returns true when user has Administrator role', () => {
      expect(canUserReopenWorkPackage(closedWp, ['Administrator'], 'other-person-999')).toBe(true)
      expect(canUserReopenWorkPackage(closedWp, ['Viewer', 'Administrator'], null)).toBe(true)
    })

    it('returns true when user has Management role', () => {
      expect(canUserReopenWorkPackage(closedWp, ['Management'], 'other-person-999')).toBe(true)
      expect(canUserReopenWorkPackage(closedWp, ['TeamLead', 'Management'], 'random-user')).toBe(true)
    })

    it('returns true when caller is the Work Package Owner (case-insensitive person ID comparison)', () => {
      expect(canUserReopenWorkPackage(closedWp, [], 'owner-person-123')).toBe(true)
      expect(canUserReopenWorkPackage(closedWp, ['Operator'], 'OWNER-PERSON-123')).toBe(true)
      expect(canUserReopenWorkPackage({ ...closedWp, ownerPersonId: 'OWNER-UUID' }, [], 'owner-uuid')).toBe(true)
    })

    it('returns false when caller is not the owner and lacks leadership roles', () => {
      expect(canUserReopenWorkPackage(closedWp, ['Operator'], 'stranger-person-456')).toBe(false)
      expect(canUserReopenWorkPackage(closedWp, [], 'stranger-person-456')).toBe(false)
      expect(canUserReopenWorkPackage(closedWp, [], null)).toBe(false)
      expect(canUserReopenWorkPackage(closedWp, [], '')).toBe(false)
    })
  })

  describe('Eligibility Gate: hasUnfinishedWorkPackageRequests & countUnfinishedWorkPackageRequests', () => {
    it('returns false and count 0 when active scope items array is empty or null', () => {
      expect(hasUnfinishedWorkPackageRequests([])).toBe(false)
      expect(countUnfinishedWorkPackageRequests([])).toBe(0)
      expect(hasUnfinishedWorkPackageRequests(null as any)).toBe(false)
      expect(countUnfinishedWorkPackageRequests(null as any)).toBe(0)
    })

    it('returns false and count 0 when all requests are COMPLETED, CANCELLED, or REJECTED', () => {
      const scopeItems = [
        { requestId: 'r1', status: 'COMPLETED' },
        { requestId: 'r2', status: 'CANCELLED' },
        { requestId: 'r3', status: 'REJECTED' },
        { requestId: 'r4', requestStatus: 'completed' },
        { requestId: 'r5', request: { status: 'CANCELLED' } },
      ]
      expect(hasUnfinishedWorkPackageRequests(scopeItems)).toBe(false)
      expect(countUnfinishedWorkPackageRequests(scopeItems)).toBe(0)
    })

    it('returns true and correct count when at least one request remains unfinished', () => {
      const scopeItems = [
        { requestId: 'r1', status: 'COMPLETED' },
        { requestId: 'r2', status: 'IN_PROGRESS' }, // unfinished
        { requestId: 'r3', status: 'CANCELLED' },
        { requestId: 'r4', status: 'PAUSED' }, // unfinished
        { requestId: 'r5', status: 'CAPTURED' }, // unfinished
      ]
      expect(hasUnfinishedWorkPackageRequests(scopeItems)).toBe(true)
      expect(countUnfinishedWorkPackageRequests(scopeItems)).toBe(3)
    })

    it('considers requests with ASSIGNED, EVALUATING, and ACCEPTED as unfinished', () => {
      const scopeItems = [
        { requestId: 'r1', status: 'ASSIGNED' },
        { requestId: 'r2', status: 'EVALUATING' },
        { requestId: 'r3', status: 'ACCEPTED' },
      ]
      expect(hasUnfinishedWorkPackageRequests(scopeItems)).toBe(true)
      expect(countUnfinishedWorkPackageRequests(scopeItems)).toBe(3)
    })
  })

  describe('Expired Deadline Detection: isWorkPackageDeadlineExpired', () => {
    it('returns false when deadline is null, undefined, or empty', () => {
      expect(isWorkPackageDeadlineExpired(null)).toBe(false)
      expect(isWorkPackageDeadlineExpired(undefined)).toBe(false)
      expect(isWorkPackageDeadlineExpired('')).toBe(false)
    })

    it('returns true when deadline date is in the past relative to reference date', () => {
      const pastDeadline = '2026-01-15T00:00:00Z'
      const refDate = new Date('2026-10-16T12:00:00Z')
      expect(isWorkPackageDeadlineExpired(pastDeadline, refDate)).toBe(true)
    })

    it('returns false when deadline date is in the future relative to reference date', () => {
      const futureDeadline = '2026-12-31T00:00:00Z'
      const refDate = new Date('2026-10-16T12:00:00Z')
      expect(isWorkPackageDeadlineExpired(futureDeadline, refDate)).toBe(false)
    })

    it('returns false for invalid date strings', () => {
      expect(isWorkPackageDeadlineExpired('not-a-valid-date')).toBe(false)
    })
  })

  describe('Template & UI Affordance Contract Checks (SCR-WP-001, CR-035 §4 TD-006)', () => {
    describe('Action Strip Button', () => {
      it('renders Re-Open action button with data-testid="wp-reopen-button"', () => {
        expect(viewContent).toContain('data-testid="wp-reopen-button"')
      })

      it('guards Re-Open button with isSelectedClosed and canReopen conditions', () => {
        expect(viewContent).toContain('v-if="isSelectedClosed"')
        expect(viewContent).toContain('v-if="canReopen"')
      })

      it('disables the button when ineligible or submitting', () => {
        expect(viewContent).toContain(':disabled="!isReopenEligible || isSubmittingAction || isSubmittingReopen"')
      })

      it('binds tooltip with title="reopenDisabledTooltip"', () => {
        expect(viewContent).toContain(':title="reopenDisabledTooltip"')
      })

      it('triggers openReopenModal on click', () => {
        expect(viewContent).toContain('@click="openReopenModal"')
      })

      it('displays explanatory note when ineligible with data-testid="wp-reopen-ineligible-note"', () => {
        expect(viewContent).toContain('data-testid="wp-reopen-ineligible-note"')
        expect(viewContent).toContain('Permanently sealed (all requests completed)')
      })
    })

    describe('Re-Open Modal Dialog', () => {
      it('includes modal container with data-testid="wp-reopen-modal"', () => {
        expect(viewContent).toContain('data-testid="wp-reopen-modal"')
      })

      it('renders modal conditionally with v-if="showReopenModal"', () => {
        expect(viewContent).toContain('v-if="showReopenModal"')
      })

      it('displays context card with unfinished request count data-testid="wp-reopen-unfinished-count"', () => {
        expect(viewContent).toContain('data-testid="wp-reopen-context-card"')
        expect(viewContent).toContain('data-testid="wp-reopen-unfinished-count"')
      })

      it('displays expired deadline warning banner with data-testid="wp-reopen-deadline-warning"', () => {
        expect(viewContent).toContain('data-testid="wp-reopen-deadline-warning"')
        expect(viewContent).toContain('Target Deadline Expired')
      })

      it('includes date picker for optional revised deadline with data-testid="wp-reopen-deadline-input"', () => {
        expect(viewContent).toContain('data-testid="wp-reopen-deadline-input"')
      })

      it('includes mandatory reason textarea with data-testid="wp-reopen-reason-input"', () => {
        expect(viewContent).toContain('data-testid="wp-reopen-reason-input"')
        expect(viewContent).toContain('Re-Open Justification Reason')
      })

      it('includes confirm button with data-testid="wp-reopen-confirm-button"', () => {
        expect(viewContent).toContain('data-testid="wp-reopen-confirm-button"')
        expect(viewContent).toContain(':disabled="isSubmittingReopen || !reopenForm.reason.trim()"')
      })

      it('includes cancel button with data-testid="wp-reopen-cancel-button"', () => {
        expect(viewContent).toContain('data-testid="wp-reopen-cancel-button"')
      })

      it('includes error alert with data-testid="wp-reopen-error-alert"', () => {
        expect(viewContent).toContain('data-testid="wp-reopen-error-alert"')
      })
    })

    describe('Computed Properties Contract', () => {
      it('defines reopenDisabledTooltip with the exact specified message', () => {
        expect(viewContent).toContain('Cannot re-open: all requests in this package are completed or cancelled.')
      })

      it('computes isSelectedClosed based on CLOSED status', () => {
        expect(viewContent).toContain("const isSelectedClosed = computed(() => selectedStatus.value === 'CLOSED')")
      })

      it('computes isReopenEligible from hasUnfinishedRequests', () => {
        expect(viewContent).toContain('const isReopenEligible = computed(() => hasUnfinishedRequests.value)')
      })
    })
  })
})
