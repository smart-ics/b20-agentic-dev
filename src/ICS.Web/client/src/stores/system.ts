import { defineStore } from 'pinia'
import { ref } from 'vue'
import { apiClient } from '@/services/api'

export interface SystemContext {
  security: {
    userId: string | null
    personId: string | null
    roles: string[]
    isAuthenticated: boolean
  }
  audit: {
    userId: string | null
    personId: string | null
    timestamp: string
    ipAddress: string | null
    userAgent: string | null
  }
}

export const useSystemStore = defineStore('system', () => {
  const isInitialized = ref<boolean>(false)
  const appTitle = ref<string>('ICS Operational System')
  const modules = ref<string[]>([])
  const context = ref<SystemContext | null>(null)
  const loading = ref<boolean>(false)
  const errorMessage = ref<string | null>(null)

  async function fetchModules() {
    loading.value = true
    errorMessage.value = null
    try {
      const response = await apiClient.get<{ modules: string[] }>('/system/modules')
      modules.value = response.data.modules
    } catch (err: any) {
      errorMessage.value = err.message || 'Failed to load system modules'
    } finally {
      loading.value = false
    }
  }

  async function fetchContext() {
    try {
      const response = await apiClient.get<SystemContext>('/system/context')
      context.value = response.data
    } catch (err: any) {
      console.error('[SystemStore] Failed to fetch system context:', err)
    }
  }

  function initialize() {
    isInitialized.value = true
  }

  return {
    isInitialized,
    appTitle,
    modules,
    context,
    loading,
    errorMessage,
    fetchModules,
    fetchContext,
    initialize
  }
})
