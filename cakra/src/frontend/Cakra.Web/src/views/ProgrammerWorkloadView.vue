<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'

import { httpClient } from '@/api/http'

/**
 * SCR-MGT-003: Programmer Workload Review Screen
 * (Architecture §7, §8 — UC-MGT-004, §9 — FEAT-MGT-004, §13, §19.4, §19.6, §20, §21).
 *
 * - Populates optional Person filter dropdown from `GET /api/v1/organization/persons/active`.
 * - Fetches real-time programmer active workload from
 *   `GET /api/v1/analytics/programmer-workload` (with optional `personId` query parameter)
 *   via `httpClient`.
 * - Displays Bootstrap 5 summary cards and workload breakdown table per person
 *   (`CapturedCount`, `EvaluatingCount`, `AcceptedCount`, `InProgressCount`, `EscalatedCount`,
 *   `TotalActiveCount`, `StalledRequestsCount`, `IsOverloaded`), plus an expandable/selectable
 *   active request queue for drill-down and management reassignment navigation.
 */

export interface ActivePersonOption {
  id: string
  personId?: string
  firstName: string
  lastName: string
  fullName: string
  email: string
  status: string
}

export interface CustomerPortfolioRequestItemDto {
  requestId: string
  id: string
  title: string
  description: string
  requestType: string
  status: string
  priority: string
  ownerPersonId: string | null
  assigneePersonId?: string | null
  ownerName: string | null
  assigneeName?: string | null
  customerId: string | null
  customerName: string | null
  customerCode: string | null
  productId: string | null
  workPackageId: string | null
  escalationReason: string | null
  resolutionOutcome: string | null
  resolutionSummary: string | null
  resolvedBy: string | null
  resolvedAt: string | null
  createdAt: string
  updatedAt: string | null
  lastUpdatedAt: string
  isBlocked: boolean
  isActive: boolean
}

export interface ProgrammerActiveWorkloadDto {
  personId: string
  ownerPersonId: string
  personName: string
  fullName: string
  email?: string | null
  capturedCount: number
  evaluatingCount: number
  acceptedCount: number
  inProgressCount: number
  escalatedCount: number
  totalActiveCount: number
  activeRequestsCount: number
  stalledRequestsCount: number
  isOverloaded: boolean
  subStateCounts: Record<string, number>
  activeRequests: CustomerPortfolioRequestItemDto[]
}

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const route = useRoute()

const activePersons = ref<ActivePersonOption[]>([])
const selectedPersonId = ref<string>('')
const workloads = ref<ProgrammerActiveWorkloadDto[]>([])
const expandedPersonId = ref<string | null>(null)

const isLoadingPersons = ref(false)
const isLoadingWorkloads = ref(false)
const errorMessage = ref<string | null>(null)

const totalActiveRequestsAcrossTeam = computed<number>(() =>
  workloads.value.reduce((acc, w) => acc + (w.totalActiveCount ?? 0), 0),
)

const totalEscalatedAcrossTeam = computed<number>(() =>
  workloads.value.reduce((acc, w) => acc + (w.escalatedCount ?? 0), 0),
)

const overloadedProgrammersCount = computed<number>(
  () => workloads.value.filter((w) => w.isOverloaded).length,
)

const selectedPersonWorkload = computed<ProgrammerActiveWorkloadDto | null>(() => {
  if (!expandedPersonId.value) {
    return null
  }
  return workloads.value.find((w) => w.personId === expandedPersonId.value) ?? null
})

function extractErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof AxiosError) {
    if (err.response?.status === 403) {
      return 'Access denied: Management role is required to view programmer active workload analytics.'
    }
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

function resolvePersonId(person: ActivePersonOption): string {
  return person.id || person.personId || ''
}

function resolvePersonLabel(person: ActivePersonOption): string {
  const name = person.fullName || `${person.firstName} ${person.lastName}`.trim()
  return person.email ? `${name} (${person.email})` : name
}

function resolveRequestId(item: CustomerPortfolioRequestItemDto): string {
  return item.requestId || item.id
}

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) {
    return '—'
  }
  const parsed = new Date(iso)
  if (Number.isNaN(parsed.getTime())) {
    return iso
  }
  return parsed.toLocaleString()
}

function statusBadgeClass(status: string): string {
  switch (status?.toUpperCase()) {
    case 'CAPTURED':
      return 'text-bg-secondary'
    case 'EVALUATING':
      return 'text-bg-info'
    case 'ACCEPTED':
      return 'text-bg-primary'
    case 'IN_PROGRESS':
      return 'text-bg-primary'
    case 'ESCALATED':
      return 'text-bg-danger'
    case 'COMPLETED':
      return 'text-bg-success'
    case 'REJECTED':
      return 'text-bg-dark'
    default:
      return 'text-bg-secondary'
  }
}

function priorityBadgeClass(priority: string): string {
  switch (priority?.toUpperCase()) {
    case 'URGENT':
      return 'text-bg-danger'
    case 'HIGH':
      return 'text-bg-warning'
    case 'NORMAL':
      return 'text-bg-info'
    case 'LOW':
      return 'text-bg-secondary'
    default:
      return 'text-bg-secondary'
  }
}

function toggleQueueExpansion(personId: string): void {
  expandedPersonId.value = expandedPersonId.value === personId ? null : personId
}

async function loadActivePersons(): Promise<void> {
  isLoadingPersons.value = true
  try {
    const response = await httpClient.get<ActivePersonOption[]>('/organization/persons/active')
    activePersons.value = response.data ?? []
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load active persons.')
  } finally {
    isLoadingPersons.value = false
  }
}

async function loadWorkloads(): Promise<void> {
  isLoadingWorkloads.value = true
  errorMessage.value = null

  try {
    const params: Record<string, string> = {}
    if (selectedPersonId.value.trim()) {
      params.personId = selectedPersonId.value.trim()
    }

    const response = await httpClient.get<ProgrammerActiveWorkloadDto[]>(
      '/analytics/programmer-workload',
      { params },
    )
    workloads.value = response.data ?? []

    if (workloads.value.length === 1 && workloads.value[0]) {
      expandedPersonId.value = workloads.value[0].personId
    } else if (
      expandedPersonId.value &&
      !workloads.value.some((w) => w.personId === expandedPersonId.value)
    ) {
      expandedPersonId.value = workloads.value[0]?.personId ?? null
    }
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(
      err,
      'Failed to load programmer active workload analytics.',
    )
    workloads.value = []
  } finally {
    isLoadingWorkloads.value = false
  }
}

async function handleFilterChange(): Promise<void> {
  await loadWorkloads()
}

async function handleClearFilter(): Promise<void> {
  selectedPersonId.value = ''
  await loadWorkloads()
}

onMounted(async () => {
  if (typeof route.query.personId === 'string') {
    selectedPersonId.value = route.query.personId.trim()
  }

  await Promise.all([loadActivePersons(), loadWorkloads()])
})
</script>

<template>
  <section data-screen-id="SCR-MGT-003" class="programmer-workload-view">
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-3 mb-4">
      <div>
        <h1 class="h3 mb-1">Programmer Workload Review</h1>
        <p class="text-body-secondary mb-0">
          Real-time active request distribution across programmers by lifecycle sub-state and overload indicators (SCR-MGT-003).
        </p>
      </div>
      <div class="d-flex align-items-center gap-2">
        <router-link to="/analytics/customer-portfolio" class="btn btn-outline-secondary btn-sm">
          <i class="bi bi-building me-1" aria-hidden="true"></i>
          Customer Portfolio
        </router-link>
        <router-link to="/analytics/programmer-performance" class="btn btn-outline-secondary btn-sm">
          <i class="bi bi-graph-up me-1" aria-hidden="true"></i>
          Programmer Performance
        </router-link>
      </div>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      class="alert alert-danger alert-dismissible fade show"
      role="alert"
      data-testid="workload-error-alert"
    >
      <i class="bi bi-exclamation-triangle-fill me-2" aria-hidden="true"></i>
      {{ errorMessage }}
      <button
        type="button"
        class="btn-close"
        aria-label="Close"
        @click="errorMessage = null"
      ></button>
    </div>

    <!-- Optional Person Filter Card -->
    <div class="card shadow-sm mb-4">
      <div class="card-body">
        <form class="row g-3 align-items-end" @submit.prevent="loadWorkloads">
          <div class="col-12 col-md-7 col-lg-6">
            <label for="workloadPersonSelect" class="form-label fw-semibold">
              Filter by Programmer / Person (Optional)
            </label>
            <select
              id="workloadPersonSelect"
              v-model="selectedPersonId"
              class="form-select"
              :disabled="isLoadingPersons || isLoadingWorkloads"
              data-testid="workload-person-selector"
              @change="handleFilterChange"
            >
              <option value="">All Active Programmers / Persons</option>
              <option
                v-for="person in activePersons"
                :key="resolvePersonId(person)"
                :value="resolvePersonId(person)"
              >
                {{ resolvePersonLabel(person) }}
              </option>
            </select>
          </div>
          <div class="col-12 col-md-5 col-lg-4 d-flex gap-2">
            <button
              type="submit"
              class="btn btn-primary"
              :disabled="isLoadingWorkloads"
              data-testid="load-workload-button"
            >
              <span
                v-if="isLoadingWorkloads"
                class="spinner-border spinner-border-sm me-1"
                role="status"
                aria-hidden="true"
              ></span>
              <i v-else class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>
              Refresh Workload
            </button>
            <button
              v-if="selectedPersonId"
              type="button"
              class="btn btn-outline-secondary"
              :disabled="isLoadingWorkloads"
              data-testid="clear-workload-filter-button"
              @click="handleClearFilter"
            >
              Show All
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Summary Metric Cards -->
    <div class="row g-3 mb-4">
      <div class="col-12 col-sm-6 col-lg-3">
        <div class="card shadow-sm h-100">
          <div class="card-body">
            <div class="text-body-secondary small text-uppercase fw-semibold">
              Programmers Listed
            </div>
            <div class="display-6 fw-bold mt-1" data-testid="summary-programmers-count">
              {{ workloads.length }}
            </div>
            <div class="small text-body-secondary mt-1">
              Active organizational persons evaluated
            </div>
          </div>
        </div>
      </div>

      <div class="col-12 col-sm-6 col-lg-3">
        <div class="card shadow-sm h-100 border-primary">
          <div class="card-body">
            <div class="text-body-secondary small text-uppercase fw-semibold">
              Total Active Requests
            </div>
            <div
              class="display-6 fw-bold text-primary mt-1"
              data-testid="summary-total-active-requests"
            >
              {{ totalActiveRequestsAcrossTeam }}
            </div>
            <div class="small text-body-secondary mt-1">
              Open requests across displayed programmers
            </div>
          </div>
        </div>
      </div>

      <div class="col-12 col-sm-6 col-lg-3">
        <div class="card shadow-sm h-100 border-danger">
          <div class="card-body">
            <div class="text-body-secondary small text-uppercase fw-semibold">
              Escalated Blockers
            </div>
            <div
              class="display-6 fw-bold text-danger mt-1"
              data-testid="summary-total-escalated"
            >
              {{ totalEscalatedAcrossTeam }}
            </div>
            <div class="small text-body-secondary mt-1">
              Requests currently in ESCALATED status
            </div>
          </div>
        </div>
      </div>

      <div class="col-12 col-sm-6 col-lg-3">
        <div class="card shadow-sm h-100 border-warning">
          <div class="card-body">
            <div class="text-body-secondary small text-uppercase fw-semibold">
              Overloaded Programmers
            </div>
            <div
              class="display-6 fw-bold text-warning-emphasis mt-1"
              data-testid="summary-overloaded-count"
            >
              {{ overloadedProgrammersCount }}
            </div>
            <div class="small text-body-secondary mt-1">
              5+ active requests or 1+ escalated blocker
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Loading Indicator -->
    <div v-if="isLoadingWorkloads" class="text-center py-5" data-testid="workload-loading">
      <div class="spinner-border text-primary" role="status">
        <span class="visually-hidden">Loading programmer active workload...</span>
      </div>
      <p class="text-body-secondary mt-2 mb-0">Loading real-time programmer workload...</p>
    </div>

    <!-- Programmer Workload Breakdown Table -->
    <div v-else class="card shadow-sm mb-4">
      <div class="card-header d-flex justify-content-between align-items-center">
        <h2 class="h5 mb-0">
          <i class="bi bi-bar-chart-steps me-2 text-primary" aria-hidden="true"></i>
          Active Workload by Programmer &amp; Lifecycle Sub-State
        </h2>
        <span class="badge text-bg-primary">{{ workloads.length }} programmers</span>
      </div>
      <div class="card-body p-0">
        <div v-if="workloads.length === 0" class="p-4 text-center text-body-secondary">
          No programmer workload records found.
        </div>
        <div v-else class="table-responsive">
          <table
            class="table table-hover align-middle mb-0"
            data-testid="programmer-workload-table"
          >
            <thead class="table-light">
              <tr>
                <th scope="col">Programmer</th>
                <th scope="col" class="text-center">CAPTURED</th>
                <th scope="col" class="text-center">EVALUATING</th>
                <th scope="col" class="text-center">ACCEPTED</th>
                <th scope="col" class="text-center">IN_PROGRESS</th>
                <th scope="col" class="text-center">ESCALATED</th>
                <th scope="col" class="text-center">Total Active</th>
                <th scope="col" class="text-center">Stalled (72h+)</th>
                <th scope="col" class="text-center">Capacity Status</th>
                <th scope="col" class="text-end">Active Queue</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="item in workloads"
                :key="item.personId"
                :class="{ 'table-active': expandedPersonId === item.personId }"
              >
                <td>
                  <div class="fw-semibold">{{ item.personName || item.fullName }}</div>
                  <div v-if="item.email" class="small text-body-secondary">{{ item.email }}</div>
                </td>
                <td class="text-center">
                  <span class="badge text-bg-secondary">{{ item.capturedCount }}</span>
                </td>
                <td class="text-center">
                  <span class="badge text-bg-info">{{ item.evaluatingCount }}</span>
                </td>
                <td class="text-center">
                  <span class="badge text-bg-primary">{{ item.acceptedCount }}</span>
                </td>
                <td class="text-center">
                  <span class="badge text-bg-primary">{{ item.inProgressCount }}</span>
                </td>
                <td class="text-center">
                  <span
                    class="badge"
                    :class="item.escalatedCount > 0 ? 'text-bg-danger' : 'text-bg-light border'"
                  >
                    {{ item.escalatedCount }}
                  </span>
                </td>
                <td class="text-center">
                  <span class="fw-bold fs-6">{{ item.totalActiveCount }}</span>
                </td>
                <td class="text-center">
                  <span
                    class="badge"
                    :class="
                      item.stalledRequestsCount > 0 ? 'text-bg-warning' : 'text-bg-light border'
                    "
                  >
                    {{ item.stalledRequestsCount }}
                  </span>
                </td>
                <td class="text-center">
                  <span
                    class="badge"
                    :class="item.isOverloaded ? 'text-bg-danger' : 'text-bg-success'"
                    data-testid="workload-overload-badge"
                  >
                    {{ item.isOverloaded ? 'OVERLOADED' : 'BALANCED' }}
                  </span>
                </td>
                <td class="text-end">
                  <button
                    type="button"
                    class="btn btn-sm"
                    :class="
                      expandedPersonId === item.personId
                        ? 'btn-primary'
                        : 'btn-outline-primary'
                    "
                    :data-testid="`inspect-queue-${item.personId}`"
                    @click="toggleQueueExpansion(item.personId)"
                  >
                    <i
                      class="bi me-1"
                      :class="
                        expandedPersonId === item.personId ? 'bi-chevron-up' : 'bi-chevron-down'
                      "
                      aria-hidden="true"
                    ></i>
                    Queue ({{ item.activeRequests?.length ?? 0 }})
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <!-- Selected / Expanded Programmer Active Request Queue Card -->
    <div
      v-if="selectedPersonWorkload"
      class="card shadow-sm mb-4"
      data-testid="expanded-programmer-queue-card"
    >
      <div class="card-header d-flex flex-wrap justify-content-between align-items-center gap-2">
        <div>
          <h3 class="h5 mb-0">
            <i class="bi bi-person-lines-fill me-2 text-primary" aria-hidden="true"></i>
            Active Request Queue: {{ selectedPersonWorkload.personName }}
          </h3>
        </div>
        <div class="d-flex align-items-center gap-2">
          <span class="badge text-bg-primary">
            {{ selectedPersonWorkload.activeRequests.length }} active requests
          </span>
          <router-link
            :to="`/analytics/programmer-performance?personId=${encodeURIComponent(selectedPersonWorkload.personId)}`"
            class="btn btn-outline-secondary btn-sm"
          >
            <i class="bi bi-graph-up me-1" aria-hidden="true"></i>
            Historical Performance
          </router-link>
        </div>
      </div>
      <div class="card-body p-0">
        <div
          v-if="selectedPersonWorkload.activeRequests.length === 0"
          class="p-4 text-center text-body-secondary"
        >
          {{ selectedPersonWorkload.personName }} currently has zero active requests assigned.
        </div>
        <div v-else class="table-responsive">
          <table
            class="table table-hover align-middle mb-0"
            data-testid="programmer-active-requests-table"
          >
            <thead class="table-light">
              <tr>
                <th scope="col">Request Title</th>
                <th scope="col">Customer</th>
                <th scope="col">Type</th>
                <th scope="col">Priority</th>
                <th scope="col">Status</th>
                <th scope="col">Escalation / Blocker Note</th>
                <th scope="col">Last Updated</th>
                <th scope="col" class="text-end">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="req in selectedPersonWorkload.activeRequests"
                :key="resolveRequestId(req)"
              >
                <td>
                  <router-link
                    :to="`/requests/${resolveRequestId(req)}`"
                    class="fw-semibold text-decoration-none"
                  >
                    {{ req.title }}
                  </router-link>
                </td>
                <td>
                  <span v-if="req.customerName">
                    {{ req.customerName }}
                    <span v-if="req.customerCode" class="text-body-secondary small">
                      ({{ req.customerCode }})
                    </span>
                  </span>
                  <span v-else class="text-body-secondary">—</span>
                </td>
                <td>
                  <span class="badge text-bg-light border">{{ req.requestType }}</span>
                </td>
                <td>
                  <span class="badge" :class="priorityBadgeClass(req.priority)">
                    {{ req.priority }}
                  </span>
                </td>
                <td>
                  <span class="badge" :class="statusBadgeClass(req.status)">
                    {{ req.status }}
                  </span>
                </td>
                <td>
                  <span v-if="req.escalationReason" class="text-danger small">
                    {{ req.escalationReason }}
                  </span>
                  <span v-else class="text-body-secondary">—</span>
                </td>
                <td class="small text-body-secondary">
                  {{ formatDateTime(req.lastUpdatedAt) }}
                </td>
                <td class="text-end">
                  <router-link
                    :to="`/requests/${resolveRequestId(req)}`"
                    class="btn btn-outline-primary btn-sm"
                  >
                    Open / Reassign
                  </router-link>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </section>
</template>
