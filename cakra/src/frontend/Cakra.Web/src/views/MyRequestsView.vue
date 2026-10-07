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
const selectedPriorityFilter = ref<string>('')
const searchQuery = ref<string>('')
const isLoading = ref(false)
const isSubTasksLoading = ref(false)
const subTaskOperatingId = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const actionSuccessMessage = ref<string | null>(null)
const showCompletedSubTasks = ref(false)

const totalAssignedCount = computed(() => myRequests.value.length)
const inProgressCount = computed(
  () => myRequests.value.filter((req) => (req.status ?? '').toUpperCase() === 'IN_PROGRESS').length,
)
const pausedCount = computed(
  () => myRequests.value.filter((req) => (req.status ?? '').toUpperCase() === 'PAUSED').length,
)
const overallCompletionPercentage = computed(() => {
  let totalTasks = 0
  let completedTasks = 0
  for (const req of myRequests.value) {
    totalTasks += req.totalSubTasksCount ?? 0
    completedTasks += req.completedSubTasksCount ?? 0
  }
  return totalTasks > 0 ? Math.round((completedTasks / totalTasks) * 100) : 0
})

const filteredMyRequests = computed<AssignedRequestItem[]>(() => {
  let list = myRequests.value

  if (selectedStatusFilter.value) {
    list = list.filter(
      (req) => (req.status ?? '').toUpperCase() === selectedStatusFilter.value.toUpperCase(),
    )
  }

  if (selectedPriorityFilter.value) {
    list = list.filter(
      (req) => (req.priority ?? '').toUpperCase() === selectedPriorityFilter.value.toUpperCase(),
    )
  }

  if (searchQuery.value.trim()) {
    const q = searchQuery.value.trim().toLowerCase()
    list = list.filter((req) => {
      return (
        (req.id ?? '').toLowerCase().includes(q) ||
        (req.title ?? '').toLowerCase().includes(q) ||
        (req.description ?? '').toLowerCase().includes(q) ||
        (req.customerName ?? '').toLowerCase().includes(q) ||
        (req.customerCode ?? '').toLowerCase().includes(q) ||
        (req.productName ?? '').toLowerCase().includes(q) ||
        (req.productCode ?? '').toLowerCase().includes(q)
      )
    })
  }

  return list
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

const subTaskSearchQuery = ref<string>('')

const completedSubTasksCount = computed(
  () => assignedSubTasks.value.filter((st) => st.isCompleted).length,
)

const subTasksCompletionRate = computed(() => {
  const total = assignedSubTasks.value.length
  if (total === 0) return 0
  return Math.round((completedSubTasksCount.value / total) * 100)
})

const filteredAssignedSubTasks = computed(() => {
  let list = showCompletedSubTasks.value
    ? assignedSubTasks.value
    : assignedSubTasks.value.filter((st) => !st.isCompleted)

  if (subTaskSearchQuery.value.trim()) {
    const q = subTaskSearchQuery.value.trim().toLowerCase()
    list = list.filter(
      (st) =>
        st.title.toLowerCase().includes(q) ||
        st.requestTitle.toLowerCase().includes(q) ||
        st.requestId.toLowerCase().includes(q),
    )
  }

  return list
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

    <!-- Executive KPI Metrics Summary Highlights -->
    <div class="row g-2 mb-2" data-testid="my-workspace-kpis">
      <div class="col-6 col-md-3">
        <div class="card bg-slate-900/90 border border-slate-800 text-slate-100 p-2 h-100 shadow-md">
          <div class="d-flex align-items-center justify-content-between text-slate-400 mb-1">
            <span class="fs-11 fw-semibold text-uppercase tracking-wider">Assigned Requests</span>
            <i class="bi bi-folder2-open text-primary" aria-hidden="true"></i>
          </div>
          <div class="d-flex align-items-baseline gap-1.5">
            <span class="fs-4 fw-bold text-white">{{ totalAssignedCount }}</span>
            <span class="fs-11 text-slate-400">in scope</span>
          </div>
        </div>
      </div>
      <div class="col-6 col-md-3">
        <div class="card bg-slate-900/90 border border-slate-800 text-slate-100 p-2 h-100 shadow-md">
          <div class="d-flex align-items-center justify-content-between text-slate-400 mb-1">
            <span class="fs-11 fw-semibold text-uppercase tracking-wider">In Progress</span>
            <i class="bi bi-lightning-charge-fill text-info" aria-hidden="true"></i>
          </div>
          <div class="d-flex align-items-baseline gap-1.5">
            <span class="fs-4 fw-bold text-cyan-400">{{ inProgressCount }}</span>
            <span class="fs-11 text-slate-400">active &bull; {{ pausedCount }} paused</span>
          </div>
        </div>
      </div>
      <div class="col-6 col-md-3">
        <div class="card bg-slate-900/90 border border-slate-800 text-slate-100 p-2 h-100 shadow-md">
          <div class="d-flex align-items-center justify-content-between text-slate-400 mb-1">
            <span class="fs-11 fw-semibold text-uppercase tracking-wider">Pending Sub-Tasks</span>
            <i class="bi bi-check2-circle text-warning" aria-hidden="true"></i>
          </div>
          <div class="d-flex align-items-baseline gap-1.5">
            <span class="fs-4 fw-bold text-amber-400">{{ pendingSubTasksCount }}</span>
            <span class="fs-11 text-slate-400">actionable</span>
          </div>
        </div>
      </div>
      <div class="col-6 col-md-3">
        <div class="card bg-slate-900/90 border border-slate-800 text-slate-100 p-2 h-100 shadow-md">
          <div class="d-flex align-items-center justify-content-between text-slate-400 mb-1">
            <span class="fs-11 fw-semibold text-uppercase tracking-wider">Subtask Velocity</span>
            <i class="bi bi-graph-up-arrow text-success" aria-hidden="true"></i>
          </div>
          <div class="d-flex align-items-baseline gap-1.5">
            <span class="fs-4 fw-bold text-emerald-400">{{ overallCompletionPercentage }}%</span>
            <span class="fs-11 text-slate-400">done</span>
          </div>
          <div class="progress mt-1 bg-slate-800" style="height: 4px;">
            <div
              class="progress-bar bg-success"
              role="progressbar"
              :style="{ width: `${overallCompletionPercentage}%` }"
              :aria-valuenow="overallCompletionPercentage"
              aria-valuemin="0"
              aria-valuemax="100"
            ></div>
          </div>
        </div>
      </div>
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

    <!-- Tab 1: High-Density My Assigned Requests Table / Cards Card -->
    <div v-if="activeTab === 'requests'" class="card border border-slate-800 bg-slate-900/90 text-slate-100 shadow-md" data-testid="my-requests-card">
      <!-- Status & Search Toolbar (CR-016 TD-001) -->
      <div class="card-header bg-slate-950/60 border-b border-slate-800 py-1 px-3 d-flex flex-wrap align-items-center justify-content-between gap-2">
        <div class="d-flex align-items-center gap-2 flex-wrap">
          <!-- Universal Search Input -->
          <div class="input-group input-group-sm" style="width: 220px;">
            <span class="input-group-text bg-slate-800 border-slate-700 border-end-0 text-slate-400 py-0 px-2">
              <i class="bi bi-search" style="font-size: 11px;"></i>
            </span>
            <input
              v-model="searchQuery"
              type="text"
              class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 border-start-0 ps-0 focus:border-cyan-400"
              placeholder="Search tickets..."
              style="font-size: 11.5px;"
              data-testid="my-requests-search-input"
            />
            <button
              v-if="searchQuery"
              type="button"
              class="btn btn-outline-secondary border-start-0 bg-slate-800 border-slate-700 text-slate-400 py-0 px-1.5"
              @click="searchQuery = ''"
            >
              <i class="bi bi-x-circle-fill" style="font-size: 10px;"></i>
            </button>
          </div>

          <!-- Status Dropdown -->
          <div class="d-flex align-items-center gap-1">
            <label for="myStatusFilterSelect" class="form-label mb-0 fs-11 text-nowrap text-slate-300">Status:</label>
            <select
              id="myStatusFilterSelect"
              v-model="selectedStatusFilter"
              class="form-select form-select-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
              style="min-width: 110px; max-width: 140px;"
              data-testid="my-status-filter-select"
            >
              <option value="">ALL</option>
              <option v-for="status in MY_REQUEST_STATUSES" :key="status" :value="status">
                {{ status }}
              </option>
            </select>
          </div>

          <!-- Priority Filter -->
          <div class="d-flex align-items-center gap-1">
            <label for="myPriorityFilterSelect" class="form-label mb-0 fs-11 text-nowrap">Priority:</label>
            <select
              id="myPriorityFilterSelect"
              v-model="selectedPriorityFilter"
              class="form-select form-select-sm"
              style="min-width: 95px; max-width: 120px;"
              data-testid="my-priority-filter-select"
            >
              <option value="">ALL</option>
              <option value="URGENT">URGENT</option>
              <option value="HIGH">HIGH</option>
              <option value="NORMAL">NORMAL</option>
              <option value="LOW">LOW</option>
            </select>
          </div>
        </div>

        <div class="d-flex align-items-center gap-2">
          <!-- Status Filter Tabs -->
          <ul class="nav nav-pills gap-1 d-none d-lg-flex" data-testid="my-status-tabs">
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
      </div>

      <!-- Modern Cards Grid View (Exclusive View) -->
      <div class="p-3" data-testid="my-requests-cards-container">
        <div v-if="isLoading" class="text-center py-4 text-slate-400">
          <span class="spinner-border spinner-border-sm me-2 text-cyan-400" role="status" aria-hidden="true"></span>
          Loading assigned requests...
        </div>

        <div
          v-else-if="filteredMyRequests.length === 0"
          class="text-center py-4 text-slate-400"
          data-testid="empty-my-requests-row"
        >
          {{
            selectedStatusFilter || searchQuery || selectedPriorityFilter
              ? 'No assigned operational requests match current filter criteria.'
              : 'You currently have no assigned operational requests.'
          }}
        </div>

        <div v-else class="row g-2.5">
          <div
            v-for="req in filteredMyRequests"
            :key="req.id"
            :data-request-id="req.id"
            class="col-12 col-md-6 col-lg-4"
            data-testid="my-request-row"
          >
            <div
              class="card h-100 p-3 shadow-xs border border-slate-800 bg-slate-900/90 text-slate-100"
              style="cursor: pointer; transition: transform 0.15s ease, box-shadow 0.15s ease;"
              data-testid="my-request-grid-card"
              @click="navigateToDetail(req.id)"
            >
              <div class="d-flex align-items-center justify-content-between mb-1.5">
                <div class="d-flex align-items-center gap-1.5">
                  <router-link
                    :to="`/requests/${req.id}`"
                    class="font-monospace fw-bold text-cyan-400 text-decoration-none"
                    style="font-size: 11.5px;"
                    data-testid="my-request-id-link"
                    @click.stop
                  >
                    {{ req.id }}
                  </router-link>
                  <span v-if="req.priority" class="badge" :class="priorityBadgeClass(req.priority)">
                    {{ req.priority }}
                  </span>
                </div>
                <span
                  class="badge"
                  :class="statusBadgeClass(req.status)"
                  data-testid="my-request-status-badge"
                >
                  {{ req.status }}
                </span>
              </div>

              <h6 class="fw-bold text-slate-100 text-truncate mb-1" :title="req.title">
                <router-link
                  :to="`/requests/${req.id}`"
                  class="fw-semibold text-decoration-none text-slate-100 hover:text-cyan-400"
                  data-testid="my-request-title-link"
                  @click.stop
                >
                  {{ req.title }}
                </router-link>
              </h6>

              <p class="text-slate-400 small mb-2 text-truncate" style="font-size: 11.5px;">
                {{ req.description || 'No description provided.' }}
              </p>

              <div class="d-flex align-items-center gap-1 mb-2 flex-wrap">
                <span class="badge bg-slate-800 border border-slate-700 text-slate-300" style="font-size: 10.5px;" :title="resolveCustomerDisplay(req)">
                  <i class="bi bi-building me-1"></i>{{ resolveCustomerDisplay(req) }}
                </span>
                <span class="badge bg-slate-800 border border-slate-700 text-slate-300" style="font-size: 10.5px;" :title="resolveProductDisplay(req)">
                  <i class="bi bi-box me-1"></i>{{ resolveProductDisplay(req) }}
                </span>
                <span class="badge bg-slate-800 border border-slate-700 text-slate-300" style="font-size: 10.5px;" :title="resolveAssigneeDisplay(req)">
                  <i class="bi bi-person me-1"></i>{{ resolveAssigneeDisplay(req) }}
                </span>
              </div>

              <div class="mt-auto pt-2 border-t border-slate-800">
                <div
                  v-if="(req.totalSubTasksCount ?? 0) > 0"
                  class="mb-1.5"
                  data-testid="my-request-progress"
                >
                  <div class="d-flex justify-content-between align-items-center fs-11 mb-1">
                    <span class="text-slate-400">Subtasks ({{ req.completedSubTasksCount ?? 0 }}/{{ req.totalSubTasksCount ?? 0 }})</span>
                    <span class="fw-semibold text-slate-200">{{ req.completionPercentage ?? 0 }}%</span>
                  </div>
                  <div class="progress bg-slate-800" style="height: 5px;">
                    <div
                      class="progress-bar bg-cyan-500"
                      role="progressbar"
                      :style="{ width: `${req.completionPercentage ?? 0}%` }"
                    ></div>
                  </div>
                </div>

                <div class="d-flex align-items-center justify-content-between fs-11 text-slate-400 mt-1">
                  <span><i class="bi bi-clock me-1"></i>{{ formatTimestamp(req.createdAt) }}</span>
                  <router-link
                    :to="`/requests/${req.id}`"
                    class="btn btn-outline-cyan btn-sm py-0 px-1.5 fs-11"
                    data-testid="my-request-detail-button"
                    @click.stop
                  >
                    View
                  </router-link>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Tab 2: Assigned Sub-Tasks Personal Queue Card (CR-006, TD-009, TD-010) -->
    <div v-if="activeTab === 'subtasks'" class="card border border-slate-800 bg-slate-900/90 text-slate-100 shadow-md" data-testid="assigned-subtasks-card">
      <div class="card-header py-1.5 px-3 bg-slate-950/60 border-b border-slate-800 d-flex align-items-center justify-content-between flex-wrap gap-2">
        <div class="d-flex align-items-center gap-2 flex-wrap">
          <span class="fw-semibold small">
            <i class="bi bi-list-task me-1 text-cyan-400" aria-hidden="true"></i>
            Actionable Sub-Tasks Queue
          </span>
          <span class="badge text-bg-secondary" data-testid="assigned-subtasks-total-badge">
            {{ filteredAssignedSubTasks.length }} item(s)
          </span>

          <span class="badge bg-slate-800 border border-slate-700 text-slate-300 font-monospace" style="font-size: 10.5px;">
            <span class="text-emerald-400 fw-semibold">{{ completedSubTasksCount }}</span>/{{ assignedSubTasks.length }} done ({{ subTasksCompletionRate }}%)
          </span>
        </div>

        <div class="d-flex align-items-center gap-2">
          <!-- Subtasks Search Input -->
          <div class="input-group input-group-sm" style="width: 200px;">
            <span class="input-group-text bg-slate-800 border-slate-700 border-end-0 text-slate-400 py-0 px-2">
              <i class="bi bi-search" style="font-size: 11px;"></i>
            </span>
            <input
              v-model="subTaskSearchQuery"
              type="text"
              class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 border-start-0 ps-0 focus:border-cyan-400"
              placeholder="Filter tasks..."
              style="font-size: 11.5px;"
              data-testid="subtask-search-input"
            />
            <button
              v-if="subTaskSearchQuery"
              type="button"
              class="btn btn-outline-secondary border-start-0 bg-slate-800 border-slate-700 text-slate-400 py-0 px-1.5"
              @click="subTaskSearchQuery = ''"
            >
              <i class="bi bi-x-circle-fill" style="font-size: 10px;"></i>
            </button>
          </div>

          <div class="form-check form-switch m-0 small">
            <input
              id="showCompletedSubTasksCheck"
              v-model="showCompletedSubTasks"
              type="checkbox"
              class="form-check-input bg-slate-900 border-slate-700"
              role="switch"
              data-testid="show-completed-subtasks-toggle"
            />
            <label for="showCompletedSubTasksCheck" class="form-check-label text-slate-400" style="font-size: 11.5px;">
              Show completed
            </label>
          </div>
        </div>
      </div>

      <div class="table-responsive">
        <table class="table table-hover align-middle mb-0" data-testid="assigned-subtasks-table">
          <thead class="table-dark">
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
                  <span>{{ subTaskSearchQuery ? 'No assigned sub-tasks match your search criteria.' : `You have no ${showCompletedSubTasks ? '' : 'pending'} assigned sub-tasks.` }}</span>
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
