<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'

import { httpClient } from '@/api/http'

/**
 * SCR-REQ-001: Request List Screen (Architecture §7, §8, §9 — FEAT-REQ-001..008, §19.4, §19.6, §20, §21).
 *
 * - Renders a Bootstrap 5 table listing operational requests with columns:
 *   ID, Title, Customer, Product, Status, Assignee, CreatedAt.
 * - Provides filter dropdowns above the table for `status` and `assignee`
 *   (populated from `GET /api/v1/organization/persons/active`).
 * - Provides a "Create Request" button navigating to `/requests/create` (`SCR-REQ-002`)
 *   and row/link navigation to `/requests/${id}` (`SCR-REQ-003`).
 * - Provides pagination controls (`Previous` / `Next`) below the table.
 * - Queries `GET /api/v1/requests` with filter & pagination query parameters via `httpClient`.
 */

export interface RequestListItem {
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
  items: RequestListItem[]
  requests?: RequestListItem[]
  totalCount: number
  page: number
  pageSize: number
  offset: number
  totalPages: number
}

export interface ActivePersonOption {
  id: string
  personId?: string
  firstName: string
  lastName: string
  fullName: string
  email: string
  status: string
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

const requests = ref<RequestListItem[]>([])
const activePersons = ref<ActivePersonOption[]>([])
const isLoading = ref(false)
const errorMessage = ref<string | null>(null)

const filters = reactive({
  status: '',
  assigneeId: '',
  page: 1,
  pageSize: 20,
})

const totalCount = ref(0)
const totalPages = ref(0)

const personNameById = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const person of activePersons.value) {
    const label = person.fullName || `${person.firstName} ${person.lastName}`.trim()
    map[person.id] = label
  }
  return map
})

const hasActiveFilters = computed(
  () => filters.status.trim().length > 0 || filters.assigneeId.trim().length > 0,
)

const canGoPrevious = computed(() => !isLoading.value && filters.page > 1)
const canGoNext = computed(
  () => !isLoading.value && totalPages.value > 0 && filters.page < totalPages.value,
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

function statusBadgeClass(status: string): string {
  switch (status.toUpperCase()) {
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

function priorityBadgeClass(priority: string): string {
  switch ((priority || '').toUpperCase()) {
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

function resolveCustomerDisplay(item: RequestListItem): string {
  if (item.customerName && item.customerName.trim().length > 0) {
    return item.customerCode ? `${item.customerName} (${item.customerCode})` : item.customerName
  }
  if (item.customerCode && item.customerCode.trim().length > 0) {
    return item.customerCode
  }
  return item.customerId ?? '—'
}

function resolveProductDisplay(item: RequestListItem): string {
  if (item.productName && item.productName.trim().length > 0) {
    return item.productCode ? `${item.productName} (${item.productCode})` : item.productName
  }
  if (item.productCode && item.productCode.trim().length > 0) {
    return item.productCode
  }
  return item.productId ?? '—'
}

function resolveAssigneeDisplay(item: RequestListItem): string {
  if (item.assigneeName && item.assigneeName.trim().length > 0) {
    return item.assigneeName
  }
  if (item.ownerName && item.ownerName.trim().length > 0) {
    return item.ownerName
  }
  const assigneeId = item.assigneePersonId ?? item.ownerPersonId
  if (assigneeId) {
    return personNameById.value[assigneeId] ?? assigneeId
  }
  return 'Unassigned'
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

async function loadActivePersons(): Promise<void> {
  try {
    const response = await httpClient.get<ActivePersonOption[]>('/organization/persons/active')
    activePersons.value = response.data
  } catch {
    // Non-fatal if active persons lookup fails on initial load
    activePersons.value = []
  }
}

async function loadRequests(): Promise<void> {
  isLoading.value = true
  errorMessage.value = null

  try {
    const queryParams: Record<string, string | number> = {
      page: filters.page,
      pageSize: filters.pageSize,
    }

    if (filters.status.trim().length > 0) {
      queryParams.status = filters.status.trim()
    }

    if (filters.assigneeId.trim().length > 0) {
      queryParams.assigneeId = filters.assigneeId.trim()
    }

    const response = await httpClient.get<PagedRequestGridResponse | RequestListItem[]>(
      '/requests',
      { params: queryParams },
    )

    if (Array.isArray(response.data)) {
      requests.value = response.data
      totalCount.value = response.data.length
      totalPages.value = response.data.length > 0 ? 1 : 0
    } else {
      const items = response.data.items ?? response.data.requests ?? []
      requests.value = items
      totalCount.value = response.data.totalCount ?? items.length
      filters.page = response.data.page ?? filters.page
      filters.pageSize = response.data.pageSize ?? filters.pageSize
      totalPages.value =
        response.data.totalPages ??
        (filters.pageSize > 0 ? Math.ceil(totalCount.value / filters.pageSize) : 0)
    }
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load requests.')
  } finally {
    isLoading.value = false
  }
}

async function handleFilterChange(): Promise<void> {
  filters.page = 1
  await loadRequests()
}

async function handleResetFilters(): Promise<void> {
  filters.status = ''
  filters.assigneeId = ''
  filters.page = 1
  await loadRequests()
}

async function goToPage(targetPage: number): Promise<void> {
  if (targetPage < 1 || (totalPages.value > 0 && targetPage > totalPages.value)) {
    return
  }
  filters.page = targetPage
  await loadRequests()
}

async function navigateToCreate(): Promise<void> {
  await router.push('/requests/create')
}

async function navigateToDetail(id: string): Promise<void> {
  await router.push(`/requests/${id}`)
}

onMounted(async () => {
  await Promise.all([loadActivePersons(), loadRequests()])
})
</script>

<template>
  <section data-screen-id="SCR-REQ-001">
    <!-- Compact Screen Header -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2">
        <h1 class="op-screen-title">
          <i class="bi bi-card-checklist text-primary" aria-hidden="true"></i>
          Operational Requests
        </h1>
        <span class="badge text-bg-light border text-secondary font-monospace">SCR-REQ-001</span>
        <span class="text-body-secondary small ms-1">
          &bull; {{ totalCount }} total
        </span>
      </div>

      <div class="d-flex align-items-center gap-2">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm"
          :disabled="isLoading"
          data-testid="refresh-requests-button"
          @click="loadRequests"
        >
          <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>
          Refresh
        </button>

        <router-link
          to="/requests/create"
          class="btn btn-primary btn-sm"
          data-testid="create-request-button"
          @click.prevent="navigateToCreate"
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
      data-testid="request-list-error-alert"
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

    <!-- Compact Inline Filter Toolbar -->
    <div class="op-toolbar">
      <div class="d-flex flex-wrap align-items-center gap-2 w-100">
        <!-- Status Filter -->
        <div class="d-flex align-items-center gap-1">
          <label for="statusFilterSelect" class="form-label mb-0 fs-11 text-nowrap">Status:</label>
          <select
            id="statusFilterSelect"
            v-model="filters.status"
            class="form-select form-select-sm"
            style="min-width: 130px; max-width: 170px;"
            :disabled="isLoading"
            data-testid="status-filter-select"
            @change="handleFilterChange"
          >
            <option value="">All Statuses</option>
            <option v-for="status in REQUEST_STATUSES" :key="status" :value="status">
              {{ status }}
            </option>
          </select>
        </div>

        <!-- Assignee Filter -->
        <div class="d-flex align-items-center gap-1">
          <label for="assigneeFilterSelect" class="form-label mb-0 fs-11 text-nowrap">Assignee:</label>
          <select
            id="assigneeFilterSelect"
            v-model="filters.assigneeId"
            class="form-select form-select-sm"
            style="min-width: 160px; max-width: 240px;"
            :disabled="isLoading"
            data-testid="assignee-filter-select"
            @change="handleFilterChange"
          >
            <option value="">All Assignees</option>
            <option
              v-for="person in activePersons"
              :key="person.id"
              :value="person.id"
            >
              {{ person.fullName }} ({{ person.email }})
            </option>
          </select>
        </div>

        <!-- Clear Button -->
        <div class="ms-auto">
          <button
            v-if="hasActiveFilters"
            type="button"
            class="btn btn-outline-secondary btn-sm"
            :disabled="isLoading"
            data-testid="clear-filters-button"
            @click="handleResetFilters"
          >
            <i class="bi bi-x-circle me-1" aria-hidden="true"></i>
            Clear Filters
          </button>
        </div>
      </div>
    </div>

    <!-- High-Density Requests Table -->
    <div class="card border">
      <div class="table-responsive">
        <table
          class="table table-hover align-middle mb-0"
          data-testid="requests-table"
        >
          <thead>
            <tr>
              <th scope="col" style="width: 110px;">ID</th>
              <th scope="col">Title</th>
              <th scope="col" style="width: 150px;">Customer</th>
              <th scope="col" style="width: 140px;">Product</th>
              <th scope="col" style="width: 110px;">Status</th>
              <th scope="col" style="width: 150px;">Assignee</th>
              <th scope="col" style="width: 130px;">CreatedAt</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="isLoading">
              <td colspan="7" class="text-center py-4 text-body-secondary">
                <span
                  class="spinner-border spinner-border-sm me-2"
                  role="status"
                  aria-hidden="true"
                ></span>
                Loading requests...
              </td>
            </tr>

            <tr v-else-if="requests.length === 0">
              <td
                colspan="7"
                class="text-center py-4 text-body-secondary"
                data-testid="empty-requests-row"
              >
                No operational requests found matching the current filters.
              </td>
            </tr>

            <tr
              v-for="req in requests"
              v-else
              :key="req.id"
              :data-request-id="req.id"
              class="cursor-pointer"
              style="cursor: pointer"
              data-testid="request-row"
              @click="navigateToDetail(req.id)"
            >
              <!-- ID Column -->
              <td>
                <router-link
                  :to="`/requests/${req.id}`"
                  class="font-monospace text-decoration-none fw-semibold"
                  style="font-size: 11.5px;"
                  data-testid="request-id-link"
                  @click.stop
                >
                  {{ req.id }}
                </router-link>
              </td>

              <!-- Title Column -->
              <td>
                <div class="d-flex align-items-center gap-1.5 flex-nowrap">
                  <router-link
                    :to="`/requests/${req.id}`"
                    class="fw-semibold text-decoration-none text-dark text-truncate"
                    style="max-width: 380px;"
                    data-testid="request-title-link"
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

              <!-- Customer Column -->
              <td>
                <span class="text-truncate d-inline-block" style="max-width: 145px;" :title="resolveCustomerDisplay(req)">
                  {{ resolveCustomerDisplay(req) }}
                </span>
              </td>

              <!-- Product Column -->
              <td>
                <span class="text-truncate d-inline-block" style="max-width: 135px;" :title="resolveProductDisplay(req)">
                  {{ resolveProductDisplay(req) }}
                </span>
              </td>

              <!-- Status Column -->
              <td>
                <span
                  class="badge"
                  :class="statusBadgeClass(req.status)"
                  data-testid="request-status-badge"
                >
                  {{ req.status }}
                </span>
              </td>

              <!-- Assignee Column -->
              <td>
                <span :class="req.ownerPersonId ? 'text-dark' : 'text-body-secondary fst-italic'" class="text-truncate d-inline-block" style="max-width: 145px;">
                  <i class="bi bi-person me-0.5 text-secondary" aria-hidden="true"></i>
                  {{ resolveAssigneeDisplay(req) }}
                </span>
              </td>

              <!-- CreatedAt Column -->
              <td class="text-body-secondary fs-11">
                {{ formatTimestamp(req.createdAt) }}
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Compact Pagination Controls Below Table -->
      <div
        class="card-footer bg-white d-flex flex-wrap justify-content-between align-items-center gap-2 py-1 px-3"
        data-testid="requests-pagination"
      >
        <div class="small text-body-secondary fs-11" data-testid="pagination-summary">
          Page <strong>{{ filters.page }}</strong> of
          <strong>{{ Math.max(totalPages, 1) }}</strong>
          ({{ totalCount }} total {{ totalCount === 1 ? 'request' : 'requests' }})
        </div>

        <nav aria-label="Request list pagination">
          <ul class="pagination pagination-sm mb-0">
            <li class="page-item" :class="{ disabled: !canGoPrevious }">
              <button
                type="button"
                class="page-link py-0.5 px-2"
                :disabled="!canGoPrevious"
                data-testid="pagination-prev-button"
                @click="goToPage(filters.page - 1)"
              >
                <i class="bi bi-chevron-left me-0.5" aria-hidden="true"></i>
                Prev
              </button>
            </li>

            <li class="page-item active" aria-current="page">
              <span class="page-link py-0.5 px-2" data-testid="pagination-current-page">
                {{ filters.page }}
              </span>
            </li>

            <li class="page-item" :class="{ disabled: !canGoNext }">
              <button
                type="button"
                class="page-link py-0.5 px-2"
                :disabled="!canGoNext"
                data-testid="pagination-next-button"
                @click="goToPage(filters.page + 1)"
              >
                Next
                <i class="bi bi-chevron-right ms-0.5" aria-hidden="true"></i>
              </button>
            </li>
          </ul>
        </nav>
      </div>
    </div>
  </section>
</template>
