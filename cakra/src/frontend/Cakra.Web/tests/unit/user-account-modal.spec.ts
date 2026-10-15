import { describe, expect, it } from 'vitest'
import {
  checkIsPersonUnmapped,
  resolvePersonId,
  resolvePersonName,
  isValidEmail,
  type ActivePersonOption,
} from '@/components/UserAccountModal.vue'
import type { UpdateUserRequest } from '@/api/users'

describe('UserAccountModal Component & Contract Tests (SCR-USR-002, CR-033)', () => {
  describe('checkIsPersonUnmapped', () => {
    it('returns false when mode is not edit (create mode), even with empty personId', () => {
      expect(checkIsPersonUnmapped(false, undefined)).toBe(false)
      expect(checkIsPersonUnmapped(false, null)).toBe(false)
      expect(checkIsPersonUnmapped(false, '')).toBe(false)
      expect(checkIsPersonUnmapped(false, '00000000-0000-0000-0000-000000000000')).toBe(false)
    })

    it('returns true when in edit mode and personId is missing or empty', () => {
      expect(checkIsPersonUnmapped(true, undefined)).toBe(true)
      expect(checkIsPersonUnmapped(true, null)).toBe(true)
      expect(checkIsPersonUnmapped(true, '')).toBe(true)
    })

    it('returns true when in edit mode and personId is Guid.Empty (00000000-0000-0000-0000-000000000000)', () => {
      expect(checkIsPersonUnmapped(true, '00000000-0000-0000-0000-000000000000')).toBe(true)
    })

    it('returns false when in edit mode and personId is an established GUID', () => {
      expect(checkIsPersonUnmapped(true, 'a1b2c3d4-e5f6-7890-abcd-ef1234567890')).toBe(false)
    })
  })

  describe('UpdateUserRequest contract', () => {
    it('accepts personId as an optional string property', () => {
      const requestWithPerson: UpdateUserRequest = {
        email: 'user@example.com',
        status: 'ACTIVE',
        personId: 'b2c3d4e5-f6a7-8901-bcde-f12345678901',
      }
      expect(requestWithPerson.personId).toBe('b2c3d4e5-f6a7-8901-bcde-f12345678901')

      const requestWithoutPerson: UpdateUserRequest = {
        email: 'user@example.com',
        status: 'LOCKED',
      }
      expect(requestWithoutPerson.personId).toBeUndefined()
    })
  })

  describe('resolvePersonId & resolvePersonName', () => {
    it('resolves personId preferring id or personId property', () => {
      expect(resolvePersonId({ id: 'p123' })).toBe('p123')
      expect(resolvePersonId({ personId: 'pid456' })).toBe('pid456')
      expect(resolvePersonId({ id: '  p789  ' })).toBe('p789')
      expect(resolvePersonId({})).toBe('')
    })

    it('resolves person display name from fullName or name parts', () => {
      const withFullName: ActivePersonOption = { fullName: 'John Doe', firstName: 'John', lastName: 'Doe' }
      expect(resolvePersonName(withFullName)).toBe('John Doe')

      const withoutFullName: ActivePersonOption = { firstName: 'Jane', lastName: 'Smith' }
      expect(resolvePersonName(withoutFullName)).toBe('Jane Smith')

      const onlyFirst: ActivePersonOption = { firstName: 'Alice' }
      expect(resolvePersonName(onlyFirst)).toBe('Alice')

      const emptyPerson: ActivePersonOption = {}
      expect(resolvePersonName(emptyPerson)).toBe('Unnamed Person')
    })
  })

  describe('isValidEmail', () => {
    it('validates proper email formats', () => {
      expect(isValidEmail('admin@example.com')).toBe(true)
      expect(isValidEmail('john.doe@company.org')).toBe(true)
    })

    it('rejects invalid email formats', () => {
      expect(isValidEmail('invalid-email')).toBe(false)
      expect(isValidEmail('missing@domain')).toBe(false)
      expect(isValidEmail('@nodomain.com')).toBe(false)
      expect(isValidEmail('')).toBe(false)
    })
  })
})
