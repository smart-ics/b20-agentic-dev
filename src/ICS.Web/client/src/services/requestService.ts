import apiClient from './api'

export interface RequestSummaryDto {
  requestId: string
  title: string
  description: string
  type: string
  priority: string
  status: string
  requesterPersonId?: string | null
  requesterName?: string | null
  ownerPersonId?: string | null
  ownerName?: string | null
  customerId?: string | null
  customerName?: string | null
  productId?: string | null
  productName?: string | null
  workPackageId?: string | null
  isAwaitingManagementDecision: boolean
  escalatedAt?: string | null
  createdAt: string
  updatedAt?: string | null
  closedAt?: string | null
  resolutionOutcome?: string | null
}

export interface RequestResolutionDto {
  resolutionId: string
  requestId: string
  outcome: string
  summary: string
  resolvedByPersonId: string
  resolvedByName?: string | null
  resolvedAt: string
}

export interface RequestAssignmentDto {
  assignmentId: string
  requestId: string
  ownerPersonId: string
  ownerName?: string | null
  assignedByPersonId: string
  assignedByName?: string | null
  assignedAt: string
  note?: string | null
  isActive: boolean
}

export interface RequestStateHistoryDto {
  stateHistoryId: string
  requestId: string
  fromStatus?: string | null
  toStatus: string
  actorPersonId: string
  actorName?: string | null
  reason?: string | null
  changedAt: string
}

export interface RequestDto {
  requestId: string
  title: string
  description: string
  type: string
  priority: string
  status: string
  isTerminal: boolean
  isActive: boolean
  requesterPersonId?: string | null
  requesterContactId?: string | null
  requesterName?: string | null
  ownerPersonId?: string | null
  ownerName?: string | null
  customerId?: string | null
  customerName?: string | null
  customerCode?: string | null
  productId?: string | null
  productName?: string | null
  productCode?: string | null
  workPackageId?: string | null
  isAwaitingManagementDecision: boolean
  managementDecisionQuestion?: string | null
  managementDecisionOptions?: string | null
  managementDecisionImpact?: string | null
  managementDecisionRequestedAt?: string | null
  escalationReason?: string | null
  escalatedByPersonId?: string | null
  escalatedByName?: string | null
  escalatedAt?: string | null
  createdAt: string
  updatedAt?: string | null
  closedAt?: string | null
  resolution?: RequestResolutionDto | null
  assignments?: RequestAssignmentDto[] | null
  stateHistories?: RequestStateHistoryDto[] | null
}

export interface RequestGridResultDto {
  totalCount: number
  items: RequestSummaryDto[]
}

export interface MyAssignedRequestsResponseDto {
  activeCount: number
  awaitingDecisionCount: number
  escalatedCount: number
  items: RequestSummaryDto[]
}

export interface RequestGridFilterParams {
  searchTerm?: string
  customerId?: string
  productId?: string
  ownerPersonId?: string
  statuses?: string
  priority?: string
  fromDate?: string
  toDate?: string
  skip?: number
  take?: number
}

export interface CreateRequestPayload {
  title: string
  description: string
  type: string
  priority?: string
  customerId?: string
  productId?: string
  requesterPersonId?: string
  requesterContactId?: string
  requesterName?: string
  workPackageId?: string
  initialOwnerPersonId?: string
  assignedByPersonId?: string
  requestId?: string
}

export interface AssignOwnerPayload {
  newOwnerPersonId: string
  assignedByPersonId?: string
  note?: string
}

export interface EvaluateRequestPayload {
  evaluatedByPersonId?: string
  notes?: string
}

export interface AcceptResponsibilityPayload {
  acceptedByPersonId?: string
  notes?: string
}

export interface RejectRequestPayload {
  reason: string
  rejectedByPersonId?: string
}

export interface StartProgressPayload {
  actorPersonId?: string
  notes?: string
}

export interface EscalateRequestPayload {
  reason: string
  requiredAssistance?: string
  escalatedByPersonId?: string
}

export interface RequestManagementDecisionPayload {
  question: string
  options?: string
  impact?: string
  requestedByPersonId?: string
}

export interface CompleteRequestPayload {
  summary: string
  outcome?: string
  reviewerPersonId?: string
}

export interface ReworkRequestPayload {
  feedback: string
  reviewerPersonId?: string
}

export interface ReassignOwnerPayload {
  newOwnerPersonId: string
  reason?: string
  reassignedByPersonId?: string
}

export interface ResolveEscalationPayload {
  actorPersonId?: string
  notes?: string
}

export interface CustomerLookupDto {
  customerId: string
  code: string
  name: string
  status?: string
}

export interface ProductLookupDto {
  productId: string
  code: string
  name: string
  status?: string
}

export interface PersonLookupDto {
  personId: string
  name: string
  email?: string
  primaryRole?: string
  isActive?: boolean
}

export const requestService = {
  async getRequests(params?: RequestGridFilterParams): Promise<RequestGridResultDto> {
    const res = await apiClient.get<RequestGridResultDto>('/requests', { params })
    return res.data
  },

  async getRequestById(id: string): Promise<RequestDto> {
    const res = await apiClient.get<RequestDto>(`/requests/${id}`)
    return res.data
  },

  async createRequest(payload: CreateRequestPayload): Promise<RequestDto> {
    const res = await apiClient.post<RequestDto>('/requests', payload)
    return res.data
  },

  async getMyAssignedRequests(includeClosed = false): Promise<MyAssignedRequestsResponseDto> {
    const res = await apiClient.get<MyAssignedRequestsResponseDto>('/requests/my-assigned', {
      params: { includeClosed }
    })
    return res.data
  },

  async getRequestHistory(id: string): Promise<RequestStateHistoryDto[]> {
    const res = await apiClient.get<RequestStateHistoryDto[]>(`/requests/${id}/history`)
    return res.data
  },

  async assignOwner(id: string, payload: AssignOwnerPayload): Promise<RequestDto> {
    const res = await apiClient.post<RequestDto>(`/requests/${id}/assign`, payload)
    return res.data
  },

  async evaluateRequest(id: string, payload?: EvaluateRequestPayload): Promise<RequestDto> {
    const res = await apiClient.post<RequestDto>(`/requests/${id}/evaluate`, payload ?? {})
    return res.data
  },

  async acceptResponsibility(id: string, payload?: AcceptResponsibilityPayload): Promise<RequestDto> {
    const res = await apiClient.post<RequestDto>(`/requests/${id}/accept`, payload ?? {})
    return res.data
  },

  async rejectRequest(id: string, payload: RejectRequestPayload): Promise<RequestDto> {
    const res = await apiClient.post<RequestDto>(`/requests/${id}/reject`, payload)
    return res.data
  },

  async startProgress(id: string, payload?: StartProgressPayload): Promise<RequestDto> {
    const res = await apiClient.post<RequestDto>(`/requests/${id}/start-progress`, payload ?? {})
    return res.data
  },

  async escalateRequest(id: string, payload: EscalateRequestPayload): Promise<RequestDto> {
    const res = await apiClient.post<RequestDto>(`/requests/${id}/escalate`, payload)
    return res.data
  },

  async requestManagementDecision(id: string, payload: RequestManagementDecisionPayload): Promise<RequestDto> {
    const res = await apiClient.post<RequestDto>(`/requests/${id}/management-decision`, payload)
    return res.data
  },

  async completeRequest(id: string, payload: CompleteRequestPayload): Promise<RequestDto> {
    const res = await apiClient.post<RequestDto>(`/requests/${id}/complete`, payload)
    return res.data
  },

  async reworkRequest(id: string, payload: ReworkRequestPayload): Promise<RequestDto> {
    const res = await apiClient.post<RequestDto>(`/requests/${id}/rework`, payload)
    return res.data
  },

  async reassignOwner(id: string, payload: ReassignOwnerPayload): Promise<RequestDto> {
    const res = await apiClient.post<RequestDto>(`/requests/${id}/reassign`, payload)
    return res.data
  },

  async resolveEscalation(id: string, payload?: ResolveEscalationPayload): Promise<RequestDto> {
    const res = await apiClient.post<RequestDto>(`/requests/${id}/resolve-escalation`, payload ?? {})
    return res.data
  },

  async getActiveCustomers(): Promise<CustomerLookupDto[]> {
    const res = await apiClient.get<CustomerLookupDto[]>('/requests/customers')
    return res.data
  },

  async getActiveProducts(): Promise<ProductLookupDto[]> {
    const res = await apiClient.get<ProductLookupDto[]>('/requests/products')
    return res.data
  },

  async getActivePersons(): Promise<PersonLookupDto[]> {
    const res = await apiClient.get<PersonLookupDto[]>('/requests/persons')
    return res.data
  }
}

export default requestService
