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
    | 'EVALUATING'
    | 'ACCEPTED'
    | 'REJECTED'
    | 'IN_PROGRESS'
    | 'ESCALATED'
    | 'COMPLETED'
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
  addSubTask,
  completeSubTask,
  reopenSubTask,
  removeSubTask,
  getAssignedSubTasks,
}

export default requestService
