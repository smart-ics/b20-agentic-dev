<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'

import { httpClient } from '@/api/http'

/**
 * SCR-REQ-005: Request Search & History Screen
 * (Architecture §7, §8 — UC-COL-002, UC-COL-003, §9 — FEAT-COL-001..004, §18, §19.4, §19.6, §20, §21).
 *
 * - Renders a Bootstrap 5 search form with Customer (`GET /api/v1/customers/active`),
 *   Product (`GET /api/v1/products/active`), Status, and Keyword filter inputs.
 * - Renders a results table below (`GET /api/v1/requests` with filter & pagination parameters).
 * - Renders a selected request state history panel calling `GET /api/v1/requests/${id}/history`.
 */

export interface ActiveCustomerOption {
  id: string
  customerId?: string
  customerCode: string
  code: string
  customerName: string
  name: string
  status: string
}

export interface ActiveProductOption {
  id: string
  productId?: string
  code: string
  productCode?: string
  name: string
  productName?: string
  status: string
}

export interface SearchRequestItem {
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
  createdAt: string
  updatedAt?: string | null
}

export interface PagedRequestGridResponse {
  items: SearchRequestItem[]
  requests?: SearchRequestItem[]
  totalCount: number
  page: number
  pageSize: number
  offset: number
  totalPages: number
}

export interface RequestStateHistoryEntry {
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

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const REQUEST_STATUSES = [
  'CAPTURED',
  'EVALUATING',
  'ACCEPTED',
  'IN_PROGRESS',
  'ESCALATED',
  'COMPLETED',
  'REJECTED',
] as const

const router = useRouter()

const activeCustomers = ref<ActiveCustomerOption[]>([])
const activeProducts = ref<ActiveProductOption[]>([])
const searchResults = ref<SearchRequestItem[]>([])
const selectedRequest = ref<SearchRequestItem | null>(null)
const selectedRequestHistory = ref<RequestStateHistoryEntry[]>([])

const isLoadingLookups = ref(false)
const isSearching = ref(false)
const isLoadingHistory = ref(false)
const errorMessage = ref<string | null>(null)

const searchFilters = reactive({
  customerId: '',
  productId: '',
  status: '',
  searchTerm: '',
  page: 1,
  pageSize: 20,
})

const totalCount = ref(0)
const totalPages = ref(0)

const hasActiveFilters = computed(
  () =>
    searchFilters.customerId.trim().length > 0 ||
    searchFilters.productId.trim().length > 0 ||
    searchFilters.status.trim().length > 0 ||
    searchFilters.searchTerm.trim().length > 0,
)

const canGoPrevious = computed(() => !isSearching.value && searchFilters.page > 1)
const canGoNext = computed(
  () => !isSearching.value && totalPages.value > 0 && searchFilters.page < totalPages.value,
)

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

function resolveCustomerLabel(customer: ActiveCustomerOption): string {
  const name = customer.customerName || customer.name
  const code = customer.customerCode || customer.code
  return code ? `${name} (${code})` : name
}

function resolveProductLabel(product: ActiveProductOption): string {
  const name = product.name || product.productName || ''
  const code = product.code || product.productCode || ''
  return code ? `${name} (${code})` : name
}

function resolveCustomerDisplay(item: SearchRequestItem): string {
  if (item.customerName && item.customerName.trim().length > 0) {
    return item.customerCode ? `${item.customerName} (${item.customerCode})` : item.customerName
  }
  if (item.customerCode && item.customerCode.trim().length > 0) {
    return item.customerCode
  }
  return item.customerId ?? '—'
}

function resolveProductDisplay(item: SearchRequestItem): string {
  if (item.productName && item.productName.trim().length > 0) {
    return item.productCode ? `${item.productName} (${item.productCode})` : item.productName
  }
  if (item.productCode && item.productCode.trim().length > 0) {
    return item.productCode
  }
  return item.productId ?? '—'
}

function resolveAssigneeDisplay(item: SearchRequestItem): string {
  if (item.assigneeName && item.assigneeName.trim().length > 0) {
    return item.assigneeName
  }
  if (item.ownerName && item.ownerName.trim().length > 0) {
    return item.ownerName
  }
  return item.assigneePersonId ?? item.ownerPersonId ?? 'Unassigned'
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

async function loadLookups(): Promise<void> {
  isLoadingLookups.value = true

  try {
    const [customersResponse, productsResponse] = await Promise.all([
      httpClient.get<ActiveCustomerOption[]>('/customers/active'),
      httpClient.get<ActiveProductOption[]>('/products/active'),
    ])
    activeCustomers.value = customersResponse.data
    activeProducts.value = productsResponse.data
  } catch {
    activeCustomers.value = []
    activeProducts.value = []
  } finally {
    isLoadingLookups.value = false
  }
}

async function loadSelectedRequestHistory(req: SearchRequestItem): Promise<void> {
  selectedRequest.value = req
  isLoadingHistory.value = true
  errorMessage.value = null

  try {
    const response = await httpClient.get<RequestStateHistoryEntry[]>(`/requests/${req.id}/history`)
    selectedRequestHistory.value = response.data
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load request state history.')
    selectedRequestHistory.value = []
  } finally {
    isLoadingHistory.value = false
  }
}

async function executeSearch(): Promise<void> {
  isSearching.value = true
  errorMessage.value = null

  try {
    const params: Record<string, string | number> = {
      page: searchFilters.page,
      pageSize: searchFilters.pageSize,
    }

    if (searchFilters.customerId.trim().length > 0) {
      params.customerId = searchFilters.customerId.trim()
    }
    if (searchFilters.productId.trim().length > 0) {
      params.productId = searchFilters.productId.trim()
    }
    if (searchFilters.status.trim().length > 0) {
      params.status = searchFilters.status.trim()
    }
    if (searchFilters.searchTerm.trim().length > 0) {
      params.searchTerm = searchFilters.searchTerm.trim()
    }

    const response = await httpClient.get<PagedRequestGridResponse | SearchRequestItem[]>(
      '/requests',
      { params },
    )

    let items: SearchRequestItem[] = []
    if (Array.isArray(response.data)) {
      items = response.data
      totalCount.value = items.length
      totalPages.value = items.length > 0 ? 1 : 0
    } else {
      items = response.data.items ?? response.data.requests ?? []
      totalCount.value = response.data.totalCount ?? items.length
      searchFilters.page = response.data.page ?? searchFilters.page
      searchFilters.pageSize = response.data.pageSize ?? searchFilters.pageSize
      totalPages.value =
        response.data.totalPages ??
        (searchFilters.pageSize > 0 ? Math.ceil(totalCount.value / searchFilters.pageSize) : 0)
    }

    searchResults.value = items

    if (items.length > 0) {
      const existingSelection = selectedRequest.value
        ? items.find((item) => item.id === selectedRequest.value?.id)
        : undefined
      await loadSelectedRequestHistory(existingSelection ?? items[0])
    } else {
      selectedRequest.value = null
      selectedRequestHistory.value = []
    }
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to search requests.')
  } finally {
    isSearching.value = false
  }
}

async function handleSearchSubmit(): Promise<void> {
  searchFilters.page = 1
  await executeSearch()
}

async function handleResetSearch(): Promise<void> {
  searchFilters.customerId = ''
  searchFilters.productId = ''
  searchFilters.status = ''
  searchFilters.searchTerm = ''
  searchFilters.page = 1
  await executeSearch()
}

async function goToPage(targetPage: number): Promise<void> {
  if (targetPage < 1 || (totalPages.value > 0 && targetPage > totalPages.value)) {
    return
  }
  searchFilters.page = targetPage
  await executeSearch()
}

async function navigateToDetail(id: string): Promise<void> {
  await router.push(`/requests/${id}`)
}

onMounted(async () => {
  await Promise.all([loadLookups(), executeSearch()])
})
</script>

<template>
  <section class="container-fluid py-2" data-screen-id="SCR-REQ-005">
    <!-- Screen Header -->
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-3 mb-4">
      <div>
        <div class="d-flex align-items-center gap-2">
          <h1 class="h3 mb-0 fw-bold">Request Search &amp; History</h1>
          <span class="badge text-bg-light border text-secondary">SCR-REQ-005</span>
        </div>
        <p class="text-body-secondary small mb-0 mt-1">
          Search operational requests by customer, product, status, or keyword, and inspect full state transition histories.
        </p>
      </div>

      <div class="d-flex align-items-center gap-2">
        <router-link
          to="/requests"
          class="btn btn-outline-secondary btn-sm"
          data-testid="all-requests-link"
        >
          <i class="bi bi-card-checklist me-1" aria-hidden="true"></i>
          All Requests
        </router-link>

        <router-link
          to="/requests/my"
          class="btn btn-outline-secondary btn-sm"
          data-testid="my-requests-link"
        >
          <i class="bi bi-person-workspace me-1" aria-hidden="true"></i>
          My Requests
        </router-link>
      </div>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      role="alert"
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center gap-2 mb-4"
      data-testid="request-search-error-alert"
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

    <!-- Search Form Card -->
    <div class="card shadow-sm border-0 mb-4">
      <div class="card-header bg-body-tertiary py-3">
        <span class="fw-semibold">
          <i class="bi bi-search me-2 text-primary" aria-hidden="true"></i>
          Search Filters
        </span>
      </div>

      <div class="card-body p-4">
        <form
          data-testid="request-search-form"
          @submit.prevent="handleSearchSubmit"
        >
          <div class="row g-3 align-items-end">
            <!-- Customer Filter -->
            <div class="col-12 col-md-6 col-lg-3">
              <label for="searchCustomerSelect" class="form-label small fw-medium mb-1">
                Customer
              </label>
              <select
                id="searchCustomerSelect"
                v-model="searchFilters.customerId"
                class="form-select"
                :disabled="isSearching || isLoadingLookups"
                data-testid="search-customer-select"
              >
                <option value="">All Customers</option>
                <option
                  v-for="customer in activeCustomers"
                  :key="customer.id"
                  :value="customer.id"
                >
                  {{ resolveCustomerLabel(customer) }}
                </option>
              </select>
            </div>

            <!-- Product Filter -->
            <div class="col-12 col-md-6 col-lg-3">
              <label for="searchProductSelect" class="form-label small fw-medium mb-1">
                Product
              </label>
              <select
                id="searchProductSelect"
                v-model="searchFilters.productId"
                class="form-select"
                :disabled="isSearching || isLoadingLookups"
                data-testid="search-product-select"
              >
                <option value="">All Products</option>
                <option
                  v-for="product in activeProducts"
                  :key="product.id"
                  :value="product.id"
                >
                  {{ resolveProductLabel(product) }}
                </option>
              </select>
            </div>

            <!-- Status Filter -->
            <div class="col-12 col-md-6 col-lg-2">
              <label for="searchStatusSelect" class="form-label small fw-medium mb-1">
                Status
              </label>
              <select
                id="searchStatusSelect"
                v-model="searchFilters.status"
                class="form-select"
                :disabled="isSearching"
                data-testid="search-status-select"
              >
                <option value="">All Statuses</option>
                <option v-for="status in REQUEST_STATUSES" :key="status" :value="status">
                  {{ status }}
                </option>
              </select>
            </div>

            <!-- Keyword Search -->
            <div class="col-12 col-md-6 col-lg-2">
              <label for="searchKeywordInput" class="form-label small fw-medium mb-1">
                Keyword
              </label>
              <input
                id="searchKeywordInput"
                v-model="searchFilters.searchTerm"
                type="search"
                class="form-control"
                placeholder="Title or description..."
                :disabled="isSearching"
                data-testid="search-keyword-input"
              />
            </div>

            <!-- Submit & Clear Buttons -->
            <div class="col-12 col-lg-2 d-flex gap-2">
              <button
                type="submit"
                class="btn btn-primary flex-grow-1"
                :disabled="isSearching"
                data-testid="search-submit-button"
              >
                <i class="bi bi-search me-1" aria-hidden="true"></i>
                Search
              </button>
              <button
                v-if="hasActiveFilters"
                type="button"
                class="btn btn-outline-secondary"
                :disabled="isSearching"
                data-testid="search-reset-button"
                @click="handleResetSearch"
              >
                Reset
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>

    <!-- Search Results Table Card -->
    <div class="card shadow-sm border-0 mb-4">
      <div class="card-header bg-body-tertiary py-3 d-flex justify-content-between align-items-center">
        <span class="fw-semibold">
          <i class="bi bi-table me-2 text-primary" aria-hidden="true"></i>
          Search Results
        </span>
        <span class="badge text-bg-secondary" data-testid="search-results-count">
          {{ totalCount }}
        </span>
      </div>

      <div class="card-body p-0">
        <div class="table-responsive">
          <table
            class="table table-hover align-middle mb-0"
            data-testid="search-results-table"
          >
            <thead class="table-light">
              <tr>
                <th scope="col" class="ps-4">ID</th>
                <th scope="col">Title</th>
                <th scope="col">Customer</th>
                <th scope="col">Product</th>
                <th scope="col">Status</th>
                <th scope="col">Assignee</th>
                <th scope="col">CreatedAt</th>
                <th scope="col" class="pe-4 text-end">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="isSearching">
                <td colspan="8" class="text-center py-5 text-body-secondary">
                  <span
                    class="spinner-border spinner-border-sm me-2"
                    role="status"
                    aria-hidden="true"
                  ></span>
                  Searching requests...
                </td>
              </tr>

              <tr v-else-if="searchResults.length === 0">
                <td
                  colspan="8"
                  class="text-center py-5 text-body-secondary"
                  data-testid="empty-search-results-row"
                >
                  No requests matched your search criteria.
                </td>
              </tr>

              <tr
                v-for="req in searchResults"
                v-else
                :key="req.id"
                :data-request-id="req.id"
                :class="{ 'table-active': selectedRequest?.id === req.id }"
                style="cursor: pointer"
                data-testid="search-result-row"
                @click="loadSelectedRequestHistory(req)"
              >
                <td class="ps-4">
                  <router-link
                    :to="`/requests/${req.id}`"
                    class="font-monospace small text-decoration-none"
                    data-testid="search-result-id-link"
                    @click.stop
                  >
                    {{ req.id }}
                  </router-link>
                </td>

                <td>
                  <div class="d-flex align-items-center gap-2 flex-wrap">
                    <router-link
                      :to="`/requests/${req.id}`"
                      class="fw-semibold text-decoration-none text-body"
                      data-testid="search-result-title-link"
                      @click.stop
                    >
                      {{ req.title }}
                    </router-link>
                    <span
                      v-if="req.priority"
                      class="badge"
                      :class="priorityBadgeClass(req.priority)"
                    >
                      {{ req.priority }}
                    </span>
                  </div>
                </td>

                <td>
                  {{ resolveCustomerDisplay(req) }}
                </td>

                <td>
                  {{ resolveProductDisplay(req) }}
                </td>

                <td>
                  <span
                    class="badge"
                    :class="statusBadgeClass(req.status)"
                    data-testid="search-result-status-badge"
                  >
                    {{ req.status }}
                  </span>
                </td>

                <td>
                  <span :class="req.ownerPersonId ? '' : 'text-body-secondary fst-italic'">
                    <i class="bi bi-person me-1 text-secondary" aria-hidden="true"></i>
                    {{ resolveAssigneeDisplay(req) }}
                  </span>
                </td>

                <td class="text-body-secondary small">
                  {{ formatTimestamp(req.createdAt) }}
                </td>

                <td class="pe-4 text-end">
                  <div class="d-inline-flex gap-2">
                    <button
                      type="button"
                      class="btn btn-outline-secondary btn-sm"
                      data-testid="select-request-history-button"
                      @click.stop="loadSelectedRequestHistory(req)"
                    >
                      <i class="bi bi-clock-history me-1" aria-hidden="true"></i>
                      History
                    </button>
                    <button
                      type="button"
                      class="btn btn-outline-primary btn-sm"
                      data-testid="open-request-detail-button"
                      @click.stop="navigateToDetail(req.id)"
                    >
                      Detail
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Pagination Controls -->
      <div
        class="card-footer bg-white border-top d-flex flex-wrap justify-content-between align-items-center gap-3 px-4 py-3"
        data-testid="search-pagination"
      >
        <div class="small text-body-secondary">
          Showing page <strong>{{ searchFilters.page }}</strong> of
          <strong>{{ Math.max(totalPages, 1) }}</strong>
          ({{ totalCount }} total {{ totalCount === 1 ? 'request' : 'requests' }})
        </div>

        <nav aria-label="Request search pagination">
          <ul class="pagination pagination-sm mb-0">
            <li class="page-item" :class="{ disabled: !canGoPrevious }">
              <button
                type="button"
                class="page-link"
                :disabled="!canGoPrevious"
                data-testid="search-pagination-prev-button"
                @click="goToPage(searchFilters.page - 1)"
              >
                Previous
              </button>
            </li>
            <li class="page-item active" aria-current="page">
              <span class="page-link">{{ searchFilters.page }}</span>
            </li>
            <li class="page-item" :class="{ disabled: !canGoNext }">
              <button
                type="button"
                class="page-link"
                :disabled="!canGoNext"
                data-testid="search-pagination-next-button"
                @click="goToPage(searchFilters.page + 1)"
              >
                Next
              </button>
            </li>
          </ul>
        </nav>
      </div>
    </div>

    <!-- Selected Request State History Panel -->
    <div class="card shadow-sm border-0" data-testid="selected-request-history-panel">
      <div class="card-header bg-body-tertiary py-3 d-flex flex-wrap justify-content-between align-items-center gap-2">
        <div class="d-flex align-items-center gap-2">
          <i class="bi bi-clock-history text-primary" aria-hidden="true"></i>
          <span class="fw-semibold">Selected Request State History</span>
          <span v-if="selectedRequest" class="text-body-secondary small">
            — {{ selectedRequest.title }} (<code>{{ selectedRequest.id }}</code>)
          </span>
        </div>

        <router-link
          v-if="selectedRequest"
          :to="`/requests/${selectedRequest.id}`"
          class="btn btn-outline-primary btn-sm"
          data-testid="history-panel-detail-link"
        >
          Open Request Detail
        </router-link>
      </div>

      <div class="card-body p-4">
        <div v-if="isLoadingHistory" class="text-center py-4 text-body-secondary">
          <span
            class="spinner-border spinner-border-sm me-2"
            role="status"
            aria-hidden="true"
          ></span>
          Loading request state history...
        </div>

        <div
          v-else-if="!selectedRequest"
          class="text-center py-4 text-body-secondary"
          data-testid="no-selected-request-message"
        >
          Select a request from the search results above to inspect its state transition history.
        </div>

        <div
          v-else-if="selectedRequestHistory.length === 0"
          class="text-center py-4 text-body-secondary"
          data-testid="empty-selected-history-message"
        >
          No state history entries found for this request.
        </div>

        <ul
          v-else
          class="list-group list-group-flush"
          data-testid="selected-request-history-list"
        >
          <li
            v-for="entry in selectedRequestHistory"
            :key="entry.id"
            class="list-group-item px-0 py-3"
            data-testid="selected-request-history-item"
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
                  by <strong>{{ entry.actorName || entry.actorPersonId }}</strong>
                </span>
                <span v-if="entry.assignedOwnerPersonId" class="small text-body-secondary">
                  (Owner: {{ entry.assignedOwnerName || entry.assignedOwnerPersonId }})
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
