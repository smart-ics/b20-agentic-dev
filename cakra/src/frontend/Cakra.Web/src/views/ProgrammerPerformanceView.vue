<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'

import { httpClient } from '@/api/http'

/**
 * SCR-MGT-002: Programmer Request Performance Screen
 * (Architecture §7, §8 — UC-MGT-003, §9 — FEAT-MGT-003, §13, §19.4, §19.6, §20, §21).
 *
 * - Populates Person selector dropdown from `GET /api/v1/organization/persons/active`.
 * - Provides month range inputs (`startMonth` and `endMonth` in `YYYY-MM` format) and filter button.
 * - Fetches historical programmer performance snapshot metrics from
 *   `GET /api/v1/analytics/programmer-performance` with query parameters (`personId`, `startMonth`, `endMonth`)
 *   via `httpClient`.
 * - Displays summary KPIs, a Bootstrap 5 table for monthly performance series (`monthlyPerformance` /
 *   `monthlySeries`: rows = months, columns = metric values), a Bootstrap 5 table for underlying
 *   `dailyWorkloadSnapshots`, and a Bootstrap 5 table for `monthlyCustomerSnapshots` when present.
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

export interface ProgrammerMonthlyPerformanceItemDto {
  yearMonth: string
  personId: string
  personName?: string | null
  completedRequestsCount: number
  rejectedRequestsCount: number
  maxActiveRequestsCount: number
  pausedRequestsCount?: number
  escalatedRequestsCount?: number
  avgResolutionHours: number
}

export interface DailyWorkloadSnapshotDto {
  snapshotId: string
  id: string
  snapshotDate: string
  personId: string
  personName?: string | null
  activeRequestsCount: number
  pausedRequestsCount?: number
  escalatedRequestsCount?: number
  stalledRequestsCount: number
  completedRequestsToday: number
  avgAgeHours: number
  capturedAt: string
}

export interface MonthlyCustomerPerformanceSnapshotDto {
  snapshotId: string
  id: string
  yearMonth: string
  customerId: string
  customerName?: string | null
  totalRequests: number
  resolvedRequestsCount: number
  rejectedRequestsCount: number
  avgResolutionHours: number
  slaMetCount: number
  slaBreachedCount: number
  capturedAt: string
}

export interface ProgrammerPerformanceReportDto {
  personId?: string | null
  personName?: string | null
  startMonth?: string | null
  endMonth?: string | null
  totalCompletedRequests: number
  totalRejectedRequests: number
  avgResolutionHours: number
  monthlySeries: ProgrammerMonthlyPerformanceItemDto[]
  monthlyPerformance?: ProgrammerMonthlyPerformanceItemDto[]
  dailyWorkloadSnapshots: DailyWorkloadSnapshotDto[]
  monthlyCustomerSnapshots: MonthlyCustomerPerformanceSnapshotDto[]
}

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const route = useRoute()

const activePersons = ref<ActivePersonOption[]>([])
const report = ref<ProgrammerPerformanceReportDto | null>(null)
const isLoadingPersons = ref(false)
const isLoadingReport = ref(false)
const errorMessage = ref<string | null>(null)

const filters = reactive({
  personId: '',
  startMonth: '',
  endMonth: '',
})

const monthlyPerformance = computed<ProgrammerMonthlyPerformanceItemDto[]>(
  () => report.value?.monthlySeries ?? report.value?.monthlyPerformance ?? [],
)

const dailyWorkloadSnapshots = computed<DailyWorkloadSnapshotDto[]>(
  () => report.value?.dailyWorkloadSnapshots ?? [],
)

const monthlyCustomerSnapshots = computed<MonthlyCustomerPerformanceSnapshotDto[]>(
  () => report.value?.monthlyCustomerSnapshots ?? [],
)

function extractErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof AxiosError) {
    if (err.response?.status === 403) {
      return 'Access denied: Management role is required to view programmer performance analytics.'
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

function formatDecimal(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(Number(value))) {
    return '0.00'
  }
  return Number(value).toFixed(2)
}

function formatDate(iso: string | null | undefined): string {
  if (!iso) {
    return '—'
  }
  const parsed = new Date(iso)
  if (Number.isNaN(parsed.getTime())) {
    return iso
  }
  return parsed.toISOString().slice(0, 10)
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

async function loadPerformanceReport(): Promise<void> {
  isLoadingReport.value = true
  errorMessage.value = null

  try {
    const params: Record<string, string> = {}
    if (filters.personId.trim()) {
      params.personId = filters.personId.trim()
    }
    if (filters.startMonth.trim()) {
      params.startMonth = filters.startMonth.trim()
    }
    if (filters.endMonth.trim()) {
      params.endMonth = filters.endMonth.trim()
    }

    const response = await httpClient.get<ProgrammerPerformanceReportDto>(
      '/analytics/programmer-performance',
      { params },
    )
    report.value = response.data
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(
      err,
      'Failed to load programmer performance analytics.',
    )
    report.value = null
  } finally {
    isLoadingReport.value = false
  }
}

async function handleResetFilters(): Promise<void> {
  filters.personId = ''
  filters.startMonth = ''
  filters.endMonth = ''
  await loadPerformanceReport()
}

onMounted(async () => {
  if (typeof route.query.personId === 'string') {
    filters.personId = route.query.personId.trim()
  }
  if (typeof route.query.startMonth === 'string') {
    filters.startMonth = route.query.startMonth.trim()
  }
  if (typeof route.query.endMonth === 'string') {
    filters.endMonth = route.query.endMonth.trim()
  }

  await Promise.all([loadActivePersons(), loadPerformanceReport()])
})
</script>

<template>
  <section data-screen-id="SCR-MGT-002" class="programmer-performance-view">
    <!-- Compact Screen Header -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2">
        <h1 class="op-screen-title">
          <i class="bi bi-graph-up text-primary" aria-hidden="true"></i>
          Programmer Performance Analytics
        </h1>
        <span class="badge text-bg-light border text-secondary font-monospace">SCR-MGT-002</span>
      </div>

      <div class="d-flex align-items-center gap-2">
        <router-link to="/analytics/customer-portfolio" class="btn btn-outline-secondary btn-sm">
          <i class="bi bi-building me-1" aria-hidden="true"></i>
          Customer Portfolio
        </router-link>
        <router-link to="/analytics/programmer-workload" class="btn btn-outline-secondary btn-sm">
          <i class="bi bi-people me-1" aria-hidden="true"></i>
          Programmer Workload
        </router-link>
      </div>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center gap-2 py-1 px-2 mb-2"
      role="alert"
      data-testid="performance-error-alert"
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

    <!-- Compact Filter Toolbar -->
    <div class="op-toolbar">
      <form class="d-flex flex-wrap align-items-center gap-2 w-100" @submit.prevent="loadPerformanceReport">
        <div class="d-flex align-items-center gap-1">
          <label for="performancePersonSelect" class="form-label mb-0 fs-11 text-nowrap">Programmer:</label>
          <select
            id="performancePersonSelect"
            v-model="filters.personId"
            class="form-select form-select-sm"
            style="min-width: 170px; max-width: 250px;"
            :disabled="isLoadingPersons || isLoadingReport"
            data-testid="person-selector"
          >
            <option value="">All Programmers / Persons</option>
            <option
              v-for="person in activePersons"
              :key="resolvePersonId(person)"
              :value="resolvePersonId(person)"
            >
              {{ resolvePersonLabel(person) }}
            </option>
          </select>
        </div>

        <div class="d-flex align-items-center gap-1">
          <label for="startMonthInput" class="form-label mb-0 fs-11 text-nowrap">Start:</label>
          <input
            id="startMonthInput"
            v-model="filters.startMonth"
            type="month"
            class="form-control form-control-sm"
            style="width: 130px;"
            placeholder="YYYY-MM"
            :disabled="isLoadingReport"
            data-testid="start-month-input"
          />
        </div>

        <div class="d-flex align-items-center gap-1">
          <label for="endMonthInput" class="form-label mb-0 fs-11 text-nowrap">End:</label>
          <input
            id="endMonthInput"
            v-model="filters.endMonth"
            type="month"
            class="form-control form-control-sm"
            style="width: 130px;"
            placeholder="YYYY-MM"
            :disabled="isLoadingReport"
            data-testid="end-month-input"
          />
        </div>

        <div class="d-flex align-items-center gap-1 ms-auto">
          <button
            type="submit"
            class="btn btn-primary btn-sm"
            :disabled="isLoadingReport"
            data-testid="load-performance-button"
          >
            <span
              v-if="isLoadingReport"
              class="spinner-border spinner-border-sm me-1"
              role="status"
              aria-hidden="true"
            ></span>
            <i v-else class="bi bi-funnel me-1" aria-hidden="true"></i>
            Filter
          </button>
          <button
            type="button"
            class="btn btn-outline-secondary btn-sm"
            :disabled="isLoadingReport"
            data-testid="reset-performance-button"
            @click="handleResetFilters"
          >
            Reset
          </button>
        </div>
      </form>
    </div>

    <!-- Loading Indicator -->
    <div v-if="isLoadingReport" class="text-center py-4" data-testid="performance-loading">
      <div class="spinner-border spinner-border-sm text-primary" role="status">
        <span class="visually-hidden">Loading programmer performance report...</span>
      </div>
      <p class="text-body-secondary small mt-1 mb-0">Loading historical performance snapshots...</p>
    </div>

    <template v-else-if="report">
      <!-- Compact Operational Metric Ribbon -->
      <div class="op-metric-ribbon">
        <div class="op-stat-item">
          <span class="op-stat-label text-success">Completed:</span>
          <span
            class="op-stat-val text-success"
            data-testid="kpi-total-completed"
          >
            {{ report.totalCompletedRequests }}
          </span>
          <span class="text-muted fs-11 ms-1">({{ report.personName || 'All Programmers' }})</span>
        </div>

        <div class="op-stat-item">
          <span class="op-stat-label">Rejected:</span>
          <span class="op-stat-val text-secondary" data-testid="kpi-total-rejected">
            {{ report.totalRejectedRequests }}
          </span>
        </div>

        <div class="op-stat-item">
          <span class="op-stat-label">Avg Turnaround:</span>
          <span
            class="op-stat-val text-primary"
            data-testid="kpi-avg-resolution-hours"
          >
            {{ formatDecimal(report.avgResolutionHours) }} <span class="fs-11 fw-normal text-muted">hrs</span>
          </span>
        </div>

        <div class="op-stat-item">
          <span class="op-stat-label">Daily Snapshots:</span>
          <span class="op-stat-val" data-testid="kpi-daily-snapshots-count">
            {{ dailyWorkloadSnapshots.length }}
          </span>
          <span class="text-muted fs-11 ms-1">({{ monthlyPerformance.length }} monthly series)</span>
        </div>
      </div>

      <!-- Monthly Performance Series Table -->
      <div class="card border mb-2">
        <div class="card-header py-1 px-3 d-flex justify-content-between align-items-center">
          <span class="fw-semibold">
            <i class="bi bi-calendar3 me-1 text-primary" aria-hidden="true"></i>
            Monthly Performance Summary
          </span>
          <span class="badge text-bg-primary">{{ monthlyPerformance.length }} rows</span>
        </div>
        <div class="card-body p-0">
          <div
            v-if="monthlyPerformance.length === 0"
            class="p-3 text-center text-body-secondary small"
          >
            No monthly performance records found for the selected filter criteria.
          </div>
          <div v-else class="table-responsive">
            <table
              class="table table-hover align-middle mb-0"
              data-testid="monthly-performance-table"
            >
              <thead>
                <tr>
                  <th scope="col" style="width: 120px;">Month</th>
                  <th scope="col">Programmer</th>
                  <th scope="col" class="text-end" style="width: 110px;">Completed</th>
                  <th scope="col" class="text-end" style="width: 100px;">Rejected</th>
                  <th scope="col" class="text-end" style="width: 120px;">Peak Active</th>
                  <th scope="col" class="text-end" style="width: 110px;">Paused</th>
                  <th scope="col" class="text-end" style="width: 130px;">Avg Turnaround</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="item in monthlyPerformance"
                  :key="`${item.yearMonth}-${item.personId}`"
                >
                  <td>
                    <span class="badge text-bg-light border font-monospace">
                      {{ item.yearMonth }}
                    </span>
                  </td>
                  <td class="fw-semibold">{{ item.personName || item.personId }}</td>
                  <td class="text-end">
                    <span class="badge text-bg-success">{{ item.completedRequestsCount }}</span>
                  </td>
                  <td class="text-end">
                    <span class="badge text-bg-secondary">{{ item.rejectedRequestsCount }}</span>
                  </td>
                  <td class="text-end">{{ item.maxActiveRequestsCount }}</td>
                  <td class="text-end">
                    <span
                      class="badge"
                      :class="
                        (item.pausedRequestsCount ?? item.escalatedRequestsCount ?? 0) > 0
                          ? 'text-bg-warning text-dark'
                          : 'text-bg-light border'
                      "
                    >
                      {{ item.pausedRequestsCount ?? item.escalatedRequestsCount ?? 0 }}
                    </span>
                  </td>
                  <td class="text-end font-monospace fs-11">
                    {{ formatDecimal(item.avgResolutionHours) }} hrs
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Daily Workload Snapshots Table -->
      <div class="card border mb-2">
        <div class="card-header py-1 px-3 d-flex justify-content-between align-items-center">
          <span class="fw-semibold">
            <i class="bi bi-clock-history me-1 text-secondary" aria-hidden="true"></i>
            Daily Workload Snapshots
          </span>
          <span class="badge text-bg-secondary">{{ dailyWorkloadSnapshots.length }} snapshots</span>
        </div>
        <div class="card-body p-0">
          <div
            v-if="dailyWorkloadSnapshots.length === 0"
            class="p-3 text-center text-body-secondary small"
          >
            No daily workload snapshots recorded for the selected filter criteria.
          </div>
          <div v-else class="table-responsive">
            <table
              class="table table-hover align-middle mb-0"
              data-testid="daily-snapshots-table"
            >
              <thead>
                <tr>
                  <th scope="col" style="width: 105px;">Date</th>
                  <th scope="col">Programmer</th>
                  <th scope="col" class="text-end" style="width: 110px;">Active</th>
                  <th scope="col" class="text-end" style="width: 100px;">Paused</th>
                  <th scope="col" class="text-end" style="width: 110px;">Stalled (72h+)</th>
                  <th scope="col" class="text-end" style="width: 120px;">Completed Today</th>
                  <th scope="col" class="text-end" style="width: 110px;">Avg Age</th>
                  <th scope="col" style="width: 130px;">Captured At</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="snap in dailyWorkloadSnapshots"
                  :key="snap.snapshotId || snap.id"
                >
                  <td class="font-monospace fs-11">{{ formatDate(snap.snapshotDate) }}</td>
                  <td class="fw-semibold">{{ snap.personName || snap.personId }}</td>
                  <td class="text-end">{{ snap.activeRequestsCount }}</td>
                  <td class="text-end">
                    <span
                      class="badge"
                      :class="
                        (snap.pausedRequestsCount ?? snap.escalatedRequestsCount ?? 0) > 0
                          ? 'text-bg-warning text-dark'
                          : 'text-bg-light border'
                      "
                    >
                      {{ snap.pausedRequestsCount ?? snap.escalatedRequestsCount ?? 0 }}
                    </span>
                  </td>
                  <td class="text-end">
                    <span
                      class="badge"
                      :class="
                        snap.stalledRequestsCount > 0 ? 'text-bg-warning' : 'text-bg-light border'
                      "
                    >
                      {{ snap.stalledRequestsCount }}
                    </span>
                  </td>
                  <td class="text-end">
                    <span class="badge text-bg-success">{{ snap.completedRequestsToday }}</span>
                  </td>
                  <td class="text-end font-monospace fs-11">{{ formatDecimal(snap.avgAgeHours) }} hrs</td>
                  <td class="text-body-secondary fs-11">{{ formatDateTime(snap.capturedAt) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Monthly Customer Performance Snapshots Table (if present) -->
      <div v-if="monthlyCustomerSnapshots.length > 0" class="card border">
        <div class="card-header py-1 px-3 d-flex justify-content-between align-items-center">
          <span class="fw-semibold">
            <i class="bi bi-building-check me-1 text-info" aria-hidden="true"></i>
            Monthly Customer Performance Snapshots
          </span>
          <span class="badge text-bg-info">{{ monthlyCustomerSnapshots.length }} snapshots</span>
        </div>
        <div class="card-body p-0">
          <div class="table-responsive">
            <table
              class="table table-hover align-middle mb-0"
              data-testid="monthly-customer-snapshots-table"
            >
              <thead>
                <tr>
                  <th scope="col" style="width: 90px;">Month</th>
                  <th scope="col">Customer</th>
                  <th scope="col" class="text-end" style="width: 110px;">Total</th>
                  <th scope="col" class="text-end" style="width: 90px;">Resolved</th>
                  <th scope="col" class="text-end" style="width: 90px;">Rejected</th>
                  <th scope="col" class="text-end" style="width: 120px;">Avg Turnaround</th>
                  <th scope="col" class="text-end" style="width: 90px;">SLA Met</th>
                  <th scope="col" class="text-end" style="width: 100px;">SLA Breached</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="custSnap in monthlyCustomerSnapshots"
                  :key="custSnap.snapshotId || custSnap.id"
                >
                  <td class="font-monospace fs-11">{{ custSnap.yearMonth }}</td>
                  <td class="fw-semibold">{{ custSnap.customerName || custSnap.customerId }}</td>
                  <td class="text-end">{{ custSnap.totalRequests }}</td>
                  <td class="text-end">{{ custSnap.resolvedRequestsCount }}</td>
                  <td class="text-end">{{ custSnap.rejectedRequestsCount }}</td>
                  <td class="text-end font-monospace fs-11">
                    {{ formatDecimal(custSnap.avgResolutionHours) }} hrs
                  </td>
                  <td class="text-end">
                    <span class="badge text-bg-success">{{ custSnap.slaMetCount }}</span>
                  </td>
                  <td class="text-end">
                    <span
                      class="badge"
                      :class="
                        custSnap.slaBreachedCount > 0 ? 'text-bg-danger' : 'text-bg-light border'
                      "
                    >
                      {{ custSnap.slaBreachedCount }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </template>
  </section>
</template>
