<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, reactive, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'

import { httpClient } from '@/api/http'
import PostDetailModal from '@/views/PostDetailModal.vue'

/**
 * SCR-FEED-001: Operational Feed Screen
 * (Architecture §6, §7, §8 — UC-AWR-001..003, UC-FCOL-003..005, §9 — FEAT-AWR-001, FEAT-FCOL-001..003, FEAT-FCOL-005, §12, §19.4, §20, §21).
 *
 * - Renders a Bootstrap 5 card-based operational feed layout (`data-screen-id="SCR-FEED-001"`).
 * - Each feed item card displays:
 *   Title, Author/AuthorName, Customer/CustomerName, Product/ProductName, CreatedAt,
 *   CommentCount, ReactionCount (plus per-type reaction badges), ContentExcerpt/Summary,
 *   and LatestCommentExcerpt when present.
 * - Renders a Bootstrap 5 danger badge (`badge bg-danger`) on cards where `isException === true`,
 *   displaying `exceptionType` (`ESCALATION`, `REJECTION`, `STALLED`).
 * - Provides filter controls for Customer (`GET /api/v1/customers/active`),
 *   Product (`GET /api/v1/products/active`), and Exception-only toggle (`isException`).
 * - Operational posts are derived from request lifecycle events (CR-001 / SCR-POST-002 decommissioned).
 * - Feed item cards are clickable to open `PostDetailModal.vue` (`SCR-POST-001`) and provide
 *   direct navigation links to Request Detail (`/requests/${requestId}`) when a request reference
 *   is present (UC-FCOL-004).
 * - Provides pagination controls (`Previous` / `Next`) calling `GET /api/v1/feed` with
 *   `customerId`, `productId`, `isException`, `pageSize`, and `offset`.
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

const router = useRouter()

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

function formatTimestamp(value?: string | null): string {
  if (!value) {
    return '—'
  }
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }
  return parsed.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function resolveFeedItemKey(item: FeedItem): string {
  return item.feedItemId || item.id || item.postId
}

function resolvePostId(item: FeedItem): string {
  return (item.postId || item.id || item.feedItemId || '').trim()
}

function isSystemGenerated(item: FeedItem): boolean {
  const source = (item.postType || item.source || '').toUpperCase()
  return source === 'SYSTEM_GENERATED'
}

function resolveAuthorDisplay(item: FeedItem): string {
  const author = item.author ?? item.authorName
  if (author && author.trim().length > 0) {
    return author.trim()
  }
  if (isSystemGenerated(item)) {
    return 'SYSTEM'
  }
  if (item.authorPersonId) {
    return `Person #${item.authorPersonId.slice(0, 8)}`
  }
  return 'Operational User'
}

function resolveCustomerDisplay(item: FeedItem): string {
  const name = item.customer ?? item.customerName
  if (name && name.trim().length > 0) {
    return name.trim()
  }
  if (item.customerId && customerNameById.value[item.customerId]) {
    return customerNameById.value[item.customerId]
  }
  return item.customerId ? `Customer #${item.customerId.slice(0, 8)}` : '—'
}

function resolveProductDisplay(item: FeedItem): string {
  const name = item.product ?? item.productName
  if (name && name.trim().length > 0) {
    return name.trim()
  }
  if (item.productId && productNameById.value[item.productId]) {
    return productNameById.value[item.productId]
  }
  return item.productId ? `Product #${item.productId.slice(0, 8)}` : '—'
}

function resolveExcerpt(item: FeedItem): string {
  const excerpt = item.contentExcerpt ?? item.summary ?? ''
  return excerpt.trim()
}

function resolveReactionBreakdown(item: FeedItem): Record<string, number> {
  if (item.reactionCounts && typeof item.reactionCounts === 'object') {
    const entries: Record<string, number> = {}
    for (const [key, val] of Object.entries(item.reactionCounts)) {
      if (typeof val === 'number' && val > 0) {
        entries[key.toUpperCase()] = val
      }
    }
    return entries
  }

  if (item.reactionCountsJson && item.reactionCountsJson.trim().startsWith('{')) {
    try {
      const parsed = JSON.parse(item.reactionCountsJson) as Record<string, unknown>
      const entries: Record<string, number> = {}
      for (const [key, val] of Object.entries(parsed)) {
        if (typeof val === 'number' && val > 0) {
          entries[key.toUpperCase()] = val
        }
      }
      return entries
    } catch {
      return {}
    }
  }

  return {}
}

function resolveReactionCount(item: FeedItem): number {
  if (typeof item.reactionCount === 'number' && item.reactionCount >= 0) {
    return item.reactionCount
  }
  const breakdown = resolveReactionBreakdown(item)
  return Object.values(breakdown).reduce((sum, count) => sum + count, 0)
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

function resolveRequestDisplay(item: FeedItem): string {
  if (
    (item.referenceType ?? '').toUpperCase() === 'REQUEST' &&
    item.referenceDisplay &&
    item.referenceDisplay.trim().length > 0
  ) {
    return item.referenceDisplay.trim()
  }
  const reqId = resolveRequestId(item)
  return reqId ? `Request #${reqId.slice(0, 8)}` : 'View Request'
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

function handlePostUpdated(): void {
  void loadFeed()
}

function navigateToRequest(requestId: string): void {
  void router.push(`/requests/${requestId}`)
}

onMounted(async () => {
  await Promise.all([loadReferenceLookups(), loadFeed()])
})
</script>

<template>
  <section data-screen-id="SCR-FEED-001">
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

    <!-- Compact Inline Filter Toolbar (UC-FCOL-005, FEAT-FCOL-005) -->
    <div class="op-toolbar" data-testid="feed-filter-bar">
      <form class="d-flex flex-wrap align-items-center gap-2 w-100" @submit.prevent="applyFilters">
        <!-- Customer Filter -->
        <div class="d-flex align-items-center gap-1">
          <label for="feedFilterCustomer" class="form-label text-nowrap mb-0 fs-11">Customer:</label>
          <select
            id="feedFilterCustomer"
            v-model="filters.customerId"
            class="form-select form-select-sm"
            style="min-width: 140px; max-width: 200px;"
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
        <div class="d-flex align-items-center gap-1">
          <label for="feedFilterProduct" class="form-label text-nowrap mb-0 fs-11">Product:</label>
          <select
            id="feedFilterProduct"
            v-model="filters.productId"
            class="form-select form-select-sm"
            style="min-width: 130px; max-width: 180px;"
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
        <div class="d-flex align-items-center gap-1">
          <label for="feedFilterExceptionType" class="form-label text-nowrap mb-0 fs-11">Exception:</label>
          <select
            id="feedFilterExceptionType"
            v-model="filters.exceptionType"
            class="form-select form-select-sm"
            style="min-width: 105px;"
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
        <div class="form-check form-switch mb-0 d-flex align-items-center gap-1 ms-1">
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
            <span class="badge bg-danger px-1 py-0 me-0.5" style="font-size: 9px;">!</span>
            Exceptions Only
          </label>
        </div>

        <!-- Filter Actions -->
        <div class="d-flex align-items-center gap-1 ms-auto">
          <button
            type="submit"
            class="btn btn-primary btn-sm"
            :disabled="isLoadingFeed"
            data-testid="apply-feed-filters-btn"
          >
            <i class="bi bi-funnel-fill me-1" aria-hidden="true"></i>
            Filter
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
    <div v-else class="d-flex flex-column gap-1.5" data-testid="feed-card-list">
      <article
        v-for="item in feedItems"
        :key="resolveFeedItemKey(item)"
        class="op-feed-row"
        :class="{ 'is-exception': item.isException }"
        role="button"
        tabindex="0"
        :data-testid="`feed-card-${resolvePostId(item)}`"
        @click="openPostDetailModal(item)"
        @keydown.enter="openPostDetailModal(item)"
      >
        <!-- Row 1: Key Metadata Badges + Title + Author + Timestamp -->
        <div class="d-flex flex-wrap align-items-center justify-content-between gap-1.5">
          <div class="d-flex flex-wrap align-items-center gap-1.5 min-w-0">
            <!-- Exception Badge -->
            <span
              v-if="item.isException"
              class="badge bg-danger"
              data-testid="feed-card-exception-badge"
            >
              <i class="bi bi-exclamation-triangle-fill me-1" aria-hidden="true"></i>
              {{ item.exceptionType || 'EXCEPTION' }}
            </span>

            <!-- Post Source Badge -->
            <span
              class="badge"
              :class="
                isSystemGenerated(item)
                  ? 'bg-info bg-opacity-10 text-info border border-info border-opacity-25'
                  : 'bg-primary bg-opacity-10 text-primary border border-primary border-opacity-25'
              "
            >
              {{ isSystemGenerated(item) ? 'SYS' : 'POST' }}
            </span>

            <!-- Item Title -->
            <span class="fw-semibold text-dark text-truncate" style="max-width: 420px;" data-testid="feed-card-title">
              {{ item.title }}
            </span>

            <!-- Customer Badge -->
            <span
              v-if="item.customerId || item.customerName || item.customer"
              class="badge bg-light text-dark border text-truncate"
              style="max-width: 140px;"
              data-testid="feed-card-customer"
              :title="resolveCustomerDisplay(item)"
            >
              <i class="bi bi-building me-1 text-secondary" aria-hidden="true"></i>
              {{ resolveCustomerDisplay(item) }}
            </span>

            <!-- Product Badge -->
            <span
              v-if="item.productId || item.productName || item.product"
              class="badge bg-light text-dark border text-truncate"
              style="max-width: 130px;"
              data-testid="feed-card-product"
              :title="resolveProductDisplay(item)"
            >
              <i class="bi bi-box-seam me-1 text-secondary" aria-hidden="true"></i>
              {{ resolveProductDisplay(item) }}
            </span>
          </div>

          <!-- Author and CreatedAt Timestamp -->
          <div class="d-flex align-items-center gap-2 ms-auto flex-shrink-0">
            <span class="text-body-secondary fs-11" data-testid="feed-card-author">
              <i class="bi bi-person me-0.5" aria-hidden="true"></i>
              {{ resolveAuthorDisplay(item) }}
            </span>
            <span class="text-body-secondary fs-11" data-testid="feed-card-created-at">
              <i class="bi bi-clock me-0.5" aria-hidden="true"></i>
              {{ formatTimestamp(item.createdAt) }}
            </span>
          </div>
        </div>

        <!-- Row 2: Content Excerpt (if present) -->
        <div
          v-if="resolveExcerpt(item)"
          class="text-body-secondary text-truncate fs-12 ps-0.5"
          data-testid="feed-card-excerpt"
        >
          {{ resolveExcerpt(item) }}
        </div>

        <!-- Row 3: Latest Comment Preview (if present) -->
        <div
          v-if="item.latestCommentExcerpt"
          class="bg-light rounded px-2 py-0.5 text-truncate fs-11 border-start border-2 border-primary text-secondary"
          data-testid="feed-card-latest-comment"
        >
          <i class="bi bi-chat-quote me-1 text-secondary" aria-hidden="true"></i>
          <span class="fw-semibold me-1">Latest:</span>
          <span>{{ item.latestCommentExcerpt }}</span>
        </div>

        <!-- Row 4: Counts & Inline Actions -->
        <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 pt-0.5">
          <div class="d-flex flex-wrap align-items-center gap-1.5">
            <!-- CommentCount -->
            <span
              class="badge text-bg-light border text-secondary"
              data-testid="feed-card-comment-count"
            >
              <i class="bi bi-chat-left-text me-1" aria-hidden="true"></i>
              {{ item.commentCount ?? 0 }} {{ (item.commentCount ?? 0) === 1 ? 'Comment' : 'Comments' }}
            </span>

            <!-- ReactionCount -->
            <span
              class="badge text-bg-light border text-secondary"
              data-testid="feed-card-reaction-count"
            >
              <i class="bi bi-hand-thumbs-up me-1" aria-hidden="true"></i>
              {{ resolveReactionCount(item) }} {{ resolveReactionCount(item) === 1 ? 'Reaction' : 'Reactions' }}
            </span>

            <!-- Individual Reaction Type Pills -->
            <span
              v-for="(count, rType) in resolveReactionBreakdown(item)"
              :key="rType"
              class="badge text-bg-light border text-muted fs-11 d-none d-md-inline-block"
            >
              {{ rType }}: {{ count }}
            </span>
          </div>

          <!-- Quick Navigation Actions -->
          <div class="d-flex align-items-center gap-1 ms-auto" @click.stop>
            <RouterLink
              v-if="resolveRequestId(item)"
              :to="`/requests/${resolveRequestId(item)}`"
              class="btn btn-sm btn-outline-primary py-0 px-1.5 fs-11"
              data-testid="feed-card-request-link"
              @click.stop="navigateToRequest(resolveRequestId(item)!)"
            >
              <i class="bi bi-box-arrow-up-right me-0.5" aria-hidden="true"></i>
              {{ resolveRequestDisplay(item) }}
            </RouterLink>

            <RouterLink
              v-if="resolveWorkPackageId(item)"
              :to="`/work-packages/${resolveWorkPackageId(item)}`"
              class="btn btn-sm btn-outline-secondary py-0 px-1.5 fs-11"
              data-testid="feed-card-work-package-link"
            >
              <i class="bi bi-kanban me-0.5" aria-hidden="true"></i>
              WP
            </RouterLink>

            <button
              type="button"
              class="btn btn-sm btn-light border py-0 px-1.5 fs-11 text-secondary"
              data-testid="feed-card-open-modal-btn"
              @click.stop="openPostDetailModal(item)"
            >
              <i class="bi bi-chat-dots me-0.5" aria-hidden="true"></i>
              View Thread
            </button>
          </div>
        </div>
      </article>
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
