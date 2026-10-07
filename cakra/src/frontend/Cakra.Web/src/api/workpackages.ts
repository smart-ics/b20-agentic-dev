import httpClient from './http'
import type { RequestDto } from './requests'

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
  complexity?: number
  blockerNote?: string | null
  ownerPersonId?: string | null
  requestOwnerPersonId?: string | null
  ownerName?: string | null
  requestOwnerName?: string | null
  customerId?: string | null
  customerName?: string | null
  productId?: string | null
  productName?: string | null
  request?: RequestDto | null
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

export interface UpdateWorkPackageContextPayload {
  customerId?: string | null
  productId?: string | null
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
 * Work Package Scope Pressure tiers benchmarked against C_org (Architecture CR-025 §4 TD-002).
 */
export type PressureTier =
  | 'UNPLANNED'
  | 'NOMINAL'
  | 'ELEVATED'
  | 'CRITICAL'
  | 'IMPOSSIBLE'

export const PRESSURE_TIERS: readonly PressureTier[] = [
  'UNPLANNED',
  'NOMINAL',
  'ELEVATED',
  'CRITICAL',
  'IMPOSSIBLE',
] as const

/**
 * Observable Operational Health Invariant states in severity precedence order (Architecture CR-025 §4 TD-003).
 */
export type HealthState =
  | 'DEADLINE_BREACHED'
  | 'ACTIVE_BLOCKERS'
  | 'DORMANT'
  | 'WIP_STAGNANT'
  | 'FLOWING'

export const HEALTH_STATES: readonly HealthState[] = [
  'DEADLINE_BREACHED',
  'ACTIVE_BLOCKERS',
  'DORMANT',
  'WIP_STAGNANT',
  'FLOWING',
] as const

/**
 * Single-day bucket within the 14-day Flow Activity Barcode (Architecture CR-025 §4 TD-004, TD-005).
 */
export interface FlowBarcodeDayDto {
  date: string
  closedCount: number
  stateMutationCount: number
  blockedCount: number
}

/**
 * Telemetry read model for a single active Work Package (Architecture CR-025 §4 TD-005).
 */
export interface WorkPackageTelemetryDto {
  id: string
  name: string
  objective: string
  status: string
  ownerPersonId: string
  ownerName?: string | null
  customerId?: string | null
  customerName?: string | null
  productId?: string | null
  productName?: string | null
  deadline?: string | null
  totalComplexity: number
  completedComplexity: number
  remainingComplexity: number
  totalRequestsCount: number
  remainingRequestsCount: number
  workingDaysRemaining?: number | null
  requiredDailyBurn?: number | null
  orgCapacityShare?: number | null
  pressureTier: PressureTier | string
  healthState: HealthState | string
  deadlineBreached: boolean
  blockedRequestsCount: number
  dormantDays: number
  isDormant: boolean
  activeWipCount: number
  oldestActiveWipDays: number
  isWipStagnant: boolean
  outflow14dCount: number
  flowBarcode: FlowBarcodeDayDto[]
}

/**
 * 2D Pressure × Health triage matrix distribution model (Architecture CR-025 §4 TD-005).
 */
export interface PressureHealthMatrixDto {
  cells: Record<string, Record<string, number>>
  rowTotals: Record<string, number>
  columnTotals: Record<string, number>
}

/**
 * Macro portfolio metrics displayed in the top ribbon (Architecture CR-025 §4 TD-005).
 */
export interface PortfolioMetricsDto {
  totalActiveWorkPackages: number
  withDeadlineCount: number
  withoutDeadlineCount: number
  orgDailyThroughput: number
  portfolioAggregateLoadPercentage: number
  totalInvariantViolationsCount: number
}

/**
 * Root read model for the Executive Operations Cockpit (Architecture CR-025 §4 TD-005).
 */
export interface OperationsCockpitDto {
  portfolioMetrics: PortfolioMetricsDto
  matrix: PressureHealthMatrixDto
  packages: WorkPackageTelemetryDto[]
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
 * Updates or clears the Customer and Product context associations of a work package (CR-024).
 */
export async function updateWorkPackageContext(
  id: string,
  payload: UpdateWorkPackageContextPayload,
): Promise<WorkPackageDto> {
  const response = await httpClient.put<WorkPackageDto>(`/work-packages/${id}/context`, payload)
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

/**
 * Retrieves executive operations cockpit telemetry, portfolio metrics, and the 2D Pressure × Health triage matrix
 * (Architecture CR-025 §4 TD-005; SCR-WP-002).
 */
export async function getOperationsCockpit(
  asOfDate?: string | null,
): Promise<OperationsCockpitDto> {
  const params: Record<string, string> = {}
  if (asOfDate) {
    params.asOfDate = asOfDate
  }
  const response = await httpClient.get<OperationsCockpitDto>(
    '/work-packages/operations-cockpit',
    { params },
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
  updateWorkPackageContext,
  assignWorkPackageOwner,
  activateWorkPackage,
  closeWorkPackage,
  addRequestToWorkPackage,
  removeRequestFromWorkPackage,
  reorderWorkPackageRequests,
  getOperationsCockpit,
}

export default workPackageService
