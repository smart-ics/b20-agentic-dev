<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink, useRouter } from 'vue-router'

import { httpClient } from '@/api/http'
import { useAuthStore } from '@/stores/auth'

/**
 * SCR-FEED-001: Feed Timeline Card Component (CR-012)
 *
 * Encapsulates:
 * 1. Default-expanded inline comments with 2-comment windowing & inline expansion toggle.
 * 2. Anchored inline comment input with Enter to submit and Shift+Enter for newline.
 * 3. Facebook-style action bar with quick-toggle SEEN reaction and floating 6-reaction palette.
 * 4. Explicit modal open delegation on title click and "View full thread" button.
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

export interface PostCommentItem {
  id: string
  commentId?: string
  postId: string
  authorPersonId: string
  authorName?: string | null
  author?: string | null
  content: string
  status?: string
  isActive?: boolean
  createdAt: string
  updatedAt?: string | null
}

export interface PostReactionItem {
  id: string
  reactionId?: string
  postId: string
  commentId?: string | null
  personId: string
  personName?: string | null
  reactionType: string
  isActive: boolean
  removedAt?: string | null
  createdAt: string
  updatedAt?: string | null
}

export type ReactionTypeCode =
  | 'SEEN'
  | 'EXPERIENCED'
  | 'HAVE_IDEA'
  | 'SIMILAR_ISSUE'
  | 'DUPLICATE'
  | 'NEED_CLARIFICATION'

export interface ReactionOptionMeta {
  type: ReactionTypeCode
  label: string
  iconClass: string
  emoji: string
  colorClass: string
  description: string
}

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const props = withDefaults(
  defineProps<{
    item: FeedItem
    customerNameMap?: Record<string, string>
    productNameMap?: Record<string, string>
  }>(),
  {
    customerNameMap: () => ({}),
    productNameMap: () => ({}),
  },
)

const emit = defineEmits<{
  (e: 'open-modal', item: FeedItem): void
  (e: 'post-updated', item: FeedItem): void
}>()

const router = useRouter()
const authStore = useAuthStore()

const REACTION_OPTIONS: readonly ReactionOptionMeta[] = [
  {
    type: 'SEEN',
    label: 'Seen',
    iconClass: 'bi-hand-thumbs-up-fill',
    emoji: '👍',
    colorClass: 'text-primary',
    description: 'Acknowledge that this operational update has been seen',
  },
  {
    type: 'EXPERIENCED',
    label: 'Experienced',
    iconClass: 'bi-person-check-fill',
    emoji: '👤',
    colorClass: 'text-success',
    description: 'Indicate first-hand operational experience with this scenario',
  },
  {
    type: 'HAVE_IDEA',
    label: 'Have Idea',
    iconClass: 'bi-lightbulb-fill',
    emoji: '💡',
    colorClass: 'text-warning',
    description: 'Signal that you have a potential solution or suggestion',
  },
  {
    type: 'SIMILAR_ISSUE',
    label: 'Similar Issue',
    iconClass: 'bi-flag-fill',
    emoji: '🚩',
    colorClass: 'text-danger',
    description: 'Flag that a similar issue was encountered elsewhere',
  },
  {
    type: 'DUPLICATE',
    label: 'Duplicate',
    iconClass: 'bi-files',
    emoji: '📄',
    colorClass: 'text-secondary',
    description: 'Flag that this post duplicates an existing operational thread',
  },
  {
    type: 'NEED_CLARIFICATION',
    label: 'Need Clarification',
    iconClass: 'bi-question-circle-fill',
    emoji: '❓',
    colorClass: 'text-info',
    description: 'Request additional operational context or clarification',
  },
] as const

// Local Card States
const comments = ref<PostCommentItem[]>([])
const showAllComments = ref<boolean>(false)
const isLoadingComments = ref<boolean>(false)
const isSubmittingComment = ref<boolean>(false)
const newCommentText = ref<string>('')
const commentTextareaRef = ref<HTMLTextAreaElement | null>(null)
const commentError = ref<string | null>(null)

const localCommentCount = ref<number>(props.item.commentCount ?? 0)
const localReactionCounts = ref<Record<string, number>>({})
const activeUserReaction = ref<ReactionTypeCode | null>(null)
const isTogglingReaction = ref<boolean>(false)
const showReactionPalette = ref<boolean>(false)
let paletteHideTimer: ReturnType<typeof setTimeout> | null = null

// Computed Helpers
const resolvedPostId = computed<string>(() => (props.item.postId || props.item.id || props.item.feedItemId || '').trim())

const currentPersonId = computed<string>(() => (authStore.currentUser?.personId ?? '').trim().toLowerCase())

const isSystemGenerated = computed<boolean>(() => {
  const source = (props.item.postType || props.item.source || '').toUpperCase()
  return source === 'SYSTEM_GENERATED'
})

const authorDisplay = computed<string>(() => {
  const author = props.item.author ?? props.item.authorName
  if (author && author.trim().length > 0) {
    return author.trim()
  }
  if (isSystemGenerated.value) {
    return 'SYSTEM'
  }
  if (props.item.authorPersonId) {
    return `Person #${props.item.authorPersonId.slice(0, 8)}`
  }
  return 'Operational User'
})

const customerDisplay = computed<string>(() => {
  const name = props.item.customer ?? props.item.customerName
  if (name && name.trim().length > 0) {
    return name.trim()
  }
  if (props.item.customerId && props.customerNameMap[props.item.customerId]) {
    return props.customerNameMap[props.item.customerId]
  }
  return props.item.customerId ? `Customer #${props.item.customerId.slice(0, 8)}` : ''
})

const productDisplay = computed<string>(() => {
  const name = props.item.product ?? props.item.productName
  if (name && name.trim().length > 0) {
    return name.trim()
  }
  if (props.item.productId && props.productNameMap[props.item.productId]) {
    return props.productNameMap[props.item.productId]
  }
  return props.item.productId ? `Product #${props.item.productId.slice(0, 8)}` : ''
})

const contentExcerpt = computed<string>(() => {
  const text = props.item.contentExcerpt ?? props.item.summary ?? ''
  return text.trim()
})

const effectiveRequestId = computed<string | null>(() => {
  if (props.item.requestId && props.item.requestId.trim().length > 0) {
    return props.item.requestId.trim()
  }
  if (
    (props.item.referenceType ?? '').toUpperCase() === 'REQUEST' &&
    props.item.referenceId &&
    props.item.referenceId.trim().length > 0
  ) {
    return props.item.referenceId.trim()
  }
  return null
})

const effectiveRequestDisplay = computed<string>(() => {
  if (
    (props.item.referenceType ?? '').toUpperCase() === 'REQUEST' &&
    props.item.referenceDisplay &&
    props.item.referenceDisplay.trim().length > 0
  ) {
    return props.item.referenceDisplay.trim()
  }
  const reqId = effectiveRequestId.value
  return reqId ? `Request #${reqId.slice(0, 8)}` : 'View Request'
})

const effectiveWorkPackageId = computed<string | null>(() => {
  if (props.item.workPackageId && props.item.workPackageId.trim().length > 0) {
    return props.item.workPackageId.trim()
  }
  if (
    (props.item.referenceType ?? '').toUpperCase() === 'WORK_PACKAGE' &&
    props.item.referenceId &&
    props.item.referenceId.trim().length > 0
  ) {
    return props.item.referenceId.trim()
  }
  return null
})

// Comment Windowing
const displayedComments = computed<PostCommentItem[]>(() => {
  if (showAllComments.value || comments.value.length <= 2) {
    return comments.value
  }
  return comments.value.slice(-2)
})

const hiddenCommentCount = computed<number>(() => {
  return Math.max(0, comments.value.length - 2)
})

// Reaction Breakdown & Active Count
const totalReactionCount = computed<number>(() => {
  return Object.values(localReactionCounts.value).reduce((sum, count) => sum + count, 0)
})

const activeReactionMeta = computed<ReactionOptionMeta | null>(() => {
  if (!activeUserReaction.value) {
    return null
  }
  return REACTION_OPTIONS.find((r) => r.type === activeUserReaction.value) ?? null
})

const presentReactionOptions = computed<ReactionOptionMeta[]>(() => {
  return REACTION_OPTIONS.filter((option) => (localReactionCounts.value[option.type] ?? 0) > 0)
})

// Helper Functions
function getInitials(name?: string | null): string {
  if (!name || !name.trim()) {
    return 'OP'
  }
  const parts = name.trim().split(/\s+/)
  if (parts.length >= 2) {
    return (parts[0][0] + parts[1][0]).toUpperCase()
  }
  return name.slice(0, 2).toUpperCase()
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

function formatRelativeTime(value?: string | null): string {
  if (!value) {
    return '—'
  }
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }
  const now = new Date()
  const diffSec = Math.floor((now.getTime() - date.getTime()) / 1000)
  if (diffSec < 45) {
    return 'just now'
  }
  if (diffSec < 3600) {
    const mins = Math.floor(diffSec / 60)
    return `${mins}m ago`
  }
  if (diffSec < 86400) {
    const hours = Math.floor(diffSec / 3600)
    return `${hours}h ago`
  }
  if (diffSec < 604800) {
    const days = Math.floor(diffSec / 86400)
    return `${days}d ago`
  }
  return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
}

function formatCommentAuthor(comment: PostCommentItem): string {
  const name = comment.author ?? comment.authorName
  if (name && name.trim()) {
    return name.trim()
  }
  if (comment.authorPersonId) {
    return `Person #${comment.authorPersonId.slice(0, 8)}`
  }
  return 'Operational User'
}

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

function initReactionCounts(): void {
  const initialCounts: Record<string, number> = {}
  for (const option of REACTION_OPTIONS) {
    initialCounts[option.type] = 0
  }

  if (props.item.reactionCounts && typeof props.item.reactionCounts === 'object') {
    for (const [key, val] of Object.entries(props.item.reactionCounts)) {
      if (typeof val === 'number' && val > 0) {
        initialCounts[key.toUpperCase()] = val
      }
    }
  } else if (props.item.reactionCountsJson && props.item.reactionCountsJson.trim().startsWith('{')) {
    try {
      const parsed = JSON.parse(props.item.reactionCountsJson) as Record<string, unknown>
      for (const [key, val] of Object.entries(parsed)) {
        if (typeof val === 'number' && val > 0) {
          initialCounts[key.toUpperCase()] = val
        }
      }
    } catch {
      // Ignored
    }
  }

  localReactionCounts.value = initialCounts
}

async function loadComments(): Promise<void> {
  const postId = resolvedPostId.value
  if (!postId || localCommentCount.value <= 0) {
    return
  }

  isLoadingComments.value = true
  commentError.value = null

  try {
    const response = await httpClient.get<PostCommentItem[]>(`/posts/${postId}/comments`)
    if (Array.isArray(response.data)) {
      comments.value = response.data
      localCommentCount.value = Math.max(localCommentCount.value, response.data.length)
    }
  } catch {
    // Non-blocking failure for comments lazy loading
  } finally {
    isLoadingComments.value = false
  }
}

async function submitComment(): Promise<void> {
  const postId = resolvedPostId.value
  const trimmed = newCommentText.value.trim()
  if (!postId || !trimmed || isSubmittingComment.value) {
    return
  }

  isSubmittingComment.value = true
  commentError.value = null

  try {
    const response = await httpClient.post<PostCommentItem>(`/posts/${postId}/comments`, {
      content: trimmed,
      body: trimmed,
    })

    const createdComment: PostCommentItem = response.data?.id
      ? response.data
      : {
          id: `tmp-${Date.now()}`,
          postId,
          authorPersonId: currentPersonId.value || 'current-user',
          authorName: 'You',
          content: trimmed,
          createdAt: new Date().toISOString(),
        }

    comments.value = [...comments.value, createdComment]
    localCommentCount.value++
    newCommentText.value = ''

    emit('post-updated', {
      ...props.item,
      commentCount: localCommentCount.value,
      latestCommentExcerpt: trimmed,
    })
  } catch (err: unknown) {
    commentError.value = extractErrorMessage(err, 'Unable to post comment. Please try again.')
  } finally {
    isSubmittingComment.value = false
  }
}

function handleCommentKeydown(e: KeyboardEvent): void {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault()
    void submitComment()
  }
}

function focusCommentInput(): void {
  commentTextareaRef.value?.focus()
}

// Reaction Handlers
async function toggleQuickSeenReaction(): Promise<void> {
  if (activeUserReaction.value === 'SEEN') {
    await removeActiveReaction('SEEN')
  } else {
    await applyReaction('SEEN')
  }
}

async function handlePaletteSelect(type: ReactionTypeCode): Promise<void> {
  showReactionPalette.value = false
  if (activeUserReaction.value === type) {
    await removeActiveReaction(type)
  } else {
    await applyReaction(type)
  }
}

async function applyReaction(type: ReactionTypeCode): Promise<void> {
  const postId = resolvedPostId.value
  if (!postId || isTogglingReaction.value) {
    return
  }

  const prevReaction = activeUserReaction.value
  isTogglingReaction.value = true

  // Optimistic local update
  const updatedCounts = { ...localReactionCounts.value }
  if (prevReaction && updatedCounts[prevReaction] > 0) {
    updatedCounts[prevReaction] = Math.max(0, updatedCounts[prevReaction] - 1)
  }
  updatedCounts[type] = (updatedCounts[type] ?? 0) + 1
  localReactionCounts.value = updatedCounts
  activeUserReaction.value = type

  try {
    await httpClient.post(`/posts/${postId}/reactions`, {
      reactionType: type,
    })

    emit('post-updated', {
      ...props.item,
      reactionCounts: localReactionCounts.value,
      reactionCount: totalReactionCount.value,
    })
  } catch {
    // Rollback on failure
    initReactionCounts()
    activeUserReaction.value = prevReaction
  } finally {
    isTogglingReaction.value = false
  }
}

async function removeActiveReaction(type: ReactionTypeCode): Promise<void> {
  const postId = resolvedPostId.value
  if (!postId || isTogglingReaction.value) {
    return
  }

  isTogglingReaction.value = true

  // Optimistic local update
  const updatedCounts = { ...localReactionCounts.value }
  if (updatedCounts[type] > 0) {
    updatedCounts[type] = Math.max(0, updatedCounts[type] - 1)
  }
  localReactionCounts.value = updatedCounts
  activeUserReaction.value = null

  try {
    await httpClient.delete(`/posts/${postId}/reactions/${encodeURIComponent(type)}`)

    emit('post-updated', {
      ...props.item,
      reactionCounts: localReactionCounts.value,
      reactionCount: totalReactionCount.value,
    })
  } catch {
    // Rollback on failure
    initReactionCounts()
    activeUserReaction.value = type
  } finally {
    isTogglingReaction.value = false
  }
}

// Palette Hover Handling
function onReactionAreaMouseEnter(): void {
  if (paletteHideTimer) {
    clearTimeout(paletteHideTimer)
    paletteHideTimer = null
  }
  showReactionPalette.value = true
}

function onReactionAreaMouseLeave(): void {
  paletteHideTimer = setTimeout(() => {
    showReactionPalette.value = false
  }, 350)
}

function openModal(): void {
  emit('open-modal', props.item)
}

function navigateToRequest(requestId: string): void {
  void router.push(`/requests/${requestId}`)
}

watch(
  () => props.item,
  () => {
    localCommentCount.value = props.item.commentCount ?? 0
    initReactionCounts()
  },
  { deep: true },
)

onMounted(() => {
  initReactionCounts()
  void loadComments()
})
</script>

<template>
  <article
    class="op-feed-row op-feed-card p-3 mb-2 rounded-3 border bg-white shadow-xs"
    :class="{ 'is-exception border-danger-subtle': item.isException }"
    :data-testid="`feed-card-${resolvedPostId}`"
  >
    <!-- Header Row: Exception, SYS/POST badge, Title, Customer, Product, Author, Timestamp -->
    <div class="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-2">
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
            isSystemGenerated
              ? 'bg-info bg-opacity-10 text-info border border-info border-opacity-25'
              : 'bg-primary bg-opacity-10 text-primary border border-primary border-opacity-25'
          "
        >
          {{ isSystemGenerated ? 'SYS' : 'POST' }}
        </span>

        <!-- Post Title Trigger -->
        <button
          type="button"
          class="btn btn-link text-dark fw-semibold text-start p-0 text-decoration-none text-truncate"
          style="max-width: 440px;"
          data-testid="feed-card-title"
          :title="item.title"
          @click.stop="openModal"
        >
          {{ item.title }}
        </button>

        <!-- Customer Tag -->
        <span
          v-if="customerDisplay"
          class="badge bg-light text-dark border text-truncate"
          style="max-width: 140px;"
          data-testid="feed-card-customer"
          :title="customerDisplay"
        >
          <i class="bi bi-building me-1 text-secondary" aria-hidden="true"></i>
          {{ customerDisplay }}
        </span>

        <!-- Product Tag -->
        <span
          v-if="productDisplay"
          class="badge bg-light text-dark border text-truncate"
          style="max-width: 130px;"
          data-testid="feed-card-product"
          :title="productDisplay"
        >
          <i class="bi bi-box-seam me-1 text-secondary" aria-hidden="true"></i>
          {{ productDisplay }}
        </span>
      </div>

      <!-- Author and CreatedAt Timestamp -->
      <div class="d-flex align-items-center gap-2 ms-auto flex-shrink-0">
        <span class="text-body-secondary fs-11" data-testid="feed-card-author">
          <i class="bi bi-person me-0.5" aria-hidden="true"></i>
          {{ authorDisplay }}
        </span>
        <span class="text-body-secondary fs-11" data-testid="feed-card-created-at">
          <i class="bi bi-clock me-0.5" aria-hidden="true"></i>
          {{ formatTimestamp(item.createdAt) }}
        </span>
      </div>
    </div>

    <!-- Content Excerpt -->
    <div
      v-if="contentExcerpt"
      class="text-secondary fs-12 mb-2 text-break"
      data-testid="feed-card-excerpt"
    >
      {{ contentExcerpt }}
    </div>

    <!-- Contextual Links Row (Request / Work Package) -->
    <div v-if="effectiveRequestId || effectiveWorkPackageId" class="d-flex align-items-center gap-1.5 mb-2" @click.stop>
      <RouterLink
        v-if="effectiveRequestId"
        :to="`/requests/${effectiveRequestId}`"
        class="btn btn-sm btn-outline-primary py-0 px-1.5 fs-11"
        data-testid="feed-card-request-link"
        @click.stop="navigateToRequest(effectiveRequestId)"
      >
        <i class="bi bi-box-arrow-up-right me-0.5" aria-hidden="true"></i>
        {{ effectiveRequestDisplay }}
      </RouterLink>

      <RouterLink
        v-if="effectiveWorkPackageId"
        :to="`/work-packages/${effectiveWorkPackageId}`"
        class="btn btn-sm btn-outline-secondary py-0 px-1.5 fs-11"
        data-testid="feed-card-work-package-link"
      >
        <i class="bi bi-kanban me-0.5" aria-hidden="true"></i>
        WP
      </RouterLink>
    </div>

    <!-- Engagement Summary Row (Reactions Count + Comments Count) -->
    <div class="d-flex justify-content-between align-items-center text-muted fs-11 py-1 mb-1">
      <!-- Reactions breakdown badges -->
      <div class="d-flex align-items-center gap-1.5" data-testid="feed-card-reaction-summary">
        <span v-if="totalReactionCount > 0" class="d-flex align-items-center gap-1">
          <span
            v-for="opt in presentReactionOptions"
            :key="opt.type"
            class="d-inline-flex align-items-center"
            :title="`${opt.label}: ${localReactionCounts[opt.type]}`"
          >
            <span>{{ opt.emoji }}</span>
          </span>
          <span class="ms-1 fw-semibold text-dark" data-testid="feed-card-reaction-count">
            {{ totalReactionCount }}
          </span>
        </span>
        <span v-else class="text-body-tertiary">
          No reactions yet
        </span>
      </div>

      <!-- Comment Count Indicator -->
      <div class="d-flex align-items-center gap-1 text-secondary" data-testid="feed-card-comment-count">
        <i class="bi bi-chat-left-text me-0.5" aria-hidden="true"></i>
        <span>{{ localCommentCount }} {{ localCommentCount === 1 ? 'comment' : 'comments' }}</span>
      </div>
    </div>

    <!-- Facebook-Style Action Bar (React / Comment / View Thread) -->
    <div class="d-flex align-items-center justify-content-between border-top border-bottom py-1 my-1">
      <!-- React Button + Floating Palette Container -->
      <div
        class="position-relative flex-grow-1 text-center"
        @mouseenter="onReactionAreaMouseEnter"
        @mouseleave="onReactionAreaMouseLeave"
      >
        <!-- Floating Reaction Palette -->
        <div
          v-if="showReactionPalette"
          class="op-reaction-palette"
          data-testid="feed-card-palette"
          @mouseenter="onReactionAreaMouseEnter"
          @mouseleave="onReactionAreaMouseLeave"
        >
          <button
            v-for="option in REACTION_OPTIONS"
            :key="option.type"
            type="button"
            class="op-reaction-palette-btn"
            :class="{ 'opacity-50': isTogglingReaction }"
            :title="option.label"
            :disabled="isTogglingReaction"
            @click.stop="handlePaletteSelect(option.type)"
          >
            {{ option.emoji }}
          </button>
        </div>

        <!-- React Action Button -->
        <button
          type="button"
          class="op-action-btn w-100"
          :class="activeReactionMeta ? activeReactionMeta.colorClass : ''"
          data-testid="feed-card-react-btn"
          :disabled="isTogglingReaction"
          @click.stop="toggleQuickSeenReaction"
        >
          <i
            class="bi"
            :class="activeReactionMeta ? activeReactionMeta.iconClass : 'bi-hand-thumbs-up'"
            aria-hidden="true"
          ></i>
          <span>{{ activeReactionMeta ? activeReactionMeta.label : 'Seen' }}</span>
        </button>
      </div>

      <!-- Comment Button (focuses inline textarea) -->
      <div class="flex-grow-1 text-center">
        <button
          type="button"
          class="op-action-btn w-100"
          data-testid="feed-card-comment-btn"
          @click.stop="focusCommentInput"
        >
          <i class="bi bi-chat-left-text" aria-hidden="true"></i>
          <span>Comment</span>
        </button>
      </div>

      <!-- View Full Thread Button -->
      <div class="flex-grow-1 text-center">
        <button
          type="button"
          class="op-action-btn w-100 text-secondary"
          data-testid="feed-card-open-modal-btn"
          @click.stop="openModal"
        >
          <i class="bi bi-chat-dots" aria-hidden="true"></i>
          <span>View full thread</span>
        </button>
      </div>
    </div>

    <!-- Comments Section -->
    <div class="pt-2">
      <!-- Loading comments spinner -->
      <div v-if="isLoadingComments" class="text-center py-2 text-muted fs-11">
        <div class="spinner-border spinner-border-sm text-primary me-1" role="status"></div>
        <span>Loading comments...</span>
      </div>

      <!-- Expand Comments Toggle (when > 2 comments exist and not expanded) -->
      <div
        v-if="!isLoadingComments && hiddenCommentCount > 0 && !showAllComments"
        class="mb-2 ps-1"
      >
        <button
          type="button"
          class="btn btn-link btn-sm text-decoration-none p-0 text-muted fs-11 fw-semibold"
          data-testid="feed-card-expand-comments-btn"
          @click.stop="showAllComments = true"
        >
          <i class="bi bi-arrow-return-right me-1" aria-hidden="true"></i>
          View previous comments ({{ hiddenCommentCount }})
        </button>
      </div>

      <!-- Comments List (Speech Bubbles) -->
      <div
        v-if="displayedComments.length > 0"
        class="d-flex flex-column gap-2 mb-2"
        data-testid="feed-card-comments-list"
      >
        <div
          v-for="comment in displayedComments"
          :key="comment.id || comment.commentId"
          class="d-flex align-items-start gap-2"
          :data-testid="`feed-comment-${comment.id || comment.commentId}`"
        >
          <!-- Commenter Avatar Initials -->
          <div class="op-avatar-circle op-avatar-circle-sm" :title="formatCommentAuthor(comment)">
            {{ getInitials(formatCommentAuthor(comment)) }}
          </div>

          <!-- Speech Bubble -->
          <div class="flex-grow-1">
            <div class="op-comment-bubble d-inline-block max-w-100">
              <div class="d-flex align-items-baseline gap-2">
                <span class="fw-semibold text-dark fs-12">{{ formatCommentAuthor(comment) }}</span>
                <span class="text-muted fs-10" :title="formatTimestamp(comment.createdAt)">
                  {{ formatRelativeTime(comment.createdAt) }}
                </span>
              </div>
              <div class="text-secondary fs-12 mt-0.5 text-break">
                {{ comment.content }}
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Anchored Inline Comment Input Box -->
      <div class="d-flex align-items-start gap-2 pt-1" @click.stop>
        <!-- Current User Avatar -->
        <div class="op-avatar-circle op-avatar-circle-sm" title="You">
          {{ getInitials(authStore.currentUser?.personId || 'ME') }}
        </div>

        <!-- Input Box Form -->
        <div class="flex-grow-1 position-relative">
          <textarea
            ref="commentTextareaRef"
            v-model="newCommentText"
            rows="1"
            class="form-control form-control-sm pe-5 fs-12 rounded-3 bg-light"
            placeholder="Write an operational comment... (Enter to submit, Shift+Enter for newline)"
            data-testid="feed-card-comment-input"
            :disabled="isSubmittingComment"
            @keydown="handleCommentKeydown"
          ></textarea>

          <!-- Inline Send Button -->
          <button
            type="button"
            class="btn btn-sm btn-link text-primary position-absolute end-0 top-50 translate-middle-y me-1 p-1"
            :class="{ 'opacity-25': !newCommentText.trim() || isSubmittingComment }"
            :disabled="!newCommentText.trim() || isSubmittingComment"
            data-testid="feed-card-comment-submit-btn"
            title="Send comment"
            @click.stop="submitComment"
          >
            <span
              v-if="isSubmittingComment"
              class="spinner-border spinner-border-sm"
              role="status"
            ></span>
            <i v-else class="bi bi-send-fill fs-13" aria-hidden="true"></i>
          </button>
        </div>
      </div>

      <!-- Inline Comment Error Message -->
      <div v-if="commentError" class="text-danger fs-11 mt-1 ps-4">
        <i class="bi bi-exclamation-circle me-1" aria-hidden="true"></i>
        {{ commentError }}
      </div>
    </div>
  </article>
</template>
