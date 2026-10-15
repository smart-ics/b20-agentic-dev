import { describe, expect, it } from 'vitest'
import {
  statusBadgeClass,
  countPendingUsers,
  filterUsersByStatus,
  type UserStatusFilter,
} from '@/views/UserManagementView.vue'

describe('UserManagementView Presentation & Filtering Tests (SCR-USR-001, CR-033)', () => {
  describe('statusBadgeClass', () => {
    it('maps PENDING status (case-insensitive) to info badge styling', () => {
      expect(statusBadgeClass('PENDING')).toBe('badge text-bg-info text-dark')
      expect(statusBadgeClass('Pending')).toBe('badge text-bg-info text-dark')
      expect(statusBadgeClass('pending')).toBe('badge text-bg-info text-dark')
    })

    it('maps ACTIVE status to success badge styling', () => {
      expect(statusBadgeClass('ACTIVE')).toBe('badge text-bg-success')
      expect(statusBadgeClass('active')).toBe('badge text-bg-success')
    })

    it('maps LOCKED status to danger badge styling', () => {
      expect(statusBadgeClass('LOCKED')).toBe('badge text-bg-danger')
      expect(statusBadgeClass('locked')).toBe('badge text-bg-danger')
    })

    it('maps SUSPENDED status to warning badge styling', () => {
      expect(statusBadgeClass('SUSPENDED')).toBe('badge text-bg-warning text-dark')
      expect(statusBadgeClass('suspended')).toBe('badge text-bg-warning text-dark')
    })

    it('maps unknown, empty, or undefined status to secondary badge styling', () => {
      expect(statusBadgeClass('UNKNOWN')).toBe('badge text-bg-secondary')
      expect(statusBadgeClass('')).toBe('badge text-bg-secondary')
      expect(statusBadgeClass(null)).toBe('badge text-bg-secondary')
      expect(statusBadgeClass(undefined)).toBe('badge text-bg-secondary')
    })
  })

  describe('countPendingUsers', () => {
    it('accurately counts users in PENDING status case-insensitively', () => {
      const users = [
        { status: 'ACTIVE' },
        { status: 'Pending' },
        { status: 'PENDING' },
        { status: 'LOCKED' },
        { status: 'SUSPENDED' },
        { status: 'pending' },
        { status: null },
      ]
      expect(countPendingUsers(users)).toBe(3)
    })

    it('returns 0 when no pending users exist', () => {
      const users = [
        { status: 'ACTIVE' },
        { status: 'LOCKED' },
      ]
      expect(countPendingUsers(users)).toBe(0)
      expect(countPendingUsers([])).toBe(0)
    })
  })

  describe('filterUsersByStatus', () => {
    const sampleUsers = [
      { id: '1', username: 'alice', status: 'ACTIVE' },
      { id: '2', username: 'bob', status: 'Pending' },
      { id: '3', username: 'charlie', status: 'PENDING' },
      { id: '4', username: 'david', status: 'LOCKED' },
      { id: '5', username: 'eve', status: 'SUSPENDED' },
    ]

    it('returns all users when filter is ALL', () => {
      const filtered = filterUsersByStatus(sampleUsers, 'ALL')
      expect(filtered).toHaveLength(5)
    })

    it('isolates only PENDING accounts when filter is PENDING', () => {
      const filtered = filterUsersByStatus(sampleUsers, 'PENDING')
      expect(filtered).toHaveLength(2)
      expect(filtered.map((u) => u.username)).toEqual(['bob', 'charlie'])
    })

    it('isolates only ACTIVE accounts when filter is ACTIVE', () => {
      const filtered = filterUsersByStatus(sampleUsers, 'ACTIVE')
      expect(filtered).toHaveLength(1)
      expect(filtered[0]?.username).toBe('alice')
    })

    it('isolates only LOCKED accounts when filter is LOCKED', () => {
      const filtered = filterUsersByStatus(sampleUsers, 'LOCKED')
      expect(filtered).toHaveLength(1)
      expect(filtered[0]?.username).toBe('david')
    })

    it('isolates only SUSPENDED accounts when filter is SUSPENDED', () => {
      const filtered = filterUsersByStatus(sampleUsers, 'SUSPENDED')
      expect(filtered).toHaveLength(1)
      expect(filtered[0]?.username).toBe('eve')
    })

    it('allows assigning all valid UserStatusFilter union values', () => {
      const filters: UserStatusFilter[] = ['ALL', 'ACTIVE', 'PENDING', 'LOCKED', 'SUSPENDED']
      expect(filters).toHaveLength(5)
    })
  })
})
