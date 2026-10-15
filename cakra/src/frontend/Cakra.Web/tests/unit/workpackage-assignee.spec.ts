import { describe, expect, it } from 'vitest'
import {
  applyInlineReassignOptimisticUpdate,
  getScopeItemActiveBlockersCount,
  isRequestCompleted,
  isScopeItemBlocked,
  parseTaskListTasks,
  resolveOwnerName,
  resolvePersonName,
  resolveScopeItemBlockerReason,
  resolveScopeItemOwnerDisplay,
  syncCandidateAssigneesOnOwnerChange,
  type CandidateTaskItem,
} from '@/views/WorkPackageView.vue'

describe('WorkPackage candidate preview assignee & reactive sync', () => {
  describe('resolvePersonName', () => {
    it('returns fullName when present', () => {
      const person = { id: 'p1', fullName: 'Alice Smith', firstName: 'Alice', lastName: 'Smith' }
      expect(resolvePersonName(person)).toBe('Alice Smith')
    })

    it('falls back to firstName and lastName when fullName is missing', () => {
      const person = { firstName: 'Bob', lastName: 'Jones' }
      expect(resolvePersonName(person)).toBe('Bob Jones')
    })

    it('returns Unknown when all name fields are empty', () => {
      const person = { firstName: '', lastName: '' }
      expect(resolvePersonName(person)).toBe('Unknown')
    })
  })

  describe('syncCandidateAssigneesOnOwnerChange', () => {
    it('updates candidate tasks that were defaulted to the old owner', () => {
      const candidates: CandidateTaskItem[] = [
        { title: 'Task 1', complexity: 1, assigneePersonId: 'owner-a' },
        { title: 'Task 2', complexity: 2, assigneePersonId: 'owner-a' },
      ]
      const overriddenSet = new Set<CandidateTaskItem>()

      syncCandidateAssigneesOnOwnerChange(candidates, 'owner-b', 'owner-a', overriddenSet)

      expect(candidates[0].assigneePersonId).toBe('owner-b')
      expect(candidates[1].assigneePersonId).toBe('owner-b')
    })

    it('updates candidate tasks with empty/null assignee when owner is selected', () => {
      const candidates: CandidateTaskItem[] = [
        { title: 'Task 1', complexity: 1, assigneePersonId: '' },
        { title: 'Task 2', complexity: 2, assigneePersonId: null },
      ]
      const overriddenSet = new Set<CandidateTaskItem>()

      syncCandidateAssigneesOnOwnerChange(candidates, 'owner-a', '', overriddenSet)

      expect(candidates[0].assigneePersonId).toBe('owner-a')
      expect(candidates[1].assigneePersonId).toBe('owner-a')
    })

    it('preserves manually overridden candidate tasks when package owner changes', () => {
      const task1: CandidateTaskItem = { title: 'Task 1', complexity: 1, assigneePersonId: 'owner-a' }
      const task2: CandidateTaskItem = { title: 'Task 2', complexity: 2, assigneePersonId: 'specialist-x' }
      const candidates: CandidateTaskItem[] = [task1, task2]

      const overriddenSet = new Set<CandidateTaskItem>()
      overriddenSet.add(task2)

      syncCandidateAssigneesOnOwnerChange(candidates, 'owner-b', 'owner-a', overriddenSet)

      // task 1 was defaulted, so it updates to owner-b
      expect(task1.assigneePersonId).toBe('owner-b')
      // task 2 was overridden, so specialist-x is preserved
      expect(task2.assigneePersonId).toBe('specialist-x')
    })

    it('preserves tasks explicitly set to Unassigned (empty string) when package owner changes', () => {
      const task1: CandidateTaskItem = { title: 'Task 1', complexity: 1, assigneePersonId: 'owner-a' }
      const task2: CandidateTaskItem = { title: 'Task 2', complexity: 2, assigneePersonId: '' }
      const candidates: CandidateTaskItem[] = [task1, task2]

      const overriddenSet = new Set<CandidateTaskItem>()
      overriddenSet.add(task2) // User selected "Unassigned" explicitly

      syncCandidateAssigneesOnOwnerChange(candidates, 'owner-b', 'owner-a', overriddenSet)

      expect(task1.assigneePersonId).toBe('owner-b')
      expect(task2.assigneePersonId).toBe('')
    })
  })

  describe('parseTaskListTasks integration with default assignee', () => {
    it('applies default package owner when parsing candidate tasks', () => {
      const raw = `
        - First task 1pt
        - Second task 2pts
      `
      const parsed = parseTaskListTasks(raw, 'default-owner-uuid')

      expect(parsed).toHaveLength(2)
      expect(parsed[0]).toEqual({
        title: 'First task',
        complexity: 1,
        assigneePersonId: 'default-owner-uuid',
      })
      expect(parsed[1]).toEqual({
        title: 'Second task',
        complexity: 2,
        assigneePersonId: 'default-owner-uuid',
      })
    })

    it('sets null assignee when default owner is omitted', () => {
      const raw = `- Unassigned candidate task`
      const parsed = parseTaskListTasks(raw)

      expect(parsed).toHaveLength(1)
      expect(parsed[0].assigneePersonId).toBeNull()
    })
  })

  describe('resolveOwnerName', () => {
    it('returns empty string when personId is null, undefined, or empty', () => {
      expect(resolveOwnerName(null)).toBe('')
      expect(resolveOwnerName(undefined)).toBe('')
      expect(resolveOwnerName('')).toBe('')
    })

    it('resolves person display name from person map', () => {
      const map = { 'person-1': 'Jane Doe' }
      expect(resolveOwnerName('person-1', map)).toBe('Jane Doe')
    })

    it('falls back to personId when not present in person map', () => {
      expect(resolveOwnerName('unknown-uuid', {})).toBe('unknown-uuid')
    })
  })

  describe('resolveScopeItemOwnerDisplay', () => {
    const personMap = {
      'person-alice': 'Alice Cooper',
      'person-bob': 'Bob Marley',
    }

    it('resolves owner name via ownerPersonId from personNameMap', () => {
      const item = {
        ownerPersonId: 'person-alice',
        ownerName: 'Old Alice',
      }
      expect(resolveScopeItemOwnerDisplay(item, personMap)).toBe('Alice Cooper')
    })

    it('resolves owner name via requestOwnerPersonId alias from personNameMap', () => {
      const item = {
        requestOwnerPersonId: 'person-bob',
      }
      expect(resolveScopeItemOwnerDisplay(item, personMap)).toBe('Bob Marley')
    })

    it('falls back to item.ownerName if ID not found in personNameMap', () => {
      const item = {
        ownerPersonId: 'person-charlie',
        ownerName: 'Charlie Brown',
      }
      expect(resolveScopeItemOwnerDisplay(item, personMap)).toBe('Charlie Brown')
    })

    it('falls back to item.requestOwnerName if ID not found in map and ownerName is empty', () => {
      const item = {
        ownerPersonId: 'person-charlie',
        requestOwnerName: 'Charlie Brown Alias',
      }
      expect(resolveScopeItemOwnerDisplay(item, personMap)).toBe('Charlie Brown Alias')
    })

    it('falls back to nested request.ownerName if item ownerName fields are empty', () => {
      const item = {
        ownerPersonId: 'person-charlie',
        request: { ownerName: 'Charlie From Nested Request' },
      }
      expect(resolveScopeItemOwnerDisplay(item, personMap)).toBe('Charlie From Nested Request')
    })

    it('falls back to raw ID when no map entry and no name fields exist', () => {
      const item = {
        ownerPersonId: 'person-david',
      }
      expect(resolveScopeItemOwnerDisplay(item, personMap)).toBe('person-david')
    })

    it('returns Unassigned when ownerPersonId, requestOwnerPersonId, and all names are missing', () => {
      const item1 = {}
      const item2 = { ownerPersonId: null, ownerName: null }
      const item3 = { ownerPersonId: '', ownerName: '' }

      expect(resolveScopeItemOwnerDisplay(item1, personMap)).toBe('Unassigned')
      expect(resolveScopeItemOwnerDisplay(item2, personMap)).toBe('Unassigned')
      expect(resolveScopeItemOwnerDisplay(item3, personMap)).toBe('Unassigned')
    })
  })

  describe('isRequestCompleted', () => {
    it('returns true when status is COMPLETED or CANCELLED', () => {
      expect(isRequestCompleted({ status: 'COMPLETED' })).toBe(true)
      expect(isRequestCompleted({ status: 'completed' })).toBe(true)
      expect(isRequestCompleted({ status: 'CANCELLED' })).toBe(true)
      expect(isRequestCompleted({ requestStatus: 'COMPLETED' })).toBe(true)
      expect(isRequestCompleted({ request: { status: 'CANCELLED' } })).toBe(true)
    })

    it('returns false when status is active (CAPTURED, ASSIGNED, IN_PROGRESS, PAUSED)', () => {
      expect(isRequestCompleted({ status: 'CAPTURED' })).toBe(false)
      expect(isRequestCompleted({ status: 'ASSIGNED' })).toBe(false)
      expect(isRequestCompleted({ status: 'IN_PROGRESS' })).toBe(false)
      expect(isRequestCompleted({ status: 'PAUSED' })).toBe(false)
      expect(isRequestCompleted({})).toBe(false)
    })
  })

  describe('applyInlineReassignOptimisticUpdate', () => {
    it('reactively updates local scope item ownership fields and nested request', () => {
      const personMap = {
        'new-owner-id': 'Diana Prince',
      }
      const scopeItems = [
        {
          requestId: 'req-1',
          ownerPersonId: 'old-owner-id',
          requestOwnerPersonId: 'old-owner-id',
          ownerName: 'Old Owner',
          requestOwnerName: 'Old Owner',
          request: { ownerPersonId: 'old-owner-id', ownerName: 'Old Owner' },
        },
        {
          requestId: 'req-2',
          ownerPersonId: 'other-owner',
        },
      ]

      const updated = applyInlineReassignOptimisticUpdate(
        scopeItems,
        'req-1',
        'new-owner-id',
        personMap,
      )

      expect(updated).toBe(true)
      expect(scopeItems[0].ownerPersonId).toBe('new-owner-id')
      expect(scopeItems[0].requestOwnerPersonId).toBe('new-owner-id')
      expect(scopeItems[0].ownerName).toBe('Diana Prince')
      expect(scopeItems[0].requestOwnerName).toBe('Diana Prince')
      expect(scopeItems[0].request?.ownerPersonId).toBe('new-owner-id')
      expect(scopeItems[0].request?.ownerName).toBe('Diana Prince')

      // req-2 should remain untouched
      expect(scopeItems[1].ownerPersonId).toBe('other-owner')
    })

    it('returns false when target requestId is not in scope items', () => {
      const scopeItems = [{ requestId: 'req-1', ownerPersonId: 'owner-1' }]
      const updated = applyInlineReassignOptimisticUpdate(
        scopeItems,
        'non-existent-req',
        'new-owner-id',
        {},
      )

      expect(updated).toBe(false)
      expect(scopeItems[0].ownerPersonId).toBe('owner-1')
    })
  })

  describe('inline reassignment eligibility rules (CR-030 TD-005)', () => {
    it('allows inline reassignment only when package is open (canModifyPackage) and request is open (!isRequestCompleted)', () => {
      const isEligibleForInlineReassign = (
        canModifyPackage: boolean,
        item: { status?: string },
      ) => canModifyPackage && !isRequestCompleted(item)

      // Open package + active request -> eligible for dropdown
      expect(isEligibleForInlineReassign(true, { status: 'ASSIGNED' })).toBe(true)
      expect(isEligibleForInlineReassign(true, { status: 'IN_PROGRESS' })).toBe(true)
      expect(isEligibleForInlineReassign(true, { status: 'PAUSED' })).toBe(true)
      expect(isEligibleForInlineReassign(true, { status: 'CAPTURED' })).toBe(true)

      // Closed package -> read-only
      expect(isEligibleForInlineReassign(false, { status: 'ASSIGNED' })).toBe(false)
      expect(isEligibleForInlineReassign(false, { status: 'IN_PROGRESS' })).toBe(false)

      // Completed/cancelled request -> read-only
      expect(isEligibleForInlineReassign(true, { status: 'COMPLETED' })).toBe(false)
      expect(isEligibleForInlineReassign(true, { status: 'CANCELLED' })).toBe(false)
      expect(isEligibleForInlineReassign(false, { status: 'COMPLETED' })).toBe(false)
    })
  })

  describe('WorkPackage Scope Items Blocker Badges & Summaries (CR-032 / SCR-WP-001)', () => {
    describe('getScopeItemActiveBlockersCount', () => {
      it('returns item.activeBlockersCount when present and greater than 0', () => {
        expect(getScopeItemActiveBlockersCount({ activeBlockersCount: 2 })).toBe(2)
      })

      it('returns item.request.activeBlockersCount when item.activeBlockersCount is not set', () => {
        expect(
          getScopeItemActiveBlockersCount({
            request: { id: 'req-1', activeBlockersCount: 3 } as any,
          }),
        ).toBe(3)
      })

      it('returns 0 when neither item nor request has active blockers', () => {
        expect(getScopeItemActiveBlockersCount({ activeBlockersCount: 0 })).toBe(0)
        expect(getScopeItemActiveBlockersCount({ request: { id: 'req-1', activeBlockersCount: 0 } as any })).toBe(0)
        expect(getScopeItemActiveBlockersCount(null)).toBe(0)
        expect(getScopeItemActiveBlockersCount(undefined)).toBe(0)
      })
    })

    describe('isScopeItemBlocked', () => {
      it('returns true when item has active blockers count > 0', () => {
        expect(isScopeItemBlocked({ activeBlockersCount: 1, status: 'IN_PROGRESS' })).toBe(true)
      })

      it('returns true when nested item.request has active blockers count > 0', () => {
        expect(
          isScopeItemBlocked({
            status: 'ASSIGNED',
            request: { activeBlockersCount: 2, status: 'ASSIGNED' } as any,
          }),
        ).toBe(true)
      })

      it('returns true when item.request has hasActiveBlockers flag true', () => {
        expect(
          isScopeItemBlocked({
            status: 'IN_PROGRESS',
            request: { hasActiveBlockers: true } as any,
          }),
        ).toBe(true)
      })

      it('returns true when status is PAUSED even if active count is 0', () => {
        expect(isScopeItemBlocked({ status: 'PAUSED' })).toBe(true)
        expect(isScopeItemBlocked({ requestStatus: 'PAUSED' })).toBe(true)
        expect(isScopeItemBlocked({ request: { status: 'PAUSED' } as any })).toBe(true)
      })

      it('returns false when status is not PAUSED and active blockers count is 0', () => {
        expect(isScopeItemBlocked({ status: 'IN_PROGRESS', activeBlockersCount: 0 })).toBe(false)
        expect(isScopeItemBlocked({ status: 'ASSIGNED' })).toBe(false)
        expect(isScopeItemBlocked(null)).toBe(false)
        expect(isScopeItemBlocked(undefined)).toBe(false)
      })
    })

    describe('resolveScopeItemBlockerReason', () => {
      it('returns blockerNote from item when populated (TD-007 backend alignment)', () => {
        const item = {
          blockerNote: 'Blocked by database migration deadlock',
          request: {
            blockers: [{ reason: 'Different blocker reason', isCleared: false }],
          } as any,
        }
        expect(resolveScopeItemBlockerReason(item)).toBe('Blocked by database migration deadlock')
      })

      it('returns latest uncleared blocker reason from request.blockers when blockerNote is missing', () => {
        const item = {
          blockerNote: null,
          request: {
            blockers: [
              { reason: 'Old resolved blocker', isCleared: true },
              { reason: 'Awaiting firewall rule exception', isCleared: false },
            ],
          } as any,
        }
        expect(resolveScopeItemBlockerReason(item)).toBe('Awaiting firewall rule exception')
      })

      it('falls back to assignments PAUSED note when blockers list is empty', () => {
        const item = {
          request: {
            assignments: [
              { newStatus: 'IN_PROGRESS', notes: 'Starting task' },
              { newStatus: 'PAUSED', notes: 'Paused awaiting product manager signoff' },
            ],
          } as any,
        }
        expect(resolveScopeItemBlockerReason(item)).toBe('Paused awaiting product manager signoff')
      })

      it('falls back to activeBlockersCount summary when count > 0 and no notes exist', () => {
        const item = {
          activeBlockersCount: 3,
        }
        expect(resolveScopeItemBlockerReason(item)).toBe('3 active blocker(s) impeding request')
      })

      it('falls back to default paused text when no notes exist and count is 0', () => {
        expect(resolveScopeItemBlockerReason({})).toBe('Work paused on request')
        expect(resolveScopeItemBlockerReason(null)).toBe('')
      })
    })
  })
})


