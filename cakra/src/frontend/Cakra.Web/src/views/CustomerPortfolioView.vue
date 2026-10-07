<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'

import { httpClient } from '@/api/http'
import CreateCustomerModal from '@/components/CreateCustomerModal.vue'
import EditCustomerModal from '@/components/EditCustomerModal.vue'
import type { CustomerDto } from '@/api/customers'

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
  isPaused?: boolean
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
  pausedRequestsCount?: number
  openBlockersCount: number
  blockedRequestsCount?: number
  escalatedRequestsCount?: number
  recentCompletionsCount: number
  resolvedRequestsCount?: number
  completedRequestsCount?: number
  rejectedRequestsCount: number
  totalRequestsCount: number
  activeRequests: CustomerPortfolioRequestItemDto[]
  pausedRequests?: CustomerPortfolioRequestItemDto[]
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

const isCreateModalOpen = ref(false)
const isEditModalOpen = ref(false)
const editingCustomerId = ref<string | null>(null)

const activeRequestsList = computed<CustomerPortfolioRequestItemDto[]>(
  () => portfolio.value?.activeRequests ?? [],
)

const openBlockersList = computed<CustomerPortfolioRequestItemDto[]>(
  () => portfolio.value?.pausedRequests ?? portfolio.value?.openBlockers ?? portfolio.value?.blockedRequests ?? [],
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

function openCreateModal(): void {
  isCreateModalOpen.value = true
}

function openEditModal(id?: string): void {
  editingCustomerId.value = id || selectedCustomerId.value || null
  isEditModalOpen.value = true
}

async function handleCustomerSaved(savedCustomer?: CustomerDto): Promise<void> {
  await loadActiveCustomers()
  if (savedCustomer?.id || savedCustomer?.customerId) {
    selectedCustomerId.value = savedCustomer.id || savedCustomer.customerId || selectedCustomerId.value
  }
  await loadCustomerPortfolio()
}

onMounted(async () => {
  await loadActiveCustomers()
})
</script>

<template>
  <section data-screen-id="SCR-MGT-001" class="customer-portfolio-view">
    <!-- Compact Screen Header -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2">
        <h1 class="op-screen-title">
          <i class="bi bi-building text-primary" aria-hidden="true"></i>
          Customer Portfolio Analytics
        </h1>
        <span class="badge text-bg-light border text-secondary font-monospace">SCR-MGT-001</span>
      </div>

      <div class="d-flex align-items-center gap-2">
        <button
          type="button"
          class="btn btn-primary btn-sm"
          data-testid="add-customer-button"
          @click="openCreateModal"
        >
          <i class="bi bi-building-add me-1" aria-hidden="true"></i>
          Add Customer
        </button>
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
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center gap-2 py-1 px-2 mb-2"
      role="alert"
      data-testid="portfolio-error-alert"
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

    <!-- Compact Customer Selector Toolbar -->
    <div class="op-toolbar">
      <form class="d-flex flex-wrap align-items-center gap-2 w-100" @submit.prevent="loadCustomerPortfolio">
        <div class="d-flex align-items-center gap-1">
          <label for="customerPortfolioSelect" class="form-label mb-0 fs-11 text-nowrap">Select Customer:</label>
          <select
            id="customerPortfolioSelect"
            v-model="selectedCustomerId"
            class="form-select form-select-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
            style="min-width: 180px; max-width: 280px;"
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

        <button
          type="submit"
          class="btn btn-primary btn-sm"
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
          Load
        </button>

        <!-- Customer Identity Quick Bar if loaded -->
        <div v-if="portfolio" class="d-flex align-items-center gap-2 ms-auto" data-testid="customer-summary-card">
          <div class="d-flex align-items-center gap-1.5">
            <span class="fw-bold">{{ portfolio.customerName }}</span>
            <span class="badge text-bg-light border font-monospace">{{ portfolio.customerCode }}</span>
            <span
              class="badge"
              :class="portfolio.customerStatus === 'ACTIVE' ? 'text-bg-success' : 'text-bg-secondary'"
            >
              {{ portfolio.customerStatus }}
            </span>
          </div>

          <span
            class="badge"
            :class="portfolio.hasActiveMaintenanceContract ? 'text-bg-success' : 'text-bg-warning'"
            data-testid="contract-status-badge"
          >
            <i
              class="bi me-0.5"
              :class="portfolio.hasActiveMaintenanceContract ? 'bi-shield-check' : 'bi-shield-exclamation'"
              aria-hidden="true"
            ></i>
            {{ portfolio.hasActiveMaintenanceContract ? 'Contract: ACTIVE' : 'NO CONTRACT' }}
          </span>

          <button
            type="button"
            class="btn btn-outline-secondary btn-sm py-0 px-1.5 fs-11"
            data-testid="edit-customer-button"
            @click="openEditModal(portfolio.customerId)"
          >
            <i class="bi bi-pencil me-0.5" aria-hidden="true"></i>
            Edit
          </button>
        </div>
      </form>
    </div>

    <!-- Loading Indicator -->
    <div v-if="isLoadingPortfolio" class="text-center py-4" data-testid="portfolio-loading">
      <div class="spinner-border spinner-border-sm text-primary" role="status">
        <span class="visually-hidden">Loading customer portfolio...</span>
      </div>
      <p class="text-body-secondary small mt-1 mb-0">Loading customer request portfolio...</p>
    </div>

    <!-- Portfolio Content -->
    <template v-else-if="portfolio">
      <!-- Compact Operational Metric Ribbon -->
      <div class="op-metric-ribbon">
        <div class="op-stat-item">
          <span class="op-stat-label">Active Requests:</span>
          <span class="op-stat-val text-primary" data-testid="metric-active-requests">
            {{ portfolio.activeRequestsCount }}
          </span>
        </div>

        <div class="op-stat-item">
          <span class="op-stat-label text-warning-emphasis">Paused Requests:</span>
          <span class="op-stat-val text-warning-emphasis" data-testid="metric-open-blockers" data-testid-alias="metric-paused-requests">
            {{ portfolio.pausedRequestsCount ?? portfolio.openBlockersCount }}
          </span>
        </div>

        <div class="op-stat-item">
          <span class="op-stat-label text-success">Completions:</span>
          <span class="op-stat-val text-success" data-testid="metric-recent-completions">
            {{ portfolio.recentCompletionsCount }}
          </span>
        </div>

        <div class="op-stat-item">
          <span class="op-stat-label">Total Requests:</span>
          <span class="op-stat-val" data-testid="metric-total-requests">
            {{ portfolio.totalRequestsCount }}
          </span>
          <span class="text-muted fs-11 ms-1">(Rejected: {{ portfolio.rejectedRequestsCount }})</span>
        </div>
      </div>

      <!-- Paused Requests (PAUSED) Table -->
      <div v-if="openBlockersList.length > 0" class="card bg-slate-900/90 border border-slate-800 text-slate-100 mb-2">
        <div class="card-header bg-slate-950/60 border-b border-slate-800 text-amber-400 py-1 px-3 d-flex justify-content-between align-items-center">
          <span class="fw-semibold">
            <i class="bi bi-pause-circle-fill me-1" aria-hidden="true"></i>
            Paused Requests (PAUSED)
          </span>
          <span class="badge text-bg-warning text-dark">{{ openBlockersList.length }}</span>
        </div>
        <div class="card-body p-0">
          <div class="table-responsive">
            <table
              class="table table-hover align-middle mb-0"
              data-testid="open-blockers-table"
              data-testid-alias="paused-requests-table"
            >
              <thead>
                <tr>
                  <th scope="col">Request</th>
                  <th scope="col" style="width: 80px;">Priority</th>
                  <th scope="col" style="width: 90px;">Status</th>
                  <th scope="col" style="width: 140px;">Assigned Owner</th>
                  <th scope="col">Paused Reason / Notes</th>
                  <th scope="col" style="width: 125px;">Last Updated</th>
                  <th scope="col" style="width: 120px;" class="text-end">Action</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in openBlockersList" :key="resolveRequestId(item)">
                  <td>
                    <router-link
                      :to="`/requests/${resolveRequestId(item)}`"
                      class="fw-semibold text-decoration-none text-slate-100 hover:text-cyan-400"
                    >
                      {{ item.title }}
                    </router-link>
                    <span class="badge text-bg-light border ms-1 fs-11">{{ item.requestType }}</span>
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
                    <span class="text-warning-emphasis fw-medium">{{ item.escalationReason || '—' }}</span>
                  </td>
                  <td class="text-body-secondary fs-11">
                    {{ formatDateTime(item.lastUpdatedAt) }}
                  </td>
                  <td class="text-end">
                    <router-link
                      :to="`/requests/${resolveRequestId(item)}`"
                      class="btn btn-outline-warning text-dark btn-sm py-0 px-1.5 fs-11"
                    >
                      Review
                    </router-link>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Active Requests Table -->
      <div class="card bg-slate-900/90 border border-slate-800 text-slate-100 mb-2">
        <div class="card-header bg-slate-950/60 border-b border-slate-800 py-1 px-3 d-flex justify-content-between align-items-center">
          <span class="fw-semibold">
            <i class="bi bi-list-task me-1 text-primary" aria-hidden="true"></i>
            Active Requests
          </span>
          <span class="badge text-bg-primary">{{ activeRequestsList.length }}</span>
        </div>
        <div class="card-body p-0">
          <div v-if="activeRequestsList.length === 0" class="p-3 text-center text-body-secondary small">
            No active requests for this customer.
          </div>
          <div v-else class="table-responsive">
            <table
              class="table table-hover align-middle mb-0"
              data-testid="active-requests-table"
            >
              <thead>
                <tr>
                  <th scope="col">Title</th>
                  <th scope="col" style="width: 80px;">Type</th>
                  <th scope="col" style="width: 80px;">Priority</th>
                  <th scope="col" style="width: 90px;">Status</th>
                  <th scope="col" style="width: 140px;">Assigned Owner</th>
                  <th scope="col" style="width: 120px;">Created</th>
                  <th scope="col" style="width: 120px;">Last Activity</th>
                  <th scope="col" style="width: 70px;" class="text-end">Action</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in activeRequestsList" :key="resolveRequestId(item)">
                  <td>
                    <router-link
                      :to="`/requests/${resolveRequestId(item)}`"
                      class="fw-semibold text-decoration-none text-slate-100 hover:text-cyan-400 text-truncate d-inline-block"
                      style="max-width: 380px;"
                    >
                      {{ item.title }}
                    </router-link>
                  </td>
                  <td>
                    <span class="badge text-bg-light border fs-11">{{ item.requestType }}</span>
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
                  <td class="text-body-secondary fs-11">{{ formatDateTime(item.createdAt) }}</td>
                  <td class="text-body-secondary fs-11">{{ formatDateTime(item.lastUpdatedAt) }}</td>
                  <td class="text-end">
                    <router-link
                      :to="`/requests/${resolveRequestId(item)}`"
                      class="btn btn-outline-primary btn-sm py-0 px-1.5 fs-11"
                    >
                      View
                    </router-link>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Recent Completions (COMPLETED) Table -->
      <div class="card bg-slate-900/90 border border-slate-800 text-slate-100 mb-2">
        <div class="card-header bg-slate-950/60 border-b border-slate-800 py-1 px-3 d-flex justify-content-between align-items-center">
          <span class="fw-semibold">
            <i class="bi bi-check2-circle me-1 text-success" aria-hidden="true"></i>
            Recent Completions (COMPLETED)
          </span>
          <span class="badge text-bg-success">{{ recentCompletionsList.length }}</span>
        </div>
        <div class="card-body p-0">
          <div
            v-if="recentCompletionsList.length === 0"
            class="p-3 text-center text-body-secondary small"
          >
            No completed requests recorded for this customer yet.
          </div>
          <div v-else class="table-responsive">
            <table
              class="table table-hover align-middle mb-0"
              data-testid="recent-completions-table"
            >
              <thead>
                <tr>
                  <th scope="col">Title</th>
                  <th scope="col" style="width: 100px;">Outcome</th>
                  <th scope="col" style="width: 140px;">Assigned Owner</th>
                  <th scope="col">Resolution Summary</th>
                  <th scope="col" style="width: 125px;">Resolved At</th>
                  <th scope="col" style="width: 70px;" class="text-end">Action</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in recentCompletionsList" :key="resolveRequestId(item)">
                  <td>
                    <router-link
                      :to="`/requests/${resolveRequestId(item)}`"
                      class="fw-semibold text-decoration-none text-slate-100 hover:text-cyan-400 text-truncate d-inline-block"
                      style="max-width: 320px;"
                    >
                      {{ item.title }}
                    </router-link>
                  </td>
                  <td>
                    <span class="badge text-bg-success">
                      {{ item.resolutionOutcome || item.status }}
                    </span>
                  </td>
                  <td>{{ item.ownerName || '—' }}</td>
                  <td class="text-truncate" style="max-width: 250px;">{{ item.resolutionSummary || '—' }}</td>
                  <td class="text-body-secondary fs-11">
                    {{ formatDateTime(item.resolvedAt || item.lastUpdatedAt) }}
                  </td>
                  <td class="text-end">
                    <router-link
                      :to="`/requests/${resolveRequestId(item)}`"
                      class="btn btn-outline-secondary btn-sm py-0 px-1.5 fs-11"
                    >
                      View
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
    <div v-else class="card bg-slate-900/90 border border-slate-800 text-slate-100">
      <div class="card-body text-center py-4 text-slate-400 small">
        <i class="bi bi-building fs-3 d-block mb-1" aria-hidden="true"></i>
        Select an active customer above to inspect their real-time request portfolio, paused requests, and recent completions.
      </div>
    </div>

    <!-- Modals (SCR-CUST-001 & SCR-CUST-002) -->
    <CreateCustomerModal
      :show="isCreateModalOpen"
      @close="isCreateModalOpen = false"
      @saved="handleCustomerSaved"
    />
    <EditCustomerModal
      :show="isEditModalOpen"
      :customer-id="editingCustomerId"
      @close="isEditModalOpen = false; editingCustomerId = null"
      @saved="handleCustomerSaved"
    />
  </section>
</template>
