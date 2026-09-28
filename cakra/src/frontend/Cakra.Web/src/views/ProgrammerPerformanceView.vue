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
  escalatedRequestsCount: number
  avgResolutionHours: number
}

export interface DailyWorkloadSnapshotDto {
  snapshotId: string
  id: string
  snapshotDate: string
  personId: string
  personName?: string | null
  activeRequestsCount: number
  escalatedRequestsCount: number
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
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-3 mb-4">
      <div>
        <h1 class="h3 mb-1">Programmer Performance Analytics</h1>
        <p class="text-body-secondary mb-0">
          Historical monthly throughput, resolution turnaround time, and daily workload snapshots (SCR-MGT-002).
        </p>
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
      class="alert alert-danger alert-dismissible fade show"
      role="alert"
      data-testid="performance-error-alert"
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

    <!-- Filter Form Card -->
    <div class="card shadow-sm mb-4">
      <div class="card-body">
        <form class="row g-3 align-items-end" @submit.prevent="loadPerformanceReport">
          <div class="col-12 col-md-4">
            <label for="performancePersonSelect" class="form-label fw-semibold">
              Programmer / Person
            </label>
            <select
              id="performancePersonSelect"
              v-model="filters.personId"
              class="form-select"
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

          <div class="col-12 col-sm-6 col-md-3">
            <label for="startMonthInput" class="form-label fw-semibold">
              Start Month (YYYY-MM)
            </label>
            <input
              id="startMonthInput"
              v-model="filters.startMonth"
              type="month"
              class="form-control"
              placeholder="YYYY-MM"
              :disabled="isLoadingReport"
              data-testid="start-month-input"
            />
          </div>

          <div class="col-12 col-sm-6 col-md-3">
            <label for="endMonthInput" class="form-label fw-semibold">
              End Month (YYYY-MM)
            </label>
            <input
              id="endMonthInput"
              v-model="filters.endMonth"
              type="month"
              class="form-control"
              placeholder="YYYY-MM"
              :disabled="isLoadingReport"
              data-testid="end-month-input"
            />
          </div>

          <div class="col-12 col-md-2 d-flex gap-2">
            <button
              type="submit"
              class="btn btn-primary flex-grow-1"
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
              class="btn btn-outline-secondary"
              :disabled="isLoadingReport"
              data-testid="reset-performance-button"
              @click="handleResetFilters"
            >
              Reset
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Loading Indicator -->
    <div v-if="isLoadingReport" class="text-center py-5" data-testid="performance-loading">
      <div class="spinner-border text-primary" role="status">
        <span class="visually-hidden">Loading programmer performance report...</span>
      </div>
      <p class="text-body-secondary mt-2 mb-0">Loading historical performance snapshots...</p>
    </div>

    <template v-else-if="report">
      <!-- Summary KPI Cards -->
      <div class="row g-3 mb-4">
        <div class="col-12 col-sm-6 col-lg-3">
          <div class="card shadow-sm h-100 border-success">
            <div class="card-body">
              <div class="text-body-secondary small text-uppercase fw-semibold">
                Total Completed Requests
              </div>
              <div
                class="display-6 fw-bold text-success mt-1"
                data-testid="kpi-total-completed"
              >
                {{ report.totalCompletedRequests }}
              </div>
              <div class="small text-body-secondary mt-1">
                {{ report.personName || 'All Programmers' }}
              </div>
            </div>
          </div>
        </div>

        <div class="col-12 col-sm-6 col-lg-3">
          <div class="card shadow-sm h-100 border-secondary">
            <div class="card-body">
              <div class="text-body-secondary small text-uppercase fw-semibold">
                Total Rejected Requests
              </div>
              <div class="display-6 fw-bold mt-1" data-testid="kpi-total-rejected">
                {{ report.totalRejectedRequests }}
              </div>
              <div class="small text-body-secondary mt-1">
                Closed with REJECTED outcome
              </div>
            </div>
          </div>
        </div>

        <div class="col-12 col-sm-6 col-lg-3">
          <div class="card shadow-sm h-100 border-primary">
            <div class="card-body">
              <div class="text-body-secondary small text-uppercase fw-semibold">
                Avg Resolution Turnaround
              </div>
              <div
                class="display-6 fw-bold text-primary mt-1"
                data-testid="kpi-avg-resolution-hours"
              >
                {{ formatDecimal(report.avgResolutionHours) }} <span class="fs-5">hrs</span>
              </div>
              <div class="small text-body-secondary mt-1">
                Across selected period
              </div>
            </div>
          </div>
        </div>

        <div class="col-12 col-sm-6 col-lg-3">
          <div class="card shadow-sm h-100">
            <div class="card-body">
              <div class="text-body-secondary small text-uppercase fw-semibold">
                Daily Snapshot Records
              </div>
              <div class="display-6 fw-bold mt-1" data-testid="kpi-daily-snapshots-count">
                {{ dailyWorkloadSnapshots.length }}
              </div>
              <div class="small text-body-secondary mt-1">
                Monthly series rows: {{ monthlyPerformance.length }}
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Monthly Performance Series Table -->
      <div class="card shadow-sm mb-4">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h2 class="h5 mb-0">
            <i class="bi bi-calendar3 me-2 text-primary" aria-hidden="true"></i>
            Monthly Performance Summary
          </h2>
          <span class="badge text-bg-primary">{{ monthlyPerformance.length }} rows</span>
        </div>
        <div class="card-body p-0">
          <div
            v-if="monthlyPerformance.length === 0"
            class="p-4 text-center text-body-secondary"
          >
            No monthly performance records found for the selected filter criteria.
          </div>
          <div v-else class="table-responsive">
            <table
              class="table table-hover align-middle mb-0"
              data-testid="monthly-performance-table"
            >
              <thead class="table-light">
                <tr>
                  <th scope="col">Month (YYYY-MM)</th>
                  <th scope="col">Programmer</th>
                  <th scope="col" class="text-end">Completed Requests</th>
                  <th scope="col" class="text-end">Rejected Requests</th>
                  <th scope="col" class="text-end">Peak Active Requests</th>
                  <th scope="col" class="text-end">Escalated Observations</th>
                  <th scope="col" class="text-end">Avg Resolution (Hours)</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="item in monthlyPerformance"
                  :key="`${item.yearMonth}-${item.personId}`"
                >
                  <td>
                    <span class="badge text-bg-light border font-monospace fs-6">
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
                        item.escalatedRequestsCount > 0 ? 'text-bg-danger' : 'text-bg-light border'
                      "
                    >
                      {{ item.escalatedRequestsCount }}
                    </span>
                  </td>
                  <td class="text-end font-monospace">
                    {{ formatDecimal(item.avgResolutionHours) }} hrs
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Daily Workload Snapshots Table -->
      <div class="card shadow-sm mb-4">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h2 class="h5 mb-0">
            <i class="bi bi-clock-history me-2 text-secondary" aria-hidden="true"></i>
            Daily Workload Snapshots (`analytics.DailyWorkloadSnapshots`)
          </h2>
          <span class="badge text-bg-secondary">{{ dailyWorkloadSnapshots.length }} snapshots</span>
        </div>
        <div class="card-body p-0">
          <div
            v-if="dailyWorkloadSnapshots.length === 0"
            class="p-4 text-center text-body-secondary"
          >
            No daily workload snapshots recorded for the selected filter criteria.
          </div>
          <div v-else class="table-responsive">
            <table
              class="table table-hover align-middle mb-0"
              data-testid="daily-snapshots-table"
            >
              <thead class="table-light">
                <tr>
                  <th scope="col">Snapshot Date</th>
                  <th scope="col">Programmer</th>
                  <th scope="col" class="text-end">Active Requests</th>
                  <th scope="col" class="text-end">Escalated</th>
                  <th scope="col" class="text-end">Stalled (72h+)</th>
                  <th scope="col" class="text-end">Completed Today</th>
                  <th scope="col" class="text-end">Avg Age (Hours)</th>
                  <th scope="col">Captured At</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="snap in dailyWorkloadSnapshots"
                  :key="snap.snapshotId || snap.id"
                >
                  <td class="font-monospace">{{ formatDate(snap.snapshotDate) }}</td>
                  <td class="fw-semibold">{{ snap.personName || snap.personId }}</td>
                  <td class="text-end">{{ snap.activeRequestsCount }}</td>
                  <td class="text-end">
                    <span
                      class="badge"
                      :class="
                        snap.escalatedRequestsCount > 0 ? 'text-bg-danger' : 'text-bg-light border'
                      "
                    >
                      {{ snap.escalatedRequestsCount }}
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
                  <td class="text-end font-monospace">{{ formatDecimal(snap.avgAgeHours) }} hrs</td>
                  <td class="small text-body-secondary">{{ formatDateTime(snap.capturedAt) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Monthly Customer Performance Snapshots Table (if present) -->
      <div v-if="monthlyCustomerSnapshots.length > 0" class="card shadow-sm mb-4">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h2 class="h5 mb-0">
            <i class="bi bi-building-check me-2 text-info" aria-hidden="true"></i>
            Monthly Customer Performance Snapshots (`analytics.MonthlyCustomerPerformanceSnapshots`)
          </h2>
          <span class="badge text-bg-info">{{ monthlyCustomerSnapshots.length }} snapshots</span>
        </div>
        <div class="card-body p-0">
          <div class="table-responsive">
            <table
              class="table table-hover align-middle mb-0"
              data-testid="monthly-customer-snapshots-table"
            >
              <thead class="table-light">
                <tr>
                  <th scope="col">Month</th>
                  <th scope="col">Customer</th>
                  <th scope="col" class="text-end">Total Requests</th>
                  <th scope="col" class="text-end">Resolved</th>
                  <th scope="col" class="text-end">Rejected</th>
                  <th scope="col" class="text-end">Avg Resolution (Hours)</th>
                  <th scope="col" class="text-end">SLA Met</th>
                  <th scope="col" class="text-end">SLA Breached</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="custSnap in monthlyCustomerSnapshots"
                  :key="custSnap.snapshotId || custSnap.id"
                >
                  <td class="font-monospace">{{ custSnap.yearMonth }}</td>
                  <td class="fw-semibold">{{ custSnap.customerName || custSnap.customerId }}</td>
                  <td class="text-end">{{ custSnap.totalRequests }}</td>
                  <td class="text-end">{{ custSnap.resolvedRequestsCount }}</td>
                  <td class="text-end">{{ custSnap.rejectedRequestsCount }}</td>
                  <td class="text-end font-monospace">
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
