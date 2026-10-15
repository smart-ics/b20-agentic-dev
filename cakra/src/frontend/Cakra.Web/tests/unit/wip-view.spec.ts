import { describe, expect, it } from 'vitest'
import type { PersonWorkInProgressDto, TaskWorkInProgressDto } from '@/api/requests'
import {
  burnBarClass,
  burnHeatTier,
  burnTextClass,
  elapsedColorClass,
  elapsedDurationTier,
  elapsedGlowClass,
  isPersonOverBudget,
  personBorderClass,
  personWorkloadTier,
  priorityBadgeVariant,
  resolveCustomerDisplay,
  taskRowBgClass,
} from '@/views/WorkInProgressView.vue'

describe('WorkInProgressView - Compact Feed & Dual Time Signals (CR-034 / SCR-REQ-006)', () => {
  describe('Signal 1: Pure Elapsed Heat Tiers', () => {
    it('classifies elapsed hours <= 4.0 as ok tier (calm green)', () => {
      expect(elapsedDurationTier(0)).toBe('ok')
      expect(elapsedDurationTier(2.0)).toBe('ok')
      expect(elapsedDurationTier(4.0)).toBe('ok')
      expect(elapsedColorClass('ok')).toBe('eh-ok')
      expect(elapsedGlowClass('ok')).toBe('')
    })

    it('classifies elapsed hours > 4.0 and <= 8.0 as warn tier (amber with subtle glow)', () => {
      expect(elapsedDurationTier(4.01)).toBe('warn')
      expect(elapsedDurationTier(6.5)).toBe('warn')
      expect(elapsedDurationTier(8.0)).toBe('warn')
      expect(elapsedColorClass('warn')).toBe('eh-warn')
      expect(elapsedGlowClass('warn')).toBe('glow-warn')
    })

    it('classifies elapsed hours > 8.0 as crit tier (escalate red with bright glow)', () => {
      expect(elapsedDurationTier(8.01)).toBe('crit')
      expect(elapsedDurationTier(11.0)).toBe('crit')
      expect(elapsedDurationTier(24.0)).toBe('crit')
      expect(elapsedColorClass('crit')).toBe('eh-crit')
      expect(elapsedGlowClass('crit')).toBe('glow-red')
    })
  })

  describe('Signal 2: Budget Burn Tiers', () => {
    it('classifies burn percentage < 75% as on-track tier (green bar & text)', () => {
      expect(burnHeatTier(0)).toBe('ok')
      expect(burnHeatTier(50)).toBe('ok')
      expect(burnHeatTier(74)).toBe('ok')
      expect(burnBarClass('ok')).toBe('bh-ok')
      expect(burnTextClass('ok')).toBe('bh-text-ok')
    })

    it('classifies burn percentage between 75% and 99% as near-limit tier (amber bar & text)', () => {
      expect(burnHeatTier(75)).toBe('warn')
      expect(burnHeatTier(85)).toBe('warn')
      expect(burnHeatTier(99)).toBe('warn')
      expect(burnBarClass('warn')).toBe('bh-warn')
      expect(burnTextClass('warn')).toBe('bh-text-warn')
    })

    it('classifies burn percentage >= 100% as over-budget tier (red bar & text)', () => {
      expect(burnHeatTier(100)).toBe('crit')
      expect(burnHeatTier(138)).toBe('crit')
      expect(burnHeatTier(200)).toBe('crit')
      expect(burnBarClass('crit')).toBe('bh-crit')
      expect(burnTextClass('crit')).toBe('bh-text-crit')
    })
  })

  describe('Workload Triage: Left-border color & overload classification', () => {
    const createPerson = (
      hasInProgress: boolean,
      pausedCount: number,
    ): PersonWorkInProgressDto => ({
      personId: 'p-1',
      personName: 'Test Engineer',
      email: 'engineer@cakra.id',
      totalActiveTasksCount: (hasInProgress ? 1 : 0) + pausedCount,
      inProgressTask: hasInProgress
        ? ({
            requestId: 'REQ-1',
            title: 'Active Task',
            description: '',
            requestType: 'Bug',
            status: 'IN_PROGRESS',
            priority: 'NORMAL',
            complexity: 1,
            budgetHours: 2.0,
            burnPercentage: 50,
            isOverBudget: false,
            totalInProgressSeconds: 3600,
            totalInProgressHours: 1.0,
            totalInProgressFormatted: '1h 0m',
            createdAt: '2026-10-15T00:00:00Z',
          } as TaskWorkInProgressDto)
        : null,
      pausedTasks: Array.from({ length: pausedCount }, (_, i) => ({
        requestId: `REQ-PAUSED-${i + 1}`,
        title: `Paused Task ${i + 1}`,
        description: '',
        requestType: 'Task',
        status: 'PAUSED',
        priority: 'NORMAL',
        complexity: 2,
        budgetHours: 4.0,
        burnPercentage: 25,
        isOverBudget: false,
        totalInProgressSeconds: 3600,
        totalInProgressHours: 1.0,
        totalInProgressFormatted: '1h 0m',
        createdAt: '2026-10-15T00:00:00Z',
      })),
    })

    it('classifies 0-1 paused tasks as normal tier with ok-row green left-border', () => {
      const p0 = createPerson(true, 0)
      expect(personWorkloadTier(p0)).toBe('normal')
      expect(personBorderClass(p0)).toBe('ok-row')

      const p1 = createPerson(true, 1)
      expect(personWorkloadTier(p1)).toBe('normal')
      expect(personBorderClass(p1)).toBe('ok-row')
    })

    it('classifies 2-3 paused tasks as busy tier with busy-row amber left-border', () => {
      const p2 = createPerson(true, 2)
      expect(personWorkloadTier(p2)).toBe('busy')
      expect(personBorderClass(p2)).toBe('busy-row')

      const p3 = createPerson(true, 3)
      expect(personWorkloadTier(p3)).toBe('busy')
      expect(personBorderClass(p3)).toBe('busy-row')
    })

    it('classifies 4+ paused tasks as overloaded tier with crit-row red left-border', () => {
      const p4 = createPerson(true, 4)
      expect(personWorkloadTier(p4)).toBe('overloaded')
      expect(personBorderClass(p4)).toBe('crit-row')

      const p6 = createPerson(true, 6)
      expect(personWorkloadTier(p6)).toBe('overloaded')
      expect(personBorderClass(p6)).toBe('crit-row')
    })

    it('classifies idle persons with no active and no paused tasks as idle-row', () => {
      const idle = createPerson(false, 0)
      expect(personWorkloadTier(idle)).toBe('idle')
      expect(personBorderClass(idle)).toBe('idle-row')
    })
  })

  describe('isPersonOverBudget predicate', () => {
    it('returns true when inProgressTask has isOverBudget true', () => {
      const person: PersonWorkInProgressDto = {
        personId: 'p-1',
        personName: 'Test',
        totalActiveTasksCount: 1,
        inProgressTask: {
          requestId: 'REQ-1',
          title: 'Active Over Budget',
          description: '',
          requestType: 'Bug',
          status: 'IN_PROGRESS',
          priority: 'NORMAL',
          complexity: 1,
          budgetHours: 2.0,
          burnPercentage: 150,
          isOverBudget: true,
          totalInProgressSeconds: 10800,
          totalInProgressHours: 3.0,
          totalInProgressFormatted: '3h 0m',
          createdAt: '2026-10-15T00:00:00Z',
        },
        pausedTasks: [],
      }
      expect(isPersonOverBudget(person)).toBe(true)
    })

    it('returns true when inProgressTask burnPercentage >= 100', () => {
      const person: PersonWorkInProgressDto = {
        personId: 'p-1',
        personName: 'Test',
        totalActiveTasksCount: 1,
        inProgressTask: {
          requestId: 'REQ-1',
          title: 'Active 100%',
          description: '',
          requestType: 'Bug',
          status: 'IN_PROGRESS',
          priority: 'NORMAL',
          complexity: 2,
          budgetHours: 4.0,
          burnPercentage: 100,
          isOverBudget: false,
          totalInProgressSeconds: 14400,
          totalInProgressHours: 4.0,
          totalInProgressFormatted: '4h 0m',
          createdAt: '2026-10-15T00:00:00Z',
        },
        pausedTasks: [],
      }
      expect(isPersonOverBudget(person)).toBe(true)
    })

    it('returns true when a paused task is over budget even if active task is on track', () => {
      const person: PersonWorkInProgressDto = {
        personId: 'p-1',
        personName: 'Test',
        totalActiveTasksCount: 2,
        inProgressTask: {
          requestId: 'REQ-1',
          title: 'Active On Track',
          description: '',
          requestType: 'Bug',
          status: 'IN_PROGRESS',
          priority: 'NORMAL',
          complexity: 3,
          budgetHours: 8.0,
          burnPercentage: 25,
          isOverBudget: false,
          totalInProgressSeconds: 7200,
          totalInProgressHours: 2.0,
          totalInProgressFormatted: '2h 0m',
          createdAt: '2026-10-15T00:00:00Z',
        },
        pausedTasks: [
          {
            requestId: 'REQ-PAUSED-1',
            title: 'Paused Over Budget',
            description: '',
            requestType: 'Task',
            status: 'PAUSED',
            priority: 'NORMAL',
            complexity: 1,
            budgetHours: 2.0,
            burnPercentage: 120,
            isOverBudget: true,
            totalInProgressSeconds: 8640,
            totalInProgressHours: 2.4,
            totalInProgressFormatted: '2h 24m',
            createdAt: '2026-10-15T00:00:00Z',
          },
        ],
      }
      expect(isPersonOverBudget(person)).toBe(true)
    })

    it('returns false when neither active nor paused tasks are over budget', () => {
      const person: PersonWorkInProgressDto = {
        personId: 'p-1',
        personName: 'Test',
        totalActiveTasksCount: 1,
        inProgressTask: {
          requestId: 'REQ-1',
          title: 'Healthy Task',
          description: '',
          requestType: 'Bug',
          status: 'IN_PROGRESS',
          priority: 'NORMAL',
          complexity: 3,
          budgetHours: 8.0,
          burnPercentage: 50,
          isOverBudget: false,
          totalInProgressSeconds: 14400,
          totalInProgressHours: 4.0,
          totalInProgressFormatted: '4h 0m',
          createdAt: '2026-10-15T00:00:00Z',
        },
        pausedTasks: [],
      }
      expect(isPersonOverBudget(person)).toBe(false)
    })
  })

  describe('taskRowBgClass heat tinting', () => {
    it('applies over-bg when task is over budget', () => {
      const task: TaskWorkInProgressDto = {
        requestId: 'REQ-1',
        title: 'Task',
        description: '',
        requestType: 'Bug',
        status: 'IN_PROGRESS',
        priority: 'NORMAL',
        complexity: 1,
        budgetHours: 2.0,
        burnPercentage: 120,
        isOverBudget: true,
        totalInProgressSeconds: 8640,
        totalInProgressHours: 2.4,
        totalInProgressFormatted: '2h 24m',
        createdAt: '2026-10-15T00:00:00Z',
      }
      expect(taskRowBgClass(task)).toBe('over-bg')
    })

    it('applies warn-bg when elapsed hours is in warn tier (>4h) and not over budget', () => {
      const task: TaskWorkInProgressDto = {
        requestId: 'REQ-2',
        title: 'Task',
        description: '',
        requestType: 'Bug',
        status: 'IN_PROGRESS',
        priority: 'NORMAL',
        complexity: 4,
        budgetHours: 16.0,
        burnPercentage: 38,
        isOverBudget: false,
        totalInProgressSeconds: 21600,
        totalInProgressHours: 6.0,
        totalInProgressFormatted: '6h 0m',
        createdAt: '2026-10-15T00:00:00Z',
      }
      expect(taskRowBgClass(task)).toBe('warn-bg')
    })

    it('returns empty string when task is on track and <= 4h', () => {
      const task: TaskWorkInProgressDto = {
        requestId: 'REQ-3',
        title: 'Task',
        description: '',
        requestType: 'Bug',
        status: 'IN_PROGRESS',
        priority: 'NORMAL',
        complexity: 2,
        budgetHours: 4.0,
        burnPercentage: 50,
        isOverBudget: false,
        totalInProgressSeconds: 7200,
        totalInProgressHours: 2.0,
        totalInProgressFormatted: '2h 0m',
        createdAt: '2026-10-15T00:00:00Z',
      }
      expect(taskRowBgClass(task)).toBe('')
    })
  })

  describe('resolveCustomerDisplay', () => {
    it('displays customer name and code together when both exist', () => {
      const task = {
        customerName: 'Bank Nusantara',
        customerCode: 'BN',
      } as TaskWorkInProgressDto
      expect(resolveCustomerDisplay(task)).toBe('Bank Nusantara (BN)')
    })

    it('displays customer name only when code is not present', () => {
      const task = {
        customerName: 'Bank Nusantara',
      } as TaskWorkInProgressDto
      expect(resolveCustomerDisplay(task)).toBe('Bank Nusantara')
    })

    it('displays customer code when name is blank', () => {
      const task = {
        customerName: '',
        customerCode: 'BN',
      } as TaskWorkInProgressDto
      expect(resolveCustomerDisplay(task)).toBe('BN')
    })

    it('displays customerId fallback when both name and code are absent', () => {
      const task = {
        customerId: 'cust-uuid-1234',
      } as TaskWorkInProgressDto
      expect(resolveCustomerDisplay(task)).toBe('cust-uuid-1234')
    })

    it('returns dash when no customer info is provided', () => {
      const task = {} as TaskWorkInProgressDto
      expect(resolveCustomerDisplay(task)).toBe('—')
    })
  })

  describe('priorityBadgeVariant', () => {
    it('maps URGENT to danger', () => {
      expect(priorityBadgeVariant('URGENT')).toBe('danger')
    })
    it('maps HIGH to warning', () => {
      expect(priorityBadgeVariant('HIGH')).toBe('warning')
    })
    it('maps LOW and NORMAL to neutral', () => {
      expect(priorityBadgeVariant('LOW')).toBe('neutral')
      expect(priorityBadgeVariant('NORMAL')).toBe('neutral')
    })
  })

  describe('Automated Test Selectors Preservation (TD-007 / SCR-REQ-006)', () => {
    it('verifies all required data-testid and data-screen-id attributes are present in WorkInProgressView.vue', async () => {
      const viewContent = (await import('@/views/WorkInProgressView.vue?raw')).default

      const expectedSelectors = [
        'data-screen-id="SCR-REQ-006"',
        'data-testid="wip-screen"',
        'data-testid="wip-kpis"',
        'data-testid="refresh-wip-button"',
        'data-testid="wip-error-alert"',
        'data-testid="wip-loading"',
        'data-testid="wip-empty-state"',
        'data-testid="wip-persons-container"',
        'data-testid="person-card"',
        ':data-person-id="person.personId"',
        'data-testid="person-status-active"',
        'data-testid="person-status-paused"',
        'data-testid="person-tasks-count-badge"',
        'data-testid="in-progress-card"',
        'data-testid="task-title-link"',
        'data-testid="task-priority-badge"',
        'data-testid="task-customer-badge"',
        'data-testid="task-id-link"',
        'data-testid="in-progress-time-badge"',
        'data-testid="task-view-link"',
        'data-testid="idle-placeholder"',
        'data-testid="paused-tasks-list"',
        'data-testid="paused-task-card"',
        'data-testid="paused-task-title-link"',
        'data-testid="paused-task-priority-badge"',
        'data-testid="paused-task-customer-badge"',
        'data-testid="paused-task-id-link"',
        'data-testid="paused-time-badge"',
        'data-testid="paused-task-view-link"',
        'data-testid="empty-paused-tasks"',
      ]

      for (const selector of expectedSelectors) {
        expect(viewContent).toContain(selector)
      }
    })
  })
})

