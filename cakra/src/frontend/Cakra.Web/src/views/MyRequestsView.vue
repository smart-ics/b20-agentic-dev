<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { httpClient } from '@/api/http'
import type { RequestDto } from '@/api/requests'
import {
  completeSubTask as apiCompleteSubTask,
  reopenSubTask as apiReopenSubTask,
  getAssignedSubTasks as apiGetAssignedSubTasks,
} from '@/api/requests'
import { useAuthStore } from '@/stores/auth'

/**
 * SCR-REQ-004: My Requests Screen & Personal Workspace
 * (Architecture §7, §8 — UC-COL-004, §9 — FEAT-COL-001..004, CR-006 TD-009, TD-010).
 *
 * - Renders operational requests assigned to the current authenticated user.
 * - Renders personal assigned sub-tasks queue across active requests with one-click completion toggle.
 * - Calls `GET /api/v1/requests/my` and `GET /api/v1/requests/assigned-subtasks`.
 * - Provides navigation links to `/requests/${id}` (`SCR-REQ-003`).
 */

export interface AssignedRequestItem {
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
  totalSubTasksCount?: number
  completedSubTasksCount?: number
  completionPercentage?: number
  createdAt: string
  updatedAt?: string | null
}

export interface FlattenedAssignedSubTask {
  id: string
  requestId: string
  requestTitle: string
  requestStatus: string
  title: string
  isCompleted: boolean
  sortOrder: number
  createdAt: string
  completedAt?: string | null
  completedByName?: string | null
  assigneeName?: string | null
}

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const MY_REQUEST_STATUSES = [
  'ASSIGNED',
  'IN_PROGRESS',
  'PAUSED',
  'COMPLETED',
  'CANCELLED',
] as const

const STATUS_FILTER_TABS = [
  { value: '', label: 'ALL' },
  { value: 'ASSIGNED', label: 'ASSIGNED' },
  { value: 'IN_PROGRESS', label: 'IN_PROGRESS' },
  { value: 'PAUSED', label: 'PAUSED' },
  { value: 'COMPLETED', label: 'COMPLETED' },
  { value: 'CANCELLED', label: 'CANCELLED' },
] as const

const router = useRouter()
const authStore = useAuthStore()

const activeTab = ref<'requests' | 'subtasks'>('requests')
const myRequests = ref<AssignedRequestItem[]>([])
const assignedSubTaskRequests = ref<RequestDto[]>([])
const selectedStatusFilter = ref<string>('')
const isLoading = ref(false)
const isSubTasksLoading = ref(false)
const subTaskOperatingId = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const actionSuccessMessage = ref<string | null>(null)
const showCompletedSubTasks = ref(false)

const filteredMyRequests = computed<AssignedRequestItem[]>(() => {
  if (!selectedStatusFilter.value) {
    return myRequests.value
  }
  return myRequests.value.filter(
    (req) => (req.status ?? '').toUpperCase() === selectedStatusFilter.value.toUpperCase(),
  )
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

function statusBadgeClass(status: string): string {
  switch ((status ?? '').toUpperCase()) {
    case 'CAPTURED':
      return 'text-bg-secondary'
    case 'ASSIGNED':
      return 'text-bg-info text-dark'
    case 'IN_PROGRESS':
      return 'text-bg-primary'
    case 'PAUSED':
      return 'text-bg-warning text-dark'
    case 'COMPLETED':
      return 'text-bg-success'
    case 'CANCELLED':
      return 'text-bg-danger'
    case 'EVALUATING':
      return 'text-bg-info text-dark'
    case 'ACCEPTED':
      return 'text-bg-info text-dark'
    case 'ESCALATED':
      return 'text-bg-warning text-dark'
    case 'REJECTED':
      return 'text-bg-danger'
    default:
      return 'text-bg-secondary'
  }
}

function priorityBadgeClass(priority: string): string {
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

function resolveCustomerDisplay(item: AssignedRequestItem): string {
  if (item.customerName && item.customerName.trim().length > 0) {
    return item.customerCode ? `${item.customerName} (${item.customerCode})` : item.customerName
  }
  if (item.customerCode && item.customerCode.trim().length > 0) {
    return item.customerCode
  }
  return item.customerId ?? '—'
}

function resolveProductDisplay(item: AssignedRequestItem): string {
  if (item.productName && item.productName.trim().length > 0) {
    return item.productCode ? `${item.productName} (${item.productCode})` : item.productName
  }
  if (item.productCode && item.productCode.trim().length > 0) {
    return item.productCode
  }
  return item.productId ?? '—'
}

function resolveAssigneeDisplay(item: AssignedRequestItem): string {
  if (item.assigneeName && item.assigneeName.trim().length > 0) {
    return item.assigneeName
  }
  if (item.ownerName && item.ownerName.trim().length > 0) {
    return item.ownerName
  }
  return item.assigneePersonId ?? item.ownerPersonId ?? 'Assigned to Me'
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

const assignedSubTasks = computed<FlattenedAssignedSubTask[]>(() => {
  const list: FlattenedAssignedSubTask[] = []
  const currentPersonId = authStore.currentUser?.personId?.toLowerCase()

  for (const req of assignedSubTaskRequests.value) {
    if (!req.subTasks) continue
    for (const st of req.subTasks) {
      const isAssigned =
        !currentPersonId ||
        !st.assigneePersonId ||
        st.assigneePersonId.toLowerCase() === currentPersonId

      if (isAssigned) {
        list.push({
          id: st.id,
          requestId: req.id,
          requestTitle: req.title,
          requestStatus: req.status,
          title: st.title,
          isCompleted: st.isCompleted,
          sortOrder: st.sortOrder,
          createdAt: st.createdAt,
          completedAt: st.completedAt,
          completedByName: st.completedByName,
          assigneeName: st.assigneeName,
        })
      }
    }
  }
  return list
})

const filteredAssignedSubTasks = computed(() => {
  if (showCompletedSubTasks.value) {
    return assignedSubTasks.value
  }
  return assignedSubTasks.value.filter((st) => !st.isCompleted)
})

const pendingSubTasksCount = computed(
  () => assignedSubTasks.value.filter((st) => !st.isCompleted).length,
)

async function loadMyRequests(): Promise<void> {
  isLoading.value = true
  errorMessage.value = null

  try {
    const response = await httpClient.get<AssignedRequestItem[] | { items?: AssignedRequestItem[] }>(
      '/requests/my',
    )
    if (Array.isArray(response.data)) {
      myRequests.value = response.data
    } else {
      myRequests.value = response.data.items ?? []
    }
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load assigned requests.')
  } finally {
    isLoading.value = false
  }
}

async function loadAssignedSubTasks(): Promise<void> {
  isSubTasksLoading.value = true
  try {
    const data = await apiGetAssignedSubTasks(authStore.currentUser?.personId)
    assignedSubTaskRequests.value = data
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load assigned sub-tasks.')
  } finally {
    isSubTasksLoading.value = false
  }
}

async function handleToggleAssignedSubTask(item: FlattenedAssignedSubTask): Promise<void> {
  subTaskOperatingId.value = item.id
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    if (item.isCompleted) {
      await apiReopenSubTask(item.requestId, item.id)
      item.isCompleted = false
      actionSuccessMessage.value = `Sub-task "${item.title}" reopened.`
    } else {
      await apiCompleteSubTask(item.requestId, item.id)
      item.isCompleted = true
      actionSuccessMessage.value = `Sub-task "${item.title}" completed.`
    }
    await loadAssignedSubTasks()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to update sub-task status.')
  } finally {
    subTaskOperatingId.value = null
  }
}

async function refreshAll(): Promise<void> {
  await Promise.all([loadMyRequests(), loadAssignedSubTasks()])
}

async function navigateToDetail(id: string): Promise<void> {
  await router.push(`/requests/${id}`)
}

onMounted(async () => {
  await refreshAll()
})
</script>

<template>
  <section data-screen-id="SCR-REQ-004">
    <!-- Compact Screen Header -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2">
        <h1 class="op-screen-title">
          <i class="bi bi-person-workspace text-primary" aria-hidden="true"></i>
          My Assigned Requests & Sub-Tasks
        </h1>
        <span class="badge text-bg-light border text-secondary font-monospace">SCR-REQ-004</span>
      </div>

      <div class="d-flex align-items-center gap-2">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm"
          :disabled="isLoading || isSubTasksLoading"
          data-testid="refresh-my-requests-button"
          @click="refreshAll"
        >
          <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>
          Refresh
        </button>

        <router-link
          to="/requests"
          class="btn btn-outline-secondary btn-sm"
          data-testid="all-requests-link"
        >
          <i class="bi bi-card-checklist me-1" aria-hidden="true"></i>
          All Requests
        </router-link>

        <router-link
          to="/requests/create"
          class="btn btn-primary btn-sm"
          data-testid="create-request-link"
        >
          <i class="bi bi-plus-lg me-1" aria-hidden="true"></i>
          Create Request
        </router-link>
      </div>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      role="alert"
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center gap-2 py-1 px-2 mb-2"
      data-testid="my-requests-error-alert"
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
      class="alert alert-success alert-dismissible fade show d-flex align-items-center gap-2 py-1 px-2 mb-2"
      data-testid="my-requests-success-alert"
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

    <!-- Workspace Tabs Navigation (CR-006, TD-009, TD-010) -->
    <ul class="nav nav-tabs mb-2" data-testid="my-workspace-tabs">
      <li class="nav-item">
        <button
          type="button"
          class="nav-link py-1 px-3"
          :class="{ active: activeTab === 'requests' }"
          data-testid="tab-requests"
          @click="activeTab = 'requests'"
        >
          <i class="bi bi-card-checklist me-1" aria-hidden="true"></i>
          Assigned Requests
          <span class="badge text-bg-secondary ms-1" data-testid="tab-requests-count">{{ myRequests.length }}</span>
        </button>
      </li>
      <li class="nav-item">
        <button
          type="button"
          class="nav-link py-1 px-3"
          :class="{ active: activeTab === 'subtasks' }"
          data-testid="tab-subtasks"
          @click="activeTab = 'subtasks'"
        >
          <i class="bi bi-check2-square me-1" aria-hidden="true"></i>
          Assigned Sub-Tasks
          <span
            class="badge ms-1"
            :class="pendingSubTasksCount > 0 ? 'text-bg-primary' : 'text-bg-secondary'"
            data-testid="tab-subtasks-count"
          >
            {{ pendingSubTasksCount }}
          </span>
        </button>
      </li>
    </ul>

    <!-- Tab 1: High-Density My Assigned Requests Table Card -->
    <div v-if="activeTab === 'requests'" class="card border" data-testid="my-requests-card">
      <!-- Status Filter Toolbar & Tabs (CR-016 TD-001) -->
      <div class="card-header bg-body-tertiary py-1 px-3 d-flex flex-wrap align-items-center justify-content-between gap-2">
        <div class="d-flex align-items-center gap-1">
          <label for="myStatusFilterSelect" class="form-label mb-0 fs-11 text-nowrap">Status:</label>
          <select
            id="myStatusFilterSelect"
            v-model="selectedStatusFilter"
            class="form-select form-select-sm"
            style="min-width: 130px; max-width: 170px;"
            data-testid="my-status-filter-select"
          >
            <option value="">ALL</option>
            <option v-for="status in MY_REQUEST_STATUSES" :key="status" :value="status">
              {{ status }}
            </option>
          </select>
        </div>

        <ul class="nav nav-pills gap-1" data-testid="my-status-tabs">
          <li v-for="tab in STATUS_FILTER_TABS" :key="tab.value" class="nav-item">
            <button
              type="button"
              class="nav-link py-0.5 px-2 btn-sm font-monospace fs-11"
              :class="{ active: selectedStatusFilter === tab.value }"
              :data-testid="`my-status-tab-${tab.value || 'ALL'}`"
              @click="selectedStatusFilter = tab.value"
            >
              {{ tab.label }}
            </button>
          </li>
        </ul>
      </div>

      <div class="table-responsive">
        <table
          class="table table-hover align-middle mb-0"
          data-testid="my-requests-table"
        >
          <thead>
            <tr>
              <th scope="col" style="width: 105px;">ID</th>
              <th scope="col">Title</th>
              <th scope="col" style="width: 150px;">Customer</th>
              <th scope="col" style="width: 140px;">Product</th>
              <th scope="col" style="width: 105px;">Status</th>
              <th scope="col" style="width: 120px;">Progress</th>
              <th scope="col" style="width: 140px;">Assignee</th>
              <th scope="col" style="width: 125px;">CreatedAt</th>
              <th scope="col" style="width: 90px;" class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="isLoading">
              <td colspan="9" class="text-center py-4 text-body-secondary">
                <span
                  class="spinner-border spinner-border-sm me-2"
                  role="status"
                  aria-hidden="true"
                ></span>
                Loading assigned requests...
              </td>
            </tr>

            <tr v-else-if="filteredMyRequests.length === 0">
              <td
                colspan="9"
                class="text-center py-4 text-body-secondary"
                data-testid="empty-my-requests-row"
              >
                {{
                  selectedStatusFilter
                    ? `No assigned operational requests match status "${selectedStatusFilter}".`
                    : 'You currently have no assigned operational requests.'
                }}
              </td>
            </tr>

            <tr
              v-for="req in filteredMyRequests"
              v-else
              :key="req.id"
              :data-request-id="req.id"
              style="cursor: pointer"
              data-testid="my-request-row"
              @click="navigateToDetail(req.id)"
            >
              <td>
                <router-link
                  :to="`/requests/${req.id}`"
                  class="font-monospace text-decoration-none fw-semibold"
                  style="font-size: 11.5px;"
                  data-testid="my-request-id-link"
                  @click.stop
                >
                  {{ req.id }}
                </router-link>
              </td>

              <td>
                <div class="d-flex align-items-center gap-1.5 flex-nowrap">
                  <router-link
                    :to="`/requests/${req.id}`"
                    class="fw-semibold text-decoration-none text-dark text-truncate"
                    style="max-width: 360px;"
                    data-testid="my-request-title-link"
                    :title="req.title"
                    @click.stop
                  >
                    {{ req.title }}
                  </router-link>
                  <span
                    v-if="req.priority"
                    class="badge flex-shrink-0"
                    :class="priorityBadgeClass(req.priority)"
                  >
                    {{ req.priority }}
                  </span>
                </div>
              </td>

              <td>
                <span class="text-truncate d-inline-block" style="max-width: 145px;" :title="resolveCustomerDisplay(req)">
                  {{ resolveCustomerDisplay(req) }}
                </span>
              </td>

              <td>
                <span class="text-truncate d-inline-block" style="max-width: 135px;" :title="resolveProductDisplay(req)">
                  {{ resolveProductDisplay(req) }}
                </span>
              </td>

              <td>
                <span
                  class="badge"
                  :class="statusBadgeClass(req.status)"
                  data-testid="my-request-status-badge"
                >
                  {{ req.status }}
                </span>
              </td>

              <!-- Progress Column -->
              <td>
                <div
                  v-if="(req.totalSubTasksCount ?? 0) > 0"
                  class="d-flex flex-column gap-1"
                  data-testid="my-request-progress"
                >
                  <div class="d-flex justify-content-between align-items-center" style="font-size: 11px;">
                    <span class="fw-semibold">{{ req.completionPercentage ?? 0 }}%</span>
                    <span class="text-body-secondary small">({{ req.completedSubTasksCount ?? 0 }}/{{ req.totalSubTasksCount ?? 0 }})</span>
                  </div>
                  <div class="progress" style="height: 6px;">
                    <div
                      class="progress-bar"
                      :class="(req.completionPercentage ?? 0) === 100 ? 'bg-success' : 'bg-primary'"
                      role="progressbar"
                      :style="{ width: `${req.completionPercentage ?? 0}%` }"
                      :aria-valuenow="req.completionPercentage ?? 0"
                      aria-valuemin="0"
                      aria-valuemax="100"
                    ></div>
                  </div>
                </div>
                <span v-else class="text-body-secondary small" style="font-size: 11px;">—</span>
              </td>

              <td>
                <span class="text-truncate d-inline-block" style="max-width: 135px;">
                  <i class="bi bi-person me-0.5 text-secondary" aria-hidden="true"></i>
                  {{ resolveAssigneeDisplay(req) }}
                </span>
              </td>

              <td class="text-body-secondary fs-11">
                {{ formatTimestamp(req.createdAt) }}
              </td>

              <td class="text-end" @click.stop>
                <router-link
                  :to="`/requests/${req.id}`"
                  class="btn btn-outline-primary btn-sm py-0 px-1.5 fs-11"
                  data-testid="my-request-detail-button"
                  @click.stop
                >
                  View
                </router-link>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Tab 2: Assigned Sub-Tasks Personal Queue Card (CR-006, TD-009, TD-010) -->
    <div v-if="activeTab === 'subtasks'" class="card border" data-testid="assigned-subtasks-card">
      <div class="card-header py-1 px-3 bg-body-tertiary d-flex align-items-center justify-content-between flex-wrap gap-2">
        <div class="d-flex align-items-center gap-2">
          <span class="fw-semibold small">
            <i class="bi bi-list-task me-1 text-primary" aria-hidden="true"></i>
            Actionable Sub-Tasks Queue
          </span>
          <span class="badge text-bg-secondary" data-testid="assigned-subtasks-total-badge">
            {{ filteredAssignedSubTasks.length }} item(s)
          </span>
        </div>
        <div class="form-check form-switch m-0 small">
          <input
            id="showCompletedSubTasksCheck"
            v-model="showCompletedSubTasks"
            type="checkbox"
            class="form-check-input"
            role="switch"
            data-testid="show-completed-subtasks-toggle"
          />
          <label for="showCompletedSubTasksCheck" class="form-check-label text-body-secondary" style="font-size: 11.5px;">
            Show completed
          </label>
        </div>
      </div>

      <div class="table-responsive">
        <table class="table table-hover align-middle mb-0" data-testid="assigned-subtasks-table">
          <thead>
            <tr>
              <th scope="col" style="width: 50px;" class="text-center">Done</th>
              <th scope="col">Sub-Task</th>
              <th scope="col" style="width: 250px;">Parent Request</th>
              <th scope="col" style="width: 130px;">Request Status</th>
              <th scope="col" style="width: 140px;">Created</th>
              <th scope="col" style="width: 180px;">Completion Details</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="isSubTasksLoading">
              <td colspan="6" class="text-center py-4 text-body-secondary">
                <span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                Loading assigned sub-tasks...
              </td>
            </tr>

            <tr v-else-if="filteredAssignedSubTasks.length === 0">
              <td colspan="6" class="text-center py-4 text-body-secondary" data-testid="empty-assigned-subtasks-row">
                <div class="d-flex flex-column align-items-center gap-1">
                  <i class="bi bi-check2-circle text-success" style="font-size: 24px;" aria-hidden="true"></i>
                  <span>You have no {{ showCompletedSubTasks ? '' : 'pending' }} assigned sub-tasks.</span>
                </div>
              </td>
            </tr>

            <tr
              v-for="subTask in filteredAssignedSubTasks"
              v-else
              :key="subTask.id"
              :data-subtask-id="subTask.id"
              data-testid="assigned-subtask-row"
            >
              <!-- Done Toggle Checkbox -->
              <td class="text-center">
                <div class="form-check d-inline-block m-0">
                  <input
                    :id="`personal-subtask-chk-${subTask.id}`"
                    type="checkbox"
                    class="form-check-input"
                    style="cursor: pointer;"
                    :checked="subTask.isCompleted"
                    :disabled="subTaskOperatingId === subTask.id"
                    title="Click to toggle completion status"
                    data-testid="personal-subtask-checkbox"
                    @change="handleToggleAssignedSubTask(subTask)"
                  />
                </div>
              </td>

              <!-- Sub-Task Title -->
              <td>
                <label
                  :for="`personal-subtask-chk-${subTask.id}`"
                  class="mb-0 fw-medium d-block text-break"
                  :class="{ 'text-decoration-line-through text-body-secondary': subTask.isCompleted }"
                  style="cursor: pointer; font-size: 13px;"
                  data-testid="assigned-subtask-title"
                >
                  {{ subTask.title }}
                </label>
              </td>

              <!-- Parent Request Link & Title -->
              <td>
                <div class="d-flex flex-column gap-0.5">
                  <router-link
                    :to="`/requests/${subTask.requestId}`"
                    class="fw-semibold text-decoration-none text-truncate"
                    style="max-width: 240px; font-size: 12.5px;"
                    :title="subTask.requestTitle"
                    data-testid="parent-request-link"
                  >
                    {{ subTask.requestTitle }}
                  </router-link>
                  <span class="font-monospace text-body-secondary" style="font-size: 10.5px;">
                    {{ subTask.requestId }}
                  </span>
                </div>
              </td>

              <!-- Request Status Badge -->
              <td>
                <span class="badge" :class="statusBadgeClass(subTask.requestStatus)" style="font-size: 10px;">
                  {{ subTask.requestStatus }}
                </span>
              </td>

              <!-- Assigned Date -->
              <td class="text-body-secondary" style="font-size: 11px;">
                {{ formatTimestamp(subTask.createdAt) }}
              </td>

              <!-- Completion Details -->
              <td>
                <span v-if="subTask.isCompleted" class="text-success small d-flex flex-column" style="font-size: 11px;">
                  <span><i class="bi bi-check-circle me-1"></i>Completed</span>
                  <span v-if="subTask.completedByName" class="text-body-secondary">by {{ subTask.completedByName }}</span>
                  <span v-if="subTask.completedAt" class="text-body-secondary">{{ formatTimestamp(subTask.completedAt) }}</span>
                </span>
                <span v-else class="badge text-bg-warning-subtle border border-warning-subtle text-warning-emphasis" style="font-size: 10px;">
                  Pending
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>
