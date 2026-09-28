<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'

import { httpClient } from '@/api/http'

/**
 * SCR-MGT-001: Customer Progress Review / Customer Portfolio Screen
 * (Architecture §7, §8 — UC-MGT-002, §9 — FEAT-MGT-002, §13, §19.4, §19.6, §20, §21).
 *
 * - Populates Customer selector dropdown from `GET /api/v1/customers/active`.
 * - Fetches real-time customer request portfolio from
 *   `GET /api/v1/analytics/customer-portfolio?customerId=${id}` via `httpClient`.
 * - Displays customer identity, maintenance contract status badge (`HasActiveMaintenanceContract` /
 *   `ContractStatus`), summary metric cards (`ActiveRequestsCount`, `OpenBlockersCount`,
 *   `RecentCompletionsCount`), and Bootstrap 5 tables for Open Blockers (`ESCALATED`),
 *   Active Requests, and Recent Completions (`COMPLETED`).
 */

export interface ActiveCustomerOption {
  id: string
  customerId?: string
  customerCode: string
  code: string
  customerName: string
  name: string
  status: string
  hasActiveMaintenanceContract: boolean
  isActive: boolean
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

export interface CustomerRequestPortfolioDto {
  customerId: string
  customerCode: string
  customerName: string
  customerStatus: string
  hasActiveMaintenanceContract: boolean
  contractStatus: string
  activeRequestsCount: number
  openBlockersCount: number
  blockedRequestsCount?: number
  escalatedRequestsCount?: number
  recentCompletionsCount: number
  resolvedRequestsCount?: number
  completedRequestsCount?: number
  rejectedRequestsCount: number
  totalRequestsCount: number
  activeRequests: CustomerPortfolioRequestItemDto[]
  openBlockers: CustomerPortfolioRequestItemDto[]
  blockedRequests?: CustomerPortfolioRequestItemDto[]
  recentCompletions: CustomerPortfolioRequestItemDto[]
  requests: CustomerPortfolioRequestItemDto[]
}

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const route = useRoute()

const activeCustomers = ref<ActiveCustomerOption[]>([])
const selectedCustomerId = ref<string>('')
const portfolio = ref<CustomerRequestPortfolioDto | null>(null)

const isLoadingCustomers = ref(false)
const isLoadingPortfolio = ref(false)
const errorMessage = ref<string | null>(null)

const activeRequestsList = computed<CustomerPortfolioRequestItemDto[]>(
  () => portfolio.value?.activeRequests ?? [],
)

const openBlockersList = computed<CustomerPortfolioRequestItemDto[]>(
  () => portfolio.value?.openBlockers ?? portfolio.value?.blockedRequests ?? [],
)

const recentCompletionsList = computed<CustomerPortfolioRequestItemDto[]>(
  () => portfolio.value?.recentCompletions ?? [],
)

function extractErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof AxiosError) {
    if (err.response?.status === 403) {
      return 'Access denied: Management role is required to view customer portfolio analytics.'
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

function resolveCustomerId(customer: ActiveCustomerOption): string {
  return customer.id || customer.customerId || ''
}

function resolveCustomerLabel(customer: ActiveCustomerOption): string {
  const name = customer.customerName || customer.name
  const code = customer.customerCode || customer.code
  return code ? `${name} (${code})` : name
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

async function loadActiveCustomers(): Promise<void> {
  isLoadingCustomers.value = true
  errorMessage.value = null

  try {
    const response = await httpClient.get<ActiveCustomerOption[]>('/customers/active')
    activeCustomers.value = response.data ?? []

    const queryCustomerId =
      typeof route.query.customerId === 'string' ? route.query.customerId.trim() : ''

    if (queryCustomerId) {
      selectedCustomerId.value = queryCustomerId
    } else if (!selectedCustomerId.value && activeCustomers.value.length > 0) {
      const firstCustomer = activeCustomers.value[0]
      if (firstCustomer) {
        selectedCustomerId.value = resolveCustomerId(firstCustomer)
      }
    }

    if (selectedCustomerId.value) {
      await loadCustomerPortfolio()
    }
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load active customers.')
  } finally {
    isLoadingCustomers.value = false
  }
}

async function loadCustomerPortfolio(): Promise<void> {
  const id = selectedCustomerId.value.trim()
  if (!id) {
    portfolio.value = null
    return
  }

  isLoadingPortfolio.value = true
  errorMessage.value = null

  try {
    const response = await httpClient.get<CustomerRequestPortfolioDto>(
      `/analytics/customer-portfolio?customerId=${encodeURIComponent(id)}`,
    )
    portfolio.value = response.data
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(
      err,
      'Failed to load customer request portfolio analytics.',
    )
    portfolio.value = null
  } finally {
    isLoadingPortfolio.value = false
  }
}

async function handleCustomerChange(): Promise<void> {
  await loadCustomerPortfolio()
}

onMounted(async () => {
  await loadActiveCustomers()
})
</script>

<template>
  <section data-screen-id="SCR-MGT-001" class="customer-portfolio-view">
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-3 mb-4">
      <div>
        <h1 class="h3 mb-1">Customer Progress Review</h1>
        <p class="text-body-secondary mb-0">
          Real-time operational request portfolio, open blockers, recent completions, and maintenance contract status per customer (SCR-MGT-001).
        </p>
      </div>
      <div class="d-flex align-items-center gap-2">
        <router-link to="/analytics/programmer-workload" class="btn btn-outline-secondary btn-sm">
          <i class="bi bi-people me-1" aria-hidden="true"></i>
          Programmer Workload
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
      data-testid="portfolio-error-alert"
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

    <!-- Customer Selector Card -->
    <div class="card shadow-sm mb-4">
      <div class="card-body">
        <form class="row g-3 align-items-end" @submit.prevent="loadCustomerPortfolio">
          <div class="col-12 col-md-8 col-lg-6">
            <label for="customerPortfolioSelect" class="form-label fw-semibold">
              Select Customer
            </label>
            <select
              id="customerPortfolioSelect"
              v-model="selectedCustomerId"
              class="form-select"
              :disabled="isLoadingCustomers || isLoadingPortfolio"
              data-testid="customer-selector"
              @change="handleCustomerChange"
            >
              <option value="">-- Choose an active customer --</option>
              <option
                v-for="customer in activeCustomers"
                :key="resolveCustomerId(customer)"
                :value="resolveCustomerId(customer)"
              >
                {{ resolveCustomerLabel(customer) }}
              </option>
            </select>
          </div>
          <div class="col-12 col-md-4 col-lg-3 d-flex gap-2">
            <button
              type="submit"
              class="btn btn-primary"
              :disabled="!selectedCustomerId || isLoadingPortfolio"
              data-testid="load-portfolio-button"
            >
              <span
                v-if="isLoadingPortfolio"
                class="spinner-border spinner-border-sm me-1"
                role="status"
                aria-hidden="true"
              ></span>
              <i v-else class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>
              Load Portfolio
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Loading Indicator -->
    <div v-if="isLoadingPortfolio" class="text-center py-5" data-testid="portfolio-loading">
      <div class="spinner-border text-primary" role="status">
        <span class="visually-hidden">Loading customer portfolio...</span>
      </div>
      <p class="text-body-secondary mt-2 mb-0">Loading customer request portfolio...</p>
    </div>

    <!-- Portfolio Content -->
    <template v-else-if="portfolio">
      <!-- Customer Header & Contract Summary Card -->
      <div class="card shadow-sm mb-4" data-testid="customer-summary-card">
        <div class="card-body d-flex flex-wrap justify-content-between align-items-center gap-3">
          <div>
            <div class="d-flex align-items-center gap-2 mb-1">
              <h2 class="h4 mb-0">{{ portfolio.customerName }}</h2>
              <span class="badge text-bg-light border font-monospace">
                {{ portfolio.customerCode }}
              </span>
              <span
                class="badge"
                :class="
                  portfolio.customerStatus === 'ACTIVE' ? 'text-bg-success' : 'text-bg-secondary'
                "
              >
                {{ portfolio.customerStatus }}
              </span>
            </div>
            <div class="text-body-secondary small">
              Customer ID: <span class="font-monospace">{{ portfolio.customerId }}</span>
            </div>
          </div>

          <div class="d-flex align-items-center gap-2">
            <span class="text-body-secondary small fw-semibold">Maintenance Contract:</span>
            <span
              class="badge fs-6"
              :class="
                portfolio.hasActiveMaintenanceContract ? 'text-bg-success' : 'text-bg-warning'
              "
              data-testid="contract-status-badge"
            >
              <i
                class="bi me-1"
                :class="
                  portfolio.hasActiveMaintenanceContract
                    ? 'bi-shield-check'
                    : 'bi-shield-exclamation'
                "
                aria-hidden="true"
              ></i>
              {{
                portfolio.hasActiveMaintenanceContract
                  ? `ACTIVE (${portfolio.contractStatus || 'ACTIVE'})`
                  : `NO ACTIVE CONTRACT (${portfolio.contractStatus || 'NONE'})`
              }}
            </span>
          </div>
        </div>
      </div>

      <!-- Summary Metric Cards -->
      <div class="row g-3 mb-4">
        <div class="col-12 col-sm-6 col-lg-3">
          <div class="card shadow-sm h-100 border-primary">
            <div class="card-body">
              <div class="text-body-secondary small text-uppercase fw-semibold">
                Active Requests
              </div>
              <div
                class="display-6 fw-bold text-primary mt-1"
                data-testid="metric-active-requests"
              >
                {{ portfolio.activeRequestsCount }}
              </div>
              <div class="small text-body-secondary mt-1">
                Captured, Evaluating, Accepted, In Progress, Escalated
              </div>
            </div>
          </div>
        </div>

        <div class="col-12 col-sm-6 col-lg-3">
          <div class="card shadow-sm h-100 border-danger">
            <div class="card-body">
              <div class="text-body-secondary small text-uppercase fw-semibold">
                Open Blockers (Escalated)
              </div>
              <div
                class="display-6 fw-bold text-danger mt-1"
                data-testid="metric-open-blockers"
              >
                {{ portfolio.openBlockersCount }}
              </div>
              <div class="small text-body-secondary mt-1">
                Escalated requests requiring management attention
              </div>
            </div>
          </div>
        </div>

        <div class="col-12 col-sm-6 col-lg-3">
          <div class="card shadow-sm h-100 border-success">
            <div class="card-body">
              <div class="text-body-secondary small text-uppercase fw-semibold">
                Recent Completions
              </div>
              <div
                class="display-6 fw-bold text-success mt-1"
                data-testid="metric-recent-completions"
              >
                {{ portfolio.recentCompletionsCount }}
              </div>
              <div class="small text-body-secondary mt-1">
                Completed requests delivered for customer
              </div>
            </div>
          </div>
        </div>

        <div class="col-12 col-sm-6 col-lg-3">
          <div class="card shadow-sm h-100">
            <div class="card-body">
              <div class="text-body-secondary small text-uppercase fw-semibold">
                Total Recorded Requests
              </div>
              <div class="display-6 fw-bold mt-1" data-testid="metric-total-requests">
                {{ portfolio.totalRequestsCount }}
              </div>
              <div class="small text-body-secondary mt-1">
                Rejected: {{ portfolio.rejectedRequestsCount }}
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Open Blockers (ESCALATED) Table -->
      <div class="card shadow-sm mb-4 border-danger">
        <div class="card-header bg-danger-subtle text-danger-emphasis d-flex justify-content-between align-items-center">
          <h3 class="h5 mb-0">
            <i class="bi bi-exclamation-octagon-fill me-2" aria-hidden="true"></i>
            Open Blockers (ESCALATED)
          </h3>
          <span class="badge text-bg-danger">{{ openBlockersList.length }}</span>
        </div>
        <div class="card-body p-0">
          <div v-if="openBlockersList.length === 0" class="p-4 text-center text-body-secondary">
            No open blockers (`ESCALATED`) for this customer.
          </div>
          <div v-else class="table-responsive">
            <table
              class="table table-hover align-middle mb-0"
              data-testid="open-blockers-table"
            >
              <thead class="table-light">
                <tr>
                  <th scope="col">Request</th>
                  <th scope="col">Priority</th>
                  <th scope="col">Status</th>
                  <th scope="col">Assigned Owner</th>
                  <th scope="col">Escalation Reason</th>
                  <th scope="col">Last Updated</th>
                  <th scope="col" class="text-end">Action</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in openBlockersList" :key="resolveRequestId(item)">
                  <td>
                    <router-link
                      :to="`/requests/${resolveRequestId(item)}`"
                      class="fw-semibold text-decoration-none"
                    >
                      {{ item.title }}
                    </router-link>
                    <div class="small text-body-secondary">{{ item.requestType }}</div>
                  </td>
                  <td>
                    <span class="badge" :class="priorityBadgeClass(item.priority)">
                      {{ item.priority }}
                    </span>
                  </td>
                  <td>
                    <span class="badge" :class="statusBadgeClass(item.status)">
                      {{ item.status }}
                    </span>
                  </td>
                  <td>{{ item.ownerName || 'Unassigned' }}</td>
                  <td>
                    <span class="text-danger">{{ item.escalationReason || '—' }}</span>
                  </td>
                  <td class="small text-body-secondary">
                    {{ formatDateTime(item.lastUpdatedAt) }}
                  </td>
                  <td class="text-end">
                    <router-link
                      :to="`/requests/${resolveRequestId(item)}`"
                      class="btn btn-outline-danger btn-sm"
                    >
                      Review / Reassign
                    </router-link>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Active Requests Table -->
      <div class="card shadow-sm mb-4">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h3 class="h5 mb-0">
            <i class="bi bi-list-task me-2 text-primary" aria-hidden="true"></i>
            Active Requests
          </h3>
          <span class="badge text-bg-primary">{{ activeRequestsList.length }}</span>
        </div>
        <div class="card-body p-0">
          <div v-if="activeRequestsList.length === 0" class="p-4 text-center text-body-secondary">
            No active requests for this customer.
          </div>
          <div v-else class="table-responsive">
            <table
              class="table table-hover align-middle mb-0"
              data-testid="active-requests-table"
            >
              <thead class="table-light">
                <tr>
                  <th scope="col">Title</th>
                  <th scope="col">Type</th>
                  <th scope="col">Priority</th>
                  <th scope="col">Status</th>
                  <th scope="col">Assigned Owner</th>
                  <th scope="col">Created</th>
                  <th scope="col">Last Activity</th>
                  <th scope="col" class="text-end">Action</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in activeRequestsList" :key="resolveRequestId(item)">
                  <td>
                    <router-link
                      :to="`/requests/${resolveRequestId(item)}`"
                      class="fw-semibold text-decoration-none"
                    >
                      {{ item.title }}
                    </router-link>
                  </td>
                  <td>
                    <span class="badge text-bg-light border">{{ item.requestType }}</span>
                  </td>
                  <td>
                    <span class="badge" :class="priorityBadgeClass(item.priority)">
                      {{ item.priority }}
                    </span>
                  </td>
                  <td>
                    <span class="badge" :class="statusBadgeClass(item.status)">
                      {{ item.status }}
                    </span>
                  </td>
                  <td>{{ item.ownerName || 'Unassigned' }}</td>
                  <td class="small text-body-secondary">{{ formatDateTime(item.createdAt) }}</td>
                  <td class="small text-body-secondary">
                    {{ formatDateTime(item.lastUpdatedAt) }}
                  </td>
                  <td class="text-end">
                    <router-link
                      :to="`/requests/${resolveRequestId(item)}`"
                      class="btn btn-outline-primary btn-sm"
                    >
                      View Details
                    </router-link>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Recent Completions (COMPLETED) Table -->
      <div class="card shadow-sm mb-4">
        <div class="card-header d-flex justify-content-between align-items-center">
          <h3 class="h5 mb-0">
            <i class="bi bi-check2-circle me-2 text-success" aria-hidden="true"></i>
            Recent Completions (COMPLETED)
          </h3>
          <span class="badge text-bg-success">{{ recentCompletionsList.length }}</span>
        </div>
        <div class="card-body p-0">
          <div
            v-if="recentCompletionsList.length === 0"
            class="p-4 text-center text-body-secondary"
          >
            No completed requests recorded for this customer yet.
          </div>
          <div v-else class="table-responsive">
            <table
              class="table table-hover align-middle mb-0"
              data-testid="recent-completions-table"
            >
              <thead class="table-light">
                <tr>
                  <th scope="col">Title</th>
                  <th scope="col">Outcome</th>
                  <th scope="col">Assigned Owner</th>
                  <th scope="col">Resolution Summary</th>
                  <th scope="col">Resolved At</th>
                  <th scope="col" class="text-end">Action</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in recentCompletionsList" :key="resolveRequestId(item)">
                  <td>
                    <router-link
                      :to="`/requests/${resolveRequestId(item)}`"
                      class="fw-semibold text-decoration-none"
                    >
                      {{ item.title }}
                    </router-link>
                    <div class="small text-body-secondary">{{ item.requestType }}</div>
                  </td>
                  <td>
                    <span class="badge text-bg-success">
                      {{ item.resolutionOutcome || item.status }}
                    </span>
                  </td>
                  <td>{{ item.ownerName || '—' }}</td>
                  <td>{{ item.resolutionSummary || '—' }}</td>
                  <td class="small text-body-secondary">
                    {{ formatDateTime(item.resolvedAt || item.lastUpdatedAt) }}
                  </td>
                  <td class="text-end">
                    <router-link
                      :to="`/requests/${resolveRequestId(item)}`"
                      class="btn btn-outline-secondary btn-sm"
                    >
                      View Details
                    </router-link>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </template>

    <!-- Empty Prompt when no customer selected -->
    <div v-else class="card shadow-sm">
      <div class="card-body text-center py-5 text-body-secondary">
        <i class="bi bi-building fs-2 d-block mb-2" aria-hidden="true"></i>
        Select an active customer above to inspect their real-time request portfolio, open blockers, and recent completions.
      </div>
    </div>
  </section>
</template>
