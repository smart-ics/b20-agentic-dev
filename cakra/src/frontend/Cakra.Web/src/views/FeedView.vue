<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'

import { httpClient } from '@/api/http'
import CreateRequestModal, { type CreatedRequestResponse } from '@/components/CreateRequestModal.vue'
import FeedDossierPanel from '@/components/FeedDossierPanel.vue'
import FeedLedgerRow from '@/components/FeedLedgerRow.vue'
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
  ownerName?: string | null
  decisionNeeded?: string | null
  expectedImpact?: string | null
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
  customerCode?: string | null
  productId?: string | null
  productName?: string | null
  product?: string | null
  productCode?: string | null
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
      const formatted = code ? `${name} (${code})` : name
      map[id] = formatted
      map[id.toLowerCase()] = formatted
      map[id.toUpperCase()] = formatted
    }
  }
  return map
})

const customerCodeById = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const customer of activeCustomers.value) {
    const id = customer.id || customer.customerId || ''
    const code = customer.customerCode || customer.code || ''
    if (id && code) {
      map[id] = code
      map[id.toLowerCase()] = code
      map[id.toUpperCase()] = code
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
      const formatted = code ? `${name} (${code})` : name
      map[id] = formatted
      map[id.toLowerCase()] = formatted
      map[id.toUpperCase()] = formatted
    }
  }
  return map
})

const productCodeById = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const product of activeProducts.value) {
    const id = product.id || product.productId || ''
    const code = product.code || product.productCode || ''
    if (id && code) {
      map[id] = code
      map[id.toLowerCase()] = code
      map[id.toUpperCase()] = code
    }
  }
  return map
})

// View mode and triage selection
const viewMode = ref<'stream' | 'ledger'>('stream')
const selectedFeedItem = ref<FeedItem | null>(null)
const activeFilter = ref<'ALL' | 'EXCEPTIONS' | 'REQUESTS' | 'SYSTEM'>('ALL')

const displayedFeedItems = computed<FeedItem[]>(() => {
  if (activeFilter.value === 'EXCEPTIONS') {
    return feedItems.value.filter((item) => item.isException)
  }
  if (activeFilter.value === 'REQUESTS') {
    return feedItems.value.filter(
      (item) =>
        (item.referenceType ?? '').toUpperCase() === 'REQUEST' ||
        (item.postType ?? '').toUpperCase() === 'REQUEST',
    )
  }
  if (activeFilter.value === 'SYSTEM') {
    return feedItems.value.filter(
      (item) =>
        (item.source ?? item.postType ?? '').toUpperCase() === 'SYSTEM_GENERATED',
    )
  }
  return feedItems.value
})

function selectFeedItem(item: FeedItem): void {
  selectedFeedItem.value = item
}

function handleClaimOwnership(_item: FeedItem): void {
  void loadFeed()
}

const hasActiveFilters = computed<boolean>(
  () => searchTerm.value.trim().length > 0 || activeFilter.value !== 'ALL',
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

    if (!selectedFeedItem.value && feedItems.value.length > 0) {
      selectedFeedItem.value = feedItems.value[0]
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
        <div class="op-screen-header mb-3 d-flex flex-wrap align-items-center justify-content-between gap-2">
          <div class="d-flex align-items-center gap-2">
            <h1 class="op-screen-title fs-5 fw-bold text-slate-900 dark:text-white mb-0 d-flex align-items-center gap-2">
              <i class="bi bi-activity text-cyan-600 dark:text-cyan-400" aria-hidden="true"></i>
              Operational Feed
            </h1>
            <span class="badge bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 border border-slate-300 dark:border-slate-700 font-mono text-xs">SCR-FEED-001</span>
            <span class="text-slate-500 dark:text-slate-400 small ms-1 font-mono">
              &bull; {{ totalCount }} events
            </span>
          </div>

          <div class="d-flex align-items-center gap-2">
            <!-- View Mode Switcher -->
            <div class="btn-group btn-group-sm rounded-lg overflow-hidden border border-slate-300 dark:border-slate-700" role="group" aria-label="Feed View Mode">
              <button
                type="button"
                class="btn py-1 px-2.5 fs-11 font-medium transition"
                :class="viewMode === 'stream' ? 'bg-cyan-600 text-white dark:bg-cyan-500/20 dark:text-cyan-300 dark:border-cyan-500/30' : 'bg-white dark:bg-slate-800/80 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700'"
                @click="viewMode = 'stream'"
                title="Cards Timeline Stream"
              >
                <i class="bi bi-view-stacked me-1" aria-hidden="true"></i>
                Cards
              </button>
              <button
                type="button"
                class="btn py-1 px-2.5 fs-11 font-medium transition"
                :class="viewMode === 'ledger' ? 'bg-cyan-600 text-white dark:bg-cyan-500/20 dark:text-cyan-300 dark:border-cyan-500/30' : 'bg-white dark:bg-slate-800/80 text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700'"
                @click="viewMode = 'ledger'"
                title="High-Density Split Ledger Table"
              >
                <i class="bi bi-table me-1" aria-hidden="true"></i>
                Ledger
              </button>
            </div>

            <button
              type="button"
              class="btn btn-sm btn-primary py-1 px-2.5 font-medium shadow-sm"
              data-testid="create-request-btn"
              @click="showCreateRequestModal = true"
            >
              <i class="bi bi-plus-lg me-1" aria-hidden="true"></i>
              + Create Request
            </button>
            <button
              type="button"
              class="btn btn-sm btn-outline-secondary dark:bg-slate-800 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-700 dark:hover:text-white py-1 px-2.5 font-medium"
              :disabled="isLoadingFeed"
              data-testid="refresh-feed-btn"
              @click="loadFeed"
            >
              <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>
              Refresh
            </button>
          </div>
        </div>

        <!-- Universal Search Bar (Dedicated Full-Width Input + Quick Filter Chips) -->
        <div class="op-feed-universal-search bg-slate-50 dark:bg-slate-900/90 border border-slate-200 dark:border-slate-800 rounded-xl shadow-sm dark:shadow-lg p-2.5 mb-3" data-testid="feed-universal-search">
          <div class="input-group input-group-sm mb-2 rounded-lg overflow-hidden border border-slate-200 dark:border-slate-800">
            <span class="input-group-text bg-slate-50 dark:bg-slate-950/80 border-0 text-slate-400">
              <i class="bi bi-search" aria-hidden="true"></i>
            </span>
            <input
              id="feedUniversalSearch"
              v-model="searchTerm"
              type="text"
              class="form-control form-control-sm border-0 bg-slate-50 dark:bg-slate-950/80 text-slate-900 dark:text-slate-100 placeholder:text-slate-500 shadow-none ps-0"
              placeholder="Search by product, customer, user, request, or comment text..."
              data-testid="feed-universal-search-input"
              @input="handleSearchInput"
            />
            <button
              v-if="searchTerm.trim().length > 0"
              type="button"
              class="btn btn-sm border-0 bg-slate-50 dark:bg-slate-950/80 text-slate-400 hover:text-slate-200"
              data-testid="feed-search-clear-btn"
              aria-label="Clear Search"
              @click="clearSearch"
            >
              <i class="bi bi-x-circle-fill" aria-hidden="true"></i>
            </button>
          </div>

          <!-- Quick Filter Chips -->
          <div class="d-flex align-items-center gap-1.5 overflow-x-auto fs-11">
            <button
              type="button"
              class="btn btn-sm py-0.5 px-2.5 rounded-pill fs-11 font-medium transition"
              :class="activeFilter === 'ALL' ? 'bg-slate-900 text-white dark:bg-cyan-500/20 dark:text-cyan-300 dark:border dark:border-cyan-500/30 shadow-xs' : 'bg-slate-100 dark:bg-slate-800/80 text-slate-600 dark:text-slate-400 border border-slate-200 dark:border-slate-700/80 hover:text-slate-900 dark:hover:text-slate-200'"
              @click="activeFilter = 'ALL'"
            >
              All ({{ totalCount }})
            </button>
            <button
              type="button"
              class="btn btn-sm py-0.5 px-2.5 rounded-pill fs-11 font-medium d-inline-flex align-items-center gap-1 transition"
              :class="activeFilter === 'EXCEPTIONS' ? 'bg-rose-500 text-white dark:bg-rose-500/20 dark:text-rose-300 dark:border dark:border-rose-500/30 shadow-xs' : 'bg-rose-50 dark:bg-rose-950/30 text-rose-600 dark:text-rose-400 border border-rose-200 dark:border-rose-900/40 hover:bg-rose-100 dark:hover:bg-rose-900/60'"
              @click="activeFilter === 'EXCEPTIONS'"
            >
              <i class="bi bi-exclamation-triangle-fill" aria-hidden="true"></i>
              Exceptions ({{ exceptionCount }})
            </button>
            <button
              type="button"
              class="btn btn-sm py-0.5 px-2.5 rounded-pill fs-11 font-medium transition"
              :class="activeFilter === 'REQUESTS' ? 'bg-indigo-600 text-white dark:bg-indigo-500/20 dark:text-indigo-300 dark:border dark:border-indigo-500/30 shadow-xs' : 'bg-slate-100 dark:bg-slate-800/80 text-slate-600 dark:text-slate-400 border border-slate-200 dark:border-slate-700/80 hover:text-slate-900 dark:hover:text-slate-200'"
              @click="activeFilter === 'REQUESTS'"
            >
              Requests Only
            </button>
            <button
              type="button"
              class="btn btn-sm py-0.5 px-2.5 rounded-pill fs-11 font-medium transition"
              :class="activeFilter === 'SYSTEM' ? 'bg-purple-600 text-white dark:bg-purple-500/20 dark:text-purple-300 dark:border dark:border-purple-500/30 shadow-xs' : 'bg-slate-100 dark:bg-slate-800/80 text-slate-600 dark:text-slate-400 border border-slate-200 dark:border-slate-700/80 hover:text-slate-900 dark:hover:text-slate-200'"
              @click="activeFilter === 'SYSTEM'"
            >
              System Facts
            </button>
          </div>
        </div>

        <!-- Error Alert -->
        <div
          v-if="errorMessage"
          class="alert alert-danger alert-dismissible fade show d-flex align-items-center justify-content-between py-2 px-3 mb-2 rounded-xl border border-rose-500/30 bg-rose-500/10 text-rose-400"
          role="alert"
          data-testid="feed-error-alert"
        >
          <div>
            <i class="bi bi-exclamation-octagon-fill me-1" aria-hidden="true"></i>
            <span>{{ errorMessage }}</span>
          </div>
          <button
            type="button"
            class="btn-close py-2 px-2"
            aria-label="Close"
            @click="errorMessage = null"
          ></button>
        </div>

        <!-- Request Created Success Alert (TD-004) -->
        <div
          v-if="createdRequestAlert"
          class="alert alert-success alert-dismissible fade show rounded-xl border border-emerald-500/30 bg-emerald-500/10 text-emerald-400"
          role="alert"
          data-testid="request-created-success-alert"
        >
          <i class="bi bi-check-circle-fill me-2" aria-hidden="true"></i>
          Request <strong>{{ createdRequestAlert.id }}</strong> recorded successfully. Post published to feed.
          <router-link :to="`/requests/${createdRequestAlert.id}`" class="alert-link text-cyan-400 ms-2">View Request Detail &rarr;</router-link>
          <button type="button" class="btn-close" aria-label="Close" @click="createdRequestAlert = null"></button>
        </div>

        <!-- Loading State -->
        <div
          v-if="isLoadingFeed && feedItems.length === 0"
          class="bg-slate-50 dark:bg-slate-900/90 border border-slate-200 dark:border-slate-800 rounded-xl shadow-sm dark:shadow-lg my-2 p-5 text-center"
          data-testid="feed-loading-state"
        >
          <div class="spinner-border spinner-border-sm text-cyan-500 mb-2" role="status">
            <span class="visually-hidden">Loading operational feed...</span>
          </div>
          <p class="text-slate-500 dark:text-slate-400 small mb-0">Loading operational feed stream...</p>
        </div>

        <!-- Empty State -->
        <div
          v-else-if="!isLoadingFeed && feedItems.length === 0"
          class="bg-slate-50 dark:bg-slate-900/90 border border-slate-200 dark:border-slate-800 rounded-xl shadow-sm dark:shadow-lg my-2 p-5 text-center"
          data-testid="feed-empty-state"
        >
          <i class="bi bi-inbox text-slate-400 dark:text-slate-500 fs-3 d-block mb-2" aria-hidden="true"></i>
          <h2 class="h6 fw-semibold text-slate-900 dark:text-white mb-1">
            {{ searchTerm.trim().length > 0 ? `No feed items matching '${searchTerm.trim()}'` : 'No Operational Feed Items Found' }}
          </h2>
          <p class="text-slate-500 dark:text-slate-400 small mb-3">
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
              class="btn btn-outline-secondary btn-sm dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
              data-testid="feed-empty-clear-btn"
              @click="clearSearch"
            >
              Clear Search
            </button>
          </div>
        </div>

        <!-- Operational Feed Stream / Ledger Container -->
        <div v-else class="d-flex flex-column gap-2" data-testid="feed-card-list">
          <!-- High-Density Ledger Mode -->
          <div v-if="viewMode === 'ledger'" class="bg-slate-50 dark:bg-slate-900/90 border border-slate-200 dark:border-slate-800 rounded-xl shadow-sm dark:shadow-lg overflow-hidden mb-1">
            <div class="table-responsive">
              <table class="table table-hover table-sm align-middle mb-0 op-ledger-table fs-12 dark:text-slate-200">
                <thead class="bg-slate-100 dark:bg-slate-950/80 text-slate-600 dark:text-slate-400 border-b border-slate-200 dark:border-slate-800 font-mono fs-11">
                  <tr>
                    <th class="py-2.5 px-3" style="width: 75px;">TIME</th>
                    <th class="py-2.5 px-3" style="width: 105px;">SEVERITY</th>
                    <th class="py-2.5 px-3" style="width: 115px;">REFERENCE</th>
                    <th class="py-2.5 px-3" style="width: 115px;">OWNER</th>
                    <th class="py-2.5 px-3" style="width: 135px;">CONTEXT</th>
                    <th class="py-2.5 px-3">EVENT TITLE</th>
                    <th class="py-2.5 px-3" style="width: 95px;">SIGNALS</th>
                    <th class="py-2.5 px-3 text-end" style="width: 75px;">ACTION</th>
                  </tr>
                </thead>
                <tbody>
                  <FeedLedgerRow
                    v-for="item in displayedFeedItems"
                    :key="resolveFeedItemKey(item)"
                    :item="item"
                    :is-selected="selectedFeedItem?.postId === item.postId"
                    :customer-name-map="customerNameById"
                    :product-name-map="productNameById"
                    :customer-code-map="customerCodeById"
                    :product-code-map="productCodeById"
                    @select="selectFeedItem"
                    @open-modal="openPostDetailModal"
                  />
                </tbody>
              </table>
            </div>
          </div>

          <!-- Compact Cards Timeline Stream Mode -->
          <div v-else class="d-flex flex-column gap-2">
            <FeedTimelineCard
              v-for="item in displayedFeedItems"
              :key="resolveFeedItemKey(item)"
              :item="item"
              :customer-name-map="customerNameById"
              :product-name-map="productNameById"
              :customer-code-map="customerCodeById"
              :product-code-map="productCodeById"
              @open-modal="openPostDetailModal"
              @post-updated="handlePostUpdated"
            />
          </div>

          <!-- Bottom Loading Spinner (TD-004) -->
          <div
            v-if="isLoadingMore"
            class="op-feed-bottom-loading my-2 text-slate-400"
            data-testid="feed-bottom-loading"
          >
            <div class="spinner-border spinner-border-sm text-cyan-500" role="status">
              <span class="visually-hidden">Loading more items...</span>
            </div>
            <span>Loading more feed items...</span>
          </div>

          <!-- Load More Error & Retry (TD-004) -->
          <div
            v-if="loadMoreError"
            class="op-feed-retry-box my-2 bg-rose-500/10 border border-rose-500/30 rounded-xl p-3"
            data-testid="feed-load-more-error"
          >
            <div class="d-flex align-items-center gap-1.5 text-rose-400 small">
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
            class="op-feed-end-milestone my-3 bg-slate-50 dark:bg-slate-900/70 border border-dashed border-slate-300 dark:border-slate-800 rounded-xl p-4 text-center"
            data-testid="feed-end-milestone"
          >
            <i class="bi bi-check2-circle text-emerald-500 dark:text-emerald-400 fs-4 d-block mb-1" aria-hidden="true"></i>
            <span class="small fw-semibold text-slate-800 dark:text-slate-200">You're all caught up</span>
            <p class="text-slate-500 dark:text-slate-400 fs-11 mb-0">All {{ totalCount }} operational feed events have been loaded.</p>
          </div>

          <!-- IntersectionObserver Sentinel (TD-001) -->
          <div ref="sentinelRef" class="op-feed-sentinel" />
        </div>
      </div>

      <!-- Right Column: Sticky Contextual Panel (~33% / 4 cols on xl+) -->
      <div class="col-12 col-xl-4 col-xxl-4">
        <div class="op-feed-sticky-panel">
          <!-- Pinned Decision Dossier in Ledger Mode -->
          <FeedDossierPanel
            v-if="viewMode === 'ledger' && selectedFeedItem"
            :item="selectedFeedItem"
            :customer-name-map="customerNameById"
            :product-name-map="productNameById"
            @open-modal="openPostDetailModal"
            @post-updated="handlePostUpdated"
            @claim-ownership="handleClaimOwnership"
          />

          <!-- Summary Metrics / Active Status in Stream Mode -->
          <div v-else class="bg-slate-50 dark:bg-slate-900/90 border border-slate-200 dark:border-slate-800 rounded-xl shadow-sm dark:shadow-lg overflow-hidden text-slate-900 dark:text-slate-100">
            <div class="bg-slate-50 dark:bg-slate-950/60 border-b border-slate-200 dark:border-slate-800 py-2.5 px-3.5">
              <span class="fw-semibold small d-flex align-items-center gap-1.5 text-slate-800 dark:text-slate-200">
                <i class="bi bi-activity text-cyan-600 dark:text-cyan-400" aria-hidden="true"></i>
                Operational Stream Summary
              </span>
            </div>
            <div class="p-3.5">
              <div class="d-flex flex-column gap-2.5">
                <div class="d-flex justify-content-between align-items-center border-b border-slate-100 dark:border-slate-800/80 pb-2">
                  <span class="text-slate-500 dark:text-slate-400 fs-11">Total Stream Events</span>
                  <span class="fw-bold font-mono fs-12 text-slate-900 dark:text-white">{{ totalCount }}</span>
                </div>
                <div class="d-flex justify-content-between align-items-center border-b border-slate-100 dark:border-slate-800/80 pb-2">
                  <span class="text-slate-500 dark:text-slate-400 fs-11">Exceptions Loaded</span>
                  <span
                    class="badge font-mono"
                    :class="exceptionCount > 0 ? 'bg-rose-500/15 text-rose-400 border border-rose-500/30' : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 border border-slate-200 dark:border-slate-700'"
                  >
                    {{ exceptionCount }}
                  </span>
                </div>
                <div class="d-flex justify-content-between align-items-center">
                  <span class="text-slate-500 dark:text-slate-400 fs-11">Stream Filter Status</span>
                  <span
                    class="badge font-mono"
                    :class="hasActiveFilters ? 'bg-cyan-500/15 text-cyan-400 border border-cyan-500/30' : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 border border-slate-200 dark:border-slate-700'"
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
