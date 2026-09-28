import axios, { AxiosError, type AxiosInstance } from 'axios'

/**
 * Shared Axios HTTP client (Architecture §19.4, §19.5).
 *
 * - Base URL is the versioned REST prefix `/api/v1` (Architecture §19.6).
 * - `withCredentials` ensures the browser attaches the secure, HttpOnly,
 *   SameSite=Strict session cookie used for cookie-based authentication
 *   (Architecture §19.5).
 * - Authentication interceptors are wired below. A `401 Unauthorized`
 *   response emits a `cakra:unauthorized` browser event so the shell can
 *   clear local session state without coupling this module to the router.
 */
function resolveApiBaseUrl(): string {
  // In development, use Vite's dev proxy (/api/v1) so all requests are same-origin
  // to avoid browser CORS/cookie restrictions on localhost:5173.
  if (import.meta.env.DEV) {
    return '/api/v1'
  }

  const configured = (import.meta.env.VITE_API_BASE_URL || '').trim().replace(/\/+$/, '')
  if (!configured) {
    return '/api/v1'
  }
  if (configured.endsWith('/api/v1')) {
    return configured
  }
  if (configured.endsWith('/api')) {
    return `${configured}/api/v1`
  }
  return `${configured}/api/v1`
}

export const httpClient: AxiosInstance = axios.create({
  baseURL: resolveApiBaseUrl(),
  withCredentials: true,
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
  },
})

httpClient.interceptors.request.use((config) => {
  // Cookie-based sessions are attached automatically by the browser.
  // Placeholder for future anti-forgery/CSRF header injection.
  return config
})

httpClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    if (error.response?.status === 401) {
      window.dispatchEvent(new CustomEvent('cakra:unauthorized'))
    }

    return Promise.reject(error)
  },
)

export default httpClient
