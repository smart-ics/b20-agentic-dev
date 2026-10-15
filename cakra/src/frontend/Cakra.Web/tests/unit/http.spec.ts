import { describe, expect, it } from 'vitest'
import { resolveApiBaseUrl } from '@/api/http'

describe('resolveApiBaseUrl', () => {
  it('returns /api/v1 in development mode regardless of env var', () => {
    expect(resolveApiBaseUrl('http://localhost:8084/api', true)).toBe('/api/v1')
  })

  it('resolves configured URL ending with /api properly to /v1 without duplicating /api', () => {
    expect(resolveApiBaseUrl('http://localhost:8084/api', false)).toBe('http://localhost:8084/api/v1')
    expect(resolveApiBaseUrl('http://localhost:8084/api/', false)).toBe('http://localhost:8084/api/v1')
  })

  it('resolves configured URL ending with /api/v1 unchanged', () => {
    expect(resolveApiBaseUrl('http://localhost:8084/api/v1', false)).toBe('http://localhost:8084/api/v1')
    expect(resolveApiBaseUrl('http://localhost:8084/api/v1/', false)).toBe('http://localhost:8084/api/v1')
  })

  it('resolves host URL without /api by appending /api/v1', () => {
    expect(resolveApiBaseUrl('http://localhost:8084', false)).toBe('http://localhost:8084/api/v1')
    expect(resolveApiBaseUrl('http://localhost:8084/', false)).toBe('http://localhost:8084/api/v1')
  })

  it('falls back to /api/v1 when no env var is configured', () => {
    expect(resolveApiBaseUrl('', false)).toBe('/api/v1')
    expect(resolveApiBaseUrl('   ', false)).toBe('/api/v1')
  })
})
