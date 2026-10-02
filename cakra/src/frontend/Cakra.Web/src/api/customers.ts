import httpClient from './http'

/**
 * Customer Master Management API helper module (CR-002, SCR-CUST-001, SCR-CUST-002).
 * (Architecture §7, §8, §19.4, §19.6).
 */

export interface CustomerDto {
  id: string
  customerId?: string
  customerCode: string
  code?: string
  customerName: string
  name?: string
  status: string
  hasActiveMaintenanceContract: boolean
  isActive: boolean
  createdAt: string
  updatedAt?: string | null
}

export interface CustomerContactDto {
  id: string
  contactId?: string
  customerId: string
  name: string
  position?: string | null
  phoneNumber?: string | null
  email?: string | null
  status: string
  isActive: boolean
  createdAt: string
  updatedAt?: string | null
}

export interface CreateCustomerRequest {
  customerCode: string
  customerName: string
  hasActiveMaintenanceContract?: boolean
}

export interface UpdateCustomerRequest {
  customerCode: string
  customerName: string
  hasActiveMaintenanceContract: boolean
}

export interface CreateCustomerContactRequest {
  name: string
  position?: string | null
  phoneNumber?: string | null
  email?: string | null
}

export interface UpdateCustomerContactRequest {
  name: string
  position?: string | null
  phoneNumber?: string | null
  email?: string | null
  status?: string | null
}

export async function listCustomers(activeOnly = false): Promise<CustomerDto[]> {
  const response = await httpClient.get<CustomerDto[]>(`/customers`, {
    params: { activeOnly },
  })
  return response.data
}

export async function listActiveCustomers(): Promise<CustomerDto[]> {
  const response = await httpClient.get<CustomerDto[]>('/customers/active')
  return response.data
}

export async function getCustomerById(id: string): Promise<CustomerDto> {
  const response = await httpClient.get<CustomerDto>(`/customers/${id}`)
  return response.data
}

export async function getCustomerContacts(id: string): Promise<CustomerContactDto[]> {
  const response = await httpClient.get<CustomerContactDto[]>(`/customers/${id}/contacts`)
  return response.data
}

export async function createCustomer(payload: CreateCustomerRequest): Promise<CustomerDto> {
  const response = await httpClient.post<CustomerDto>('/customers', payload)
  return response.data
}

export async function updateCustomer(
  id: string,
  payload: UpdateCustomerRequest,
): Promise<CustomerDto> {
  const response = await httpClient.put<CustomerDto>(`/customers/${id}`, payload)
  return response.data
}

export async function activateCustomer(id: string): Promise<CustomerDto> {
  const response = await httpClient.put<CustomerDto>(`/customers/${id}/activate`)
  return response.data
}

export async function deactivateCustomer(id: string): Promise<CustomerDto> {
  const response = await httpClient.put<CustomerDto>(`/customers/${id}/deactivate`)
  return response.data
}

export async function createCustomerContact(
  customerId: string,
  payload: CreateCustomerContactRequest,
): Promise<CustomerContactDto> {
  const response = await httpClient.post<CustomerContactDto>(
    `/customers/${customerId}/contacts`,
    payload,
  )
  return response.data
}

export async function updateCustomerContact(
  customerId: string,
  contactId: string,
  payload: UpdateCustomerContactRequest,
): Promise<CustomerContactDto> {
  const response = await httpClient.put<CustomerContactDto>(
    `/customers/${customerId}/contacts/${contactId}`,
    payload,
  )
  return response.data
}
