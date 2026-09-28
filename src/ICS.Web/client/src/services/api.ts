import axios, { type AxiosInstance, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios'

/**
 * Authoritative HTTP Client configured for ICS REST API
 * Base URL: /api/v1 (Architecture §19.6)
 * Cookie-based session authentication withCredentials: true (Architecture §19.4, §19.5)
 */
export const apiClient: AxiosInstance = axios.create({
  baseURL: '/api/v1',
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json',
    'Accept': 'application/json'
  },
  timeout: 30000
})

// Request Interceptor: Attach client-side correlation or session metadata if needed
apiClient.interceptors.request.use(
  (config: InternalAxiosRequestConfig) => {
    // Session token is managed via secure, HttpOnly, SameSite=Strict cookies (Architecture §19.5)
    return config
  },
  (error: unknown) => {
    return Promise.reject(error)
  }
)

// Response Interceptor: Centralized handling for 401, 403, and ProblemDetails errors
apiClient.interceptors.response.use(
  (response: AxiosResponse) => {
    return response
  },
  (error: any) => {
    if (error.response) {
      const status = error.response.status
      if (status === 401) {
        // Unauthenticated session - redirect to login screen or emit auth expired event
        console.warn('[ICS API] 401 Unauthorized - Active session required.')
      } else if (status === 403) {
        console.warn('[ICS API] 403 Forbidden - Insufficient permissions for resource.')
      }
    }
    return Promise.reject(error)
  }
)

export default apiClient
