<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'

import { httpClient } from '@/api/http'
import { useAuthStore } from '@/stores/auth'

/**
 * SCR-POST-001: Post Detail Modal
 * (Architecture §7, §8 — UC-FCOL-001..004, UC-COL-001, §9 — FEAT-FCOL-001..003, §12, §19.4, §20, §21).
 *
 * - Renders a Bootstrap 5 modal displaying full Post thread details:
 *   Title, Body/Content, Author/AuthorName, CreatedAt, plus CustomerName, ProductName,
 *   IsException/ExceptionType, Visibility, and Status badges loaded from `GET /api/v1/posts/${postId}`.
 * - Loads and displays the chronological comments list (`Author`, `Content`, `CreatedAt`)
 *   from `GET /api/v1/posts/${postId}/comments`.
 * - Provides a comment input form (`textarea` + submit button) calling
 *   `POST /api/v1/posts/${postId}/comments`.
 * - Provides structured operational reaction buttons (`SEEN`, `EXPERIENCED`, `HAVE_IDEA`,
 *   `SIMILAR_ISSUE`, `DUPLICATE`, `NEED_CLARIFICATION`) with count badges calling
 *   `POST /api/v1/posts/${postId}/reactions` and `DELETE /api/v1/posts/${postId}/reactions/${reactionType}`.
 * - Provides contextual navigation links (`<RouterLink>` and programmatic navigation) to
 *   Request detail (`/requests/${requestId}`) when the post references a request (UC-FCOL-004),
 *   and to Work Package detail (`/work-packages/${workPackageId}`) when present.
 * - Supports visibility toggle (`POST /api/v1/posts/${postId}/visibility`) and archive
 *   (`POST /api/v1/posts/${postId}/archive`) actions.
 */

export interface PostReferenceItem {
  id: string
  postReferenceId?: string
  postId: string
  referenceType: string
  referenceId: string
  referenceDisplay?: string | null
  removedAt?: string | null
  isActive?: boolean
  createdAt: string
  updatedAt?: string | null
}

export interface PostCommentItem {
  id: string
  commentId?: string
  postId: string
  authorPersonId: string
  authorName?: string | null
  author?: string | null
  content: string
  status: string
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

export interface PostThreadDetails {
  id: string
  postId?: string
  title: string
  content: string
  body?: string
  authorPersonId?: string | null
  authorName?: string | null
  author?: string | null
  source: string
  postType?: string
  sourceEventType?: string | null
  status: 'ACTIVE' | 'ARCHIVED' | string
  visibility: 'VISIBLE' | 'HIDDEN' | string
  isException: boolean
  exceptionType?: string | null
  customerId?: string | null
  customerName?: string | null
  productId?: string | null
  productName?: string | null
  requestId?: string | null
  requestTitle?: string | null
  workPackageId?: string | null
  workPackageName?: string | null
  referenceType?: string | null
  referenceId?: string | null
  referenceDisplay?: string | null
  archivedAt?: string | null
  archivedByPersonId?: string | null
  createdAt: string
  updatedAt?: string | null
  references?: PostReferenceItem[]
  comments?: PostCommentItem[]
  reactions?: PostReactionItem[]
  reactionCounts?: Record<string, number>
  commentCount?: number
  reactionCount?: number
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
    postId?: string | null
    show?: boolean
    initialRequestId?: string | null
    initialWorkPackageId?: string | null
  }>(),
  {
    postId: null,
    show: undefined,
    initialRequestId: null,
    initialWorkPackageId: null,
  },
)

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'updated', post?: PostThreadDetails | null): void
}>()

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

/**
 * Approved structured operational reaction types per Architecture §12 and PostReactionTypes.cs.
 */
const REACTION_OPTIONS: readonly ReactionOptionMeta[] = [
  {
    type: 'SEEN',
    label: 'Seen',
    iconClass: 'bi-hand-thumbs-up',
    description: 'Acknowledge that this operational update has been seen',
  },
  {
    type: 'EXPERIENCED',
    label: 'Experienced',
    iconClass: 'bi-person-check',
    description: 'Indicate first-hand operational experience with this scenario',
  },
  {
    type: 'HAVE_IDEA',
    label: 'Have Idea',
    iconClass: 'bi-lightbulb',
    description: 'Signal that you have a potential solution or suggestion',
  },
  {
    type: 'SIMILAR_ISSUE',
    label: 'Similar Issue',
    iconClass: 'bi-flag',
    description: 'Flag that a similar issue was encountered elsewhere',
  },
  {
    type: 'DUPLICATE',
    label: 'Duplicate',
    iconClass: 'bi-files',
    description: 'Flag that this post duplicates an existing operational thread',
  },
  {
    type: 'NEED_CLARIFICATION',
    label: 'Need Clarification',
    iconClass: 'bi-question-circle',
    description: 'Request additional operational context or clarification',
  },
] as const

const resolvedPostId = computed<string>(() => {
  const fromProp = (props.postId ?? '').trim()
  if (fromProp) {
    return fromProp
  }
  const fromRoutePostId = String(route.params.postId ?? '').trim()
  if (fromRoutePostId) {
    return fromRoutePostId
  }
  return ''
})

const isModalVisible = computed<boolean>(() => {
  if (props.show !== undefined) {
    return Boolean(props.show && resolvedPostId.value)
  }
  return Boolean(resolvedPostId.value)
})

const post = ref<PostThreadDetails | null>(null)
const comments = ref<PostCommentItem[]>([])
const reactions = ref<PostReactionItem[]>([])
const localToggledReactions = ref<Set<string>>(new Set())

const isLoadingPost = ref<boolean>(false)
const isLoadingComments = ref<boolean>(false)
const isSubmittingComment = ref<boolean>(false)
const pendingReactionType = ref<string | null>(null)
const isTogglingVisibility = ref<boolean>(false)
const isArchiving = ref<boolean>(false)

const newCommentContent = ref<string>('')
const errorMessage = ref<string | null>(null)
const actionFeedbackMessage = ref<string | null>(null)

const currentPersonId = computed<string>(() => (authStore.currentUser?.personId ?? '').trim())

const postBody = computed<string>(() => post.value?.body ?? post.value?.content ?? '')

const postAuthorDisplay = computed<string>(() => {
  const author = post.value?.author ?? post.value?.authorName
  if (author && author.trim()) {
    return author.trim()
  }
  if ((post.value?.source ?? '').toUpperCase() === 'SYSTEM_GENERATED') {
    return 'SYSTEM'
  }
  return 'Operational User'
})

const isArchived = computed<boolean>(
  () => (post.value?.status ?? '').toUpperCase() === 'ARCHIVED',
)

const isHidden = computed<boolean>(
  () => (post.value?.visibility ?? '').toUpperCase() === 'HIDDEN',
)

/**
 * Resolves the referenced Request ID from direct `requestId`, primary `referenceType === 'REQUEST'`,
 * `references` collection, or `initialRequestId` prop (UC-FCOL-004).
 */
const effectiveRequestId = computed<string | null>(() => {
  const direct = (post.value?.requestId ?? '').trim()
  if (direct) {
    return direct
  }

  if (
    (post.value?.referenceType ?? '').toUpperCase() === 'REQUEST' &&
    post.value?.referenceId
  ) {
    return post.value.referenceId.trim()
  }

  const refMatch = post.value?.references?.find(
    (r) => r.isActive !== false && (r.referenceType ?? '').toUpperCase() === 'REQUEST' && r.referenceId,
  )
  if (refMatch?.referenceId) {
    return refMatch.referenceId.trim()
  }

  const fallback = (props.initialRequestId ?? '').trim()
  return fallback || null
})

const effectiveRequestLabel = computed<string>(() => {
  if (post.value?.requestTitle && post.value.requestTitle.trim()) {
    return post.value.requestTitle.trim()
  }
  if (
    (post.value?.referenceType ?? '').toUpperCase() === 'REQUEST' &&
    post.value?.referenceDisplay &&
    post.value.referenceDisplay.trim()
  ) {
    return post.value.referenceDisplay.trim()
  }
  const refMatch = post.value?.references?.find(
    (r) => r.isActive !== false && (r.referenceType ?? '').toUpperCase() === 'REQUEST' && r.referenceDisplay,
  )
  if (refMatch?.referenceDisplay && refMatch.referenceDisplay.trim()) {
    return refMatch.referenceDisplay.trim()
  }
  return effectiveRequestId.value ? `Request #${effectiveRequestId.value.slice(0, 8)}` : 'View Request'
})

/**
 * Resolves the referenced Work Package ID when present.
 */
const effectiveWorkPackageId = computed<string | null>(() => {
  const direct = (post.value?.workPackageId ?? '').trim()
  if (direct) {
    return direct
  }

  if (
    (post.value?.referenceType ?? '').toUpperCase() === 'WORK_PACKAGE' &&
    post.value?.referenceId
  ) {
    return post.value.referenceId.trim()
  }

  const refMatch = post.value?.references?.find(
    (r) =>
      r.isActive !== false &&
      (r.referenceType ?? '').toUpperCase() === 'WORK_PACKAGE' &&
      r.referenceId,
  )
  if (refMatch?.referenceId) {
    return refMatch.referenceId.trim()
  }

  const fallback = (props.initialWorkPackageId ?? '').trim()
  return fallback || null
})

const effectiveWorkPackageLabel = computed<string>(() => {
  if (post.value?.workPackageName && post.value.workPackageName.trim()) {
    return post.value.workPackageName.trim()
  }
  if (
    (post.value?.referenceType ?? '').toUpperCase() === 'WORK_PACKAGE' &&
    post.value?.referenceDisplay &&
    post.value.referenceDisplay.trim()
  ) {
    return post.value.referenceDisplay.trim()
  }
  return effectiveWorkPackageId.value
    ? `Work Package #${effectiveWorkPackageId.value.slice(0, 8)}`
    : 'View Work Package'
})

/**
 * Computes aggregated counts per reaction type from `post.reactionCounts` and active `reactions`.
 */
const reactionCountsByType = computed<Record<string, number>>(() => {
  const map: Record<string, number> = {}
  for (const option of REACTION_OPTIONS) {
    map[option.type] = 0
  }

  const serverCounts = post.value?.reactionCounts
  if (serverCounts && Object.keys(serverCounts).length > 0) {
    for (const [key, count] of Object.entries(serverCounts)) {
      const normalized = key.trim().toUpperCase()
      map[normalized] = typeof count === 'number' && count >= 0 ? count : 0
    }
    return map
  }

  for (const item of reactions.value) {
    if (item.isActive !== false && !item.removedAt) {
      const normalized = (item.reactionType ?? '').trim().toUpperCase()
      if (normalized) {
        map[normalized] = (map[normalized] ?? 0) + 1
      }
    }
  }

  return map
})

const totalReactionCount = computed<number>(() =>
  Object.values(reactionCountsByType.value).reduce((acc, val) => acc + val, 0),
)

/**
 * Determines whether the current authenticated person has an active reaction of the given type.
 */
function hasUserReacted(reactionType: string): boolean {
  const normalized = reactionType.trim().toUpperCase()
  const personId = currentPersonId.value.toLowerCase()

  if (personId) {
    const matchedInList = reactions.value.some(
      (r) =>
        r.isActive !== false &&
        !r.removedAt &&
        (r.reactionType ?? '').trim().toUpperCase() === normalized &&
        (r.personId ?? '').trim().toLowerCase() === personId,
    )
    if (matchedInList) {
      return true
    }
  }

  return localToggledReactions.value.has(normalized)
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

function formatDateTime(value?: string | null): string {
  if (!value) {
    return '—'
  }
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }
  return date.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
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

/**
 * Loads full Post thread details (`GET /api/v1/posts/${id}`) and comments (`GET /api/v1/posts/${id}/comments`).
 */
async function loadPostThread(id: string): Promise<void> {
  if (!id) {
    post.value = null
    comments.value = []
    reactions.value = []
    return
  }

  isLoadingPost.value = true
  isLoadingComments.value = true
  errorMessage.value = null

  try {
    const [postResponse, commentsResponse] = await Promise.all([
      httpClient.get<PostThreadDetails>(`/posts/${id}`),
      httpClient.get<PostCommentItem[]>(`/posts/${id}/comments`),
    ])

    const thread = postResponse.data
    post.value = thread
    comments.value = Array.isArray(commentsResponse.data)
      ? commentsResponse.data
      : (thread.comments ?? [])
    reactions.value = Array.isArray(thread.reactions) ? thread.reactions : []
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(
      err,
      'Failed to load post thread details. Please try again.',
    )
  } finally {
    isLoadingPost.value = false
    isLoadingComments.value = false
  }
}

/**
 * Refreshes comments list (`GET /api/v1/posts/${id}/comments`) and thread details (`GET /api/v1/posts/${id}`).
 */
async function refreshThreadState(): Promise<void> {
  const id = resolvedPostId.value
  if (!id) {
    return
  }

  try {
    const [postResponse, commentsResponse] = await Promise.all([
      httpClient.get<PostThreadDetails>(`/posts/${id}`),
      httpClient.get<PostCommentItem[]>(`/posts/${id}/comments`),
    ])
    post.value = postResponse.data
    comments.value = Array.isArray(commentsResponse.data)
      ? commentsResponse.data
      : (postResponse.data.comments ?? [])
    reactions.value = Array.isArray(postResponse.data.reactions)
      ? postResponse.data.reactions
      : []
  } catch {
    // Preserve existing view state on background refresh failure
  }
}

/**
 * Submits a new comment via `POST /api/v1/posts/${id}/comments` (UC-FCOL-001).
 */
async function handleCommentSubmit(): Promise<void> {
  const id = resolvedPostId.value
  const trimmed = newCommentContent.value.trim()
  if (!id || !trimmed || isSubmittingComment.value || isArchived.value) {
    return
  }

  isSubmittingComment.value = true
  errorMessage.value = null
  actionFeedbackMessage.value = null

  try {
    const response = await httpClient.post<PostCommentItem>(`/posts/${id}/comments`, {
      content: trimmed,
      body: trimmed,
    })

    if (response.data && response.data.id) {
      comments.value = [...comments.value, response.data]
    }

    newCommentContent.value = ''
    await refreshThreadState()
    actionFeedbackMessage.value = 'Comment posted.'
    emit('updated', post.value)
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Unable to post comment.')
  } finally {
    isSubmittingComment.value = false
  }
}

/**
 * Adds a reaction via `POST /api/v1/posts/${id}/reactions` (UC-FCOL-002).
 */
async function addReaction(reactionType: ReactionTypeCode): Promise<void> {
  const id = resolvedPostId.value
  if (!id || pendingReactionType.value || isArchived.value) {
    return
  }

  pendingReactionType.value = reactionType
  errorMessage.value = null
  actionFeedbackMessage.value = null

  try {
    await httpClient.post<PostReactionItem>(`/posts/${id}/reactions`, {
      reactionType,
    })

    const nextSet = new Set(localToggledReactions.value)
    nextSet.add(reactionType)
    localToggledReactions.value = nextSet

    await refreshThreadState()
    emit('updated', post.value)
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, `Unable to add ${reactionType} reaction.`)
  } finally {
    pendingReactionType.value = null
  }
}

/**
 * Soft-removes an active reaction via `DELETE /api/v1/posts/${id}/reactions/${reactionType}` (UC-FCOL-002).
 */
async function removeReaction(reactionType: ReactionTypeCode): Promise<void> {
  const id = resolvedPostId.value
  if (!id || pendingReactionType.value || isArchived.value) {
    return
  }

  pendingReactionType.value = reactionType
  errorMessage.value = null
  actionFeedbackMessage.value = null

  try {
    const response = await httpClient.delete<PostThreadDetails>(
      `/posts/${id}/reactions/${encodeURIComponent(reactionType)}`,
    )

    const nextSet = new Set(localToggledReactions.value)
    nextSet.delete(reactionType)
    localToggledReactions.value = nextSet

    if (response.data && response.data.id) {
      post.value = response.data
      reactions.value = Array.isArray(response.data.reactions) ? response.data.reactions : []
    } else {
      await refreshThreadState()
    }

    emit('updated', post.value)
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, `Unable to remove ${reactionType} reaction.`)
  } finally {
    pendingReactionType.value = null
  }
}

/**
 * Toggles a reaction on/off when clicking a reaction button.
 */
async function handleToggleReaction(reactionType: ReactionTypeCode): Promise<void> {
  if (hasUserReacted(reactionType)) {
    await removeReaction(reactionType)
  } else {
    await addReaction(reactionType)
  }
}

/**
 * Toggles Post visibility between `VISIBLE` and `HIDDEN` (`POST /api/v1/posts/${id}/visibility`).
 */
async function handleToggleVisibility(): Promise<void> {
  const id = resolvedPostId.value
  if (!id || isTogglingVisibility.value) {
    return
  }

  isTogglingVisibility.value = true
  errorMessage.value = null
  actionFeedbackMessage.value = null

  const targetVisibility = isHidden.value ? 'VISIBLE' : 'HIDDEN'

  try {
    const response = await httpClient.post<PostThreadDetails>(`/posts/${id}/visibility`, {
      visibility: targetVisibility,
    })
    if (response.data && response.data.id) {
      post.value = response.data
    } else {
      await refreshThreadState()
    }
    actionFeedbackMessage.value = `Post visibility set to ${targetVisibility}.`
    emit('updated', post.value)
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Unable to update post visibility.')
  } finally {
    isTogglingVisibility.value = false
  }
}

/**
 * Archives an active Post (`POST /api/v1/posts/${id}/archive`).
 */
async function handleArchivePost(): Promise<void> {
  const id = resolvedPostId.value
  if (!id || isArchiving.value || isArchived.value) {
    return
  }

  isArchiving.value = true
  errorMessage.value = null
  actionFeedbackMessage.value = null

  try {
    const response = await httpClient.post<PostThreadDetails>(`/posts/${id}/archive`, {})
    if (response.data && response.data.id) {
      post.value = response.data
    } else {
      await refreshThreadState()
    }
    actionFeedbackMessage.value = 'Post has been archived.'
    emit('updated', post.value)
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Unable to archive post.')
  } finally {
    isArchiving.value = false
  }
}

function handleClose(): void {
  errorMessage.value = null
  actionFeedbackMessage.value = null
  emit('close')
}

function navigateToRequest(requestId: string): void {
  emit('close')
  void router.push(`/requests/${requestId}`)
}

function navigateToWorkPackage(workPackageId: string): void {
  emit('close')
  void router.push(`/work-packages/${workPackageId}`)
}

function handleKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape' && isModalVisible.value) {
    handleClose()
  }
}

watch(
  () => [resolvedPostId.value, isModalVisible.value] as const,
  ([newId, visible]) => {
    localToggledReactions.value = new Set()
    newCommentContent.value = ''
    errorMessage.value = null
    actionFeedbackMessage.value = null

    if (visible && newId) {
      void loadPostThread(newId)
    } else if (!newId) {
      post.value = null
      comments.value = []
      reactions.value = []
    }
  },
  { immediate: true },
)

onMounted(() => {
  window.addEventListener('keydown', handleKeydown)
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <div v-if="isModalVisible" data-screen-id="SCR-POST-001">
    <!-- Bootstrap 5 Modal Dialog -->
    <div
      class="modal fade show d-block"
      tabindex="-1"
      role="dialog"
      aria-labelledby="postDetailModalTitle"
      aria-modal="true"
      @click.self="handleClose"
    >
      <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
        <div class="modal-content shadow border">
          <!-- Modal Header -->
          <div class="modal-header py-1 px-3 bg-body-tertiary">
            <div class="d-flex flex-column gap-1 pe-2 flex-grow-1">
              <div class="d-flex flex-wrap align-items-center gap-1">
                <span class="badge bg-secondary-subtle text-secondary-emphasis font-monospace" style="font-size: 10px">
                  SCR-POST-001
                </span>

                <!-- Exception Badge -->
                <span
                  v-if="post?.isException"
                  class="badge bg-danger"
                  style="font-size: 10px"
                  data-testid="post-exception-badge"
                >
                  <i class="bi bi-exclamation-triangle-fill me-1" aria-hidden="true"></i>
                  {{ post.exceptionType || 'EXCEPTION' }}
                </span>

                <!-- Status Badge -->
                <span
                  v-if="post?.status"
                  class="badge"
                  style="font-size: 10px"
                  :class="isArchived ? 'bg-secondary' : 'bg-success-subtle text-success-emphasis'"
                >
                  {{ post.status }}
                </span>

                <!-- Visibility Badge -->
                <span
                  v-if="post?.visibility"
                  class="badge"
                  style="font-size: 10px"
                  :class="isHidden ? 'bg-warning text-dark' : 'bg-light text-secondary border'"
                >
                  {{ post.visibility }}
                </span>

                <!-- Source Badge -->
                <span
                  v-if="post?.source"
                  class="badge bg-info-subtle text-info-emphasis"
                  style="font-size: 10px"
                >
                  {{ post.source === 'SYSTEM_GENERATED' ? 'System Event' : 'Operational Post' }}
                </span>
              </div>

              <h2 id="postDetailModalTitle" class="modal-title h6 fw-bold mb-0">
                {{ post?.title || (isLoadingPost ? 'Loading post thread…' : 'Operational Post Detail') }}
              </h2>
            </div>

            <button
              type="button"
              class="btn-close py-1 px-2"
              aria-label="Close modal"
              data-testid="post-modal-close-btn"
              @click="handleClose"
            ></button>
          </div>

          <!-- Modal Body -->
          <div class="modal-body p-2 p-md-3">
            <!-- Alert Banners -->
            <div
              v-if="errorMessage"
              class="alert alert-danger alert-dismissible fade show py-1 px-2 mb-2 small"
              role="alert"
              data-testid="post-modal-error"
            >
              <i class="bi bi-exclamation-octagon-fill me-1" aria-hidden="true"></i>
              {{ errorMessage }}
              <button
                type="button"
                class="btn-close py-1 px-2"
                aria-label="Dismiss error"
                @click="errorMessage = null"
              ></button>
            </div>

            <div
              v-if="actionFeedbackMessage"
              class="alert alert-success alert-dismissible fade show py-1 px-2 mb-2 small"
              role="status"
              data-testid="post-modal-feedback"
            >
              <i class="bi bi-check-circle-fill me-1" aria-hidden="true"></i>
              {{ actionFeedbackMessage }}
              <button
                type="button"
                class="btn-close py-1 px-2"
                aria-label="Dismiss notice"
                @click="actionFeedbackMessage = null"
              ></button>
            </div>

            <!-- Loading State -->
            <div
              v-if="isLoadingPost && !post"
              class="py-4 text-center text-body-secondary"
              data-testid="post-modal-loading"
            >
              <div class="spinner-border spinner-border-sm text-primary mb-1" role="status">
                <span class="visually-hidden">Loading post details…</span>
              </div>
              <p class="mb-0 small">Loading post thread and discussion…</p>
            </div>

            <!-- Post Thread Content -->
            <template v-else-if="post">
              <!-- Author, Timestamp & Context Metadata Bar -->
              <div class="d-flex flex-wrap justify-content-between align-items-center gap-1 mb-2 pb-1 border-bottom">
                <div class="d-flex flex-wrap align-items-center gap-2 text-body-secondary small" style="font-size: 11.5px">
                  <span class="fw-semibold text-body" data-testid="post-author">
                    <i class="bi bi-person-circle me-1" aria-hidden="true"></i>
                    {{ postAuthorDisplay }}
                  </span>
                  <span>•</span>
                  <time :datetime="post.createdAt" class="font-monospace" style="font-size: 11px" data-testid="post-created-at">
                    <i class="bi bi-clock me-1" aria-hidden="true"></i>
                    {{ formatDateTime(post.createdAt) }}
                  </time>
                </div>

                <!-- Context Badges (Customer & Product) -->
                <div class="d-flex flex-wrap align-items-center gap-1">
                  <span
                    v-if="post.customerName"
                    class="badge bg-primary-subtle text-primary-emphasis"
                    style="font-size: 10px"
                    data-testid="post-customer-badge"
                  >
                    <i class="bi bi-building me-1" aria-hidden="true"></i>
                    {{ post.customerName }}
                  </span>
                  <span
                    v-if="post.productName"
                    class="badge bg-secondary-subtle text-secondary-emphasis"
                    style="font-size: 10px"
                    data-testid="post-product-badge"
                  >
                    <i class="bi bi-box-seam me-1" aria-hidden="true"></i>
                    {{ post.productName }}
                  </span>
                </div>
              </div>

              <!-- Post Body / Content -->
              <div
                class="card bg-body-tertiary border mb-2 shadow-none"
                data-testid="post-body-card"
              >
                <div class="card-body p-2">
                  <p class="card-text mb-0 small" style="white-space: pre-wrap; font-size: 12.5px; line-height: 1.4" data-testid="post-body">
                    {{ postBody }}
                  </p>
                </div>
              </div>

              <!-- Contextual Reference Navigation (UC-FCOL-004: Navigate to Request / Work Package) -->
              <div
                v-if="effectiveRequestId || effectiveWorkPackageId"
                class="d-flex flex-wrap align-items-center justify-content-between gap-1 p-1 px-2 mb-2 rounded border bg-light-subtle small"
                data-testid="post-reference-links"
              >
                <div class="text-body-secondary" style="font-size: 11px">
                  <i class="bi bi-link-45deg me-1" aria-hidden="true"></i>
                  <span class="fw-semibold text-body">Referenced Context:</span>
                </div>

                <div class="d-flex flex-wrap align-items-center gap-1">
                  <RouterLink
                    v-if="effectiveRequestId"
                    :to="`/requests/${effectiveRequestId}`"
                    class="btn btn-xs btn-outline-primary d-inline-flex align-items-center gap-1 py-0 px-2"
                    style="font-size: 11px; height: 22px; line-height: 20px"
                    data-testid="post-request-link"
                    @click="navigateToRequest(effectiveRequestId)"
                  >
                    <i class="bi bi-arrow-up-right-square" aria-hidden="true"></i>
                    <span>{{ effectiveRequestLabel }}</span>
                  </RouterLink>

                  <RouterLink
                    v-if="effectiveWorkPackageId"
                    :to="`/work-packages/${effectiveWorkPackageId}`"
                    class="btn btn-xs btn-outline-secondary d-inline-flex align-items-center gap-1 py-0 px-2"
                    style="font-size: 11px; height: 22px; line-height: 20px"
                    data-testid="post-work-package-link"
                    @click="navigateToWorkPackage(effectiveWorkPackageId)"
                  >
                    <i class="bi bi-kanban" aria-hidden="true"></i>
                    <span>{{ effectiveWorkPackageLabel }}</span>
                  </RouterLink>
                </div>
              </div>

              <!-- Operational Reactions Bar (UC-FCOL-002 / FEAT-FCOL-002) -->
              <section class="mb-2" aria-label="Operational reactions">
                <div class="d-flex align-items-center justify-content-between mb-1">
                  <span class="fw-semibold small text-uppercase text-body-secondary" style="font-size: 10.5px">
                    Reactions
                    <span class="badge text-bg-secondary ms-1" style="font-size: 9px">{{ totalReactionCount }}</span>
                  </span>
                  <span class="text-body-secondary" style="font-size: 10px">
                    Click to toggle
                  </span>
                </div>

                <div class="d-flex flex-wrap gap-1" data-testid="post-reaction-buttons">
                  <button
                    v-for="option in REACTION_OPTIONS"
                    :key="option.type"
                    type="button"
                    class="btn btn-xs d-inline-flex align-items-center gap-1 py-0 px-1.5"
                    style="font-size: 11px; height: 24px; line-height: 22px"
                    :class="
                      hasUserReacted(option.type)
                        ? 'btn-primary'
                        : 'btn-outline-secondary'
                    "
                    :title="option.description"
                    :disabled="pendingReactionType !== null || isArchived"
                    :data-testid="`reaction-btn-${option.type}`"
                    :aria-pressed="hasUserReacted(option.type)"
                    @click="handleToggleReaction(option.type)"
                  >
                    <i class="bi" :class="option.iconClass" aria-hidden="true"></i>
                    <span>{{ option.label }}</span>
                    <span
                      class="badge rounded-pill ms-0.5"
                      style="font-size: 9.5px; padding: 1px 4px"
                      :class="
                        hasUserReacted(option.type)
                          ? 'bg-light text-primary'
                          : 'bg-secondary-subtle text-secondary-emphasis'
                      "
                      :data-testid="`reaction-count-${option.type}`"
                    >
                      {{ reactionCountsByType[option.type] ?? 0 }}
                    </span>
                  </button>
                </div>
              </section>

              <hr class="my-2" />

              <!-- Comments Section (UC-FCOL-001 / FEAT-FCOL-001) -->
              <section aria-label="Discussion comments">
                <div class="d-flex align-items-center justify-content-between mb-1">
                  <span class="fw-semibold small" style="font-size: 11.5px">
                    <i class="bi bi-chat-left-text me-1 text-primary" aria-hidden="true"></i>
                    Discussion Comments
                    <span class="badge text-bg-secondary ms-1" style="font-size: 10px" data-testid="post-comment-count">
                      {{ comments.length }}
                    </span>
                  </span>

                  <button
                    type="button"
                    class="btn btn-link text-decoration-none p-0 small"
                    style="font-size: 11px"
                    :disabled="isLoadingComments"
                    @click="refreshThreadState"
                  >
                    <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>Refresh
                  </button>
                </div>

                <!-- Comments Loading Indicator -->
                <div
                  v-if="isLoadingComments && comments.length === 0"
                  class="text-center py-2 text-body-secondary small"
                >
                  <span class="spinner-border spinner-border-sm me-1" role="status"></span>
                  Loading comments…
                </div>

                <!-- Empty Comments State -->
                <div
                  v-else-if="comments.length === 0"
                  class="text-center py-2 bg-body-tertiary rounded mb-2 text-body-secondary small"
                  style="font-size: 11.5px"
                  data-testid="post-comments-empty"
                >
                  No comments yet. Start the operational discussion below.
                </div>

                <!-- Comments List -->
                <ul
                  v-else
                  class="list-group list-group-flush border rounded mb-2"
                  style="max-height: 240px; overflow-y: auto"
                  data-testid="post-comments-list"
                >
                  <li
                    v-for="comment in comments"
                    :key="comment.id"
                    class="list-group-item py-1.5 px-2"
                    data-testid="post-comment-item"
                  >
                    <div class="d-flex justify-content-between align-items-center mb-0.5">
                      <span class="fw-semibold small" style="font-size: 11.5px" data-testid="comment-author">
                        <i class="bi bi-person me-1 text-secondary" aria-hidden="true"></i>
                        {{ formatCommentAuthor(comment) }}
                      </span>
                      <time
                        class="text-body-secondary font-monospace"
                        style="font-size: 10.5px"
                        :datetime="comment.createdAt"
                        data-testid="comment-created-at"
                      >
                        {{ formatDateTime(comment.createdAt) }}
                      </time>
                    </div>
                    <p
                      class="mb-0 small text-body"
                      style="white-space: pre-wrap; font-size: 12px; line-height: 1.35"
                      data-testid="comment-content"
                    >
                      {{ comment.content }}
                    </p>
                  </li>
                </ul>

                <!-- Comment Input Form -->
                <form
                  class="card bg-body-tertiary border rounded shadow-none mb-0"
                  data-testid="post-comment-form"
                  @submit.prevent="handleCommentSubmit"
                >
                  <div class="card-body p-2">
                    <div class="d-flex align-items-center justify-content-between mb-1">
                      <label for="newPostCommentTextarea" class="form-label mb-0 small fw-semibold" style="font-size: 11px">
                        Add Comment
                      </label>
                      <span v-if="isArchived" class="text-muted small" style="font-size: 10.5px">
                        Post archived (read-only).
                      </span>
                    </div>
                    <textarea
                      id="newPostCommentTextarea"
                      v-model="newCommentContent"
                      class="form-control form-control-sm mb-1"
                      rows="2"
                      placeholder="Write comment..."
                      :disabled="isSubmittingComment || isArchived"
                      required
                      data-testid="post-comment-textarea"
                    ></textarea>
                    <div class="d-flex justify-content-end">
                      <button
                        type="submit"
                        class="btn btn-primary btn-sm py-0 px-2"
                        style="font-size: 11px; height: 24px; line-height: 22px"
                        :disabled="isSubmittingComment || !newCommentContent.trim() || isArchived"
                        data-testid="post-comment-submit"
                      >
                        <span
                          v-if="isSubmittingComment"
                          class="spinner-border spinner-border-sm me-1"
                          role="status"
                          aria-hidden="true"
                        ></span>
                        <i v-else class="bi bi-send me-1" aria-hidden="true"></i>
                        Post Comment
                      </button>
                    </div>
                  </div>
                </form>
              </section>
            </template>
          </div>

          <!-- Modal Footer -->
          <div class="modal-footer py-1 px-3 bg-body-tertiary d-flex justify-content-between align-items-center">
            <div class="d-flex flex-wrap gap-1">
              <button
                v-if="post"
                type="button"
                class="btn btn-sm btn-outline-secondary py-0 px-2"
                style="font-size: 11px; height: 24px; line-height: 22px"
                :disabled="isTogglingVisibility"
                data-testid="post-visibility-toggle-btn"
                @click="handleToggleVisibility"
              >
                <i
                  class="bi me-1"
                  :class="isHidden ? 'bi-eye' : 'bi-eye-slash'"
                  aria-hidden="true"
                ></i>
                {{ isHidden ? 'Make Visible' : 'Hide Post' }}
              </button>

              <button
                v-if="post && !isArchived"
                type="button"
                class="btn btn-sm btn-outline-warning py-0 px-2"
                style="font-size: 11px; height: 24px; line-height: 22px"
                :disabled="isArchiving"
                data-testid="post-archive-btn"
                @click="handleArchivePost"
              >
                <i class="bi bi-archive me-1" aria-hidden="true"></i>
                Archive
              </button>
            </div>

            <button
              type="button"
              class="btn btn-secondary btn-sm py-0 px-2"
              style="font-size: 11px; height: 24px; line-height: 22px"
              data-testid="post-modal-footer-close-btn"
              @click="handleClose"
            >
              Close
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Bootstrap 5 Modal Backdrop -->
    <div class="modal-backdrop fade show" @click="handleClose"></div>
  </div>
</template>
