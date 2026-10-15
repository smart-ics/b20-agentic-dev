import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAuthStore } from '@/stores/auth'
import httpClient from '@/api/http'

vi.mock('@/api/http', () => ({
  default: {
    post: vi.fn(),
    get: vi.fn(),
  },
}))

describe('useAuthStore - logout', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('successfully clears session and user state upon server success', async () => {
    const store = useAuthStore()
    store.setCurrentUser({
      userId: 'test-user-id',
      personId: 'test-person-id',
      roles: ['Administrator'],
    })

    expect(store.isAuthenticated).toBe(true)

    vi.mocked(httpClient.post).mockResolvedValueOnce({ data: { message: 'Logged out successfully.' } })

    await store.logout()

    expect(httpClient.post).toHaveBeenCalledWith('/auth/logout')
    expect(store.currentUser).toBeNull()
    expect(store.isAuthenticated).toBe(false)
    expect(store.isLoading).toBe(false)
  })

  it('safely clears session state even when backend logout fails with an error (e.g. 405, 500, offline)', async () => {
    const store = useAuthStore()
    store.setCurrentUser({
      userId: 'test-user-id',
      personId: 'test-person-id',
      roles: ['Programmer'],
    })

    expect(store.isAuthenticated).toBe(true)

    vi.mocked(httpClient.post).mockRejectedValueOnce(new Error('Request failed with status code 405'))

    // Should not throw or reject
    await expect(store.logout()).resolves.toBeUndefined()

    expect(httpClient.post).toHaveBeenCalledWith('/auth/logout')
    expect(store.currentUser).toBeNull()
    expect(store.isAuthenticated).toBe(false)
    expect(store.isLoading).toBe(false)
  })
})

describe('useAuthStore - userName', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
  })

  it('resolves personName when present', () => {
    const store = useAuthStore()
    store.setCurrentUser({
      userId: 'test-user-id',
      personId: 'test-person-id',
      roles: ['Administrator'],
      username: 'admin',
      personName: 'Jane Doe',
    })

    expect(store.userName).toBe('Jane Doe')
  })

  it('falls back to username when personName is absent', () => {
    const store = useAuthStore()
    store.setCurrentUser({
      userId: 'test-user-id',
      personId: 'test-person-id',
      roles: ['Administrator'],
      username: 'john.smith',
    })

    expect(store.userName).toBe('john.smith')
  })

  it('falls back to Operational User when both are missing', () => {
    const store = useAuthStore()
    store.setCurrentUser({
      userId: 'test-user-id',
      personId: 'test-person-id',
      roles: [],
    })

    expect(store.userName).toBe('Operational User')
  })
})
