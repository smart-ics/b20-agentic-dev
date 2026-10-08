import { AxiosError } from 'axios'
import httpClient from './http'

/**
 * Organizational Person Management API helper module (CR-008, FEAT-ORG-001, SCR-ORG-001, SCR-ORG-002).
 * (Architecture §7, §8, §14, §19.4, §19.6).
 */

export interface RoleDto {
  id: string
  name: string
  description?: string | null
}

export interface PersonDto {
  id: string
  personId?: string
  firstName: string
  lastName: string
  email: string
  status: string
  fullName: string
  isActive: boolean
  createdAt: string
  updatedAt?: string | null
  roles?: string[]
}

export interface CreatePersonRequest {
  firstName: string
  lastName: string
  email: string
  roleIds?: string[]
}

export interface UpdatePersonRequest {
  firstName: string
  lastName: string
  email: string
  roleIds?: string[]
}

export interface ProblemDetailsPayload {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  errorCode?: string
  errors?: Record<string, string[]>
  [key: string]: unknown
}

/**
 * Extracts a human-readable error message from an unknown error or RFC 7807 ProblemDetails payload.
 */
export function extractErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof AxiosError) {
    const data = err.response?.data as ProblemDetailsPayload | undefined
    if (data?.detail) {
      return data.detail
    }
    if (data?.title) {
      return data.title
    }
  }
  return fallback
}

/**
 * Retrieves all master roles available in the organization module.
 * Used by SCR-ORG-002 (PersonModal.vue) to populate role selection checkboxes.
 */
export async function listRoles(): Promise<RoleDto[]> {
  const response = await httpClient.get<RoleDto[]>('/organization/roles')
  return response.data
}

/**
 * Retrieves all organizational persons (both active and inactive) ordered by name.
 * Used by Person Management directory screen (SCR-ORG-001).
 */
export async function listAllPersons(): Promise<PersonDto[]> {
  const response = await httpClient.get<PersonDto[]>('/organization/persons/all')
  return response.data
}

/**
 * Retrieves only active organizational persons ordered by name.
 * Used by UI dropdown selectors (Product Owner, Assignee, Work Package Owner).
 */
export async function listActivePersons(): Promise<PersonDto[]> {
  const response = await httpClient.get<PersonDto[]>('/organization/persons/active')
  return response.data
}

/**
 * Convenience alias for listActivePersons.
 */
export const listPersons = listActivePersons

/**
 * Retrieves an organizational person by unique identifier.
 */
export async function getPersonById(id: string): Promise<PersonDto> {
  const response = await httpClient.get<PersonDto>(`/organization/persons/${id}`)
  return response.data
}

/**
 * Creates a new organizational person (restricted to Administrator/Admin).
 */
export async function createPerson(payload: CreatePersonRequest): Promise<PersonDto> {
  const response = await httpClient.post<PersonDto>('/organization/persons', payload)
  return response.data
}

/**
 * Updates identity attributes of an existing organizational person (restricted to Administrator/Admin).
 */
export async function updatePerson(
  id: string,
  payload: UpdatePersonRequest,
): Promise<PersonDto> {
  const response = await httpClient.put<PersonDto>(`/organization/persons/${id}`, payload)
  return response.data
}

/**
 * Activates an organizational person (status 'ACTIVE'; restricted to Administrator/Admin).
 */
export async function activatePerson(id: string): Promise<PersonDto> {
  const response = await httpClient.put<PersonDto>(`/organization/persons/${id}/activate`)
  return response.data
}

/**
 * Deactivates an organizational person (status 'INACTIVE'; restricted to Administrator/Admin).
 */
export async function deactivatePerson(id: string): Promise<PersonDto> {
  const response = await httpClient.put<PersonDto>(`/organization/persons/${id}/deactivate`)
  return response.data
}
