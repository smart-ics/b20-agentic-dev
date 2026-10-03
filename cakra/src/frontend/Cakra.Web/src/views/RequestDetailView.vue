<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { httpClient } from '@/api/http'
import { useAuthStore } from '@/stores/auth'

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

const authStore = useAuthStore()

const AUTHORIZED_COMPLEXITY_ROLES = [
  'PROGRAMMER',
  'ADMINISTRATOR',
  'ADMIN',
  'DEVELOPER',
  'TEAM LEAD',
  'TEAM_LEAD',
  'MANAGER',
] as const

const canEditComplexity = computed(() => {
  if (isClosed.value) {
    return false
  }
  if (!authStore.isAuthenticated) {
    return true
  }
  const userRoles = authStore.roles.map((r) => r.toUpperCase())
  if (userRoles.length === 0) {
    return true
  }
  return userRoles.some((role) => AUTHORIZED_COMPLEXITY_ROLES.includes(role as (typeof AUTHORIZED_COMPLEXITY_ROLES)[number]))
})

const complexityForm = reactive({
  complexity: 1,
  reason: '',
  isEditing: false,
  isSubmitting: false,
})

function initComplexityForm(): void {
  complexityForm.complexity = request.value?.complexity ?? 1
  complexityForm.reason = ''
}

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

function complexityBadgeClass(complexity?: number): string {
  switch (complexity) {
    case 1:
      return 'text-bg-light border text-secondary'
    case 2:
      return 'text-bg-info text-dark'
    case 3:
      return 'text-bg-primary'
    case 4:
      return 'text-bg-warning text-dark'
    case 5:
      return 'text-bg-danger'
    default:
      return 'text-bg-light border text-secondary'
  }
}

function formatComplexity(complexity?: number): string {
  const val = complexity ?? 1
  switch (val) {
    case 1:
      return '1 (Very Low)'
    case 2:
      return '2 (Low)'
    case 3:
      return '3 (Medium)'
    case 4:
      return '4 (High)'
    case 5:
      return '5 (Very High)'
    default:
      return `${val}`
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

async function handleUpdateComplexity(): Promise<void> {
  if (!requestId.value) {
    return
  }
  complexityForm.isSubmitting = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const payload: { complexity: number; reason?: string; actorPersonId?: string } = {
      complexity: Number(complexityForm.complexity),
      reason: complexityForm.reason.trim() || undefined,
      actorPersonId: authStore.currentUser?.personId || undefined,
    }

    const response = await httpClient.patch<RequestDetail>(
      `/requests/${requestId.value}/complexity`,
      payload,
    )
    request.value = response.data
    complexityForm.isEditing = false
    complexityForm.reason = ''
    actionSuccessMessage.value = 'Request complexity updated successfully.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to update request complexity.')
  } finally {
    complexityForm.isSubmitting = false
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
  <section class="request-detail-view" data-screen-id="SCR-REQ-003">
    <!-- Screen Header -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2 flex-wrap">
        <router-link
          to="/requests"
          class="btn btn-outline-secondary btn-sm py-0 px-2"
          style="font-size: 12px; height: 26px; line-height: 24px"
          data-testid="back-to-requests-link"
          @click.prevent="navigateBackToList"
        >
          <i class="bi bi-arrow-left me-1" aria-hidden="true"></i>Back
        </router-link>
        <span class="font-monospace text-body-secondary small fw-medium" data-testid="request-detail-id">
          {{ request?.id || requestId }}
        </span>
        <span class="badge text-bg-secondary font-monospace" style="font-size: 11px">SCR-REQ-003</span>
        <template v-if="request">
          <span
            class="badge"
            style="font-size: 11px"
            :class="statusBadgeClass(request.status)"
            data-testid="request-detail-status"
          >
            {{ request.status }}
          </span>
          <span
            v-if="request.priority"
            class="badge"
            style="font-size: 11px"
            :class="priorityBadgeClass(request.priority)"
            data-testid="request-detail-priority"
          >
            {{ request.priority }}
          </span>
          <span
            class="badge"
            style="font-size: 11px"
            :class="complexityBadgeClass(request.complexity)"
            data-testid="request-detail-complexity"
          >
            Complexity: {{ formatComplexity(request.complexity) }}
          </span>
          <span
            v-if="request.requestType"
            class="badge text-bg-light border"
            style="font-size: 11px"
            data-testid="request-detail-type"
          >
            {{ request.requestType }}
          </span>
        </template>
      </div>

      <div class="d-flex align-items-center gap-1">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm py-0 px-2"
          style="font-size: 12px; height: 26px; line-height: 24px"
          :disabled="isLoading || isHistoryLoading"
          data-testid="refresh-request-detail-button"
          @click="refreshAll"
        >
          <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>Refresh
        </button>
      </div>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      role="alert"
      class="alert alert-danger alert-dismissible py-1 px-2 mb-2 small d-flex align-items-center gap-2"
      data-testid="request-detail-error-alert"
    >
      <i class="bi bi-exclamation-triangle-fill flex-shrink-0" aria-hidden="true"></i>
      <div>{{ errorMessage }}</div>
      <button
        type="button"
        class="btn-close py-1 px-2"
        aria-label="Close"
        @click="errorMessage = null"
      ></button>
    </div>

    <!-- Success Alert -->
    <div
      v-if="actionSuccessMessage"
      role="status"
      class="alert alert-success alert-dismissible py-1 px-2 mb-2 small d-flex align-items-center gap-2"
      data-testid="request-detail-success-alert"
    >
      <i class="bi bi-check-circle-fill flex-shrink-0" aria-hidden="true"></i>
      <div>{{ actionSuccessMessage }}</div>
      <button
        type="button"
        class="btn-close py-1 px-2"
        aria-label="Close"
        @click="actionSuccessMessage = null"
      ></button>
    </div>

    <!-- Loading Indicator -->
    <div v-if="isLoading && !request" class="text-center py-4">
      <div class="spinner-border spinner-border-sm text-primary" role="status">
        <span class="visually-hidden">Loading request details...</span>
      </div>
      <span class="text-body-secondary small ms-2">Loading request details...</span>
    </div>

    <!-- 2-Column Operational Workspace -->
    <div v-if="request" class="row g-2">
      <!-- Main Content Column (Left) -->
      <div class="col-12 col-lg-8">
        <!-- Request Detail Card -->
        <div class="card shadow-none border mb-2" data-testid="request-detail-card">
          <div class="card-header py-1 px-2 bg-body-tertiary">
            <h2 class="h6 mb-0 fw-bold" data-testid="request-detail-title">{{ request.title }}</h2>
          </div>
          <div class="card-body p-2">
            <!-- Description -->
            <div class="small fw-semibold text-body-secondary text-uppercase mb-1" style="font-size: 10.5px">
              Description
            </div>
            <div
              class="p-2 rounded bg-body-tertiary border mb-2 small"
              style="white-space: pre-wrap; font-size: 12.5px; line-height: 1.4"
              data-testid="request-detail-description"
            >
              {{ request.description }}
            </div>

            <!-- Evaluation Notes if present -->
            <div v-if="request.evaluationNotes" class="p-2 rounded bg-info-subtle border border-info-subtle mb-2 small">
              <div class="small fw-semibold text-info-emphasis mb-1">
                <i class="bi bi-journal-check me-1" aria-hidden="true"></i>Evaluation Notes
              </div>
              <div style="white-space: pre-wrap; font-size: 12px" data-testid="request-detail-evaluation-notes">
                {{ request.evaluationNotes }}
              </div>
            </div>

            <!-- Escalation Reason if present -->
            <div v-if="request.escalationReason" class="p-2 rounded bg-warning-subtle border border-warning-subtle mb-2 small">
              <div class="small fw-semibold text-warning-emphasis mb-1">
                <i class="bi bi-exclamation-octagon me-1" aria-hidden="true"></i>Escalation Reason
              </div>
              <div style="white-space: pre-wrap; font-size: 12px" data-testid="request-detail-escalation-reason">
                {{ request.escalationReason }}
              </div>
            </div>

            <!-- Management Decision Notes if present -->
            <div v-if="request.managementDecisionNotes" class="p-2 rounded bg-primary-subtle border border-primary-subtle mb-2 small">
              <div class="small fw-semibold text-primary-emphasis mb-1">
                <i class="bi bi-diagram-3 me-1" aria-hidden="true"></i>Management Decision Notes
              </div>
              <div style="white-space: pre-wrap; font-size: 12px" data-testid="request-detail-management-decision-notes">
                {{ request.managementDecisionNotes }}
              </div>
            </div>

            <!-- Resolution Details if present -->
            <div
              v-if="request.resolution"
              class="p-2 rounded border mb-2 small"
              :class="
                request.resolution.outcome === 'REJECTED'
                  ? 'bg-danger-subtle border-danger-subtle'
                  : 'bg-success-subtle border-success-subtle'
              "
              data-testid="request-detail-resolution"
            >
              <div class="d-flex flex-wrap justify-content-between align-items-center gap-1 mb-1">
                <span class="small fw-semibold">
                  <i class="bi bi-check2-circle me-1" aria-hidden="true"></i>
                  Resolution ({{ request.resolution.outcome }})
                </span>
                <span class="text-body-secondary" style="font-size: 11px">
                  Resolved by
                  {{ resolvePersonDisplay(request.resolution.resolvedBy, request.resolution.resolvedByName) }}
                  on {{ formatTimestamp(request.resolution.resolvedAt) }}
                </span>
              </div>
              <div style="white-space: pre-wrap; font-size: 12px" data-testid="request-detail-resolution-description">
                {{ request.resolution.description }}
              </div>
            </div>
          </div>
        </div>

        <!-- Conditional Lifecycle Actions Card -->
        <div
          v-if="!isClosed"
          class="card shadow-none border mb-2"
          data-testid="request-actions-card"
        >
          <div class="card-header py-1 px-2 bg-body-tertiary d-flex align-items-center justify-content-between">
            <span class="fw-semibold small">
              <i class="bi bi-sliders me-1 text-primary" aria-hidden="true"></i>
              Lifecycle Actions
            </span>
            <span class="badge" style="font-size: 10px" :class="statusBadgeClass(request.status)">
              Status: {{ request.status }}
            </span>
          </div>

          <div class="card-body p-2">
            <!-- CAPTURED: Assign Request Owner -->
            <div v-if="canAssign" data-testid="assign-action-section">
              <form class="border rounded p-2 bg-body-tertiary" @submit.prevent="handleAssign">
                <div class="small fw-bold mb-1">Assign Request Owner</div>
                <div class="row g-1 align-items-end">
                  <div class="col-12 col-md-5">
                    <label for="assignOwnerSelect" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                      Assignee <span class="text-danger">*</span>
                    </label>
                    <select
                      id="assignOwnerSelect"
                      v-model="assignForm.ownerPersonId"
                      class="form-select form-select-sm"
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
                    <label for="assignNotesInput" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                      Assignment Notes
                    </label>
                    <input
                      id="assignNotesInput"
                      v-model="assignForm.notes"
                      type="text"
                      class="form-control form-control-sm"
                      placeholder="Optional instructions"
                      :disabled="isSubmittingAction"
                      data-testid="assign-notes-input"
                    />
                  </div>
                  <div class="col-12 col-md-2">
                    <button
                      type="submit"
                      class="btn btn-primary btn-sm w-100"
                      :disabled="isSubmittingAction || !assignForm.ownerPersonId"
                      data-testid="assign-request-button"
                    >
                      <i class="bi bi-person-check me-1" aria-hidden="true"></i>Assign
                    </button>
                  </div>
                </div>
              </form>
            </div>

            <!-- Other Lifecycle States Action Forms Grid -->
            <div class="row g-2">
              <!-- EVALUATING: Evaluate Request -->
              <div v-if="canEvaluate" class="col-12 col-md-6" data-testid="evaluate-action-section">
                <form class="border rounded p-2 h-100 bg-body-tertiary" @submit.prevent="handleEvaluate">
                  <div class="small fw-bold mb-1">Evaluate Request</div>
                  <div class="mb-1">
                    <textarea
                      id="evaluateNotesInput"
                      v-model="evaluateForm.evaluationNotes"
                      class="form-control form-control-sm"
                      rows="2"
                      placeholder="Record triage findings..."
                      required
                      :disabled="isSubmittingAction"
                      data-testid="evaluate-notes-input"
                    ></textarea>
                  </div>
                  <button
                    type="submit"
                    class="btn btn-info text-white btn-sm"
                    :disabled="isSubmittingAction || !evaluateForm.evaluationNotes.trim()"
                    data-testid="evaluate-request-button"
                  >
                    <i class="bi bi-clipboard-check me-1" aria-hidden="true"></i>Evaluate
                  </button>
                </form>
              </div>

              <!-- EVALUATING / ACCEPTED: Accept Responsibility -->
              <div v-if="canAccept" class="col-12 col-md-6" data-testid="accept-action-section">
                <form class="border rounded p-2 h-100 bg-body-tertiary" @submit.prevent="handleAccept">
                  <div class="small fw-bold mb-1">Accept Responsibility</div>
                  <div class="mb-1">
                    <textarea
                      id="acceptNotesInput"
                      v-model="acceptForm.notes"
                      class="form-control form-control-sm"
                      rows="2"
                      placeholder="Optional acceptance notes..."
                      :disabled="isSubmittingAction"
                      data-testid="accept-notes-input"
                    ></textarea>
                  </div>
                  <button
                    type="submit"
                    class="btn btn-success btn-sm"
                    :disabled="isSubmittingAction"
                    data-testid="accept-request-button"
                  >
                    <i class="bi bi-check-lg me-1" aria-hidden="true"></i>Accept
                  </button>
                </form>
              </div>

              <!-- EVALUATING: Reject Request -->
              <div v-if="canReject" class="col-12 col-md-6" data-testid="reject-action-section">
                <form class="border rounded p-2 h-100 bg-body-tertiary" @submit.prevent="handleReject">
                  <div class="small fw-bold mb-1">Reject Request</div>
                  <div class="mb-1">
                    <textarea
                      id="rejectReasonInput"
                      v-model="rejectForm.reason"
                      class="form-control form-control-sm"
                      rows="2"
                      placeholder="Reason for rejecting..."
                      required
                      :disabled="isSubmittingAction"
                      data-testid="reject-reason-input"
                    ></textarea>
                  </div>
                  <button
                    type="submit"
                    class="btn btn-danger btn-sm"
                    :disabled="isSubmittingAction || !rejectForm.reason.trim()"
                    data-testid="reject-request-button"
                  >
                    <i class="bi bi-x-octagon me-1" aria-hidden="true"></i>Reject
                  </button>
                </form>
              </div>

              <!-- IN_PROGRESS: Complete Request -->
              <div v-if="canComplete" class="col-12 col-md-6" data-testid="complete-action-section">
                <form class="border rounded p-2 h-100 bg-body-tertiary" @submit.prevent="handleComplete">
                  <div class="small fw-bold mb-1">Complete Request</div>
                  <div class="mb-1">
                    <textarea
                      id="completeResolutionInput"
                      v-model="completeForm.resolutionDescription"
                      class="form-control form-control-sm"
                      rows="2"
                      placeholder="Describe completed solution..."
                      required
                      :disabled="isSubmittingAction"
                      data-testid="complete-resolution-input"
                    ></textarea>
                  </div>
                  <button
                    type="submit"
                    class="btn btn-success btn-sm"
                    :disabled="isSubmittingAction || !completeForm.resolutionDescription.trim()"
                    data-testid="complete-request-button"
                  >
                    <i class="bi bi-check2-all me-1" aria-hidden="true"></i>Complete
                  </button>
                </form>
              </div>

              <!-- EVALUATING / IN_PROGRESS: Escalate Request -->
              <div v-if="canEscalate" class="col-12 col-md-6" data-testid="escalate-action-section">
                <form class="border rounded p-2 h-100 bg-body-tertiary" @submit.prevent="handleEscalate">
                  <div class="small fw-bold mb-1">Escalate Request</div>
                  <div class="mb-1">
                    <textarea
                      id="escalateReasonInput"
                      v-model="escalateForm.reason"
                      class="form-control form-control-sm"
                      rows="2"
                      placeholder="Document blocker or need for management..."
                      required
                      :disabled="isSubmittingAction"
                      data-testid="escalate-reason-input"
                    ></textarea>
                  </div>
                  <button
                    type="submit"
                    class="btn btn-warning btn-sm"
                    :disabled="isSubmittingAction || !escalateForm.reason.trim()"
                    data-testid="escalate-request-button"
                  >
                    <i class="bi bi-arrow-up-circle me-1" aria-hidden="true"></i>Escalate
                  </button>
                </form>
              </div>

              <!-- Active / ESCALATED: Management Decision -->
              <div
                v-if="canManagementDecision"
                class="col-12 col-md-6"
                data-testid="management-decision-action-section"
              >
                <form
                  class="border rounded p-2 h-100 bg-body-tertiary"
                  @submit.prevent="handleManagementDecision"
                >
                  <div class="small fw-bold mb-1">Management Decision</div>
                  <div class="mb-1">
                    <textarea
                      id="managementDecisionInput"
                      v-model="managementDecisionForm.decisionDetails"
                      class="form-control form-control-sm"
                      rows="2"
                      placeholder="Determination or guidance..."
                      required
                      :disabled="isSubmittingAction"
                      data-testid="management-decision-input"
                    ></textarea>
                  </div>
                  <div v-if="isEscalated" class="mb-1">
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
                    class="btn btn-outline-primary btn-sm"
                    :disabled="isSubmittingAction || !managementDecisionForm.decisionDetails.trim()"
                    data-testid="management-decision-button"
                  >
                    <i class="bi bi-briefcase me-1" aria-hidden="true"></i>Submit Decision
                  </button>
                </form>
              </div>

              <!-- Active / ESCALATED: Reassign Ownership -->
              <div v-if="canReassign" class="col-12 col-md-6" data-testid="reassign-action-section">
                <form class="border rounded p-2 h-100 bg-body-tertiary" @submit.prevent="handleReassign">
                  <div class="small fw-bold mb-1">Reassign Ownership</div>
                  <div class="row g-1 mb-1">
                    <div class="col-12">
                      <select
                        id="reassignOwnerSelect"
                        v-model="reassignForm.newOwnerPersonId"
                        class="form-select form-select-sm"
                        required
                        :disabled="isSubmittingAction"
                        data-testid="reassign-owner-select"
                      >
                        <option value="">Select new assignee...</option>
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
                      <select
                        id="reassignTargetStatus"
                        v-model="reassignForm.targetStatusForEscalated"
                        class="form-select form-select-sm"
                        :disabled="isSubmittingAction"
                        data-testid="reassign-target-status-select"
                      >
                        <option value="EVALUATING">Target: EVALUATING</option>
                        <option value="IN_PROGRESS">Target: IN_PROGRESS</option>
                      </select>
                    </div>
                    <div class="col-12">
                      <input
                        id="reassignNotesInput"
                        v-model="reassignForm.notes"
                        type="text"
                        class="form-control form-control-sm"
                        placeholder="Reassignment notes..."
                        :disabled="isSubmittingAction"
                        data-testid="reassign-notes-input"
                      />
                    </div>
                  </div>
                  <button
                    type="submit"
                    class="btn btn-outline-secondary btn-sm"
                    :disabled="isSubmittingAction || !reassignForm.newOwnerPersonId"
                    data-testid="reassign-request-button"
                  >
                    <i class="bi bi-arrow-left-right me-1" aria-hidden="true"></i>Reassign
                  </button>
                </form>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Properties & History Column (Right) -->
      <div class="col-12 col-lg-4">
        <!-- Properties Card -->
        <div class="card shadow-none border mb-2">
          <div class="card-header py-1 px-2 bg-body-tertiary fw-semibold small">
            <i class="bi bi-info-circle me-1 text-primary" aria-hidden="true"></i>
            Properties
          </div>
          <div class="card-body p-2">
            <dl class="row mb-0 g-1 small" style="font-size: 12px">
              <dt class="col-4 text-body-secondary fw-normal">Customer:</dt>
              <dd class="col-8 fw-semibold mb-1" data-testid="request-detail-customer">
                {{ resolveCustomerDisplay(request) }}
              </dd>

              <dt class="col-4 text-body-secondary fw-normal">Product:</dt>
              <dd class="col-8 fw-semibold mb-1" data-testid="request-detail-product">
                {{ resolveProductDisplay(request) }}
              </dd>

              <dt class="col-4 text-body-secondary fw-normal">Assignee:</dt>
              <dd class="col-8 fw-semibold mb-1" data-testid="request-detail-assignee">
                <i class="bi bi-person me-1 text-secondary" aria-hidden="true"></i>
                {{ resolveAssigneeDisplay(request) }}
              </dd>

              <dt class="col-4 text-body-secondary fw-normal">Status:</dt>
              <dd class="col-8 mb-1">
                <span class="badge" style="font-size: 10px" :class="statusBadgeClass(request.status)">
                  {{ request.status }}
                </span>
              </dd>

              <dt class="col-4 text-body-secondary fw-normal">Complexity:</dt>
              <dd class="col-8 mb-1" data-testid="request-detail-complexity-property">
                <div class="d-flex align-items-center gap-1">
                  <span
                    class="badge"
                    style="font-size: 10px"
                    :class="complexityBadgeClass(request.complexity)"
                  >
                    {{ formatComplexity(request.complexity) }}
                  </span>
                  <button
                    v-if="canEditComplexity && !complexityForm.isEditing"
                    type="button"
                    class="btn btn-link btn-sm p-0 text-decoration-none"
                    style="font-size: 11px"
                    title="Change Complexity"
                    data-testid="edit-complexity-button"
                    @click="initComplexityForm(); complexityForm.isEditing = true"
                  >
                    <i class="bi bi-pencil ms-1" aria-hidden="true"></i>Edit
                  </button>
                </div>
              </dd>

              <dt class="col-4 text-body-secondary fw-normal">Created:</dt>
              <dd class="col-8 mb-1 text-body-secondary" style="font-size: 11px" data-testid="request-detail-created-at">
                {{ formatTimestamp(request.createdAt) }}
              </dd>

              <dt class="col-4 text-body-secondary fw-normal">Updated:</dt>
              <dd class="col-8 mb-0 text-body-secondary" style="font-size: 11px" data-testid="request-detail-updated-at">
                {{ formatTimestamp(request.updatedAt) }}
              </dd>
            </dl>

            <!-- Inline Complexity Editor -->
            <div
              v-if="complexityForm.isEditing"
              class="border rounded p-2 bg-body-tertiary mt-2"
              data-testid="edit-complexity-form"
            >
              <div class="fw-semibold small mb-1" style="font-size: 11px">
                <i class="bi bi-sliders me-1 text-primary" aria-hidden="true"></i>Update Complexity (1 - 5)
              </div>
              <div class="mb-1">
                <select
                  v-model.number="complexityForm.complexity"
                  class="form-select form-select-sm"
                  :disabled="complexityForm.isSubmitting"
                  data-testid="edit-complexity-select"
                >
                  <option :value="1">1 (Very Low)</option>
                  <option :value="2">2 (Low)</option>
                  <option :value="3">3 (Medium)</option>
                  <option :value="4">4 (High)</option>
                  <option :value="5">5 (Very High)</option>
                </select>
              </div>
              <div class="mb-1">
                <input
                  v-model="complexityForm.reason"
                  type="text"
                  class="form-control form-control-sm"
                  placeholder="Optional reason (max 500 chars)"
                  maxlength="500"
                  :disabled="complexityForm.isSubmitting"
                  data-testid="edit-complexity-reason-input"
                />
              </div>
              <div class="d-flex gap-1 justify-content-end">
                <button
                  type="button"
                  class="btn btn-outline-secondary btn-sm py-0 px-2"
                  style="font-size: 11px"
                  :disabled="complexityForm.isSubmitting"
                  @click="complexityForm.isEditing = false"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  class="btn btn-primary btn-sm py-0 px-2"
                  style="font-size: 11px"
                  :disabled="complexityForm.isSubmitting"
                  data-testid="save-complexity-button"
                  @click="handleUpdateComplexity"
                >
                  <span
                    v-if="complexityForm.isSubmitting"
                    class="spinner-border spinner-border-sm me-1"
                    role="status"
                    aria-hidden="true"
                  ></span>
                  Save
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- State History Timeline Card -->
        <div class="card card-table shadow-none border mb-2" data-testid="request-history-card">
          <div class="card-header py-1 px-2 bg-body-tertiary d-flex justify-content-between align-items-center">
            <span class="fw-semibold small">
              <i class="bi bi-clock-history me-1 text-primary" aria-hidden="true"></i>
              State History
            </span>
            <span class="badge text-bg-secondary" style="font-size: 11px" data-testid="request-history-count">
              {{ stateHistory.length }}
            </span>
          </div>

          <div class="card-body p-0" style="max-height: 520px; overflow-y: auto">
            <div v-if="isHistoryLoading" class="text-center py-3 text-body-secondary small">
              <span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
              Loading timeline...
            </div>

            <div
              v-else-if="stateHistory.length === 0"
              class="text-center py-3 text-body-secondary small"
              data-testid="empty-history-message"
            >
              No state transitions recorded yet.
            </div>

            <ul
              v-else
              class="list-group list-group-flush"
              data-testid="request-history-timeline"
            >
              <li
                v-for="entry in stateHistory"
                :key="entry.id"
                class="list-group-item px-2 py-1.5"
                data-testid="request-history-item"
              >
                <div class="d-flex flex-wrap justify-content-between align-items-start gap-1">
                  <div class="d-flex align-items-center gap-1 flex-wrap">
                    <span
                      v-if="entry.previousStatus"
                      class="badge"
                      style="font-size: 10px; padding: 2px 4px"
                      :class="statusBadgeClass(entry.previousStatus)"
                    >
                      {{ entry.previousStatus }}
                    </span>
                    <i
                      v-if="entry.previousStatus"
                      class="bi bi-arrow-right text-body-secondary"
                      style="font-size: 10px"
                      aria-hidden="true"
                    ></i>
                    <span class="badge" style="font-size: 10px; padding: 2px 4px" :class="statusBadgeClass(entry.newStatus)">
                      {{ entry.newStatus }}
                    </span>
                  </div>

                  <span class="text-body-secondary font-monospace" style="font-size: 10.5px">
                    {{ formatTimestamp(entry.assignedAtUtc || entry.timestamp || entry.createdAt) }}
                  </span>
                </div>

                <div class="small text-body-secondary mt-1" style="font-size: 11.5px">
                  by <strong>{{ resolvePersonDisplay(entry.actorPersonId, entry.actorName) }}</strong>
                  <span v-if="entry.assignedOwnerPersonId">
                    &rarr; {{ resolvePersonDisplay(entry.assignedOwnerPersonId, entry.assignedOwnerName) }}
                  </span>
                </div>

                <div
                  v-if="entry.notes"
                  class="mt-1 small text-body-secondary font-monospace p-1 rounded bg-body-tertiary"
                  style="white-space: pre-wrap; font-size: 11px"
                >
                  {{ entry.notes }}
                </div>
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>
