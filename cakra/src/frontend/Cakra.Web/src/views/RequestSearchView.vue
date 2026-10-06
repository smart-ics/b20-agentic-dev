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
  'ASSIGNED',
  'IN_PROGRESS',
  'PAUSED',
  'COMPLETED',
  'CANCELLED',
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
  <section data-screen-id="SCR-REQ-005">
    <!-- Compact Screen Header -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2">
        <h1 class="op-screen-title">
          <i class="bi bi-search text-primary" aria-hidden="true"></i>
          Request Search &amp; History
        </h1>
        <span class="badge text-bg-light border text-secondary font-monospace">SCR-REQ-005</span>
        <span class="text-body-secondary small ms-1">&bull; {{ totalCount }} results</span>
      </div>

      <div class="d-flex align-items-center gap-2">

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
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center gap-2 py-1 px-2 mb-2"
      data-testid="request-search-error-alert"
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

    <!-- Compact Search Toolbar -->
    <div class="op-toolbar">
      <form
        class="d-flex flex-wrap align-items-center gap-2 w-100"
        data-testid="request-search-form"
        @submit.prevent="handleSearchSubmit"
      >
        <!-- Customer Filter -->
        <div class="d-flex align-items-center gap-1">
          <label for="searchCustomerSelect" class="form-label mb-0 fs-11 text-nowrap">Customer:</label>
          <select
            id="searchCustomerSelect"
            v-model="searchFilters.customerId"
            class="form-select form-select-sm"
            style="min-width: 130px; max-width: 180px;"
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
        <div class="d-flex align-items-center gap-1">
          <label for="searchProductSelect" class="form-label mb-0 fs-11 text-nowrap">Product:</label>
          <select
            id="searchProductSelect"
            v-model="searchFilters.productId"
            class="form-select form-select-sm"
            style="min-width: 130px; max-width: 170px;"
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
        <div class="d-flex align-items-center gap-1">
          <label for="searchStatusSelect" class="form-label mb-0 fs-11 text-nowrap">Status:</label>
          <select
            id="searchStatusSelect"
            v-model="searchFilters.status"
            class="form-select form-select-sm"
            style="min-width: 110px; max-width: 140px;"
            :disabled="isSearching"
            data-testid="search-status-select"
          >
            <option value="">ALL</option>
            <option v-for="status in REQUEST_STATUSES" :key="status" :value="status">
              {{ status }}
            </option>
          </select>
        </div>

        <!-- Keyword Search -->
        <div class="d-flex align-items-center gap-1 flex-grow-1" style="min-width: 160px; max-width: 280px;">
          <input
            id="searchKeywordInput"
            v-model="searchFilters.searchTerm"
            type="search"
            class="form-control form-control-sm"
            placeholder="Search keywords..."
            :disabled="isSearching"
            data-testid="search-keyword-input"
          />
        </div>

        <!-- Submit & Clear Buttons -->
        <div class="d-flex align-items-center gap-1 ms-auto">
          <button
            type="submit"
            class="btn btn-primary btn-sm"
            :disabled="isSearching"
            data-testid="search-submit-button"
          >
            <i class="bi bi-search me-1" aria-hidden="true"></i>
            Search
          </button>
          <button
            v-if="hasActiveFilters"
            type="button"
            class="btn btn-outline-secondary btn-sm"
            :disabled="isSearching"
            data-testid="search-reset-button"
            @click="handleResetSearch"
          >
            Reset
          </button>
        </div>
      </form>
    </div>

    <!-- Search Results Table Card -->
    <div class="card border mb-2">
      <div class="table-responsive">
        <table
          class="table table-hover align-middle mb-0"
          data-testid="search-results-table"
        >
          <thead>
            <tr>
              <th scope="col" style="width: 105px;">ID</th>
              <th scope="col">Title</th>
              <th scope="col" style="width: 150px;">Customer</th>
              <th scope="col" style="width: 140px;">Product</th>
              <th scope="col" style="width: 105px;">Status</th>
              <th scope="col" style="width: 140px;">Assignee</th>
              <th scope="col" style="width: 125px;">CreatedAt</th>
              <th scope="col" style="width: 130px;" class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="isSearching">
              <td colspan="8" class="text-center py-4 text-body-secondary">
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
                class="text-center py-4 text-body-secondary"
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
              <td>
                <router-link
                  :to="`/requests/${req.id}`"
                  class="font-monospace text-decoration-none fw-semibold"
                  style="font-size: 11.5px;"
                  data-testid="search-result-id-link"
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
                    style="max-width: 340px;"
                    data-testid="search-result-title-link"
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
                  data-testid="search-result-status-badge"
                >
                  {{ req.status }}
                </span>
              </td>

              <td>
                <span :class="req.ownerPersonId ? 'text-dark' : 'text-body-secondary fst-italic'" class="text-truncate d-inline-block" style="max-width: 135px;">
                  <i class="bi bi-person me-0.5 text-secondary" aria-hidden="true"></i>
                  {{ resolveAssigneeDisplay(req) }}
                </span>
              </td>

              <td class="text-body-secondary fs-11">
                {{ formatTimestamp(req.createdAt) }}
              </td>

              <td class="text-end" @click.stop>
                <div class="d-inline-flex gap-1">
                  <button
                    type="button"
                    class="btn btn-outline-secondary btn-sm py-0 px-1.5 fs-11"
                    data-testid="select-request-history-button"
                    @click.stop="loadSelectedRequestHistory(req)"
                  >
                    <i class="bi bi-clock-history me-0.5"></i>History
                  </button>
                  <button
                    type="button"
                    class="btn btn-outline-primary btn-sm py-0 px-1.5 fs-11"
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

      <!-- Compact Pagination Controls -->
      <div
        class="card-footer bg-white d-flex flex-wrap justify-content-between align-items-center gap-2 py-1 px-3"
        data-testid="search-pagination"
      >
        <div class="small text-body-secondary fs-11">
          Page <strong>{{ searchFilters.page }}</strong> of
          <strong>{{ Math.max(totalPages, 1) }}</strong>
          ({{ totalCount }} total {{ totalCount === 1 ? 'request' : 'requests' }})
        </div>

        <nav aria-label="Request search pagination">
          <ul class="pagination pagination-sm mb-0">
            <li class="page-item" :class="{ disabled: !canGoPrevious }">
              <button
                type="button"
                class="page-link py-0.5 px-2"
                :disabled="!canGoPrevious"
                data-testid="search-pagination-prev-button"
                @click="goToPage(searchFilters.page - 1)"
              >
                Prev
              </button>
            </li>
            <li class="page-item active" aria-current="page">
              <span class="page-link py-0.5 px-2">{{ searchFilters.page }}</span>
            </li>
            <li class="page-item" :class="{ disabled: !canGoNext }">
              <button
                type="button"
                class="page-link py-0.5 px-2"
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
    <div class="card border" data-testid="selected-request-history-panel">
      <div class="card-header py-1.5 px-3 d-flex flex-wrap justify-content-between align-items-center gap-2">
        <div class="d-flex align-items-center gap-1.5">
          <i class="bi bi-clock-history text-primary" aria-hidden="true"></i>
          <span class="fw-semibold">State History</span>
          <span v-if="selectedRequest" class="text-body-secondary small">
            &bull; {{ selectedRequest.title }} (<code>{{ selectedRequest.id }}</code>)
          </span>
        </div>

        <router-link
          v-if="selectedRequest"
          :to="`/requests/${selectedRequest.id}`"
          class="btn btn-outline-primary btn-sm py-0 px-2 fs-11"
          data-testid="history-panel-detail-link"
        >
          Open Request Detail
        </router-link>
      </div>

      <div class="card-body py-2 px-3">
        <div v-if="isLoadingHistory" class="text-center py-2 text-body-secondary small">
          <span
            class="spinner-border spinner-border-sm me-2"
            role="status"
            aria-hidden="true"
          ></span>
          Loading request state history...
        </div>

        <div
          v-else-if="!selectedRequest"
          class="text-center py-2 text-body-secondary small"
          data-testid="no-selected-request-message"
        >
          Select a request from the search results above to inspect its state transition history.
        </div>

        <div
          v-else-if="selectedRequestHistory.length === 0"
          class="text-center py-2 text-body-secondary small"
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
            class="list-group-item px-0 py-1.5"
            data-testid="selected-request-history-item"
          >
            <div class="d-flex flex-wrap justify-content-between align-items-center gap-2">
              <div class="d-flex align-items-center gap-1.5 flex-wrap">
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

              <span class="small text-body-secondary fs-11">
                {{ formatTimestamp(entry.assignedAtUtc || entry.timestamp || entry.createdAt) }}
              </span>
            </div>

            <div
              v-if="entry.notes"
              class="mt-1 small text-body-secondary ps-2 border-start border-2 border-secondary"
              style="white-space: pre-wrap;"
            >
              {{ entry.notes }}
            </div>
          </li>
        </ul>
      </div>
    </div>
  </section>
</template>
