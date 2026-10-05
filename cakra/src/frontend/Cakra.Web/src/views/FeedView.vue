<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, reactive, ref } from 'vue'
import { RouterLink } from 'vue-router'

import { httpClient } from '@/api/http'
import CreateRequestModal, { type CreatedRequestResponse } from '@/components/CreateRequestModal.vue'
import FeedTimelineCard from '@/components/FeedTimelineCard.vue'
import PostDetailModal from '@/views/PostDetailModal.vue'

/**
 * SCR-FEED-001: Operational Feed Screen (CR-012)
 * (Architecture §6, §7, §8 — UC-AWR-001..003, UC-FCOL-003..005, §9 — FEAT-AWR-001, FEAT-FCOL-001..003, FEAT-FCOL-005, §12, §19.4, §20, §21).
 *
 * - Renders a timeline-style operational feed layout (`data-screen-id="SCR-FEED-001"`).
 * - Delegates per-item timeline card rendering, inline comments, and reaction palette to `FeedTimelineCard.vue`.
 * - Provides filter controls for Customer (`GET /api/v1/customers/active`),
 *   Product (`GET /api/v1/products/active`), and Exception-only toggle (`isException`).
 * - Provides search, exception type filtering, and pagination controls (`Previous` / `Next`).
 * - Coordinates `PostDetailModal.vue` (`SCR-POST-001`) and `CreateRequestModal.vue` (`SCR-REQ-002`).
 */

export interface FeedItem {
  id?: string
  feedItemId?: string
  postId: string
  authorPersonId?: string | null
  authorName?: string | null
  author?: string | null
  postType?: string | null
  source?: string | null
  title: string
  contentExcerpt?: string | null
  summary?: string | null
  status?: string
  visibility?: string
  isException: boolean
  exceptionType?: string | null
  referenceType?: string | null
  referenceId?: string | null
  referenceDisplay?: string | null
  customerId?: string | null
  customerName?: string | null
  customer?: string | null
  productId?: string | null
  productName?: string | null
  product?: string | null
  requestId?: string | null
  workPackageId?: string | null
  commentCount: number
  latestCommentExcerpt?: string | null
  reactionCountsJson?: string | null
  reactionCounts?: Record<string, number> | null
  reactionCount?: number
  createdAt: string
  updatedAt?: string | null
  lastActivityAt?: string | null
}

export interface FeedPageResponse {
  items?: FeedItem[]
  feedItems?: FeedItem[]
  totalCount?: number
  page?: number
  pageSize?: number
  offset?: number
  totalPages?: number
  hasMore?: boolean
}

export interface ActiveCustomerOption {
  id: string
  customerId?: string
  customerCode?: string
  code?: string
  customerName?: string
  name?: string
  status?: string
}

export interface ActiveProductOption {
  id: string
  productId?: string
  code?: string
  productCode?: string
  name?: string
  productName?: string
  status?: string
}

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const EXCEPTION_TYPES = ['ESCALATION', 'REJECTION', 'STALLED'] as const

const feedItems = ref<FeedItem[]>([])
const activeCustomers = ref<ActiveCustomerOption[]>([])
const activeProducts = ref<ActiveProductOption[]>([])

const isLoadingFeed = ref<boolean>(false)
const isLoadingLookups = ref<boolean>(false)

const errorMessage = ref<string | null>(null)

// Filter state for GET /api/v1/feed
const filters = reactive({
  customerId: '',
  productId: '',
  isException: false,
  exceptionType: '',
  searchTerm: '',
  pageSize: 20,
  offset: 0,
})

const totalCount = ref<number>(0)
const totalPages = ref<number>(0)
const hasMore = ref<boolean>(false)

// Create Request Modal state (CR-003, SCR-FEED-001 / SCR-REQ-002)
const showCreateRequestModal = ref<boolean>(false)
const createdRequestAlert = ref<{ id: string; title: string } | null>(null)

// Post Detail Modal state (SCR-POST-001)
const selectedPostId = ref<string | null>(null)
const selectedRequestId = ref<string | null>(null)
const selectedWorkPackageId = ref<string | null>(null)
const isPostModalOpen = ref<boolean>(false)

const customerNameById = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const customer of activeCustomers.value) {
    const id = customer.id || customer.customerId || ''
    const name = customer.customerName || customer.name || ''
    const code = customer.customerCode || customer.code || ''
    if (id) {
      map[id] = code ? `${name} (${code})` : name
    }
  }
  return map
})

const productNameById = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const product of activeProducts.value) {
    const id = product.id || product.productId || ''
    const name = product.name || product.productName || ''
    const code = product.code || product.productCode || ''
    if (id) {
      map[id] = code ? `${name} (${code})` : name
    }
  }
  return map
})

const currentPage = computed<number>(() =>
  filters.pageSize > 0 ? Math.floor(filters.offset / filters.pageSize) + 1 : 1,
)

const computedTotalPages = computed<number>(() => {
  if (totalPages.value > 0) {
    return totalPages.value
  }
  if (totalCount.value > 0 && filters.pageSize > 0) {
    return Math.ceil(totalCount.value / filters.pageSize)
  }
  return feedItems.value.length > 0 ? 1 : 0
})

const hasActiveFilters = computed<boolean>(
  () =>
    filters.customerId.trim().length > 0 ||
    filters.productId.trim().length > 0 ||
    filters.isException ||
    filters.exceptionType.trim().length > 0 ||
    filters.searchTerm.trim().length > 0,
)

const exceptionCount = computed<number>(() =>
  feedItems.value.filter((item) => item.isException).length,
)

const canGoPrevious = computed<boolean>(() => !isLoadingFeed.value && filters.offset > 0)

const canGoNext = computed<boolean>(() => {
  if (isLoadingFeed.value) {
    return false
  }
  if (hasMore.value) {
    return true
  }
  return filters.offset + feedItems.value.length < totalCount.value
})

const showingRangeStart = computed<number>(() =>
  feedItems.value.length === 0 ? 0 : filters.offset + 1,
)

const showingRangeEnd = computed<number>(() => filters.offset + feedItems.value.length)

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
  if (err instanceof Error && err.message) {
    return err.message
  }
  return fallback
}

function resolveFeedItemKey(item: FeedItem): string {
  return item.feedItemId || item.id || item.postId
}

function resolvePostId(item: FeedItem): string {
  return (item.postId || item.id || item.feedItemId || '').trim()
}

function resolveRequestId(item: FeedItem): string | null {
  if (item.requestId && item.requestId.trim().length > 0) {
    return item.requestId.trim()
  }
  if (
    (item.referenceType ?? '').toUpperCase() === 'REQUEST' &&
    item.referenceId &&
    item.referenceId.trim().length > 0
  ) {
    return item.referenceId.trim()
  }
  return null
}

function resolveWorkPackageId(item: FeedItem): string | null {
  if (item.workPackageId && item.workPackageId.trim().length > 0) {
    return item.workPackageId.trim()
  }
  if (
    (item.referenceType ?? '').toUpperCase() === 'WORK_PACKAGE' &&
    item.referenceId &&
    item.referenceId.trim().length > 0
  ) {
    return item.referenceId.trim()
  }
  return null
}

/**
 * Loads active customers (`GET /api/v1/customers/active`) and active products (`GET /api/v1/products/active`)
 * for filter selectors.
 */
async function loadReferenceLookups(): Promise<void> {
  isLoadingLookups.value = true
  try {
    const [customersResult, productsResult] = await Promise.allSettled([
      httpClient.get<ActiveCustomerOption[]>('/customers/active'),
      httpClient.get<ActiveProductOption[]>('/products/active'),
    ])

    if (customersResult.status === 'fulfilled' && Array.isArray(customersResult.value.data)) {
      activeCustomers.value = customersResult.value.data
    }

    if (productsResult.status === 'fulfilled' && Array.isArray(productsResult.value.data)) {
      activeProducts.value = productsResult.value.data
    }
  } finally {
    isLoadingLookups.value = false
  }
}

/**
 * Queries `GET /api/v1/feed` with filter & pagination query parameters (`customerId`, `productId`,
 * `isException`, `pageSize`, `offset`).
 */
async function loadFeed(): Promise<void> {
  isLoadingFeed.value = true
  errorMessage.value = null

  try {
    const params: Record<string, string | number | boolean> = {
      pageSize: filters.pageSize,
      offset: filters.offset,
    }

    if (filters.customerId.trim().length > 0) {
      params.customerId = filters.customerId.trim()
    }

    if (filters.productId.trim().length > 0) {
      params.productId = filters.productId.trim()
    }

    if (filters.isException) {
      params.isException = true
    }

    if (filters.exceptionType.trim().length > 0) {
      params.exceptionType = filters.exceptionType.trim()
      params.isException = true
    }

    if (filters.searchTerm.trim().length > 0) {
      params.searchTerm = filters.searchTerm.trim()
    }

    const response = await httpClient.get<FeedPageResponse | FeedItem[]>('/feed', {
      params,
    })

    const data = response.data
    if (Array.isArray(data)) {
      feedItems.value = data
      totalCount.value = data.length
      totalPages.value = data.length > 0 ? 1 : 0
      hasMore.value = false
    } else {
      const items = data.items ?? data.feedItems ?? []
      feedItems.value = items
      totalCount.value = typeof data.totalCount === 'number' ? data.totalCount : items.length
      totalPages.value = typeof data.totalPages === 'number' ? data.totalPages : 0
      hasMore.value =
        typeof data.hasMore === 'boolean'
          ? data.hasMore
          : filters.offset + items.length < totalCount.value
    }
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(
      err,
      'Failed to load the operational feed. Please try again.',
    )
  } finally {
    isLoadingFeed.value = false
  }
}

function applyFilters(): void {
  filters.offset = 0
  void loadFeed()
}

function clearFilters(): void {
  filters.customerId = ''
  filters.productId = ''
  filters.isException = false
  filters.exceptionType = ''
  filters.searchTerm = ''
  filters.offset = 0
  void loadFeed()
}

function handleExceptionToggleChange(): void {
  if (!filters.isException) {
    filters.exceptionType = ''
  }
  applyFilters()
}

function handleExceptionTypeChange(): void {
  if (filters.exceptionType.trim().length > 0) {
    filters.isException = true
  }
  applyFilters()
}

function goToPreviousPage(): void {
  if (!canGoPrevious.value) {
    return
  }
  filters.offset = Math.max(0, filters.offset - filters.pageSize)
  void loadFeed()
}

function goToNextPage(): void {
  if (!canGoNext.value) {
    return
  }
  filters.offset += filters.pageSize
  void loadFeed()
}

/**
 * Opens the Post Detail Modal (`SCR-POST-001`) when clicking a feed item card.
 */
function openPostDetailModal(item: FeedItem): void {
  const postId = resolvePostId(item)
  if (!postId) {
    return
  }
  selectedPostId.value = postId
  selectedRequestId.value = resolveRequestId(item)
  selectedWorkPackageId.value = resolveWorkPackageId(item)
  isPostModalOpen.value = true
}

function closePostDetailModal(): void {
  isPostModalOpen.value = false
  selectedPostId.value = null
  selectedRequestId.value = null
  selectedWorkPackageId.value = null
}

/**
 * Handles successful Request creation from `CreateRequestModal.vue` (CR-003, TD-003, TD-004).
 * Closes modal, immediately refreshes operational feed to display newly projected post at top,
 * and sets success alert banner.
 */
function handleRequestSaved(createdRequest: CreatedRequestResponse): void {
  showCreateRequestModal.value = false
  const reqId = createdRequest.id || createdRequest.requestId || ''
  const reqTitle = createdRequest.title || ''
  createdRequestAlert.value = {
    id: reqId,
    title: reqTitle,
  }
  errorMessage.value = null
  void loadFeed()
}

function handlePostUpdated(): void {
  void loadFeed()
}

onMounted(async () => {
  await Promise.all([loadReferenceLookups(), loadFeed()])
})
</script>

<template>
  <section data-screen-id="SCR-FEED-001">
    <div class="row g-3">
      <!-- Left Column: Operational Feed Stream (~67% / 8 cols on xl+) -->
      <div class="col-12 col-xl-8 col-xxl-8">
        <!-- Compact Screen Header -->
        <div class="op-screen-header">
          <div class="d-flex align-items-center gap-2">
            <h1 class="op-screen-title">
              <i class="bi bi-activity text-primary" aria-hidden="true"></i>
              Operational Feed
            </h1>
            <span class="badge text-bg-light border text-secondary font-monospace">SCR-FEED-001</span>
            <span class="text-body-secondary small ms-1">
              &bull; {{ totalCount }} events
            </span>
          </div>

          <div class="d-flex align-items-center gap-2">
            <button
              type="button"
              class="btn btn-primary btn-sm"
              data-testid="create-request-btn"
              @click="showCreateRequestModal = true"
            >
              <i class="bi bi-plus-lg me-1" aria-hidden="true"></i>
              + Create Request
            </button>
            <button
              type="button"
              class="btn btn-outline-secondary btn-sm"
              :disabled="isLoadingFeed"
              data-testid="refresh-feed-btn"
              @click="loadFeed"
            >
              <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>
              Refresh
            </button>
          </div>
        </div>

        <!-- Error Alert -->
        <div
          v-if="errorMessage"
          class="alert alert-danger alert-dismissible fade show d-flex align-items-center justify-content-between py-1 px-2 mb-2"
          role="alert"
          data-testid="feed-error-alert"
        >
          <div>
            <i class="bi bi-exclamation-octagon-fill me-1" aria-hidden="true"></i>
            <span>{{ errorMessage }}</span>
          </div>
          <button
            type="button"
            class="btn-close py-1 px-2"
            aria-label="Close"
            @click="errorMessage = null"
          ></button>
        </div>

        <!-- Request Created Success Alert (TD-004) -->
        <div
          v-if="createdRequestAlert"
          class="alert alert-success alert-dismissible fade show"
          role="alert"
          data-testid="request-created-success-alert"
        >
          <i class="bi bi-check-circle-fill me-2" aria-hidden="true"></i>
          Request <strong>{{ createdRequestAlert.id }}</strong> recorded successfully. Post published to feed.
          <router-link :to="`/requests/${createdRequestAlert.id}`" class="alert-link ms-2">View Request Detail &rarr;</router-link>
          <button type="button" class="btn-close" aria-label="Close" @click="createdRequestAlert = null"></button>
        </div>

        <!-- Loading State -->
        <div
          v-if="isLoadingFeed && feedItems.length === 0"
          class="card border-0 shadow-xs my-2"
          data-testid="feed-loading-state"
        >
          <div class="card-body py-4 text-center">
            <div class="spinner-border spinner-border-sm text-primary mb-2" role="status">
              <span class="visually-hidden">Loading operational feed...</span>
            </div>
            <p class="text-body-secondary small mb-0">Loading operational feed stream...</p>
          </div>
        </div>

        <!-- Empty State -->
        <div
          v-else-if="!isLoadingFeed && feedItems.length === 0"
          class="card border-0 shadow-xs my-2"
          data-testid="feed-empty-state"
        >
          <div class="card-body py-4 text-center">
            <i class="bi bi-inbox text-body-secondary fs-4 d-block mb-1" aria-hidden="true"></i>
            <h2 class="h6 fw-semibold mb-1">No Operational Feed Items Found</h2>
            <p class="text-body-secondary small mb-2">
              <template v-if="hasActiveFilters">
                No visible feed items match your active Customer, Product, or Exception filter criteria.
              </template>
              <template v-else>
                No operational posts or system events have been recorded in the feed yet.
              </template>
            </p>
            <div v-if="hasActiveFilters" class="d-flex justify-content-center gap-2">
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm"
                @click="clearFilters"
              >
                Clear Filters
              </button>
            </div>
          </div>
        </div>

        <!-- High-Density Operational Feed Stream -->
        <div v-else class="d-flex flex-column gap-2" data-testid="feed-card-list">
          <FeedTimelineCard
            v-for="item in feedItems"
            :key="resolveFeedItemKey(item)"
            :item="item"
            :customer-name-map="customerNameById"
            :product-name-map="productNameById"
            @open-modal="openPostDetailModal"
            @post-updated="handlePostUpdated"
          />
        </div>

    <!-- Compact Pagination Controls -->
    <nav
      class="d-flex flex-wrap justify-content-between align-items-center gap-2 mt-2 pt-1 border-top"
      aria-label="Operational feed pagination"
      data-testid="feed-pagination"
    >
      <div class="small text-body-secondary fs-11">
        <template v-if="totalCount > 0">
          Showing <strong>{{ showingRangeStart }}</strong>–<strong>{{ showingRangeEnd }}</strong> of
          <strong>{{ totalCount }}</strong> items (Page {{ currentPage }}
          <template v-if="computedTotalPages > 0">of {{ computedTotalPages }}</template>)
        </template>
        <template v-else>
          Showing 0 feed items
        </template>
      </div>

      <div class="btn-group btn-group-sm" role="group" aria-label="Pagination buttons">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm py-0.5 px-2"
          :disabled="!canGoPrevious"
          data-testid="feed-pagination-prev"
          @click="goToPreviousPage"
        >
          <i class="bi bi-chevron-left me-0.5" aria-hidden="true"></i>
          Prev
        </button>
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm py-0.5 px-2"
          :disabled="!canGoNext"
          data-testid="feed-pagination-next"
          @click="goToNextPage"
        >
          Next
          <i class="bi bi-chevron-right ms-0.5" aria-hidden="true"></i>
        </button>
      </div>
    </nav>
      </div>

      <!-- Right Column: Sticky Contextual Panel (~33% / 4 cols on xl+) -->
      <div class="col-12 col-xl-4 col-xxl-4">
        <div class="op-feed-sticky-panel">
          <!-- Card 1: Feed Filter Toolbar Card -->
          <div class="card shadow-xs" data-testid="feed-filter-bar">
            <div class="card-header d-flex align-items-center justify-content-between py-1.5 px-3">
              <span class="fw-semibold small">
                <i class="bi bi-funnel me-1 text-primary" aria-hidden="true"></i>
                Feed Filters
              </span>
              <span
                v-if="hasActiveFilters"
                class="badge bg-primary bg-opacity-10 text-primary border border-primary border-opacity-25 fs-11"
              >
                Active
              </span>
            </div>
            <div class="card-body py-2 px-3">
              <form class="d-flex flex-column gap-2" @submit.prevent="applyFilters">
                <!-- Search Term -->
                <div>
                  <label for="feedFilterSearch" class="form-label fs-11 mb-1">Search Feed:</label>
                  <div class="input-group input-group-sm">
                    <span class="input-group-text bg-light text-muted">
                      <i class="bi bi-search" aria-hidden="true"></i>
                    </span>
                    <input
                      id="feedFilterSearch"
                      v-model="filters.searchTerm"
                      type="text"
                      class="form-control form-control-sm"
                      placeholder="Search title, excerpt..."
                      data-testid="feed-filter-search"
                    />
                  </div>
                </div>

                <!-- Customer Filter -->
                <div>
                  <label for="feedFilterCustomer" class="form-label fs-11 mb-1">Customer:</label>
                  <select
                    id="feedFilterCustomer"
                    v-model="filters.customerId"
                    class="form-select form-select-sm"
                    data-testid="feed-filter-customer"
                    @change="applyFilters"
                  >
                    <option value="">All Customers</option>
                    <option
                      v-for="customer in activeCustomers"
                      :key="customer.id || customer.customerId"
                      :value="customer.id || customer.customerId"
                    >
                      {{ customer.customerName || customer.name }}
                      {{
                        customer.customerCode || customer.code
                          ? `(${customer.customerCode || customer.code})`
                          : ''
                      }}
                    </option>
                  </select>
                </div>

                <!-- Product Filter -->
                <div>
                  <label for="feedFilterProduct" class="form-label fs-11 mb-1">Product:</label>
                  <select
                    id="feedFilterProduct"
                    v-model="filters.productId"
                    class="form-select form-select-sm"
                    data-testid="feed-filter-product"
                    @change="applyFilters"
                  >
                    <option value="">All Products</option>
                    <option
                      v-for="product in activeProducts"
                      :key="product.id || product.productId"
                      :value="product.id || product.productId"
                    >
                      {{ product.name || product.productName }}
                      {{
                        product.code || product.productCode
                          ? `(${product.code || product.productCode})`
                          : ''
                      }}
                    </option>
                  </select>
                </div>

                <!-- Exception Type Filter -->
                <div>
                  <label for="feedFilterExceptionType" class="form-label fs-11 mb-1">Exception Type:</label>
                  <select
                    id="feedFilterExceptionType"
                    v-model="filters.exceptionType"
                    class="form-select form-select-sm"
                    data-testid="feed-filter-exception-type"
                    @change="handleExceptionTypeChange"
                  >
                    <option value="">Any Type</option>
                    <option v-for="exType in EXCEPTION_TYPES" :key="exType" :value="exType">
                      {{ exType }}
                    </option>
                  </select>
                </div>

                <!-- Exceptions-Only Toggle -->
                <div class="form-check form-switch pt-1 mb-0">
                  <input
                    id="feedFilterExceptionsOnly"
                    v-model="filters.isException"
                    class="form-check-input"
                    type="checkbox"
                    role="switch"
                    data-testid="feed-filter-exception"
                    @change="handleExceptionToggleChange"
                  />
                  <label class="form-check-label fw-semibold fs-11 text-nowrap" for="feedFilterExceptionsOnly">
                    <span class="badge bg-danger px-1 py-0 me-1" style="font-size: 9px;">!</span>
                    Exceptions Only
                  </label>
                </div>

                <!-- Filter Actions -->
                <div class="d-flex align-items-center gap-2 pt-2 border-top">
                  <button
                    type="submit"
                    class="btn btn-primary btn-sm flex-fill"
                    :disabled="isLoadingFeed"
                    data-testid="apply-feed-filters-btn"
                  >
                    <i class="bi bi-funnel-fill me-1" aria-hidden="true"></i>
                    Apply Filters
                  </button>
                  <button
                    v-if="hasActiveFilters"
                    type="button"
                    class="btn btn-outline-secondary btn-sm"
                    :disabled="isLoadingFeed"
                    data-testid="clear-feed-filters-btn"
                    @click="clearFilters"
                  >
                    Reset
                  </button>
                </div>
              </form>
            </div>
          </div>

          <!-- Card 2: Summary Metrics / Active Status -->
          <div class="card shadow-xs">
            <div class="card-header py-1.5 px-3">
              <span class="fw-semibold small">
                <i class="bi bi-activity me-1 text-primary" aria-hidden="true"></i>
                Operational Stream Summary
              </span>
            </div>
            <div class="card-body py-2 px-3">
              <div class="d-flex flex-column gap-2">
                <div class="d-flex justify-content-between align-items-center border-bottom pb-1.5">
                  <span class="text-body-secondary fs-11">Total Stream Events</span>
                  <span class="fw-bold fs-12 text-dark">{{ totalCount }}</span>
                </div>
                <div class="d-flex justify-content-between align-items-center border-bottom pb-1.5">
                  <span class="text-body-secondary fs-11">Exceptions on Page</span>
                  <span
                    class="badge"
                    :class="exceptionCount > 0 ? 'bg-danger' : 'bg-light text-secondary border'"
                  >
                    {{ exceptionCount }}
                  </span>
                </div>
                <div class="d-flex justify-content-between align-items-center border-bottom pb-1.5">
                  <span class="text-body-secondary fs-11">Stream Filter Status</span>
                  <span
                    class="badge"
                    :class="hasActiveFilters ? 'bg-info bg-opacity-10 text-info border border-info border-opacity-25' : 'bg-light text-secondary border'"
                  >
                    {{ hasActiveFilters ? 'Filtered' : 'Unfiltered' }}
                  </span>
                </div>
                <div class="d-flex justify-content-between align-items-center">
                  <span class="text-body-secondary fs-11">Current Page</span>
                  <span class="fs-11 text-dark fw-semibold">
                    {{ currentPage }} <span class="text-muted">/</span> {{ computedTotalPages > 0 ? computedTotalPages : 1 }}
                  </span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Create Request Modal (SCR-FEED-001 / SCR-REQ-002) -->
    <CreateRequestModal
      :show="showCreateRequestModal"
      @close="showCreateRequestModal = false"
      @saved="handleRequestSaved"
    />

    <!-- Post Detail Modal (SCR-POST-001) -->
    <PostDetailModal
      :post-id="selectedPostId"
      :show="isPostModalOpen"
      :initial-request-id="selectedRequestId"
      :initial-work-package-id="selectedWorkPackageId"
      @close="closePostDetailModal"
      @updated="handlePostUpdated"
    />
  </section>
</template>
