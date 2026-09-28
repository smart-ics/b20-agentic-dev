<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { httpClient } from '@/api/http'

/**
 * SCR-REQ-003: Request Detail Screen
 * (Architecture §7, §8 — UC-REQ-002..008, UC-COL-003, UC-MGT-001, §9 — FEAT-REQ-001..008, §18, §19.4, §20, §21).
 *
 * - Renders a Bootstrap 5 card displaying authoritative request details:
 *   Title, Description, Customer, Product, Status, Assignee, CreatedAt, UpdatedAt,
 *   plus EvaluationNotes, EscalationReason, ManagementDecisionNotes, and Resolution when present.
 * - Renders conditional lifecycle action controls based on current request state:
 *   * CAPTURED: Assign (with active person dropdown from `GET /api/v1/organization/persons/active`)
 *   * EVALUATING: Evaluate, Accept, Reject, Escalate
 *   * IN_PROGRESS: Complete, Escalate
 *   * Active / ESCALATED: Management Decision, Reassign
 * - Renders the chronological state history timeline below the card (`GET /api/v1/requests/${id}/history`).
 */

export interface RequestResolutionDetail {
  id: string
  requestId: string
  outcome: string
  description: string
  resolvedBy: string
  resolvedByName?: string | null
  resolvedAt: string
  createdAt: string
  updatedAt?: string | null
}

export interface RequestAssignmentHistoryItem {
  id: string
  requestId: string
  previousOwnerPersonId?: string | null
  previousOwnerName?: string | null
  assignedOwnerPersonId?: string | null
  assignedOwnerName?: string | null
  actorPersonId: string
  actorName?: string | null
  previousStatus?: string | null
  newStatus: string
  assignedAtUtc: string
  timestamp?: string
  notes?: string | null
  createdAt: string
  updatedAt?: string | null
}

export interface RequestDetail {
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
  resolution?: RequestResolutionDetail | null
  assignments?: RequestAssignmentHistoryItem[]
  createdAt: string
  updatedAt?: string | null
}

export interface ActivePersonOption {
  id: string
  personId?: string
  firstName: string
  lastName: string
  fullName: string
  email: string
  status: string
}

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const route = useRoute()
const router = useRouter()

const requestId = computed(() => String(route.params.id ?? '').trim())

const request = ref<RequestDetail | null>(null)
const stateHistory = ref<RequestAssignmentHistoryItem[]>([])
const activePersons = ref<ActivePersonOption[]>([])

const isLoading = ref(false)
const isHistoryLoading = ref(false)
const isSubmittingAction = ref(false)
const errorMessage = ref<string | null>(null)
const actionSuccessMessage = ref<string | null>(null)

const assignForm = reactive({
  ownerPersonId: '',
  notes: '',
})

const evaluateForm = reactive({
  evaluationNotes: '',
})

const acceptForm = reactive({
  notes: '',
})

const rejectForm = reactive({
  reason: '',
})

const escalateForm = reactive({
  reason: '',
})

const completeForm = reactive({
  resolutionDescription: '',
})

const managementDecisionForm = reactive({
  decisionDetails: '',
  targetStatus: '',
})

const reassignForm = reactive({
  newOwnerPersonId: '',
  notes: '',
  targetStatusForEscalated: 'EVALUATING',
})

const normalizedStatus = computed(() => (request.value?.status ?? '').toUpperCase())

const isCaptured = computed(() => normalizedStatus.value === 'CAPTURED')
const isEvaluating = computed(() => normalizedStatus.value === 'EVALUATING')
const isAccepted = computed(() => normalizedStatus.value === 'ACCEPTED')
const isInProgress = computed(() => normalizedStatus.value === 'IN_PROGRESS')
const isEscalated = computed(() => normalizedStatus.value === 'ESCALATED')
const isClosed = computed(
  () => normalizedStatus.value === 'COMPLETED' || normalizedStatus.value === 'REJECTED',
)

// Visibility rules for conditional lifecycle actions
const canAssign = computed(() => isCaptured.value)
const canEvaluate = computed(() => isEvaluating.value)
const canAccept = computed(() => isEvaluating.value || isAccepted.value)
const canReject = computed(() => isEvaluating.value)
const canEscalate = computed(() => isEvaluating.value || isInProgress.value)
const canComplete = computed(() => isInProgress.value)
const canManagementDecision = computed(
  () => !isClosed.value && (isEscalated.value || isEvaluating.value || isAccepted.value || isInProgress.value),
)
const canReassign = computed(
  () => !isClosed.value && (isEscalated.value || isEvaluating.value || isAccepted.value || isInProgress.value),
)

const personNameById = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const person of activePersons.value) {
    const label = person.fullName || `${person.firstName} ${person.lastName}`.trim()
    map[person.id] = label
  }
  return map
})

function extractErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof AxiosError) {
    const data = err.response?.data as ProblemDetailsPayload | undefined
    if (data?.errors) {
      const messages = Object.values(data.errors).flat()
      if (messages.length > 0) {
        return messages.join(' ')
      }
    }
    if (data?.detail) {
      return data.detail
    }
    if (data?.title) {
      return data.title
    }
  }
  return fallback
}

function statusBadgeClass(status: string | null | undefined): string {
  switch ((status ?? '').toUpperCase()) {
    case 'CAPTURED':
      return 'text-bg-secondary'
    case 'EVALUATING':
      return 'text-bg-info'
    case 'ACCEPTED':
      return 'text-bg-primary'
    case 'IN_PROGRESS':
      return 'text-bg-primary'
    case 'ESCALATED':
      return 'text-bg-warning'
    case 'COMPLETED':
      return 'text-bg-success'
    case 'REJECTED':
      return 'text-bg-danger'
    default:
      return 'text-bg-secondary'
  }
}

function priorityBadgeClass(priority: string | null | undefined): string {
  switch ((priority ?? '').toUpperCase()) {
    case 'URGENT':
      return 'text-bg-danger'
    case 'HIGH':
      return 'text-bg-warning'
    case 'LOW':
      return 'text-bg-light border text-secondary'
    default:
      return 'text-bg-light border text-body-secondary'
  }
}

function resolveCustomerDisplay(item: RequestDetail | null): string {
  if (!item) {
    return '—'
  }
  if (item.customerName && item.customerName.trim().length > 0) {
    return item.customerCode ? `${item.customerName} (${item.customerCode})` : item.customerName
  }
  if (item.customerCode && item.customerCode.trim().length > 0) {
    return item.customerCode
  }
  return item.customerId ?? '—'
}

function resolveProductDisplay(item: RequestDetail | null): string {
  if (!item) {
    return '—'
  }
  if (item.productName && item.productName.trim().length > 0) {
    return item.productCode ? `${item.productName} (${item.productCode})` : item.productName
  }
  if (item.productCode && item.productCode.trim().length > 0) {
    return item.productCode
  }
  return item.productId ?? '—'
}

function resolveAssigneeDisplay(item: RequestDetail | null): string {
  if (!item) {
    return 'Unassigned'
  }
  if (item.assigneeName && item.assigneeName.trim().length > 0) {
    return item.assigneeName
  }
  if (item.ownerName && item.ownerName.trim().length > 0) {
    return item.ownerName
  }
  const assigneeId = item.assigneePersonId ?? item.ownerPersonId
  if (assigneeId) {
    return personNameById.value[assigneeId] ?? assigneeId
  }
  return 'Unassigned'
}

function resolvePersonDisplay(personId: string | null | undefined, personName?: string | null): string {
  if (personName && personName.trim().length > 0) {
    return personName
  }
  if (personId) {
    return personNameById.value[personId] ?? personId
  }
  return '—'
}

function formatTimestamp(value: string | null | undefined): string {
  if (!value) {
    return '—'
  }
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }
  return parsed.toLocaleString()
}

async function loadActivePersons(): Promise<void> {
  try {
    const response = await httpClient.get<ActivePersonOption[]>('/organization/persons/active')
    activePersons.value = response.data
  } catch {
    activePersons.value = []
  }
}

async function loadRequestDetail(): Promise<void> {
  if (!requestId.value) {
    errorMessage.value = 'Request ID is missing from route.'
    return
  }

  isLoading.value = true
  errorMessage.value = null

  try {
    const response = await httpClient.get<RequestDetail>(`/requests/${requestId.value}`)
    request.value = response.data
    if (response.data.evaluationNotes && !evaluateForm.evaluationNotes) {
      evaluateForm.evaluationNotes = response.data.evaluationNotes
    }
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load request details.')
  } finally {
    isLoading.value = false
  }
}

async function loadStateHistory(): Promise<void> {
  if (!requestId.value) {
    return
  }

  isHistoryLoading.value = true

  try {
    const response = await httpClient.get<RequestAssignmentHistoryItem[]>(
      `/requests/${requestId.value}/history`,
    )
    stateHistory.value = response.data
  } catch (err) {
    if (!errorMessage.value) {
      errorMessage.value = extractErrorMessage(err, 'Failed to load request state history.')
    }
  } finally {
    isHistoryLoading.value = false
  }
}

async function refreshAll(): Promise<void> {
  actionSuccessMessage.value = null
  await Promise.all([loadRequestDetail(), loadStateHistory()])
}

async function handleAssign(): Promise<void> {
  if (!requestId.value) {
    return
  }

  const ownerPersonId = assignForm.ownerPersonId.trim()
  if (!ownerPersonId) {
    errorMessage.value = 'Please select an active person to assign this request.'
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const response = await httpClient.post<RequestDetail>(`/requests/${requestId.value}/assign`, {
      ownerPersonId,
      notes: assignForm.notes.trim() || null,
    })
    request.value = response.data
    assignForm.ownerPersonId = ''
    assignForm.notes = ''
    actionSuccessMessage.value = 'Request assigned and transitioned to EVALUATING.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to assign request owner.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleEvaluate(): Promise<void> {
  if (!requestId.value) {
    return
  }

  const evaluationNotes = evaluateForm.evaluationNotes.trim()
  if (!evaluationNotes) {
    errorMessage.value = 'Evaluation notes are required.'
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const response = await httpClient.post<RequestDetail>(`/requests/${requestId.value}/evaluate`, {
      evaluationNotes,
    })
    request.value = response.data
    actionSuccessMessage.value = 'Evaluation notes recorded.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to record evaluation notes.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleAccept(): Promise<void> {
  if (!requestId.value) {
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const response = await httpClient.post<RequestDetail>(`/requests/${requestId.value}/accept`, {
      notes: acceptForm.notes.trim() || null,
    })
    request.value = response.data
    acceptForm.notes = ''
    actionSuccessMessage.value = 'Request accepted and transitioned to IN_PROGRESS.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to accept request.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleReject(): Promise<void> {
  if (!requestId.value) {
    return
  }

  const reason = rejectForm.reason.trim()
  if (!reason) {
    errorMessage.value = 'Rejection reason is required.'
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const response = await httpClient.post<RequestDetail>(`/requests/${requestId.value}/reject`, {
      reason,
    })
    request.value = response.data
    rejectForm.reason = ''
    actionSuccessMessage.value = 'Request rejected.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to reject request.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleEscalate(): Promise<void> {
  if (!requestId.value) {
    return
  }

  const reason = escalateForm.reason.trim()
  if (!reason) {
    errorMessage.value = 'Escalation reason is required.'
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const response = await httpClient.post<RequestDetail>(`/requests/${requestId.value}/escalate`, {
      reason,
    })
    request.value = response.data
    escalateForm.reason = ''
    actionSuccessMessage.value = 'Request escalated.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to escalate request.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleComplete(): Promise<void> {
  if (!requestId.value) {
    return
  }

  const resolutionDescription = completeForm.resolutionDescription.trim()
  if (!resolutionDescription) {
    errorMessage.value = 'Resolution description is required to complete the request.'
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const response = await httpClient.post<RequestDetail>(`/requests/${requestId.value}/complete`, {
      resolutionDescription,
    })
    request.value = response.data
    completeForm.resolutionDescription = ''
    actionSuccessMessage.value = 'Request completed.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to complete request.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleManagementDecision(): Promise<void> {
  if (!requestId.value) {
    return
  }

  const decisionDetails = managementDecisionForm.decisionDetails.trim()
  if (!decisionDetails) {
    errorMessage.value = 'Management decision details are required.'
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const payload: Record<string, string | null> = {
      decisionDetails,
    }
    if (isEscalated.value && managementDecisionForm.targetStatus.trim().length > 0) {
      payload.targetStatus = managementDecisionForm.targetStatus.trim()
    }

    const response = await httpClient.post<RequestDetail>(
      `/requests/${requestId.value}/management-decision`,
      payload,
    )
    request.value = response.data
    managementDecisionForm.decisionDetails = ''
    managementDecisionForm.targetStatus = ''
    actionSuccessMessage.value = 'Management decision recorded.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to record management decision.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleReassign(): Promise<void> {
  if (!requestId.value) {
    return
  }

  const newOwnerPersonId = reassignForm.newOwnerPersonId.trim()
  if (!newOwnerPersonId) {
    errorMessage.value = 'Please select a new owner to reassign this request.'
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const payload: Record<string, string | null> = {
      newOwnerPersonId,
      notes: reassignForm.notes.trim() || null,
    }
    if (isEscalated.value && reassignForm.targetStatusForEscalated.trim().length > 0) {
      payload.targetStatusForEscalated = reassignForm.targetStatusForEscalated.trim()
    }

    const response = await httpClient.post<RequestDetail>(
      `/requests/${requestId.value}/reassign`,
      payload,
    )
    request.value = response.data
    reassignForm.newOwnerPersonId = ''
    reassignForm.notes = ''
    actionSuccessMessage.value = 'Request ownership reassigned.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to reassign request ownership.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function navigateBackToList(): Promise<void> {
  await router.push('/requests')
}

watch(
  () => route.params.id,
  async (newId, oldId) => {
    if (newId && newId !== oldId) {
      await Promise.all([loadRequestDetail(), loadStateHistory()])
    }
  },
)

onMounted(async () => {
  await Promise.all([loadActivePersons(), loadRequestDetail(), loadStateHistory()])
})
</script>

<template>
  <section class="container-fluid py-2" data-screen-id="SCR-REQ-003">
    <!-- Screen Header -->
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-3 mb-4">
      <div>
        <div class="d-flex align-items-center gap-2">
          <h1 class="h3 mb-0 fw-bold">Request Detail</h1>
          <span class="badge text-bg-light border text-secondary">SCR-REQ-003</span>
        </div>
        <p class="text-body-secondary small mb-0 mt-1">
          Inspect request details, execute lifecycle state transitions, and review the audit history timeline.
        </p>
      </div>

      <div class="d-flex align-items-center gap-2">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm"
          :disabled="isLoading || isHistoryLoading"
          data-testid="refresh-request-detail-button"
          @click="refreshAll"
        >
          <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>
          Refresh
        </button>

        <router-link
          to="/requests"
          class="btn btn-outline-secondary btn-sm"
          data-testid="back-to-requests-link"
          @click.prevent="navigateBackToList"
        >
          <i class="bi bi-arrow-left me-1" aria-hidden="true"></i>
          Back to Requests
        </router-link>
      </div>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      role="alert"
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center gap-2 mb-4"
      data-testid="request-detail-error-alert"
    >
      <i class="bi bi-exclamation-triangle-fill flex-shrink-0" aria-hidden="true"></i>
      <div>{{ errorMessage }}</div>
      <button
        type="button"
        class="btn-close"
        aria-label="Close"
        @click="errorMessage = null"
      ></button>
    </div>

    <!-- Success Alert -->
    <div
      v-if="actionSuccessMessage"
      role="status"
      class="alert alert-success alert-dismissible fade show d-flex align-items-center gap-2 mb-4"
      data-testid="request-detail-success-alert"
    >
      <i class="bi bi-check-circle-fill flex-shrink-0" aria-hidden="true"></i>
      <div>{{ actionSuccessMessage }}</div>
      <button
        type="button"
        class="btn-close"
        aria-label="Close"
        @click="actionSuccessMessage = null"
      ></button>
    </div>

    <!-- Loading Indicator -->
    <div v-if="isLoading && !request" class="card shadow-sm border-0 mb-4">
      <div class="card-body py-5 text-center text-body-secondary">
        <span
          class="spinner-border spinner-border-sm me-2"
          role="status"
          aria-hidden="true"
        ></span>
        Loading request details...
      </div>
    </div>

    <!-- Request Details Card -->
    <div v-if="request" class="card shadow-sm border-0 mb-4" data-testid="request-detail-card">
      <div class="card-header bg-body-tertiary py-3 d-flex flex-wrap justify-content-between align-items-center gap-2">
        <div class="d-flex align-items-center gap-2 flex-wrap">
          <span class="fw-bold fs-5" data-testid="request-detail-title">{{ request.title }}</span>
          <span
            class="badge"
            :class="statusBadgeClass(request.status)"
            data-testid="request-detail-status"
          >
            {{ request.status }}
          </span>
          <span
            v-if="request.priority"
            class="badge"
            :class="priorityBadgeClass(request.priority)"
            data-testid="request-detail-priority"
          >
            {{ request.priority }}
          </span>
          <span
            v-if="request.requestType"
            class="badge text-bg-light border text-secondary"
            data-testid="request-detail-type"
          >
            {{ request.requestType }}
          </span>
        </div>
        <span class="font-monospace small text-body-secondary" data-testid="request-detail-id">
          {{ request.id }}
        </span>
      </div>

      <div class="card-body p-4">
        <div class="row g-4">
          <div class="col-12">
            <h2 class="h6 text-uppercase text-body-secondary fw-semibold mb-2">Description</h2>
            <p class="mb-0" style="white-space: pre-wrap" data-testid="request-detail-description">
              {{ request.description }}
            </p>
          </div>

          <div class="col-12 col-sm-6 col-lg-3">
            <div class="small text-body-secondary fw-medium">Customer</div>
            <div class="fw-semibold mt-1" data-testid="request-detail-customer">
              {{ resolveCustomerDisplay(request) }}
            </div>
          </div>

          <div class="col-12 col-sm-6 col-lg-3">
            <div class="small text-body-secondary fw-medium">Product</div>
            <div class="fw-semibold mt-1" data-testid="request-detail-product">
              {{ resolveProductDisplay(request) }}
            </div>
          </div>

          <div class="col-12 col-sm-6 col-lg-3">
            <div class="small text-body-secondary fw-medium">Status</div>
            <div class="mt-1">
              <span class="badge" :class="statusBadgeClass(request.status)">
                {{ request.status }}
              </span>
            </div>
          </div>

          <div class="col-12 col-sm-6 col-lg-3">
            <div class="small text-body-secondary fw-medium">Assignee</div>
            <div class="fw-semibold mt-1" data-testid="request-detail-assignee">
              <i class="bi bi-person me-1 text-secondary" aria-hidden="true"></i>
              {{ resolveAssigneeDisplay(request) }}
            </div>
          </div>

          <div class="col-12 col-sm-6 col-lg-3">
            <div class="small text-body-secondary fw-medium">CreatedAt</div>
            <div class="mt-1 small" data-testid="request-detail-created-at">
              {{ formatTimestamp(request.createdAt) }}
            </div>
          </div>

          <div class="col-12 col-sm-6 col-lg-3">
            <div class="small text-body-secondary fw-medium">UpdatedAt</div>
            <div class="mt-1 small" data-testid="request-detail-updated-at">
              {{ formatTimestamp(request.updatedAt) }}
            </div>
          </div>

          <!-- Evaluation Notes if present -->
          <div v-if="request.evaluationNotes" class="col-12">
            <div class="p-3 rounded bg-info-subtle border border-info-subtle">
              <div class="small fw-semibold text-info-emphasis mb-1">
                <i class="bi bi-journal-check me-1" aria-hidden="true"></i>
                Evaluation Notes
              </div>
              <div style="white-space: pre-wrap" data-testid="request-detail-evaluation-notes">
                {{ request.evaluationNotes }}
              </div>
            </div>
          </div>

          <!-- Escalation Reason if present -->
          <div v-if="request.escalationReason" class="col-12">
            <div class="p-3 rounded bg-warning-subtle border border-warning-subtle">
              <div class="small fw-semibold text-warning-emphasis mb-1">
                <i class="bi bi-exclamation-octagon me-1" aria-hidden="true"></i>
                Escalation Reason
              </div>
              <div style="white-space: pre-wrap" data-testid="request-detail-escalation-reason">
                {{ request.escalationReason }}
              </div>
            </div>
          </div>

          <!-- Management Decision Notes if present -->
          <div v-if="request.managementDecisionNotes" class="col-12">
            <div class="p-3 rounded bg-primary-subtle border border-primary-subtle">
              <div class="small fw-semibold text-primary-emphasis mb-1">
                <i class="bi bi-diagram-3 me-1" aria-hidden="true"></i>
                Management Decision Notes
              </div>
              <div style="white-space: pre-wrap" data-testid="request-detail-management-decision-notes">
                {{ request.managementDecisionNotes }}
              </div>
            </div>
          </div>

          <!-- Resolution Details if present -->
          <div v-if="request.resolution" class="col-12">
            <div
              class="p-3 rounded border"
              :class="
                request.resolution.outcome === 'REJECTED'
                  ? 'bg-danger-subtle border-danger-subtle'
                  : 'bg-success-subtle border-success-subtle'
              "
              data-testid="request-detail-resolution"
            >
              <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-1">
                <span class="small fw-semibold">
                  <i class="bi bi-check2-circle me-1" aria-hidden="true"></i>
                  Resolution ({{ request.resolution.outcome }})
                </span>
                <span class="small text-body-secondary">
                  Resolved by
                  {{ resolvePersonDisplay(request.resolution.resolvedBy, request.resolution.resolvedByName) }}
                  on {{ formatTimestamp(request.resolution.resolvedAt) }}
                </span>
              </div>
              <div style="white-space: pre-wrap" data-testid="request-detail-resolution-description">
                {{ request.resolution.description }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Conditional Lifecycle Actions Card -->
    <div
      v-if="request && !isClosed"
      class="card shadow-sm border-0 mb-4"
      data-testid="request-actions-card"
    >
      <div class="card-header bg-body-tertiary py-3">
        <span class="fw-semibold">
          <i class="bi bi-sliders me-2 text-primary" aria-hidden="true"></i>
          Lifecycle Actions ({{ request.status }})
        </span>
      </div>

      <div class="card-body p-4">
        <div class="row g-4">
          <!-- CAPTURED: Assign Request Owner -->
          <div v-if="canAssign" class="col-12" data-testid="assign-action-section">
            <form class="border rounded p-3 bg-light-subtle" @submit.prevent="handleAssign">
              <h3 class="h6 fw-bold mb-3">Assign Request Owner</h3>
              <div class="row g-3 align-items-end">
                <div class="col-12 col-md-5">
                  <label for="assignOwnerSelect" class="form-label small fw-medium">
                    Assignee <span class="text-danger">*</span>
                  </label>
                  <select
                    id="assignOwnerSelect"
                    v-model="assignForm.ownerPersonId"
                    class="form-select"
                    required
                    :disabled="isSubmittingAction"
                    data-testid="assign-owner-select"
                  >
                    <option value="">Select active person...</option>
                    <option
                      v-for="person in activePersons"
                      :key="person.id"
                      :value="person.id"
                    >
                      {{ person.fullName }} ({{ person.email }})
                    </option>
                  </select>
                </div>
                <div class="col-12 col-md-5">
                  <label for="assignNotesInput" class="form-label small fw-medium">
                    Assignment Notes
                  </label>
                  <input
                    id="assignNotesInput"
                    v-model="assignForm.notes"
                    type="text"
                    class="form-control"
                    placeholder="Optional assignment instructions"
                    :disabled="isSubmittingAction"
                    data-testid="assign-notes-input"
                  />
                </div>
                <div class="col-12 col-md-2 d-grid">
                  <button
                    type="submit"
                    class="btn btn-primary"
                    :disabled="isSubmittingAction || !assignForm.ownerPersonId"
                    data-testid="assign-request-button"
                  >
                    <i class="bi bi-person-check me-1" aria-hidden="true"></i>
                    Assign
                  </button>
                </div>
              </div>
            </form>
          </div>

          <!-- EVALUATING: Evaluate Request -->
          <div v-if="canEvaluate" class="col-12 col-lg-6" data-testid="evaluate-action-section">
            <form class="border rounded p-3 h-100 bg-light-subtle" @submit.prevent="handleEvaluate">
              <h3 class="h6 fw-bold mb-2">Evaluate Request</h3>
              <div class="mb-3">
                <label for="evaluateNotesInput" class="form-label small fw-medium">
                  Evaluation Notes <span class="text-danger">*</span>
                </label>
                <textarea
                  id="evaluateNotesInput"
                  v-model="evaluateForm.evaluationNotes"
                  class="form-control"
                  rows="2"
                  placeholder="Record triage assessment and technical findings..."
                  required
                  :disabled="isSubmittingAction"
                  data-testid="evaluate-notes-input"
                ></textarea>
              </div>
              <button
                type="submit"
                class="btn btn-info text-white"
                :disabled="isSubmittingAction || !evaluateForm.evaluationNotes.trim()"
                data-testid="evaluate-request-button"
              >
                <i class="bi bi-clipboard-check me-1" aria-hidden="true"></i>
                Evaluate
              </button>
            </form>
          </div>

          <!-- EVALUATING / ACCEPTED: Accept Responsibility -->
          <div v-if="canAccept" class="col-12 col-lg-6" data-testid="accept-action-section">
            <form class="border rounded p-3 h-100 bg-light-subtle" @submit.prevent="handleAccept">
              <h3 class="h6 fw-bold mb-2">Accept Responsibility</h3>
              <div class="mb-3">
                <label for="acceptNotesInput" class="form-label small fw-medium">
                  Acceptance Notes
                </label>
                <textarea
                  id="acceptNotesInput"
                  v-model="acceptForm.notes"
                  class="form-control"
                  rows="2"
                  placeholder="Optional notes when accepting and starting work..."
                  :disabled="isSubmittingAction"
                  data-testid="accept-notes-input"
                ></textarea>
              </div>
              <button
                type="submit"
                class="btn btn-success"
                :disabled="isSubmittingAction"
                data-testid="accept-request-button"
              >
                <i class="bi bi-check-lg me-1" aria-hidden="true"></i>
                Accept
              </button>
            </form>
          </div>

          <!-- EVALUATING: Reject Request -->
          <div v-if="canReject" class="col-12 col-lg-6" data-testid="reject-action-section">
            <form class="border rounded p-3 h-100 bg-light-subtle" @submit.prevent="handleReject">
              <h3 class="h6 fw-bold mb-2">Reject Request</h3>
              <div class="mb-3">
                <label for="rejectReasonInput" class="form-label small fw-medium">
                  Rejection Reason <span class="text-danger">*</span>
                </label>
                <textarea
                  id="rejectReasonInput"
                  v-model="rejectForm.reason"
                  class="form-control"
                  rows="2"
                  placeholder="Provide reason for rejecting this request..."
                  required
                  :disabled="isSubmittingAction"
                  data-testid="reject-reason-input"
                ></textarea>
              </div>
              <button
                type="submit"
                class="btn btn-danger"
                :disabled="isSubmittingAction || !rejectForm.reason.trim()"
                data-testid="reject-request-button"
              >
                <i class="bi bi-x-octagon me-1" aria-hidden="true"></i>
                Reject
              </button>
            </form>
          </div>

          <!-- IN_PROGRESS: Complete Request -->
          <div v-if="canComplete" class="col-12 col-lg-6" data-testid="complete-action-section">
            <form class="border rounded p-3 h-100 bg-light-subtle" @submit.prevent="handleComplete">
              <h3 class="h6 fw-bold mb-2">Complete Request</h3>
              <div class="mb-3">
                <label for="completeResolutionInput" class="form-label small fw-medium">
                  Resolution Summary <span class="text-danger">*</span>
                </label>
                <textarea
                  id="completeResolutionInput"
                  v-model="completeForm.resolutionDescription"
                  class="form-control"
                  rows="2"
                  placeholder="Describe the completed solution and deliverables..."
                  required
                  :disabled="isSubmittingAction"
                  data-testid="complete-resolution-input"
                ></textarea>
              </div>
              <button
                type="submit"
                class="btn btn-success"
                :disabled="isSubmittingAction || !completeForm.resolutionDescription.trim()"
                data-testid="complete-request-button"
              >
                <i class="bi bi-check2-all me-1" aria-hidden="true"></i>
                Complete
              </button>
            </form>
          </div>

          <!-- EVALUATING / IN_PROGRESS: Escalate Request -->
          <div v-if="canEscalate" class="col-12 col-lg-6" data-testid="escalate-action-section">
            <form class="border rounded p-3 h-100 bg-light-subtle" @submit.prevent="handleEscalate">
              <h3 class="h6 fw-bold mb-2">Escalate Request</h3>
              <div class="mb-3">
                <label for="escalateReasonInput" class="form-label small fw-medium">
                  Escalation Reason <span class="text-danger">*</span>
                </label>
                <textarea
                  id="escalateReasonInput"
                  v-model="escalateForm.reason"
                  class="form-control"
                  rows="2"
                  placeholder="Document blocker or required management intervention..."
                  required
                  :disabled="isSubmittingAction"
                  data-testid="escalate-reason-input"
                ></textarea>
              </div>
              <button
                type="submit"
                class="btn btn-warning"
                :disabled="isSubmittingAction || !escalateForm.reason.trim()"
                data-testid="escalate-request-button"
              >
                <i class="bi bi-arrow-up-circle me-1" aria-hidden="true"></i>
                Escalate
              </button>
            </form>
          </div>

          <!-- Active / ESCALATED: Management Decision -->
          <div
            v-if="canManagementDecision"
            class="col-12 col-lg-6"
            data-testid="management-decision-action-section"
          >
            <form
              class="border rounded p-3 h-100 bg-light-subtle"
              @submit.prevent="handleManagementDecision"
            >
              <h3 class="h6 fw-bold mb-2">Management Decision</h3>
              <div class="mb-3">
                <label for="managementDecisionInput" class="form-label small fw-medium">
                  Decision Details <span class="text-danger">*</span>
                </label>
                <textarea
                  id="managementDecisionInput"
                  v-model="managementDecisionForm.decisionDetails"
                  class="form-control"
                  rows="2"
                  placeholder="Record management guidance or determination..."
                  required
                  :disabled="isSubmittingAction"
                  data-testid="management-decision-input"
                ></textarea>
              </div>
              <div v-if="isEscalated" class="mb-3">
                <label for="managementDecisionTargetStatus" class="form-label small fw-medium">
                  Transition Status (Optional)
                </label>
                <select
                  id="managementDecisionTargetStatus"
                  v-model="managementDecisionForm.targetStatus"
                  class="form-select form-select-sm"
                  :disabled="isSubmittingAction"
                  data-testid="management-decision-target-status-select"
                >
                  <option value="">Remain ESCALATED</option>
                  <option value="EVALUATING">Return to EVALUATING</option>
                  <option value="IN_PROGRESS">Resume IN_PROGRESS</option>
                </select>
              </div>
              <button
                type="submit"
                class="btn btn-outline-primary"
                :disabled="isSubmittingAction || !managementDecisionForm.decisionDetails.trim()"
                data-testid="management-decision-button"
              >
                <i class="bi bi-briefcase me-1" aria-hidden="true"></i>
                Management Decision
              </button>
            </form>
          </div>

          <!-- Active / ESCALATED: Reassign Ownership -->
          <div v-if="canReassign" class="col-12 col-lg-6" data-testid="reassign-action-section">
            <form class="border rounded p-3 h-100 bg-light-subtle" @submit.prevent="handleReassign">
              <h3 class="h6 fw-bold mb-2">Reassign Ownership</h3>
              <div class="row g-2 mb-3">
                <div class="col-12">
                  <label for="reassignOwnerSelect" class="form-label small fw-medium">
                    New Assignee <span class="text-danger">*</span>
                  </label>
                  <select
                    id="reassignOwnerSelect"
                    v-model="reassignForm.newOwnerPersonId"
                    class="form-select"
                    required
                    :disabled="isSubmittingAction"
                    data-testid="reassign-owner-select"
                  >
                    <option value="">Select new active person...</option>
                    <option
                      v-for="person in activePersons"
                      :key="person.id"
                      :value="person.id"
                    >
                      {{ person.fullName }} ({{ person.email }})
                    </option>
                  </select>
                </div>
                <div v-if="isEscalated" class="col-12">
                  <label for="reassignTargetStatus" class="form-label small fw-medium">
                    Target Status from ESCALATED
                  </label>
                  <select
                    id="reassignTargetStatus"
                    v-model="reassignForm.targetStatusForEscalated"
                    class="form-select form-select-sm"
                    :disabled="isSubmittingAction"
                    data-testid="reassign-target-status-select"
                  >
                    <option value="EVALUATING">EVALUATING</option>
                    <option value="IN_PROGRESS">IN_PROGRESS</option>
                  </select>
                </div>
                <div class="col-12">
                  <label for="reassignNotesInput" class="form-label small fw-medium">
                    Reassignment Notes
                  </label>
                  <input
                    id="reassignNotesInput"
                    v-model="reassignForm.notes"
                    type="text"
                    class="form-control"
                    placeholder="Reason or handover context"
                    :disabled="isSubmittingAction"
                    data-testid="reassign-notes-input"
                  />
                </div>
              </div>
              <button
                type="submit"
                class="btn btn-outline-secondary"
                :disabled="isSubmittingAction || !reassignForm.newOwnerPersonId"
                data-testid="reassign-request-button"
              >
                <i class="bi bi-arrow-left-right me-1" aria-hidden="true"></i>
                Reassign
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>

    <!-- State History Timeline Card Below Request Details -->
    <div class="card shadow-sm border-0" data-testid="request-history-card">
      <div class="card-header bg-body-tertiary py-3 d-flex justify-content-between align-items-center">
        <span class="fw-semibold">
          <i class="bi bi-clock-history me-2 text-primary" aria-hidden="true"></i>
          State History Timeline
        </span>
        <span class="badge text-bg-secondary" data-testid="request-history-count">
          {{ stateHistory.length }}
        </span>
      </div>

      <div class="card-body p-4">
        <div v-if="isHistoryLoading" class="text-center py-4 text-body-secondary">
          <span
            class="spinner-border spinner-border-sm me-2"
            role="status"
            aria-hidden="true"
          ></span>
          Loading state history timeline...
        </div>

        <div
          v-else-if="stateHistory.length === 0"
          class="text-center py-4 text-body-secondary"
          data-testid="empty-history-message"
        >
          No state transitions recorded for this request yet.
        </div>

        <ul
          v-else
          class="list-group list-group-flush"
          data-testid="request-history-timeline"
        >
          <li
            v-for="entry in stateHistory"
            :key="entry.id"
            class="list-group-item px-0 py-3"
            data-testid="request-history-item"
          >
            <div class="d-flex flex-wrap justify-content-between align-items-start gap-2">
              <div class="d-flex align-items-center gap-2 flex-wrap">
                <span
                  v-if="entry.previousStatus"
                  class="badge"
                  :class="statusBadgeClass(entry.previousStatus)"
                >
                  {{ entry.previousStatus }}
                </span>
                <i
                  v-if="entry.previousStatus"
                  class="bi bi-arrow-right text-body-secondary small"
                  aria-hidden="true"
                ></i>
                <span class="badge" :class="statusBadgeClass(entry.newStatus)">
                  {{ entry.newStatus }}
                </span>
                <span class="small text-body-secondary">
                  by
                  <strong>{{ resolvePersonDisplay(entry.actorPersonId, entry.actorName) }}</strong>
                </span>
                <span
                  v-if="entry.assignedOwnerPersonId"
                  class="small text-body-secondary"
                >
                  (Owner:
                  {{
                    resolvePersonDisplay(
                      entry.assignedOwnerPersonId,
                      entry.assignedOwnerName,
                    )
                  }})
                </span>
              </div>

              <span class="small text-body-secondary">
                {{ formatTimestamp(entry.assignedAtUtc || entry.timestamp || entry.createdAt) }}
              </span>
            </div>

            <div
              v-if="entry.notes"
              class="mt-2 small text-body-secondary"
              style="white-space: pre-wrap"
            >
              {{ entry.notes }}
            </div>
          </li>
        </ul>
      </div>
    </div>
  </section>
</template>
