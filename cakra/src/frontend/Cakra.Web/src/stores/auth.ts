import axios from 'axios'
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

import httpClient from '@/api/http'

/**
 * Authenticated user context mirrored from the server security context.
 * Architecture §19.5 (cookie-based session) and §7 (CurrentContextProvider).
 */
export interface CurrentUser {
  userId: string
  personId: string
  roles: string[]
}

/**
 * Response returned by `POST /api/v1/auth/login`.
 */
export interface LoginResponse extends CurrentUser {
  expiresAt?: string
}

/**
 * RFC 7807 ProblemDetails payload returned on authentication errors (Architecture §19.6).
 */
export interface AuthProblemDetails {
  status?: number
  title?: string
  detail?: string
  errorCode?: string
  traceId?: string
}

/**
 * Pinia authentication store managing session state, login, logout, and user profile resolution
 * (SCR-AUTH-001, UC-AUTH-001, Architecture §14, §19.4, §19.5).
 */
export const useAuthStore = defineStore('auth', () => {
  const currentUser = ref<CurrentUser | null>(null)
  const isInitialized = ref<boolean>(false)
  const isLoading = ref<boolean>(false)
  const error = ref<string | null>(null)
  const errorCode = ref<string | null>(null)

  const isAuthenticated = computed(() => currentUser.value !== null)
  const roles = computed(() => currentUser.value?.roles ?? [])

  function setCurrentUser(user: CurrentUser | null): void {
    currentUser.value = user
    isInitialized.value = true
  }

  function clear(): void {
    currentUser.value = null
  }

  function clearError(): void {
    error.value = null
    errorCode.value = null
  }

  async function login(username: string, password: string): Promise<CurrentUser> {
    isLoading.value = true
    error.value = null
    errorCode.value = null

    try {
      const response = await httpClient.post<LoginResponse>('/auth/login', {
        username,
        password,
      })

      const user: CurrentUser = {
        userId: response.data.userId,
        personId: response.data.personId,
        roles: response.data.roles ?? [],
      }

      currentUser.value = user
      isInitialized.value = true
      return user
    } catch (err: unknown) {
      currentUser.value = null

      if (axios.isAxiosError<AuthProblemDetails>(err) && err.response?.data) {
        const problem = err.response.data
        const code = problem.errorCode ?? null
        errorCode.value = code

        if (code === 'ACCOUNT_LOCKED') {
          error.value =
            problem.detail ||
            'Your account has been locked due to multiple failed login attempts. Please contact an administrator.'
        } else if (code === 'INVALID_CREDENTIALS') {
          error.value = problem.detail || 'Invalid username or password.'
        } else if (code === 'ACCOUNT_NOT_ACTIVE' || code === 'PERSON_INACTIVE') {
          error.value = problem.detail || 'Your account is not active. Please contact an administrator.'
        } else {
          error.value = problem.detail || problem.title || 'Authentication failed. Please try again.'
        }
      } else {
        errorCode.value = 'NETWORK_ERROR'
        error.value = 'Unable to reach the authentication server. Please try again.'
      }

      throw err
    } finally {
      isLoading.value = false
    }
  }

  async function logout(): Promise<void> {
    isLoading.value = true
    try {
      await httpClient.post('/auth/logout')
    } finally {
      currentUser.value = null
      error.value = null
      errorCode.value = null
      isLoading.value = false
    }
  }

  async function fetchCurrentUser(): Promise<CurrentUser | null> {
    try {
      const response = await httpClient.get<CurrentUser>('/auth/me')
      const user: CurrentUser = {
        userId: response.data.userId,
        personId: response.data.personId,
        roles: response.data.roles ?? [],
      }
      currentUser.value = user
      return user
    } catch {
      currentUser.value = null
      return null
    } finally {
      isInitialized.value = true
    }
  }

  return {
    currentUser,
    isInitialized,
    isLoading,
    error,
    errorCode,
    isAuthenticated,
    roles,
    setCurrentUser,
    clear,
    clearError,
    login,
    logout,
    fetchCurrentUser,
  }
})
