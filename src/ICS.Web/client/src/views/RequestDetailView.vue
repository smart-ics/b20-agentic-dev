<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import {
  requestService,
  type RequestDto,
  type RequestStateHistoryDto,
  type PersonLookupDto
} from '@/services/requestService'

const route = useRoute()

const requestId = computed(() => route.params.id as string)

// State
const request = ref<RequestDto | null>(null)
const stateHistory = ref<RequestStateHistoryDto[]>([])
const eligiblePersons = ref<PersonLookupDto[]>([])
const isLoading = ref(true)
const isActionLoading = ref(false)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)
const activeTab = ref<'details' | 'assignments' | 'history'>('details')

// Modals State
const showAssignModal = ref(false)
const showReassignModal = ref(false)
const showRejectModal = ref(false)
const showEscalateModal = ref(false)
const showDecisionModal = ref(false)
const showCompleteModal = ref(false)
const showReworkModal = ref(false)

// Action Forms
const assignForm = ref({ newOwnerPersonId: '', note: '' })
const reassignForm = ref({ newOwnerPersonId: '', reason: '' })
const rejectForm = ref({ reason: '' })
const escalateForm = ref({ reason: '', requiredAssistance: '' })
const decisionForm = ref({ question: '', options: '', impact: '' })
const completeForm = ref({ summary: '', outcome: 'RESOLVED' })
const reworkForm = ref({ feedback: '' })

async function loadRequest() {
  isLoading.value = true
  errorMessage.value = null
  try {
    const [reqData, histData, personsData] = await Promise.all([
      requestService.getRequestById(requestId.value),
      requestService.getRequestHistory(requestId.value),
      requestService.getActivePersons()
    ])
    request.value = reqData
    stateHistory.value = histData
    eligiblePersons.value = personsData
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || 'Failed to load request details.'
  } finally {
    isLoading.value = false
  }
}

// Action Handlers
async function handleEvaluate() {
  if (!confirm('Start evaluating this request?')) return
  isActionLoading.value = true
  try {
    request.value = await requestService.evaluateRequest(requestId.value)
    successMessage.value = 'Request moved to EVALUATING.'
    await refreshHistory()
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || 'Failed to evaluate request.'
  } finally {
    isActionLoading.value = false
  }
}

async function handleAccept() {
  if (!confirm('Accept responsibility for this request?')) return
  isActionLoading.value = true
  try {
    request.value = await requestService.acceptResponsibility(requestId.value)
    successMessage.value = 'Request accepted.'
    await refreshHistory()
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || 'Failed to accept request.'
  } finally {
    isActionLoading.value = false
  }
}

async function handleStartProgress() {
  isActionLoading.value = true
  try {
    request.value = await requestService.startProgress(requestId.value)
    successMessage.value = 'Work progress started.'
    await refreshHistory()
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || 'Failed to start progress.'
  } finally {
    isActionLoading.value = false
  }
}

async function handleResolveEscalation() {
  if (!confirm('Resolve this escalation and return request to IN_PROGRESS?')) return
  isActionLoading.value = true
  try {
    request.value = await requestService.resolveEscalation(requestId.value)
    successMessage.value = 'Escalation resolved.'
    await refreshHistory()
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || 'Failed to resolve escalation.'
  } finally {
    isActionLoading.value = false
  }
}

async function submitAssign() {
  if (!assignForm.value.newOwnerPersonId) return
  isActionLoading.value = true
  try {
    request.value = await requestService.assignOwner(requestId.value, {
      newOwnerPersonId: assignForm.value.newOwnerPersonId,
      note: assignForm.value.note
    })
    showAssignModal.value = false
    assignForm.value = { newOwnerPersonId: '', note: '' }
    successMessage.value = 'Owner assigned successfully.'
    await refreshHistory()
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || 'Failed to assign owner.'
  } finally {
    isActionLoading.value = false
  }
}

async function submitReassign() {
  if (!reassignForm.value.newOwnerPersonId) return
  isActionLoading.value = true
  try {
    request.value = await requestService.reassignOwner(requestId.value, {
      newOwnerPersonId: reassignForm.value.newOwnerPersonId,
      reason: reassignForm.value.reason
    })
    showReassignModal.value = false
    reassignForm.value = { newOwnerPersonId: '', reason: '' }
    successMessage.value = 'Ownership reassigned successfully.'
    await refreshHistory()
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || 'Failed to reassign ownership.'
  } finally {
    isActionLoading.value = false
  }
}

async function submitReject() {
  if (!rejectForm.value.reason.trim()) return
  isActionLoading.value = true
  try {
    request.value = await requestService.rejectRequest(requestId.value, {
      reason: rejectForm.value.reason.trim()
    })
    showRejectModal.value = false
    rejectForm.value = { reason: '' }
    successMessage.value = 'Request rejected.'
    await refreshHistory()
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || 'Failed to reject request.'
  } finally {
    isActionLoading.value = false
  }
}

async function submitEscalate() {
  if (!escalateForm.value.reason.trim()) return
  isActionLoading.value = true
  try {
    request.value = await requestService.escalateRequest(requestId.value, {
      reason: escalateForm.value.reason.trim(),
      requiredAssistance: escalateForm.value.requiredAssistance.trim() || undefined
    })
    showEscalateModal.value = false
    escalateForm.value = { reason: '', requiredAssistance: '' }
    successMessage.value = 'Request escalated to management.'
    await refreshHistory()
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || 'Failed to escalate request.'
  } finally {
    isActionLoading.value = false
  }
}

async function submitDecision() {
  if (!decisionForm.value.question.trim()) return
  isActionLoading.value = true
  try {
    request.value = await requestService.requestManagementDecision(requestId.value, {
      question: decisionForm.value.question.trim(),
      options: decisionForm.value.options.trim() || undefined,
      impact: decisionForm.value.impact.trim() || undefined
    })
    showDecisionModal.value = false
    decisionForm.value = { question: '', options: '', impact: '' }
    successMessage.value = 'Management decision requested.'
    await refreshHistory()
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || 'Failed to request management decision.'
  } finally {
    isActionLoading.value = false
  }
}

async function submitComplete() {
  if (!completeForm.value.summary.trim()) return
  isActionLoading.value = true
  try {
    request.value = await requestService.completeRequest(requestId.value, {
      summary: completeForm.value.summary.trim(),
      outcome: completeForm.value.outcome || 'RESOLVED'
    })
    showCompleteModal.value = false
    completeForm.value = { summary: '', outcome: 'RESOLVED' }
    successMessage.value = 'Request successfully completed.'
    await refreshHistory()
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || 'Failed to complete request.'
  } finally {
    isActionLoading.value = false
  }
}

async function submitRework() {
  if (!reworkForm.value.feedback.trim()) return
  isActionLoading.value = true
  try {
    request.value = await requestService.reworkRequest(requestId.value, {
      feedback: reworkForm.value.feedback.trim()
    })
    showReworkModal.value = false
    reworkForm.value = { feedback: '' }
    successMessage.value = 'Rework requested. Status remains IN_PROGRESS.'
    await refreshHistory()
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || 'Failed to request rework.'
  } finally {
    isActionLoading.value = false
  }
}

async function refreshHistory() {
  try {
    stateHistory.value = await requestService.getRequestHistory(requestId.value)
  } catch (e) {
    console.error('History refresh failed', e)
  }
}

function getStatusBadgeClass(status: string): string {
  switch (status) {
    case 'CAPTURED':
      return 'bg-secondary'
    case 'EVALUATING':
      return 'bg-info text-dark'
    case 'ACCEPTED':
      return 'bg-primary'
    case 'IN_PROGRESS':
      return 'bg-warning text-dark'
    case 'ESCALATED':
      return 'bg-danger'
    case 'COMPLETED':
      return 'bg-success'
    case 'REJECTED':
      return 'bg-dark'
    default:
      return 'bg-secondary'
  }
}

function getPriorityBadgeClass(priority: string): string {
  switch (priority?.toUpperCase()) {
    case 'CRITICAL':
      return 'badge bg-danger-subtle text-danger border border-danger-subtle'
    case 'HIGH':
      return 'badge bg-warning-subtle text-warning-emphasis border border-warning-subtle'
    case 'MEDIUM':
      return 'badge bg-info-subtle text-info-emphasis border border-info-subtle'
    case 'LOW':
      return 'badge bg-light text-secondary border'
    default:
      return 'badge bg-light text-secondary border'
  }
}

function formatDate(iso?: string | null): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

onMounted(() => {
  loadRequest()
})
</script>

<template>
  <div class="container py-4">
    <!-- Back Navigation -->
    <div class="mb-3 d-flex justify-content-between align-items-center">
      <RouterLink to="/requests" class="text-decoration-none text-secondary small d-inline-flex align-items-center gap-1">
        <i class="bi bi-arrow-left"></i>
        <span>Back to Request List</span>
      </RouterLink>
      <span class="badge bg-primary-subtle text-primary border border-primary-subtle font-monospace">
        SCR-REQ-003
      </span>
    </div>

    <!-- Loading State -->
    <div v-if="isLoading" class="text-center py-5">
      <div class="spinner-border text-primary" role="status"></div>
      <p class="text-secondary small mt-2">Loading request details...</p>
    </div>

    <!-- Error State -->
    <div v-else-if="!request" class="alert alert-danger">
      <i class="bi bi-exclamation-triangle-fill me-2"></i>
      {{ errorMessage || 'Request not found.' }}
    </div>

    <div v-else>
      <!-- Alerts -->
      <div v-if="errorMessage" class="alert alert-danger alert-dismissible fade show" role="alert">
        <i class="bi bi-exclamation-circle-fill me-2"></i>
        {{ errorMessage }}
        <button type="button" class="btn-close" @click="errorMessage = null" aria-label="Close"></button>
      </div>
      <div v-if="successMessage" class="alert alert-success alert-dismissible fade show" role="alert">
        <i class="bi bi-check-circle-fill me-2"></i>
        {{ successMessage }}
        <button type="button" class="btn-close" @click="successMessage = null" aria-label="Close"></button>
      </div>

      <!-- Main Header Banner -->
      <div class="card border-0 shadow-sm mb-4">
        <div class="card-body p-4">
          <div class="d-flex flex-column flex-md-row justify-content-between align-items-start gap-3">
            <div>
              <div class="d-flex flex-wrap align-items-center gap-2 mb-2">
                <span class="badge" :class="getStatusBadgeClass(request.status)">
                  {{ request.status }}
                </span>
                <span :class="getPriorityBadgeClass(request.priority)">
                  {{ request.priority }}
                </span>
                <span class="badge bg-light text-dark border font-monospace">
                  {{ request.type }}
                </span>
                <span v-if="request.isAwaitingManagementDecision" class="badge bg-warning text-dark">
                  <i class="bi bi-hourglass-split me-1"></i>Decision Required
                </span>
                <span v-if="request.escalatedAt" class="badge bg-danger">
                  <i class="bi bi-exclamation-triangle-fill me-1"></i>Escalated
                </span>
              </div>
              <h1 class="h3 fw-bold text-dark mb-1">{{ request.title }}</h1>
              <div class="text-muted small font-monospace">
                ID: {{ request.requestId }}
              </div>
            </div>

            <!-- Contextual State Action Toolbar -->
            <div class="d-flex flex-wrap gap-2">
              <!-- CAPTURED Actions -->
              <template v-if="request.status === 'CAPTURED'">
                <button
                  class="btn btn-outline-primary btn-sm d-flex align-items-center gap-1"
                  @click="showAssignModal = true"
                  :disabled="isActionLoading"
                >
                  <i class="bi bi-person-plus"></i> Assign Owner
                </button>
                <button
                  class="btn btn-primary btn-sm d-flex align-items-center gap-1"
                  @click="handleEvaluate"
                  :disabled="isActionLoading"
                >
                  <i class="bi bi-search"></i> Evaluate
                </button>
                <button
                  class="btn btn-outline-danger btn-sm"
                  @click="showRejectModal = true"
                  :disabled="isActionLoading"
                >
                  Reject
                </button>
              </template>

              <!-- EVALUATING Actions -->
              <template v-else-if="request.status === 'EVALUATING'">
                <button
                  class="btn btn-success btn-sm d-flex align-items-center gap-1"
                  @click="handleAccept"
                  :disabled="isActionLoading"
                >
                  <i class="bi bi-check-lg"></i> Accept Responsibility
                </button>
                <button
                  class="btn btn-outline-secondary btn-sm"
                  @click="showReassignModal = true"
                  :disabled="isActionLoading"
                >
                  Reassign
                </button>
                <button
                  class="btn btn-outline-danger btn-sm"
                  @click="showRejectModal = true"
                  :disabled="isActionLoading"
                >
                  Reject
                </button>
              </template>

              <!-- ACCEPTED Actions -->
              <template v-else-if="request.status === 'ACCEPTED'">
                <button
                  class="btn btn-primary btn-sm d-flex align-items-center gap-1"
                  @click="handleStartProgress"
                  :disabled="isActionLoading"
                >
                  <i class="bi bi-play-fill"></i> Start Progress
                </button>
                <button
                  class="btn btn-outline-secondary btn-sm"
                  @click="showReassignModal = true"
                  :disabled="isActionLoading"
                >
                  Reassign
                </button>
              </template>

              <!-- IN_PROGRESS Actions -->
              <template v-else-if="request.status === 'IN_PROGRESS'">
                <button
                  class="btn btn-success btn-sm d-flex align-items-center gap-1"
                  @click="showCompleteModal = true"
                  :disabled="isActionLoading"
                >
                  <i class="bi bi-check2-circle"></i> Complete Request
                </button>
                <button
                  class="btn btn-outline-warning btn-sm text-dark d-flex align-items-center gap-1"
                  @click="showReworkModal = true"
                  :disabled="isActionLoading"
                >
                  <i class="bi bi-arrow-repeat"></i> Rework
                </button>
                <button
                  class="btn btn-outline-danger btn-sm d-flex align-items-center gap-1"
                  @click="showEscalateModal = true"
                  :disabled="isActionLoading"
                >
                  <i class="bi bi-exclamation-triangle"></i> Escalate
                </button>
                <button
                  class="btn btn-outline-primary btn-sm d-flex align-items-center gap-1"
                  @click="showDecisionModal = true"
                  :disabled="isActionLoading"
                >
                  <i class="bi bi-patch-question"></i> Decision
                </button>
                <button
                  class="btn btn-outline-secondary btn-sm"
                  @click="showReassignModal = true"
                  :disabled="isActionLoading"
                >
                  Reassign
                </button>
              </template>

              <!-- ESCALATED Actions -->
              <template v-else-if="request.status === 'ESCALATED'">
                <button
                  class="btn btn-warning btn-sm text-dark d-flex align-items-center gap-1"
                  @click="handleResolveEscalation"
                  :disabled="isActionLoading"
                >
                  <i class="bi bi-arrow-counterclockwise"></i> Resolve Escalation
                </button>
                <button
                  class="btn btn-success btn-sm d-flex align-items-center gap-1"
                  @click="showCompleteModal = true"
                  :disabled="isActionLoading"
                >
                  Complete
                </button>
                <button
                  class="btn btn-outline-secondary btn-sm"
                  @click="showReassignModal = true"
                  :disabled="isActionLoading"
                >
                  Reassign
                </button>
              </template>
            </div>
          </div>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <ul class="nav nav-pills mb-4 bg-white p-2 rounded-3 shadow-sm border">
        <li class="nav-item">
          <button
            class="nav-link"
            :class="{ active: activeTab === 'details' }"
            @click="activeTab = 'details'"
          >
            <i class="bi bi-info-circle me-1"></i> Overview & Details
          </button>
        </li>
        <li class="nav-item">
          <button
            class="nav-link"
            :class="{ active: activeTab === 'assignments' }"
            @click="activeTab = 'assignments'"
          >
            <i class="bi bi-person-lines-fill me-1"></i> Ownership History
            <span class="badge bg-secondary-subtle text-dark ms-1">
              {{ request.assignments?.length || 0 }}
            </span>
          </button>
        </li>
        <li class="nav-item">
          <button
            class="nav-link"
            :class="{ active: activeTab === 'history' }"
            @click="activeTab = 'history'"
          >
            <i class="bi bi-clock-history me-1"></i> State Audit Trail
            <span class="badge bg-secondary-subtle text-dark ms-1">
              {{ stateHistory.length }}
            </span>
          </button>
        </li>
      </ul>

      <!-- Tab Content: Details -->
      <div v-if="activeTab === 'details'">
        <div class="row g-4">
          <!-- Left Column: Core Attributes -->
          <div class="col-lg-8">
            <!-- Description Card -->
            <div class="card border-0 shadow-sm mb-4">
              <div class="card-header bg-white py-3">
                <h2 class="h6 fw-bold mb-0">Description</h2>
              </div>
              <div class="card-body">
                <p class="text-dark whitespace-pre-wrap mb-0" style="white-space: pre-wrap;">
                  {{ request.description }}
                </p>
              </div>
            </div>

            <!-- Escalation Notice Card -->
            <div v-if="request.escalatedAt" class="card border-danger shadow-sm mb-4">
              <div class="card-header bg-danger-subtle text-danger py-2 fw-semibold">
                <i class="bi bi-exclamation-triangle-fill me-2"></i>Active Escalation
              </div>
              <div class="card-body">
                <div class="mb-2"><strong>Reason:</strong> {{ request.escalationReason }}</div>
                <div class="small text-muted">
                  Escalated by {{ request.escalatedByName || 'Owner' }} on {{ formatDate(request.escalatedAt) }}
                </div>
              </div>
            </div>

            <!-- Management Decision Notice Card -->
            <div v-if="request.isAwaitingManagementDecision" class="card border-warning shadow-sm mb-4">
              <div class="card-header bg-warning-subtle text-warning-emphasis py-2 fw-semibold">
                <i class="bi bi-hourglass-split me-2"></i>Management Decision Docket
              </div>
              <div class="card-body">
                <div class="mb-2"><strong>Question:</strong> {{ request.managementDecisionQuestion }}</div>
                <div v-if="request.managementDecisionOptions" class="mb-2">
                  <strong>Options Considered:</strong> {{ request.managementDecisionOptions }}
                </div>
                <div v-if="request.managementDecisionImpact" class="mb-2">
                  <strong>Expected Impact:</strong> {{ request.managementDecisionImpact }}
                </div>
                <div class="small text-muted">
                  Requested on {{ formatDate(request.managementDecisionRequestedAt) }}
                </div>
              </div>
            </div>

            <!-- Resolution Card -->
            <div v-if="request.resolution" class="card border-0 shadow-sm mb-4">
              <div class="card-header bg-white py-3 d-flex justify-content-between align-items-center">
                <h2 class="h6 fw-bold mb-0">Resolution Outcome</h2>
                <span class="badge bg-success">{{ request.resolution.outcome }}</span>
              </div>
              <div class="card-body">
                <p class="mb-2">{{ request.resolution.summary }}</p>
                <div class="small text-muted">
                  Resolved by {{ request.resolution.resolvedByName || 'Reviewer' }} on
                  {{ formatDate(request.resolution.resolvedAt) }}
                </div>
              </div>
            </div>
          </div>

          <!-- Right Column: Meta Info Card -->
          <div class="col-lg-4">
            <div class="card border-0 shadow-sm">
              <div class="card-header bg-white py-3">
                <h2 class="h6 fw-bold mb-0">Request Metadata</h2>
              </div>
              <div class="card-body p-0">
                <ul class="list-group list-group-flush small">
                  <li class="list-group-item d-flex justify-content-between py-2">
                    <span class="text-secondary">Current Assignee</span>
                    <span class="fw-semibold text-dark">{{ request.ownerName || 'Unassigned' }}</span>
                  </li>
                  <li class="list-group-item d-flex justify-content-between py-2">
                    <span class="text-secondary">Customer</span>
                    <span class="fw-semibold text-dark">{{ request.customerName || 'None' }}</span>
                  </li>
                  <li class="list-group-item d-flex justify-content-between py-2">
                    <span class="text-secondary">Product</span>
                    <span class="fw-semibold text-dark">{{ request.productName || 'None' }}</span>
                  </li>
                  <li class="list-group-item d-flex justify-content-between py-2">
                    <span class="text-secondary">Requester</span>
                    <span class="fw-semibold text-dark">{{ request.requesterName || 'System' }}</span>
                  </li>
                  <li class="list-group-item d-flex justify-content-between py-2">
                    <span class="text-secondary">Work Package</span>
                    <span class="fw-semibold text-dark">{{ request.workPackageId || 'Unlinked' }}</span>
                  </li>
                  <li class="list-group-item d-flex justify-content-between py-2">
                    <span class="text-secondary">Created At</span>
                    <span class="text-dark">{{ formatDate(request.createdAt) }}</span>
                  </li>
                  <li class="list-group-item d-flex justify-content-between py-2">
                    <span class="text-secondary">Last Updated</span>
                    <span class="text-dark">{{ formatDate(request.updatedAt) }}</span>
                  </li>
                  <li v-if="request.closedAt" class="list-group-item d-flex justify-content-between py-2">
                    <span class="text-secondary">Closed At</span>
                    <span class="text-dark">{{ formatDate(request.closedAt) }}</span>
                  </li>
                </ul>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Tab Content: Assignments -->
      <div v-else-if="activeTab === 'assignments'">
        <div class="card border-0 shadow-sm">
          <div class="card-header bg-white py-3">
            <h2 class="h6 fw-bold mb-0">Ownership Assignment Log</h2>
          </div>
          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th>Owner</th>
                  <th>Assigned By</th>
                  <th>Assigned At</th>
                  <th>Note</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="!request.assignments || request.assignments.length === 0">
                  <td colspan="5" class="text-center py-4 text-secondary">
                    No assignment records available.
                  </td>
                </tr>
                <tr v-for="a in request.assignments" :key="a.assignmentId">
                  <td class="fw-semibold text-dark">{{ a.ownerName || a.ownerPersonId }}</td>
                  <td class="text-secondary">{{ a.assignedByName || a.assignedByPersonId }}</td>
                  <td class="small text-secondary">{{ formatDate(a.assignedAt) }}</td>
                  <td class="small">{{ a.note || '—' }}</td>
                  <td>
                    <span v-if="a.isActive" class="badge bg-success-subtle text-success border border-success-subtle">
                      Active
                    </span>
                    <span v-else class="badge bg-light text-secondary border">
                      Past
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Tab Content: State Audit Trail -->
      <div v-else-if="activeTab === 'history'">
        <div class="card border-0 shadow-sm">
          <div class="card-header bg-white py-3">
            <h2 class="h6 fw-bold mb-0">Chronological State Transition Audit Trail</h2>
          </div>
          <div class="table-responsive">
            <table class="table table-hover align-middle mb-0">
              <thead class="table-light">
                <tr>
                  <th>Transition</th>
                  <th>Actor</th>
                  <th>Reason / Context</th>
                  <th>Timestamp (UTC)</th>
                </tr>
              </thead>
              <tbody>
                <tr v-if="stateHistory.length === 0">
                  <td colspan="4" class="text-center py-4 text-secondary">
                    No transition history recorded.
                  </td>
                </tr>
                <tr v-for="h in stateHistory" :key="h.stateHistoryId">
                  <td>
                    <span v-if="h.fromStatus" class="badge bg-light text-secondary border me-1">
                      {{ h.fromStatus }}
                    </span>
                    <i class="bi bi-arrow-right mx-1 text-secondary"></i>
                    <span class="badge" :class="getStatusBadgeClass(h.toStatus)">
                      {{ h.toStatus }}
                    </span>
                  </td>
                  <td class="fw-semibold text-dark">{{ h.actorName || h.actorPersonId }}</td>
                  <td class="small text-secondary">{{ h.reason || '—' }}</td>
                  <td class="small text-secondary">{{ formatDate(h.changedAt) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>

    <!-- Modals -->

    <!-- Assign Modal -->
    <div v-if="showAssignModal" class="modal d-block bg-dark bg-opacity-50" tabindex="-1">
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title fw-bold">Assign Request Owner</h5>
            <button type="button" class="btn-close" @click="showAssignModal = false"></button>
          </div>
          <form @submit.prevent="submitAssign">
            <div class="modal-body">
              <div class="mb-3">
                <label class="form-label fw-semibold">New Assignee <span class="text-danger">*</span></label>
                <select v-model="assignForm.newOwnerPersonId" class="form-select" required>
                  <option value="">-- Select Person --</option>
                  <option v-for="p in eligiblePersons" :key="p.personId" :value="p.personId">
                    {{ p.name }} ({{ p.primaryRole || 'Staff' }})
                  </option>
                </select>
              </div>
              <div class="mb-3">
                <label class="form-label fw-semibold">Assignment Note</label>
                <input v-model="assignForm.note" type="text" class="form-control" placeholder="Optional context..." />
              </div>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-outline-secondary" @click="showAssignModal = false">Cancel</button>
              <button type="submit" class="btn btn-primary" :disabled="isActionLoading">Assign</button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Reassign Modal -->
    <div v-if="showReassignModal" class="modal d-block bg-dark bg-opacity-50" tabindex="-1">
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title fw-bold">Reassign Ownership</h5>
            <button type="button" class="btn-close" @click="showReassignModal = false"></button>
          </div>
          <form @submit.prevent="submitReassign">
            <div class="modal-body">
              <div class="mb-3">
                <label class="form-label fw-semibold">New Assignee <span class="text-danger">*</span></label>
                <select v-model="reassignForm.newOwnerPersonId" class="form-select" required>
                  <option value="">-- Select Person --</option>
                  <option v-for="p in eligiblePersons" :key="p.personId" :value="p.personId">
                    {{ p.name }} ({{ p.primaryRole || 'Staff' }})
                  </option>
                </select>
              </div>
              <div class="mb-3">
                <label class="form-label fw-semibold">Reassignment Reason</label>
                <input v-model="reassignForm.reason" type="text" class="form-control" placeholder="Reason for transfer..." />
              </div>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-outline-secondary" @click="showReassignModal = false">Cancel</button>
              <button type="submit" class="btn btn-primary" :disabled="isActionLoading">Reassign</button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Reject Modal -->
    <div v-if="showRejectModal" class="modal d-block bg-dark bg-opacity-50" tabindex="-1">
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title fw-bold text-danger">Reject Request</h5>
            <button type="button" class="btn-close" @click="showRejectModal = false"></button>
          </div>
          <form @submit.prevent="submitReject">
            <div class="modal-body">
              <div class="mb-3">
                <label class="form-label fw-semibold">Rejection Reason <span class="text-danger">*</span></label>
                <textarea
                  v-model="rejectForm.reason"
                  class="form-control"
                  rows="4"
                  placeholder="Explain why this request is being rejected..."
                  required
                ></textarea>
              </div>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-outline-secondary" @click="showRejectModal = false">Cancel</button>
              <button type="submit" class="btn btn-danger" :disabled="isActionLoading">Confirm Rejection</button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Escalate Modal -->
    <div v-if="showEscalateModal" class="modal d-block bg-dark bg-opacity-50" tabindex="-1">
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title fw-bold text-danger">Escalate to Management</h5>
            <button type="button" class="btn-close" @click="showEscalateModal = false"></button>
          </div>
          <form @submit.prevent="submitEscalate">
            <div class="modal-body">
              <div class="mb-3">
                <label class="form-label fw-semibold">Escalation Reason <span class="text-danger">*</span></label>
                <textarea
                  v-model="escalateForm.reason"
                  class="form-control"
                  rows="3"
                  placeholder="Blocker, impediment, or critical risk description..."
                  required
                ></textarea>
              </div>
              <div class="mb-3">
                <label class="form-label fw-semibold">Assistance Required</label>
                <input
                  v-model="escalateForm.requiredAssistance"
                  type="text"
                  class="form-control"
                  placeholder="What help is needed from management?"
                />
              </div>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-outline-secondary" @click="showEscalateModal = false">Cancel</button>
              <button type="submit" class="btn btn-danger" :disabled="isActionLoading">Escalate</button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Management Decision Modal -->
    <div v-if="showDecisionModal" class="modal d-block bg-dark bg-opacity-50" tabindex="-1">
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title fw-bold text-primary">Request Management Decision</h5>
            <button type="button" class="btn-close" @click="showDecisionModal = false"></button>
          </div>
          <form @submit.prevent="submitDecision">
            <div class="modal-body">
              <div class="mb-3">
                <label class="form-label fw-semibold">Decision Question <span class="text-danger">*</span></label>
                <textarea
                  v-model="decisionForm.question"
                  class="form-control"
                  rows="2"
                  placeholder="What specific guidance or decision is needed?"
                  required
                ></textarea>
              </div>
              <div class="mb-3">
                <label class="form-label fw-semibold">Options Considered</label>
                <textarea
                  v-model="decisionForm.options"
                  class="form-control"
                  rows="2"
                  placeholder="Option A vs Option B..."
                ></textarea>
              </div>
              <div class="mb-3">
                <label class="form-label fw-semibold">Expected Impact</label>
                <input
                  v-model="decisionForm.impact"
                  type="text"
                  class="form-control"
                  placeholder="Timeline, scope, or cost implications"
                />
              </div>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-outline-secondary" @click="showDecisionModal = false">Cancel</button>
              <button type="submit" class="btn btn-primary" :disabled="isActionLoading">Submit Docket</button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Complete Modal -->
    <div v-if="showCompleteModal" class="modal d-block bg-dark bg-opacity-50" tabindex="-1">
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title fw-bold text-success">Complete Request</h5>
            <button type="button" class="btn-close" @click="showCompleteModal = false"></button>
          </div>
          <form @submit.prevent="submitComplete">
            <div class="modal-body">
              <div class="mb-3">
                <label class="form-label fw-semibold">Outcome</label>
                <select v-model="completeForm.outcome" class="form-select">
                  <option value="RESOLVED">Resolved / Delivered</option>
                  <option value="CANCELLED">Cancelled</option>
                </select>
              </div>
              <div class="mb-3">
                <label class="form-label fw-semibold">Resolution Summary <span class="text-danger">*</span></label>
                <textarea
                  v-model="completeForm.summary"
                  class="form-control"
                  rows="4"
                  placeholder="Provide closing summary, delivered release version, or resolution notes..."
                  required
                ></textarea>
              </div>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-outline-secondary" @click="showCompleteModal = false">Cancel</button>
              <button type="submit" class="btn btn-success" :disabled="isActionLoading">Mark Completed</button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Rework Modal -->
    <div v-if="showReworkModal" class="modal d-block bg-dark bg-opacity-50" tabindex="-1">
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content">
          <div class="modal-header">
            <h5 class="modal-title fw-bold text-warning-emphasis">Request Rework</h5>
            <button type="button" class="btn-close" @click="showReworkModal = false"></button>
          </div>
          <form @submit.prevent="submitRework">
            <div class="modal-body">
              <div class="mb-3">
                <label class="form-label fw-semibold">Rework Feedback <span class="text-danger">*</span></label>
                <textarea
                  v-model="reworkForm.feedback"
                  class="form-control"
                  rows="4"
                  placeholder="Specify what remains to be fixed or addressed before completion..."
                  required
                ></textarea>
              </div>
            </div>
            <div class="modal-footer">
              <button type="button" class="btn btn-outline-secondary" @click="showReworkModal = false">Cancel</button>
              <button type="submit" class="btn btn-warning text-dark" :disabled="isActionLoading">Return for Rework</button>
            </div>
          </form>
        </div>
      </div>
    </div>
  </div>
</template>
