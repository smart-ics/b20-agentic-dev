<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'

import { httpClient } from '@/api/http'
import CreateRequestModal, { type CreatedRequestResponse } from '@/components/CreateRequestModal.vue'
import FeedTimelineCard from '@/components/FeedTimelineCard.vue'
import PostDetailModal from '@/views/PostDetailModal.vue'

/**
 * SCR-FEED-001: Operational Feed Screen (CR-013, CR-014)
 * (Architecture §6, §7, §8 — UC-AWR-001..003, UC-FCOL-003..005, §9 — FEAT-AWR-001, FEAT-FCOL-001..003, FEAT-FCOL-005, §12, §19.4, §20, §21; CR-014 TD-006).
 *
 * - Continuous infinite scroll timeline stream with native IntersectionObserver sentinel.
 * - Incremental batch appending with Set-based ID deduplication (`postId` / `feedItemId`).
 * - Seamless stream prepending for newly created requests without scroll position displacement.
 * - End-of-stream milestone ("You're all caught up") and inline error recovery retry.
 * - Dedicated full-width universal search textbox on top of feed stream (300ms debouncing, URL sync, instant clear).
 * - Contextual sidebar summary metrics (Total Stream Events, Exceptions Loaded, Stream Filter Status).
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

const route = useRoute()
const router = useRouter()

const feedItems = ref<FeedItem[]>([])
const activeCustomers = ref<ActiveCustomerOption[]>([])
const activeProducts = ref<ActiveProductOption[]>([])

const isLoadingFeed = ref<boolean>(false)
const isLoadingMore = ref<boolean>(false)
const isLoadingLookups = ref<boolean>(false)

const errorMessage = ref<string | null>(null)
const loadMoreError = ref<string | null>(null)

// Universal search and pagination state for GET /api/v1/feed
const searchTerm = ref<string>('')
const pageSize = ref<number>(20)
const offset = ref<number>(0)

const totalCount = ref<number>(0)
const hasMore = ref<boolean>(false)

// Sentinel ref for IntersectionObserver
const sentinelRef = ref<HTMLDivElement | null>(null)
let observer: IntersectionObserver | null = null
let searchDebounceTimer: ReturnType<typeof setTimeout> | null = null

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

const hasActiveFilters = computed<boolean>(
  () => searchTerm.value.trim().length > 0,
)

const exceptionCount = computed<number>(() =>
  feedItems.value.filter((item) => item.isException).length,
)

const canLoadMore = computed<boolean>(
  () =>
    !isLoadingFeed.value &&
    !isLoadingMore.value &&
    hasMore.value &&
    !loadMoreError.value,
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
  if (err instanceof Error && err.message) {
    return err.message
  }
  return fallback
}

function resolveFeedItemKey(item: FeedItem): string {
  return (item.feedItemId || item.id || item.postId || '').trim()
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

function disconnectObserver(): void {
  if (observer) {
    observer.disconnect()
    observer = null
  }
}

function setupObserver(): void {
  disconnectObserver()
  if (!sentinelRef.value) {
    return
  }

  observer = new IntersectionObserver(
    (entries) => {
      const entry = entries[0]
      if (entry?.isIntersecting && canLoadMore.value) {
        void loadNextBatch()
      }
    },
    { root: null, rootMargin: '250px', threshold: 0.1 },
  )

  observer.observe(sentinelRef.value)
}

/**
 * Loads active customers (`GET /api/v1/customers/active`) and active products (`GET /api/v1/products/active`)
 * for feed timeline card resolution.
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

function buildQueryParams(currentOffset: number): Record<string, string | number | boolean> {
  const params: Record<string, string | number | boolean> = {
    pageSize: pageSize.value,
    offset: currentOffset,
  }

  const term = searchTerm.value.trim()
  if (term.length > 0) {
    params.searchTerm = term
  }

  return params
}

/**
 * Synchronizes search query to URL query parameter (?q=...).
 */
function syncRouteQuery(term: string): void {
  const currentQ = (typeof route.query.q === 'string' ? route.query.q : '') || ''
  if (currentQ !== term) {
    const nextQuery = { ...route.query }
    if (term.length > 0) {
      nextQuery.q = term
    } else {
      delete nextQuery.q
    }
    void router.replace({ query: nextQuery })
  }
}

/**
 * Queries `GET /api/v1/feed` with universal search query parameters.
 */
async function loadFeed(): Promise<void> {
  isLoadingFeed.value = true
  errorMessage.value = null
  loadMoreError.value = null

  try {
    const params = buildQueryParams(offset.value)
    const response = await httpClient.get<FeedPageResponse | FeedItem[]>('/feed', {
      params,
    })

    const data = response.data
    if (Array.isArray(data)) {
      feedItems.value = data
      totalCount.value = data.length
      hasMore.value = false
    } else {
      const items = data.items ?? data.feedItems ?? []
      feedItems.value = items
      totalCount.value = typeof data.totalCount === 'number' ? data.totalCount : items.length
      hasMore.value =
        typeof data.hasMore === 'boolean'
          ? data.hasMore
          : offset.value + items.length < totalCount.value
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

/**
 * Fetches the next incremental batch when the sentinel is reached.
 */
async function loadNextBatch(): Promise<void> {
  if (!canLoadMore.value && !loadMoreError.value) {
    return
  }

  isLoadingMore.value = true
  loadMoreError.value = null

  const nextOffset = offset.value + pageSize.value

  try {
    const params = buildQueryParams(nextOffset)
    const response = await httpClient.get<FeedPageResponse | FeedItem[]>('/feed', {
      params,
    })

    const data = response.data
    let newItems: FeedItem[] = []

    if (Array.isArray(data)) {
      newItems = data
      totalCount.value = feedItems.value.length + newItems.length
      hasMore.value = false
    } else {
      newItems = data.items ?? data.feedItems ?? []
      if (typeof data.totalCount === 'number') {
        totalCount.value = data.totalCount
      }
      hasMore.value =
        typeof data.hasMore === 'boolean'
          ? data.hasMore
          : nextOffset + newItems.length < totalCount.value
    }

    if (newItems.length === 0) {
      hasMore.value = false
    } else {
      const existingKeys = new Set(feedItems.value.map(resolveFeedItemKey))
      const uniqueNewItems = newItems.filter((item) => !existingKeys.has(resolveFeedItemKey(item)))
      feedItems.value.push(...uniqueNewItems)
      offset.value = nextOffset
    }
  } catch (err: unknown) {
    loadMoreError.value = extractErrorMessage(
      err,
      'Failed to load more feed items. Please try again.',
    )
  } finally {
    isLoadingMore.value = false
  }
}

function handleSearchInput(): void {
  if (searchDebounceTimer) {
    clearTimeout(searchDebounceTimer)
  }
  searchDebounceTimer = setTimeout(() => {
    syncRouteQuery(searchTerm.value.trim())
    offset.value = 0
    void loadFeed()
  }, 300)
}

function clearSearch(): void {
  if (searchDebounceTimer) {
    clearTimeout(searchDebounceTimer)
    searchDebounceTimer = null
  }
  searchTerm.value = ''
  syncRouteQuery('')
  offset.value = 0
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
 * Closes modal, prepends newly created item to index 0 of feedItems without resetting scroll position,
 * increments totalCount, and sets success alert banner.
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

  // Prepend newly created request/post directly to index 0 of feedItems without scroll displacement
  const newItem: FeedItem = {
    id: reqId,
    feedItemId: reqId,
    postId: createdRequest.id || reqId,
    authorName: 'Current User',
    author: 'Current User',
    postType: 'REQUEST',
    source: 'MANUAL',
    title: reqTitle,
    contentExcerpt: createdRequest.description || createdRequest.title || null,
    summary: createdRequest.description || createdRequest.title || null,
    status: createdRequest.status || 'OPEN',
    visibility: 'PUBLIC',
    isException: false,
    exceptionType: null,
    referenceType: 'REQUEST',
    referenceId: reqId,
    referenceDisplay: reqId,
    customerId: createdRequest.customerId || null,
    productId: createdRequest.productId || null,
    requestId: reqId,
    workPackageId: null,
    commentCount: 0,
    reactionCountsJson: null,
    reactionCounts: {},
    reactionCount: 0,
    createdAt: createdRequest.createdAt || new Date().toISOString(),
  }

  const existingKeys = new Set(feedItems.value.map(resolveFeedItemKey))
  if (!existingKeys.has(resolveFeedItemKey(newItem))) {
    feedItems.value.unshift(newItem)
    totalCount.value += 1
  }
}

function handlePostUpdated(): void {
  void loadFeed()
}

watch(
  () => hasMore.value,
  (more) => {
    if (!more) {
      disconnectObserver()
    } else {
      setupObserver()
    }
  },
)

watch(
  () => sentinelRef.value,
  (el) => {
    if (el && hasMore.value) {
      setupObserver()
    }
  },
)

watch(
  () => route.query.q,
  (newQ) => {
    const queryTerm = typeof newQ === 'string' ? newQ.trim() : ''
    if (queryTerm !== searchTerm.value.trim()) {
      searchTerm.value = queryTerm
      offset.value = 0
      void loadFeed()
    }
  },
)

onMounted(async () => {
  if (typeof route.query.q === 'string' && route.query.q.trim().length > 0) {
    searchTerm.value = route.query.q.trim()
  }
  await Promise.all([loadReferenceLookups(), loadFeed()])
  if (hasMore.value) {
    setupObserver()
  }
})

onUnmounted(() => {
  disconnectObserver()
  if (searchDebounceTimer) {
    clearTimeout(searchDebounceTimer)
    searchDebounceTimer = null
  }
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

        <!-- Universal Search Bar (Dedicated Full-Width Input) -->
        <div class="op-feed-universal-search card shadow-xs border-0 mb-3" data-testid="feed-universal-search">
          <div class="card-body p-2">
            <div class="input-group input-group-sm">
              <span class="input-group-text bg-white border-end-0 text-muted">
                <i class="bi bi-search" aria-hidden="true"></i>
              </span>
              <input
                id="feedUniversalSearch"
                v-model="searchTerm"
                type="text"
                class="form-control form-control-sm border-start-0 border-end-0 ps-0"
                placeholder="Search by product, customer, user, request, or comment text..."
                data-testid="feed-universal-search-input"
                @input="handleSearchInput"
              />
              <button
                v-if="searchTerm.trim().length > 0"
                type="button"
                class="btn btn-outline-secondary border-start-0 border bg-white text-muted"
                data-testid="feed-search-clear-btn"
                aria-label="Clear Search"
                @click="clearSearch"
              >
                <i class="bi bi-x-circle-fill" aria-hidden="true"></i>
              </button>
            </div>
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
            <h2 class="h6 fw-semibold mb-1">
              {{ searchTerm.trim().length > 0 ? `No feed items matching '${searchTerm.trim()}'` : 'No Operational Feed Items Found' }}
            </h2>
            <p class="text-body-secondary small mb-2">
              <template v-if="searchTerm.trim().length > 0">
                No visible feed items match your search query across products, customers, authors, requests, or comments.
              </template>
              <template v-else>
                No operational posts or system events have been recorded in the feed yet.
              </template>
            </p>
            <div v-if="searchTerm.trim().length > 0" class="d-flex justify-content-center gap-2">
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm"
                data-testid="feed-empty-clear-btn"
                @click="clearSearch"
              >
                Clear Search
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

          <!-- Bottom Loading Spinner (TD-004) -->
          <div
            v-if="isLoadingMore"
            class="op-feed-bottom-loading my-2"
            data-testid="feed-bottom-loading"
          >
            <div class="spinner-border spinner-border-sm text-primary" role="status">
              <span class="visually-hidden">Loading more items...</span>
            </div>
            <span>Loading more feed items...</span>
          </div>

          <!-- Load More Error & Retry (TD-004) -->
          <div
            v-if="loadMoreError"
            class="op-feed-retry-box my-2"
            data-testid="feed-load-more-error"
          >
            <div class="d-flex align-items-center gap-1.5 text-danger small">
              <i class="bi bi-exclamation-circle-fill" aria-hidden="true"></i>
              <span>{{ loadMoreError }}</span>
            </div>
            <button
              type="button"
              class="btn btn-outline-danger btn-sm py-0.5 px-2"
              @click="loadNextBatch"
            >
              <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>
              Retry
            </button>
          </div>

          <!-- End of Feed Milestone (TD-004) -->
          <div
            v-if="!hasMore && feedItems.length > 0"
            class="op-feed-end-milestone my-2"
            data-testid="feed-end-milestone"
          >
            <i class="bi bi-check2-circle text-success fs-5 d-block mb-1" aria-hidden="true"></i>
            <span class="small fw-semibold text-secondary">You're all caught up</span>
            <p class="text-muted fs-11 mb-0">All {{ totalCount }} operational feed events have been loaded.</p>
          </div>

          <!-- IntersectionObserver Sentinel (TD-001) -->
          <div ref="sentinelRef" class="op-feed-sentinel" />
        </div>
      </div>

      <!-- Right Column: Sticky Contextual Panel (~33% / 4 cols on xl+) -->
      <div class="col-12 col-xl-4 col-xxl-4">
        <div class="op-feed-sticky-panel">
          <!-- Summary Metrics / Active Status -->
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
                  <span class="text-body-secondary fs-11">Exceptions Loaded</span>
                  <span
                    class="badge"
                    :class="exceptionCount > 0 ? 'bg-danger' : 'bg-light text-secondary border'"
                  >
                    {{ exceptionCount }}
                  </span>
                </div>
                <div class="d-flex justify-content-between align-items-center">
                  <span class="text-body-secondary fs-11">Stream Filter Status</span>
                  <span
                    class="badge"
                    :class="hasActiveFilters ? 'bg-info bg-opacity-10 text-info border border-info border-opacity-25' : 'bg-light text-secondary border'"
                  >
                    {{ hasActiveFilters ? 'Filtered' : 'Unfiltered' }}
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
