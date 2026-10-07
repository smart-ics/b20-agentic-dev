import httpClient from './http'

/**
 * Work Package API helper module (SCR-WP-001, CR-015).
 * (Architecture §7, §8, §11, §19.4, §19.6; CR-015 Architecture §4 TD-002, TD-005).
 */

export interface WorkPackageScopeItem {
  id: string
  membershipId?: string
  workPackageId: string
  requestId: string
  sortOrder?: number
  addedAt: string
  removedAt?: string | null
  isActive: boolean
  createdAt: string
  updatedAt?: string | null
  title: string
  requestTitle?: string
  description?: string
  requestType?: string
  status: string
  requestStatus?: string
  priority?: string
  ownerPersonId?: string | null
  requestOwnerPersonId?: string | null
  ownerName?: string | null
  requestOwnerName?: string | null
  customerId?: string | null
  customerName?: string | null
  productId?: string | null
  productName?: string | null
}

export interface WorkPackageDto {
  id: string
  workPackageId?: string
  name: string
  objective: string
  status: 'DRAFT' | 'ACTIVE' | 'CLOSED' | string
  ownerPersonId: string
  ownerName?: string | null
  customerId?: string | null
  customerName?: string | null
  customerCode?: string | null
  productId?: string | null
  productName?: string | null
  productCode?: string | null
  deadline?: string | null
  closedReason?: string | null
  closedAt?: string | null
  createdAt: string
  updatedAt?: string | null
  requests?: WorkPackageScopeItem[]
  activeRequests?: WorkPackageScopeItem[]
  activeRequestCount?: number
  totalRequestCount?: number
}

export interface CreateWorkPackagePayload {
  name?: string
  objective: string
  ownerPersonId: string
  customerId?: string | null
  productId?: string | null
  deadline?: string | null
}

export interface UpdateWorkPackageDeadlinePayload {
  deadline: string | null
}

export interface UpdateWorkPackageObjectivePayload {
  name?: string
  objective: string
}

export interface AssignWorkPackageOwnerPayload {
  newOwnerPersonId: string
  ownerPersonId?: string
}

export interface CloseWorkPackagePayload {
  reason?: string
}

export interface AddRequestToWorkPackagePayload {
  requestId: string
}

export interface ReorderWorkPackageRequestsPayload {
  orderedRequestIds: string[]
}

/**
 * Lists work packages matching the given query filters.
 */
export async function listWorkPackages(
  params?: Record<string, string>,
): Promise<WorkPackageDto[]> {
  const response = await httpClient.get<WorkPackageDto[]>('/work-packages', { params })
  return response.data
}

/**
 * Retrieves a work package by its ID.
 */
export async function getWorkPackageById(id: string): Promise<WorkPackageDto> {
  const response = await httpClient.get<WorkPackageDto>(`/work-packages/${id}`)
  return response.data
}

/**
 * Retrieves the scope (linked operational requests) of a work package.
 */
export async function getWorkPackageScope(id: string): Promise<WorkPackageScopeItem[]> {
  const response = await httpClient.get<WorkPackageScopeItem[]>(`/work-packages/${id}/scope`)
  return response.data
}

/**
 * Creates a new work package.
 */
export async function createWorkPackage(
  payload: CreateWorkPackagePayload,
): Promise<WorkPackageDto> {
  const response = await httpClient.post<WorkPackageDto>('/work-packages', payload)
  return response.data
}

/**
 * Updates the objective/name of a work package.
 */
export async function updateWorkPackageObjective(
  id: string,
  payload: UpdateWorkPackageObjectivePayload,
): Promise<WorkPackageDto> {
  const response = await httpClient.put<WorkPackageDto>(`/work-packages/${id}/objective`, payload)
  return response.data
}

/**
 * Updates or clears the target deadline date of a work package (CR-023).
 */
export async function updateWorkPackageDeadline(
  id: string,
  payload: UpdateWorkPackageDeadlinePayload,
): Promise<WorkPackageDto> {
  const response = await httpClient.put<WorkPackageDto>(`/work-packages/${id}/deadline`, payload)
  return response.data
}

/**
 * Assigns or reassigns the owner of a work package.
 */
export async function assignWorkPackageOwner(
  id: string,
  payload: AssignWorkPackageOwnerPayload,
): Promise<WorkPackageDto> {
  const response = await httpClient.post<WorkPackageDto>(
    `/work-packages/${id}/assign-owner`,
    payload,
  )
  return response.data
}

/**
 * Activates a DRAFT work package.
 */
export async function activateWorkPackage(id: string): Promise<WorkPackageDto> {
  const response = await httpClient.post<WorkPackageDto>(`/work-packages/${id}/activate`)
  return response.data
}

/**
 * Closes a DRAFT or ACTIVE work package.
 */
export async function closeWorkPackage(
  id: string,
  payload: CloseWorkPackagePayload,
): Promise<WorkPackageDto> {
  const response = await httpClient.post<WorkPackageDto>(`/work-packages/${id}/close`, payload)
  return response.data
}

/**
 * Adds an operational request to a work package scope.
 */
export async function addRequestToWorkPackage(
  id: string,
  payload: AddRequestToWorkPackagePayload,
): Promise<WorkPackageDto> {
  const response = await httpClient.post<WorkPackageDto>(`/work-packages/${id}/requests`, payload)
  return response.data
}

/**
 * Removes an operational request from a work package scope.
 */
export async function removeRequestFromWorkPackage(
  id: string,
  requestId: string,
): Promise<WorkPackageDto> {
  const response = await httpClient.delete<WorkPackageDto>(
    `/work-packages/${id}/requests/${requestId}`,
  )
  return response.data
}

/**
 * Reorders the active requests belonging to a work package (CR-015).
 */
export async function reorderWorkPackageRequests(
  workPackageId: string,
  orderedRequestIds: string[],
): Promise<WorkPackageDto> {
  const response = await httpClient.put<WorkPackageDto>(
    `/work-packages/${workPackageId}/requests/reorder`,
    { orderedRequestIds },
  )
  return response.data
}

export const workPackageService = {
  listWorkPackages,
  getWorkPackageById,
  getWorkPackageScope,
  createWorkPackage,
  updateWorkPackageObjective,
  updateWorkPackageDeadline,
  assignWorkPackageOwner,
  activateWorkPackage,
  closeWorkPackage,
  addRequestToWorkPackage,
  removeRequestFromWorkPackage,
  reorderWorkPackageRequests,
}

export default workPackageService
