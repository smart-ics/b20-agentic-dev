import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import apiClient from '@/services/api'

export interface UserProfile {
  userId: string
  personId: string
  username: string
  roles: string[]
}

export interface LoginResponse {
  succeeded: boolean
  userId: string
  personId: string
  username: string
  roles: string[]
  expiresAt?: string
  message?: string
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<UserProfile | null>(null)
  const isCheckingAuth = ref<boolean>(false)

  const isAuthenticated = computed(() => user.value !== null)
  const userRoles = computed(() => user.value?.roles ?? [])

  function hasRole(role: string): boolean {
    return userRoles.value.includes(role)
  }

  function setUser(newUser: UserProfile | null) {
    user.value = newUser
  }

  function clearUser() {
    user.value = null
  }

  async function login(usernameOrEmail: string, password: string): Promise<LoginResponse> {
    const response = await apiClient.post<LoginResponse>('/auth/login', {
      usernameOrEmail,
      password
    })

    const data = response.data
    user.value = {
      userId: data.userId,
      personId: data.personId,
      username: data.username,
      roles: data.roles || []
    }

    return data
  }

  async function logout(): Promise<void> {
    try {
      await apiClient.post('/auth/logout')
    } finally {
      clearUser()
    }
  }

  async function checkAuth(): Promise<UserProfile | null> {
    isCheckingAuth.value = true
    try {
      const response = await apiClient.get('/auth/me')
      if (response.data && response.data.isAuthenticated && response.data.userId) {
        user.value = {
          userId: response.data.userId,
          personId: response.data.personId,
          username: response.data.username || 'Authenticated User',
          roles: response.data.roles || []
        }
        return user.value
      } else {
        user.value = null
        return null
      }
    } catch {
      user.value = null
      return null
    } finally {
      isCheckingAuth.value = false
    }
  }

  return {
    user,
    isCheckingAuth,
    isAuthenticated,
    userRoles,
    hasRole,
    setUser,
    clearUser,
    login,
    logout,
    checkAuth
  }
})
