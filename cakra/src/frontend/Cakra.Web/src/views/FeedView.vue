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
 * - Provides a "New Post" button and inline authoring form (`POST /api/v1/posts`) for human-authored
 *   operational posts (UC-FCOL-003).
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

export interface RequestLookupItem {
  id: string
  requestId?: string
  title: string
  status?: string
  customerId?: string | null
  customerName?: string | null
  productId?: string | null
  productName?: string | null
}

interface PagedRequestLookupPayload {
  items?: RequestLookupItem[]
  requests?: RequestLookupItem[]
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
const availableRequests = ref<RequestLookupItem[]>([])

const isLoadingFeed = ref<boolean>(false)
const isLoadingLookups = ref<boolean>(false)
const isSubmittingPost = ref<boolean>(false)

const errorMessage = ref<string | null>(null)
const feedbackMessage = ref<string | null>(null)

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

// New Post inline form state (POST /api/v1/posts — UC-FCOL-003)
const showCreatePostForm = ref<boolean>(false)
const createPostForm = reactive({
  title: '',
  content: '',
  customerId: '',
  productId: '',
  requestId: '',
  isException: false,
  exceptionType: '',
})

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

const isCreatePostDisabled = computed<boolean>(
  () =>
    isSubmittingPost.value ||
    createPostForm.title.trim().length === 0 ||
    createPostForm.content.trim().length === 0,
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
 * Loads active customers (`GET /api/v1/customers/active`), active products (`GET /api/v1/products/active`),
 * and recent requests (`GET /api/v1/requests`) for filter and post authoring selectors.
 */
async function loadReferenceLookups(): Promise<void> {
  isLoadingLookups.value = true
  try {
    const [customersResult, productsResult, requestsResult] = await Promise.allSettled([
      httpClient.get<ActiveCustomerOption[]>('/customers/active'),
      httpClient.get<ActiveProductOption[]>('/products/active'),
      httpClient.get<PagedRequestLookupPayload | RequestLookupItem[]>('/requests', {
        params: { pageSize: 50, page: 1 },
      }),
    ])

    if (customersResult.status === 'fulfilled' && Array.isArray(customersResult.value.data)) {
      activeCustomers.value = customersResult.value.data
    }

    if (productsResult.status === 'fulfilled' && Array.isArray(productsResult.value.data)) {
      activeProducts.value = productsResult.value.data
    }

    if (requestsResult.status === 'fulfilled') {
      const data = requestsResult.value.data
      if (Array.isArray(data)) {
        availableRequests.value = data
      } else if (data && Array.isArray(data.items)) {
        availableRequests.value = data.items
      } else if (data && Array.isArray(data.requests)) {
        availableRequests.value = data.requests
      }
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

function toggleCreatePostForm(): void {
  showCreatePostForm.value = !showCreatePostForm.value
  errorMessage.value = null
  feedbackMessage.value = null
}

function resetCreatePostForm(): void {
  createPostForm.title = ''
  createPostForm.content = ''
  createPostForm.customerId = ''
  createPostForm.productId = ''
  createPostForm.requestId = ''
  createPostForm.isException = false
  createPostForm.exceptionType = ''
}

function handleLinkedRequestSelect(): void {
  const selectedId = createPostForm.requestId.trim()
  if (!selectedId) {
    return
  }
  const matched = availableRequests.value.find(
    (r) => (r.id || r.requestId) === selectedId,
  )
  if (matched) {
    if (!createPostForm.customerId && matched.customerId) {
      createPostForm.customerId = matched.customerId
    }
    if (!createPostForm.productId && matched.productId) {
      createPostForm.productId = matched.productId
    }
  }
}

/**
 * Submits a new human-authored operational Post (`POST /api/v1/posts` — UC-FCOL-003)
 * and reloads the feed.
 */
async function handleCreateOperationalPost(): Promise<void> {
  if (isCreatePostDisabled.value) {
    return
  }

  isSubmittingPost.value = true
  errorMessage.value = null
  feedbackMessage.value = null

  try {
    const payload: Record<string, unknown> = {
      title: createPostForm.title.trim(),
      content: createPostForm.content.trim(),
      body: createPostForm.content.trim(),
      isException: createPostForm.isException,
    }

    if (createPostForm.customerId.trim().length > 0) {
      payload.customerId = createPostForm.customerId.trim()
    }

    if (createPostForm.productId.trim().length > 0) {
      payload.productId = createPostForm.productId.trim()
    }

    if (createPostForm.requestId.trim().length > 0) {
      payload.requestId = createPostForm.requestId.trim()
    }

    if (createPostForm.isException && createPostForm.exceptionType.trim().length > 0) {
      payload.exceptionType = createPostForm.exceptionType.trim()
    }

    await httpClient.post('/posts', payload)

    resetCreatePostForm()
    showCreatePostForm.value = false
    filters.offset = 0
    feedbackMessage.value = 'Operational post published to the feed.'
    await loadFeed()
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(
      err,
      'Failed to publish operational post. Please verify your inputs and try again.',
    )
  } finally {
    isSubmittingPost.value = false
  }
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
  <section class="container-fluid py-2" data-screen-id="SCR-FEED-001">
    <!-- Screen Header -->
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-3 mb-4">
      <div>
        <div class="d-flex align-items-center gap-2 mb-1">
          <span class="badge bg-secondary-subtle text-secondary-emphasis font-monospace">
            SCR-FEED-001
          </span>
          <span class="text-body-secondary small">Operational Awareness &amp; Collaboration</span>
        </div>
        <h1 class="h3 mb-1 fw-bold">Operational Feed</h1>
        <p class="text-body-secondary mb-0">
          Real-time stream of operational updates, request discussions, and exception alerts across
          customers and products.
        </p>
      </div>

      <div class="d-flex flex-wrap align-items-center gap-2">
        <button
          type="button"
          class="btn btn-outline-secondary"
          :disabled="isLoadingFeed"
          data-testid="refresh-feed-btn"
          @click="loadFeed"
        >
          <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>
          Refresh
        </button>

        <button
          type="button"
          class="btn btn-primary"
          data-testid="toggle-create-post-btn"
          @click="toggleCreatePostForm"
        >
          <i
            class="bi me-1"
            :class="showCreatePostForm ? 'bi-x-lg' : 'bi-plus-circle'"
            aria-hidden="true"
          ></i>
          {{ showCreatePostForm ? 'Cancel Post' : 'New Operational Post' }}
        </button>
      </div>
    </div>

    <!-- Error & Feedback Alerts -->
    <div
      v-if="errorMessage"
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center justify-content-between"
      role="alert"
      data-testid="feed-error-alert"
    >
      <div>
        <i class="bi bi-exclamation-octagon-fill me-2" aria-hidden="true"></i>
        <span>{{ errorMessage }}</span>
      </div>
      <button
        type="button"
        class="btn-close"
        aria-label="Close"
        @click="errorMessage = null"
      ></button>
    </div>

    <div
      v-if="feedbackMessage"
      class="alert alert-success alert-dismissible fade show d-flex align-items-center justify-content-between"
      role="alert"
      data-testid="feed-feedback-alert"
    >
      <div>
        <i class="bi bi-check-circle-fill me-2" aria-hidden="true"></i>
        <span>{{ feedbackMessage }}</span>
      </div>
      <button
        type="button"
        class="btn-close"
        aria-label="Close"
        @click="feedbackMessage = null"
      ></button>
    </div>

    <!-- Create Operational Post Form Card (POST /api/v1/posts — UC-FCOL-003) -->
    <div
      v-if="showCreatePostForm"
      class="card shadow-sm border-primary-subtle mb-4"
      data-testid="create-post-card"
    >
      <div class="card-header bg-primary-subtle text-primary-emphasis d-flex justify-content-between align-items-center">
        <span class="fw-semibold">
          <i class="bi bi-megaphone-fill me-2" aria-hidden="true"></i>
          Author New Operational Post
        </span>
        <button
          type="button"
          class="btn-close"
          aria-label="Close form"
          @click="showCreatePostForm = false"
        ></button>
      </div>
      <div class="card-body">
        <form @submit.prevent="handleCreateOperationalPost">
          <div class="row g-3">
            <div class="col-12">
              <label for="createPostTitle" class="form-label fw-semibold">
                Post Title <span class="text-danger">*</span>
              </label>
              <input
                id="createPostTitle"
                v-model="createPostForm.title"
                type="text"
                class="form-control"
                maxlength="255"
                placeholder="Summarize the operational update, finding, or blocker..."
                required
                data-testid="create-post-title-input"
              />
            </div>

            <div class="col-12">
              <label for="createPostContent" class="form-label fw-semibold">
                Operational Details <span class="text-danger">*</span>
              </label>
              <textarea
                id="createPostContent"
                v-model="createPostForm.content"
                class="form-control"
                rows="3"
                placeholder="Provide context, investigation notes, or questions for the team..."
                required
                data-testid="create-post-content-input"
              ></textarea>
            </div>

            <div class="col-12 col-md-4">
              <label for="createPostCustomer" class="form-label fw-semibold">
                Customer (Optional)
              </label>
              <select
                id="createPostCustomer"
                v-model="createPostForm.customerId"
                class="form-select"
                data-testid="create-post-customer-select"
              >
                <option value="">— None / General —</option>
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

            <div class="col-12 col-md-4">
              <label for="createPostProduct" class="form-label fw-semibold">
                Product (Optional)
              </label>
              <select
                id="createPostProduct"
                v-model="createPostForm.productId"
                class="form-select"
                data-testid="create-post-product-select"
              >
                <option value="">— None / General —</option>
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

            <div class="col-12 col-md-4">
              <label for="createPostRequest" class="form-label fw-semibold">
                Referenced Request (Optional)
              </label>
              <select
                id="createPostRequest"
                v-model="createPostForm.requestId"
                class="form-select"
                data-testid="create-post-request-select"
                @change="handleLinkedRequestSelect"
              >
                <option value="">— No Linked Request —</option>
                <option
                  v-for="req in availableRequests"
                  :key="req.id || req.requestId"
                  :value="req.id || req.requestId"
                >
                  {{ req.title }} ({{ req.status || 'ACTIVE' }})
                </option>
              </select>
            </div>

            <div class="col-12 col-md-6 d-flex align-items-center pt-2">
              <div class="form-check form-switch">
                <input
                  id="createPostIsException"
                  v-model="createPostForm.isException"
                  class="form-check-input"
                  type="checkbox"
                  role="switch"
                  data-testid="create-post-exception-toggle"
                />
                <label class="form-check-label fw-semibold" for="createPostIsException">
                  Flag as Operational Exception
                </label>
              </div>
            </div>

            <div v-if="createPostForm.isException" class="col-12 col-md-6">
              <label for="createPostExceptionType" class="form-label fw-semibold">
                Exception Type
              </label>
              <select
                id="createPostExceptionType"
                v-model="createPostForm.exceptionType"
                class="form-select"
                data-testid="create-post-exception-type-select"
              >
                <option value="">ESCALATION (Default)</option>
                <option v-for="exType in EXCEPTION_TYPES" :key="exType" :value="exType">
                  {{ exType }}
                </option>
              </select>
            </div>
          </div>

          <div class="d-flex justify-content-end gap-2 mt-3">
            <button
              type="button"
              class="btn btn-outline-secondary"
              :disabled="isSubmittingPost"
              @click="showCreatePostForm = false"
            >
              Cancel
            </button>
            <button
              type="submit"
              class="btn btn-primary"
              :disabled="isCreatePostDisabled"
              data-testid="submit-create-post-btn"
            >
              <span
                v-if="isSubmittingPost"
                class="spinner-border spinner-border-sm me-1"
                role="status"
                aria-hidden="true"
              ></span>
              <i v-else class="bi bi-send-fill me-1" aria-hidden="true"></i>
              Publish Post
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Filter Bar (UC-FCOL-005, FEAT-FCOL-005) -->
    <div class="card shadow-sm border-0 bg-body-tertiary mb-4" data-testid="feed-filter-bar">
      <div class="card-body">
        <form class="row g-3 align-items-end" @submit.prevent="applyFilters">
          <!-- Customer Filter -->
          <div class="col-12 col-md-3">
            <label for="feedFilterCustomer" class="form-label small fw-semibold text-uppercase text-body-secondary">
              Customer
            </label>
            <select
              id="feedFilterCustomer"
              v-model="filters.customerId"
              class="form-select"
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
          <div class="col-12 col-md-3">
            <label for="feedFilterProduct" class="form-label small fw-semibold text-uppercase text-body-secondary">
              Product
            </label>
            <select
              id="feedFilterProduct"
              v-model="filters.productId"
              class="form-select"
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
          <div class="col-12 col-md-2">
            <label for="feedFilterExceptionType" class="form-label small fw-semibold text-uppercase text-body-secondary">
              Exception Type
            </label>
            <select
              id="feedFilterExceptionType"
              v-model="filters.exceptionType"
              class="form-select"
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
          <div class="col-12 col-md-2">
            <div class="form-check form-switch pb-1">
              <input
                id="feedFilterExceptionsOnly"
                v-model="filters.isException"
                class="form-check-input"
                type="checkbox"
                role="switch"
                data-testid="feed-filter-exception"
                @change="handleExceptionToggleChange"
              />
              <label class="form-check-label fw-semibold" for="feedFilterExceptionsOnly">
                <span class="badge bg-danger me-1">!</span>
                Exceptions Only
              </label>
            </div>
          </div>

          <!-- Filter Actions -->
          <div class="col-12 col-md-2 d-flex gap-2 justify-content-md-end">
            <button
              type="submit"
              class="btn btn-primary flex-grow-1 flex-md-grow-0"
              :disabled="isLoadingFeed"
              data-testid="apply-feed-filters-btn"
            >
              <i class="bi bi-funnel-fill me-1" aria-hidden="true"></i>
              Filter
            </button>
            <button
              v-if="hasActiveFilters"
              type="button"
              class="btn btn-outline-secondary"
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

    <!-- Loading State -->
    <div
      v-if="isLoadingFeed && feedItems.length === 0"
      class="card border-0 shadow-sm my-4"
      data-testid="feed-loading-state"
    >
      <div class="card-body py-5 text-center">
        <div class="spinner-border text-primary mb-3" role="status">
          <span class="visually-hidden">Loading operational feed...</span>
        </div>
        <p class="text-body-secondary mb-0">Loading operational feed stream...</p>
      </div>
    </div>

    <!-- Empty State -->
    <div
      v-else-if="!isLoadingFeed && feedItems.length === 0"
      class="card border-0 shadow-sm my-4"
      data-testid="feed-empty-state"
    >
      <div class="card-body py-5 text-center">
        <i class="bi bi-inbox text-body-secondary display-5 d-block mb-3" aria-hidden="true"></i>
        <h2 class="h5 fw-semibold">No Operational Feed Items Found</h2>
        <p class="text-body-secondary mb-3">
          <template v-if="hasActiveFilters">
            No visible feed items match your active Customer, Product, or Exception filter criteria.
          </template>
          <template v-else>
            No operational posts or system events have been recorded in the feed yet.
          </template>
        </p>
        <div class="d-flex justify-content-center gap-2">
          <button
            v-if="hasActiveFilters"
            type="button"
            class="btn btn-outline-secondary btn-sm"
            @click="clearFilters"
          >
            Clear Filters
          </button>
          <button
            type="button"
            class="btn btn-primary btn-sm"
            @click="showCreatePostForm = true"
          >
            <i class="bi bi-plus-circle me-1" aria-hidden="true"></i>
            Create First Post
          </button>
        </div>
      </div>
    </div>

    <!-- Operational Feed Cards Stream -->
    <div v-else class="d-flex flex-column gap-3" data-testid="feed-card-list">
      <article
        v-for="item in feedItems"
        :key="resolveFeedItemKey(item)"
        class="card shadow-sm feed-item-card"
        :class="item.isException ? 'border-danger border-start border-4' : 'border-0'"
        role="button"
        tabindex="0"
        :data-testid="`feed-card-${resolvePostId(item)}`"
        @click="openPostDetailModal(item)"
        @keydown.enter="openPostDetailModal(item)"
      >
        <div class="card-body">
          <!-- Card Top Row: Badges + CreatedAt -->
          <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-2">
            <div class="d-flex flex-wrap align-items-center gap-2">
              <!-- Exception Badge (Architecture §12, P6-S32 Completion Criteria) -->
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
                    ? 'bg-info-subtle text-info-emphasis'
                    : 'bg-primary-subtle text-primary-emphasis'
                "
              >
                {{ isSystemGenerated(item) ? 'SYSTEM' : 'OPERATIONAL POST' }}
              </span>

              <!-- Customer Badge -->
              <span
                v-if="item.customerId || item.customerName || item.customer"
                class="badge bg-light text-dark border"
                data-testid="feed-card-customer"
              >
                <i class="bi bi-building me-1 text-secondary" aria-hidden="true"></i>
                {{ resolveCustomerDisplay(item) }}
              </span>

              <!-- Product Badge -->
              <span
                v-if="item.productId || item.productName || item.product"
                class="badge bg-light text-dark border"
                data-testid="feed-card-product"
              >
                <i class="bi bi-box-seam me-1 text-secondary" aria-hidden="true"></i>
                {{ resolveProductDisplay(item) }}
              </span>
            </div>

            <!-- CreatedAt Timestamp -->
            <small class="text-body-secondary" data-testid="feed-card-created-at">
              <i class="bi bi-clock me-1" aria-hidden="true"></i>
              {{ formatTimestamp(item.createdAt) }}
            </small>
          </div>

          <!-- Card Title -->
          <h2 class="h5 card-title fw-bold mb-2 text-body" data-testid="feed-card-title">
            {{ item.title }}
          </h2>

          <!-- Content Excerpt / Summary -->
          <p
            v-if="resolveExcerpt(item)"
            class="card-text text-body-secondary mb-3"
            data-testid="feed-card-excerpt"
          >
            {{ resolveExcerpt(item) }}
          </p>

          <!-- Metadata Row: Author, Customer, Product -->
          <div class="d-flex flex-wrap align-items-center gap-3 small text-body-secondary mb-3">
            <span data-testid="feed-card-author">
              <i class="bi bi-person-circle me-1" aria-hidden="true"></i>
              <strong>Author:</strong> {{ resolveAuthorDisplay(item) }}
            </span>

            <span>
              <i class="bi bi-building me-1" aria-hidden="true"></i>
              <strong>Customer:</strong> {{ resolveCustomerDisplay(item) }}
            </span>

            <span>
              <i class="bi bi-box-seam me-1" aria-hidden="true"></i>
              <strong>Product:</strong> {{ resolveProductDisplay(item) }}
            </span>
          </div>

          <!-- Latest Comment Excerpt Preview -->
          <div
            v-if="item.latestCommentExcerpt"
            class="bg-body-tertiary rounded p-2 mb-3 small border-start border-3 border-secondary"
            data-testid="feed-card-latest-comment"
          >
            <i class="bi bi-chat-quote-fill text-secondary me-1" aria-hidden="true"></i>
            <span class="text-body-secondary fw-semibold me-1">Latest comment:</span>
            <span class="text-body">{{ item.latestCommentExcerpt }}</span>
          </div>

          <!-- Card Footer Row: CommentCount, ReactionCount, and Contextual Navigation Links -->
          <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 pt-2 border-top">
            <div class="d-flex flex-wrap align-items-center gap-3">
              <!-- CommentCount -->
              <span
                class="badge bg-secondary-subtle text-secondary-emphasis d-inline-flex align-items-center gap-1 px-2 py-1"
                data-testid="feed-card-comment-count"
              >
                <i class="bi bi-chat-left-text" aria-hidden="true"></i>
                <span>{{ item.commentCount ?? 0 }}</span>
                <span>{{ (item.commentCount ?? 0) === 1 ? 'Comment' : 'Comments' }}</span>
              </span>

              <!-- ReactionCount -->
              <span
                class="badge bg-secondary-subtle text-secondary-emphasis d-inline-flex align-items-center gap-1 px-2 py-1"
                data-testid="feed-card-reaction-count"
              >
                <i class="bi bi-hand-thumbs-up" aria-hidden="true"></i>
                <span>{{ resolveReactionCount(item) }}</span>
                <span>{{ resolveReactionCount(item) === 1 ? 'Reaction' : 'Reactions' }}</span>
              </span>

              <!-- Individual Reaction Type Pills -->
              <span
                v-for="(count, rType) in resolveReactionBreakdown(item)"
                :key="rType"
                class="badge rounded-pill text-bg-light border small"
              >
                {{ rType }}: {{ count }}
              </span>
            </div>

            <!-- Action & Navigation Buttons (UC-FCOL-004) -->
            <div class="d-flex flex-wrap align-items-center gap-2" @click.stop>
              <!-- Direct Link to Request Detail when RequestId is present (UC-FCOL-004) -->
              <RouterLink
                v-if="resolveRequestId(item)"
                :to="`/requests/${resolveRequestId(item)}`"
                class="btn btn-sm btn-outline-primary"
                data-testid="feed-card-request-link"
                @click.stop="navigateToRequest(resolveRequestId(item)!)"
              >
                <i class="bi bi-box-arrow-up-right me-1" aria-hidden="true"></i>
                {{ resolveRequestDisplay(item) }}
              </RouterLink>

              <!-- Direct Link to Work Package Detail when WorkPackageId is present -->
              <RouterLink
                v-if="resolveWorkPackageId(item)"
                :to="`/work-packages/${resolveWorkPackageId(item)}`"
                class="btn btn-sm btn-outline-secondary"
                data-testid="feed-card-work-package-link"
              >
                <i class="bi bi-kanban me-1" aria-hidden="true"></i>
                Work Package
              </RouterLink>

              <!-- Open Post Thread Modal Button (SCR-POST-001) -->
              <button
                type="button"
                class="btn btn-sm btn-light border"
                data-testid="feed-card-open-modal-btn"
                @click.stop="openPostDetailModal(item)"
              >
                <i class="bi bi-chat-dots me-1" aria-hidden="true"></i>
                View Thread
              </button>
            </div>
          </div>
        </div>
      </article>
    </div>

    <!-- Pagination Controls -->
    <nav
      class="d-flex flex-wrap justify-content-between align-items-center gap-3 mt-4 pt-3 border-top"
      aria-label="Operational feed pagination"
      data-testid="feed-pagination"
    >
      <div class="small text-body-secondary">
        <template v-if="totalCount > 0">
          Showing <strong>{{ showingRangeStart }}</strong>–<strong>{{ showingRangeEnd }}</strong> of
          <strong>{{ totalCount }}</strong> feed items (Page {{ currentPage }}
          <template v-if="computedTotalPages > 0">of {{ computedTotalPages }}</template>)
        </template>
        <template v-else>
          Showing 0 feed items
        </template>
      </div>

      <div class="btn-group" role="group" aria-label="Pagination buttons">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm"
          :disabled="!canGoPrevious"
          data-testid="feed-pagination-prev"
          @click="goToPreviousPage"
        >
          <i class="bi bi-chevron-left me-1" aria-hidden="true"></i>
          Previous
        </button>
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm"
          :disabled="!canGoNext"
          data-testid="feed-pagination-next"
          @click="goToNextPage"
        >
          Next
          <i class="bi bi-chevron-right ms-1" aria-hidden="true"></i>
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

<style scoped>
.feed-item-card {
  cursor: pointer;
  transition:
    transform 0.12s ease-in-out,
    box-shadow 0.12s ease-in-out;
}

.feed-item-card:hover,
.feed-item-card:focus-visible {
  transform: translateY(-1px);
  box-shadow: 0 0.35rem 0.85rem rgba(0, 0, 0, 0.08) !important;
}
</style>
