import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

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
 * Placeholder auth store established by the P1-S08 scaffold.
 * The concrete login/logout flows are implemented in P2-S09/P2-S10/P2-S11.
 */
export const useAuthStore = defineStore('auth', () => {
  const currentUser = ref<CurrentUser | null>(null)

  const isAuthenticated = computed(() => currentUser.value !== null)
  const roles = computed(() => currentUser.value?.roles ?? [])

  function setCurrentUser(user: CurrentUser | null): void {
    currentUser.value = user
  }

  function clear(): void {
    currentUser.value = null
  }

  return { currentUser, isAuthenticated, roles, setCurrentUser, clear }
})
