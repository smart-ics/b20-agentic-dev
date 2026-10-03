import httpClient from './http'

/**
 * User Account Management API helper module (CR-007, SCR-USR-001, SCR-USR-002).
 * (Architecture §7, §8, §14, §19.4, §19.6).
 */

export interface UserAccountSummary {
  userId: string
  personId: string
  personName: string
  username: string
  email: string
  status: string
  failedLoginAttempts: number
  lastLoginAt?: string | null
  createdAt: string
  updatedAt?: string | null
}

export interface UserAccountDetail {
  userId: string
  personId: string
  personName: string
  username: string
  email: string
  status: string
  failedLoginAttempts: number
  lastLoginAt?: string | null
  createdAt: string
  updatedAt?: string | null
}

export interface CreateUserRequest {
  personId: string
  username: string
  email: string
  password: string
  status?: string
}

export interface UpdateUserRequest {
  email?: string
  status?: string
  newPassword?: string
}

/**
 * Retrieves all user account summaries enriched with person display names.
 */
export async function listUsers(): Promise<UserAccountSummary[]> {
  const response = await httpClient.get<UserAccountSummary[]>('/users')
  return response.data
}

/**
 * Retrieves a single user account by its unique identifier.
 */
export async function getUserById(id: string): Promise<UserAccountDetail> {
  const response = await httpClient.get<UserAccountDetail>(`/users/${id}`)
  return response.data
}

/**
 * Creates a new user account linked to an organizational person.
 */
export async function createUser(payload: CreateUserRequest): Promise<UserAccountDetail> {
  const response = await httpClient.post<UserAccountDetail>('/users', payload)
  return response.data
}

/**
 * Updates attributes, status, and/or resets password of an existing user account.
 */
export async function updateUser(
  id: string,
  payload: UpdateUserRequest,
): Promise<UserAccountDetail> {
  const response = await httpClient.put<UserAccountDetail>(`/users/${id}`, payload)
  return response.data
}
