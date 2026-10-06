import httpClient from './http'

/**
 * Request module and Sub-Task API helper module (CR-006, SCR-REQ-001..005).
 * (Architecture §4 TD-001..TD-010, §7, §8, §19.4, §19.6).
 */

export interface RequestSubTask {
  id: string
  requestId: string
  title: string
  isCompleted: boolean
  assigneePersonId?: string | null
  assigneeName?: string | null
  completedAt?: string | null
  completedByPersonId?: string | null
  completedByName?: string | null
  sortOrder: number
  createdAt: string
  updatedAt: string
}

export interface InitialSubTaskInput {
  title: string
  assigneePersonId?: string | null
  sortOrder?: number
}

export interface RequestDto {
  id: string
  requestId?: string
  title: string
  description: string
  requestType: string
  status:
    | 'CAPTURED'
    | 'ASSIGNED'
    | 'IN_PROGRESS'
    | 'PAUSED'
    | 'COMPLETED'
    | 'CANCELLED'
    | string
  priority: 'LOW' | 'NORMAL' | 'HIGH' | 'URGENT' | string
  complexity?: number
  ownerPersonId: string | null
  assigneePersonId?: string | null
  ownerName?: string | null
  assigneeName?: string | null
  customerId: string | null
  customerName?: string | null
  customerCode?: string | null
  productId: string | null
  productName?: string | null
  productCode?: string | null
  workPackageId?: string | null
  evaluationNotes?: string | null
  escalationReason?: string | null
  managementDecisionNotes?: string | null
  totalSubTasksCount: number
  completedSubTasksCount: number
  completionPercentage: number
  subTasks?: RequestSubTask[]
  createdAt: string
  updatedAt?: string | null
}

export type Request = RequestDto
export type RequestDetail = RequestDto

export interface RecordRequestPayload {
  title: string
  description?: string
  customerId?: string | null
  productId?: string | null
  workPackageId?: string | null
  requestType?: string
  priority?: string
  complexity?: number
  actorPersonId?: string | null
  initialSubTasks?: InitialSubTaskInput[]
}

/**
 * Records a new operational request (CR-019).
 */
export async function recordRequest(
  payload: RecordRequestPayload,
): Promise<RequestDto> {
  const response = await httpClient.post<RequestDto>('/requests', payload)
  return response.data
}

/**
 * Normalizes a single task line by stripping markdown checkboxes, bullets, and numbering (CR-019 TD-002).
 */
export function normalizeTaskLine(line: string): string {
  // 1. Strip markdown checkboxes like [ ], [x], [X]
  let cleaned = line.replace(/^\s*\[[ xX]?\]\s*/, '')
  // 2. Strip bullet prefixes (-, *, +) and numeric prefixes (1., 1), (1))
  cleaned = cleaned.replace(/^\s*(?:[-*+]|\d+[\.\)]|\(\d+\))\s*/, '')
  // 3. Trim whitespace
  return cleaned.trim()
}

/**
 * Parses raw multi-line task list text into normalized non-empty lines with a 255-character cap (CR-019 TD-002).
 */
export function parseTaskListText(rawText: string): string[] {
  if (!rawText) return []
  return rawText
    .split(/\r?\n/)
    .map((line) => normalizeTaskLine(line))
    .filter((line) => line.length > 0)
    .map((line) => (line.length > 255 ? line.substring(0, 255) : line))
}

export interface UpdateRequestCoreAttributesPayload {
  title: string
  description: string
  priority: string
  requestType: string
}

/**
 * Updates request core attributes (CR-018).
 */
export async function updateRequestCoreAttributes(
  id: string,
  payload: UpdateRequestCoreAttributesPayload,
): Promise<RequestDetail> {
  const response = await httpClient.put<RequestDetail>(`/requests/${id}`, payload)
  return response.data
}

/**
 * Starts active work on an assigned or paused request (CR-016 TD-002).
 */
export async function startWork(
  requestId: string,
  notes?: string | null,
): Promise<RequestDto> {
  const response = await httpClient.post<RequestDto>(`/requests/${requestId}/start`, {
    notes: notes || null,
  })
  return response.data
}

/**
 * Suspends active work on an in-progress request (CR-016 TD-002).
 */
export async function pauseWork(
  requestId: string,
  note?: string | null,
): Promise<RequestDto> {
  const response = await httpClient.post<RequestDto>(`/requests/${requestId}/pause`, {
    note: note || null,
  })
  return response.data
}

/**
 * Cancels a request from any active lifecycle state with mandatory reason (CR-016 TD-002).
 */
export async function cancelRequest(
  requestId: string,
  reason: string,
): Promise<RequestDto> {
  const response = await httpClient.post<RequestDto>(`/requests/${requestId}/cancel`, {
    reason,
  })
  return response.data
}

/**
 * Assigns an initial owner to a captured request.
 */
export async function assignRequestOwner(
  requestId: string,
  ownerPersonId: string,
  notes?: string | null,
): Promise<RequestDto> {
  const response = await httpClient.post<RequestDto>(`/requests/${requestId}/assign`, {
    ownerPersonId,
    notes: notes || null,
  })
  return response.data
}

/**
 * Reassigns request ownership, resetting active state to ASSIGNED (CR-016 TD-003).
 */
export async function reassignRequestOwner(
  requestId: string,
  newOwnerPersonId: string,
  notes?: string | null,
): Promise<RequestDto> {
  const response = await httpClient.post<RequestDto>(`/requests/${requestId}/reassign`, {
    newOwnerPersonId,
    notes: notes || null,
  })
  return response.data
}

/**
 * Completes an in-progress request with a resolution description.
 */
export async function completeRequest(
  requestId: string,
  resolutionDescription: string,
): Promise<RequestDto> {
  const response = await httpClient.post<RequestDto>(`/requests/${requestId}/complete`, {
    resolutionDescription,
  })
  return response.data
}

/**
 * Appends a new sub-task to an active request.
 */
export async function addSubTask(
  requestId: string,
  title: string,
  assigneePersonId?: string | null,
): Promise<RequestDto> {
  const response = await httpClient.post<RequestDto>(`/requests/${requestId}/subtasks`, {
    title,
    assigneePersonId: assigneePersonId || null,
  })
  return response.data
}

/**
 * Marks a pending sub-task as completed.
 */
export async function completeSubTask(
  requestId: string,
  subTaskId: string,
): Promise<RequestDto> {
  const response = await httpClient.post<RequestDto>(
    `/requests/${requestId}/subtasks/${subTaskId}/complete`,
  )
  return response.data
}

/**
 * Reopens a completed sub-task back to pending.
 */
export async function reopenSubTask(
  requestId: string,
  subTaskId: string,
): Promise<RequestDto> {
  const response = await httpClient.post<RequestDto>(
    `/requests/${requestId}/subtasks/${subTaskId}/reopen`,
  )
  return response.data
}

/**
 * Removes a sub-task from an active request.
 */
export async function removeSubTask(
  requestId: string,
  subTaskId: string,
): Promise<RequestDto> {
  const response = await httpClient.delete<RequestDto>(
    `/requests/${requestId}/subtasks/${subTaskId}`,
  )
  return response.data
}

/**
 * Retrieves all active requests containing sub-tasks assigned to the user.
 */
export async function getAssignedSubTasks(personId?: string): Promise<RequestDto[]> {
  const response = await httpClient.get<RequestDto[]>('/requests/assigned-subtasks', {
    params: personId ? { personId } : undefined,
  })
  return response.data
}

export const requestService = {
  recordRequest,
  normalizeTaskLine,
  parseTaskListText,
  startWork,
  pauseWork,
  cancelRequest,
  assignRequestOwner,
  reassignRequestOwner,
  completeRequest,
  updateRequestCoreAttributes,
  addSubTask,
  completeSubTask,
  reopenSubTask,
  removeSubTask,
  getAssignedSubTasks,
}

export default requestService
