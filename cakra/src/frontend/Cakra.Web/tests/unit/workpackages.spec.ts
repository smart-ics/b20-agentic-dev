import { describe, expect, it, vi } from 'vitest'
import httpClient from '@/api/http'
import {
  reopenWorkPackage,
  workPackageService,
  default as defaultWorkPackageService,
  type ReopenWorkPackagePayload,
  type WorkPackageDto,
} from '@/api/workpackages'

describe('workpackages API client', () => {
  it('reopenWorkPackage sends POST to /work-packages/:id/reopen with payload', async () => {
    const mockDto: WorkPackageDto = {
      id: 'wp-100',
      name: 'Alpha Package',
      objective: 'Core platform migration',
      status: 'DRAFT',
      ownerPersonId: 'person-1',
      createdAt: '2026-10-15T00:00:00Z',
    }

    const postSpy = vi.spyOn(httpClient, 'post').mockResolvedValueOnce({ data: mockDto })

    const payload: ReopenWorkPackagePayload = {
      reason: 'Additional scope requirements identified',
      deadline: '2026-11-30T00:00:00Z',
    }

    const result = await reopenWorkPackage('wp-100', payload)

    expect(postSpy).toHaveBeenCalledWith('/work-packages/wp-100/reopen', payload)
    expect(result).toEqual(mockDto)
    expect(result.status).toBe('DRAFT')
  })

  it('exports reopenWorkPackage in both named and default workPackageService', () => {
    expect(workPackageService.reopenWorkPackage).toBe(reopenWorkPackage)
    expect(defaultWorkPackageService.reopenWorkPackage).toBe(reopenWorkPackage)
  })
})
