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
  customerCode?: string | null
  productId?: string | null
  productName?: string | null
  productCode?: string | null
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

// Lookup caches to guarantee resolving Customer Code and Product Code even if backend thread only provides IDs/names
const customerCacheById = ref<Record<string, { code: string; name: string }>>({})
const productCacheById = ref<Record<string, { code: string; name: string }>>({})
const hasLoadedLookups = ref(false)

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

const postAuthorInitials = computed<string>(() => {
  const name = postAuthorDisplay.value.trim()
  if (!name || name === 'SYSTEM') {
    return 'SYS'
  }
  const parts = name.split(/\s+/).filter(Boolean)
  if (parts.length >= 2) {
    return (parts[0][0] + parts[1][0]).toUpperCase()
  }
  return name.slice(0, 2).toUpperCase()
})

const isArchived = computed<boolean>(
  () => (post.value?.status ?? '').toUpperCase() === 'ARCHIVED',
)

const isHidden = computed<boolean>(
  () => (post.value?.visibility ?? '').toUpperCase() === 'HIDDEN',
)

/**
 * Customer and Product display helpers:
 * Display code instead of name, name is shown as tooltip.
 */
const customerDisplayCode = computed<string>(() => {
  // 1. Direct code on post payload
  const directCode = post.value?.customerCode?.trim()
  if (directCode) {
    return directCode
  }

  // 2. Lookup by customer ID in cache
  const custId = post.value?.customerId?.trim().toLowerCase()
  if (custId && customerCacheById.value[custId]?.code) {
    return customerCacheById.value[custId].code
  }

  // 3. Lookup by customer name in cache values
  const name = post.value?.customerName?.trim()
  if (name) {
    const matched = Object.values(customerCacheById.value).find(
      (c) => c.name.toLowerCase() === name.toLowerCase(),
    )
    if (matched?.code) {
      return matched.code
    }
    return name
  }

  return ''
})

const customerTooltipName = computed<string>(() => {
  const custId = post.value?.customerId?.trim().toLowerCase()
  const cached = custId ? customerCacheById.value[custId] : undefined
  const name = cached?.name || post.value?.customerName?.trim()
  const code = customerDisplayCode.value.trim()

  if (name && code && name !== code) {
    return `${name} (${code})`
  }
  return name || code || ''
})

const productDisplayCode = computed<string>(() => {
  // 1. Direct code on post payload
  const directCode = post.value?.productCode?.trim()
  if (directCode) {
    return directCode
  }

  // 2. Lookup by product ID in cache
  const prodId = post.value?.productId?.trim().toLowerCase()
  if (prodId && productCacheById.value[prodId]?.code) {
    return productCacheById.value[prodId].code
  }

  // 3. Lookup by product name in cache values
  const name = post.value?.productName?.trim()
  if (name) {
    const matched = Object.values(productCacheById.value).find(
      (p) => p.name.toLowerCase() === name.toLowerCase(),
    )
    if (matched?.code) {
      return matched.code
    }
    return name
  }

  return ''
})

const productTooltipName = computed<string>(() => {
  const prodId = post.value?.productId?.trim().toLowerCase()
  const cached = prodId ? productCacheById.value[prodId] : undefined
  const name = cached?.name || post.value?.productName?.trim()
  const code = productDisplayCode.value.trim()

  if (name && code && name !== code) {
    return `${name} (${code})`
  }
  return name || code || ''
})

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

function formatCommentAuthorInitials(comment: PostCommentItem): string {
  const name = formatCommentAuthor(comment)
  const parts = name.split(/\s+/).filter(Boolean)
  if (parts.length >= 2) {
    return (parts[0][0] + parts[1][0]).toUpperCase()
  }
  return name.slice(0, 2).toUpperCase()
}

/**
 * Loads lookup caches (`/customers/active` and `/products/active`) to guarantee resolving
 * customer and product codes even for older posts or posts without embedded codes.
 */
async function ensureLookupsLoaded(): Promise<void> {
  if (hasLoadedLookups.value) {
    return
  }
  try {
    const [custRes, prodRes] = await Promise.all([
      httpClient.get<Array<{ id?: string; customerId?: string; code?: string; customerCode?: string; name?: string; customerName?: string }>>('/customers/active').catch(() => null),
      httpClient.get<Array<{ id?: string; productId?: string; code?: string; productCode?: string; name?: string; productName?: string }>>('/products/active').catch(() => null),
    ])

    if (Array.isArray(custRes?.data)) {
      const cMap: Record<string, { code: string; name: string }> = {}
      for (const c of custRes.data) {
        const id = (c.id || c.customerId || '').trim().toLowerCase()
        const code = (c.customerCode || c.code || '').trim()
        const name = (c.customerName || c.name || '').trim()
        if (id) {
          cMap[id] = { code: code || name, name: name || code }
        }
      }
      customerCacheById.value = cMap
    }

    if (Array.isArray(prodRes?.data)) {
      const pMap: Record<string, { code: string; name: string }> = {}
      for (const p of prodRes.data) {
        const id = (p.id || p.productId || '').trim().toLowerCase()
        const code = (p.productCode || p.code || '').trim()
        const name = (p.productName || p.name || '').trim()
        if (id) {
          pMap[id] = { code: code || name, name: name || code }
        }
      }
      productCacheById.value = pMap
    }

    hasLoadedLookups.value = true
  } catch {
    // Gracefully ignore lookup failure; fallbacks remain in place
  }
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
    void ensureLookupsLoaded()
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
        <div class="modal-content border-0 shadow-lg rounded-4 overflow-hidden bg-body">
          <!-- 1 & 2. Modern Collaboration Header (Prominent Title + Secondary Metadata + Compact Info Bar) -->
          <div class="modal-header border-0 pb-2 pt-3 px-3 px-md-4 d-block bg-body">
            <!-- Utility Bar: Metadata Badges + Top Right Utility Controls -->
            <div class="d-flex align-items-center justify-content-between gap-2 mb-2">
              <!-- Secondary Metadata Row (SCR-POST-001 • Active • Visible • System Event) -->
              <div class="d-flex flex-wrap align-items-center gap-1.5 text-body-secondary small">
                <span class="font-monospace text-body-secondary me-1" style="font-size: 11.5px">
                  SCR-POST-001
                </span>
                <span class="text-secondary-subtle">•</span>

                <!-- Status Badge -->
                <span
                  v-if="post?.status"
                  class="badge rounded-pill fw-medium px-2 py-0.5"
                  style="font-size: 10.5px"
                  :class="isArchived ? 'bg-secondary text-white' : 'bg-success-subtle text-success-emphasis border border-success-subtle'"
                >
                  {{ post.status }}
                </span>

                <!-- Visibility Badge -->
                <span
                  v-if="post?.visibility"
                  class="badge rounded-pill fw-medium px-2 py-0.5"
                  style="font-size: 10.5px"
                  :class="isHidden ? 'bg-warning-subtle text-warning-emphasis border border-warning-subtle' : 'bg-secondary-subtle text-body-secondary border border-secondary-subtle'"
                >
                  {{ post.visibility }}
                </span>

                <!-- Source Badge -->
                <span
                  v-if="post?.source"
                  class="badge rounded-pill fw-medium px-2 py-0.5 bg-info-subtle text-info-emphasis border border-info-subtle"
                  style="font-size: 10.5px"
                >
                  {{ post.source === 'SYSTEM_GENERATED' ? 'System Event' : 'Operational Post' }}
                </span>

                <!-- Exception Badge -->
                <span
                  v-if="post?.isException"
                  class="badge rounded-pill fw-medium px-2 py-0.5 bg-danger-subtle text-danger-emphasis border border-danger-subtle"
                  style="font-size: 10.5px"
                  data-testid="post-exception-badge"
                >
                  <i class="bi bi-exclamation-triangle-fill me-1" aria-hidden="true"></i>
                  {{ post.exceptionType || 'EXCEPTION' }}
                </span>
              </div>

              <!-- Top Right Utilities: Hide/Visible, Archive & Close -->
              <div class="d-flex align-items-center gap-1">
                <button
                  v-if="post"
                  type="button"
                  class="btn btn-sm btn-link text-body-secondary text-decoration-none py-0 px-1.5 d-inline-flex align-items-center gap-1"
                  style="font-size: 11.5px"
                  :disabled="isTogglingVisibility"
                  data-testid="post-visibility-toggle-btn"
                  :title="isHidden ? 'Make Visible' : 'Hide Post'"
                  @click="handleToggleVisibility"
                >
                  <i
                    class="bi"
                    :class="isHidden ? 'bi-eye' : 'bi-eye-slash'"
                    aria-hidden="true"
                  ></i>
                  <span class="d-none d-sm-inline">{{ isHidden ? 'Unhide' : 'Hide' }}</span>
                </button>

                <button
                  v-if="post && !isArchived"
                  type="button"
                  class="btn btn-sm btn-link text-body-secondary text-decoration-none py-0 px-1.5 d-inline-flex align-items-center gap-1"
                  style="font-size: 11.5px"
                  :disabled="isArchiving"
                  data-testid="post-archive-btn"
                  title="Archive Post"
                  @click="handleArchivePost"
                >
                  <i class="bi bi-archive" aria-hidden="true"></i>
                  <span class="d-none d-sm-inline">Archive</span>
                </button>

                <button
                  type="button"
                  class="btn-close ms-1 p-1"
                  aria-label="Close modal"
                  data-testid="post-modal-close-btn"
                  @click="handleClose"
                ></button>
              </div>
            </div>

            <!-- Prominent Request Title (Large, dominant element) -->
            <h1 id="postDetailModalTitle" class="h4 fw-bold mb-2 text-body">
              {{ post?.title || (isLoadingPost ? 'Loading post thread…' : 'Operational Post Detail') }}
            </h1>

            <!-- 2. Compact Metadata Bar (Unboxed, single muted line with Avatar) -->
            <div
              v-if="post"
              class="d-flex flex-wrap align-items-center gap-2 text-body-secondary small"
              style="font-size: 12px"
            >
              <div class="d-flex align-items-center gap-1.5">
                <span
                  class="avatar-circle rounded-circle bg-primary-subtle text-primary fw-semibold d-inline-flex align-items-center justify-content-center"
                  style="width: 22px; height: 22px; font-size: 10px"
                  aria-hidden="true"
                >
                  {{ postAuthorInitials }}
                </span>
                <span class="fw-semibold text-body" data-testid="post-author">
                  {{ postAuthorDisplay }}
                </span>
              </div>

              <span>•</span>

              <time
                :datetime="post.createdAt"
                data-testid="post-created-at"
                class="d-inline-flex align-items-center gap-1"
              >
                <i class="bi bi-clock" aria-hidden="true"></i>
                {{ formatDateTime(post.createdAt) }}
              </time>

              <template v-if="customerDisplayCode">
                <span>•</span>
                <span
                  class="d-inline-flex align-items-center gap-1 text-body-secondary font-monospace"
                  style="cursor: help"
                  :title="customerTooltipName"
                  data-testid="post-customer-badge"
                >
                  <i class="bi bi-building font-sans" aria-hidden="true"></i>
                  {{ customerDisplayCode }}
                </span>
              </template>

              <template v-if="productDisplayCode">
                <span>•</span>
                <span
                  class="d-inline-flex align-items-center gap-1 text-body-secondary font-monospace"
                  style="cursor: help"
                  :title="productTooltipName"
                  data-testid="post-product-badge"
                >
                  <i class="bi bi-box-seam font-sans" aria-hidden="true"></i>
                  {{ productDisplayCode }}
                </span>
              </template>
            </div>
          </div>

          <!-- Modal Body: Collaborative Document & Discussion Stream -->
          <div class="modal-body pt-2 pb-3 px-3 px-md-4">
            <!-- Alert Banners -->
            <div
              v-if="errorMessage"
              class="alert alert-danger alert-dismissible fade show py-1.5 px-3 mb-3 small rounded-3"
              role="alert"
              data-testid="post-modal-error"
            >
              <i class="bi bi-exclamation-octagon-fill me-1" aria-hidden="true"></i>
              {{ errorMessage }}
              <button
                type="button"
                class="btn-close py-1.5 px-2"
                aria-label="Dismiss error"
                @click="errorMessage = null"
              ></button>
            </div>

            <div
              v-if="actionFeedbackMessage"
              class="alert alert-success alert-dismissible fade show py-1.5 px-3 mb-3 small rounded-3"
              role="status"
              data-testid="post-modal-feedback"
            >
              <i class="bi bi-check-circle-fill me-1" aria-hidden="true"></i>
              {{ actionFeedbackMessage }}
              <button
                type="button"
                class="btn-close py-1.5 px-2"
                aria-label="Dismiss notice"
                @click="actionFeedbackMessage = null"
              ></button>
            </div>

            <!-- Loading State -->
            <div
              v-if="isLoadingPost && !post"
              class="py-5 text-center text-body-secondary"
              data-testid="post-modal-loading"
            >
              <div class="spinner-border spinner-border-sm text-primary mb-2" role="status">
                <span class="visually-hidden">Loading post details…</span>
              </div>
              <p class="mb-0 small">Loading collaboration workspace…</p>
            </div>

            <!-- Post Document & Collaboration Workspace -->
            <template v-else-if="post">
              <!-- 3. Post Body: Document Layout (No textbox, no inset border, natural document flow) -->
              <article class="post-document-body mb-3 py-1" data-testid="post-body-card">
                <div
                  class="post-content-text text-body"
                  style="white-space: pre-wrap; font-size: 14px; line-height: 1.65"
                  data-testid="post-body"
                >
                  {{ postBody }}
                </div>
              </article>

              <!-- 4. Referenced Context Action Button(s) (Open Request) -->
              <div
                v-if="effectiveRequestId || effectiveWorkPackageId"
                class="d-flex flex-wrap align-items-center gap-1.5 mb-3"
                data-testid="post-reference-links"
              >
                <RouterLink
                  v-if="effectiveRequestId"
                  :to="`/requests/${effectiveRequestId}`"
                  class="btn btn-sm btn-outline-secondary py-1 px-2.5 rounded-pill d-inline-flex align-items-center gap-1.5"
                  style="font-size: 11.5px; height: 28px"
                  :title="`Open ${effectiveRequestLabel}`"
                  :aria-label="`Open ${effectiveRequestLabel}`"
                  data-testid="post-request-link"
                  @click="navigateToRequest(effectiveRequestId)"
                >
                  <i class="bi bi-box-arrow-up-right text-primary" aria-hidden="true" style="font-size: 11px"></i>
                  <span>Open Request</span>
                </RouterLink>

                <RouterLink
                  v-if="effectiveWorkPackageId"
                  :to="`/work-packages/${effectiveWorkPackageId}`"
                  class="btn btn-sm btn-outline-secondary py-1 px-2.5 rounded-pill d-inline-flex align-items-center gap-1.5"
                  style="font-size: 11.5px; height: 28px"
                  data-testid="post-work-package-link"
                  @click="navigateToWorkPackage(effectiveWorkPackageId)"
                >
                  <i class="bi bi-kanban" aria-hidden="true" style="font-size: 11px"></i>
                  <span>{{ effectiveWorkPackageLabel }}</span>
                </RouterLink>
              </div>

              <!-- 5. Modernized Reaction Chips (GitHub-style rounded pills with soft background) -->
              <section class="mb-4" aria-label="Operational reactions">
                <div class="d-flex align-items-center justify-content-between mb-1.5">
                  <span class="text-body-secondary fw-semibold text-uppercase tracking-wider" style="font-size: 11px">
                    Reactions
                    <span v-if="totalReactionCount > 0" class="badge rounded-pill bg-body-secondary text-body ms-1" style="font-size: 10px">
                      {{ totalReactionCount }}
                    </span>
                  </span>
                </div>
                <div class="d-flex flex-wrap align-items-center gap-2" data-testid="post-reaction-buttons">
                  <button
                    v-for="option in REACTION_OPTIONS"
                    :key="option.type"
                    type="button"
                    class="btn btn-sm rounded-pill d-inline-flex align-items-center gap-2 py-1 px-3 transition-all border-0 shadow-none"
                    style="font-size: 13px; height: 30px"
                    :class="
                      hasUserReacted(option.type)
                        ? 'bg-primary-subtle text-primary-emphasis fw-semibold ring-1 ring-primary'
                        : 'bg-body-secondary text-body-secondary'
                    "
                    :title="`${option.label}: ${option.description}`"
                    :aria-label="option.label"
                    :disabled="pendingReactionType !== null || isArchived"
                    :data-testid="`reaction-btn-${option.type}`"
                    :aria-pressed="hasUserReacted(option.type)"
                    @click="handleToggleReaction(option.type)"
                  >
                    <i class="bi me-1" :class="option.iconClass" aria-hidden="true"></i>
                    <span
                      class="badge rounded-pill fw-semibold ms-1"
                      style="font-size: 10.5px; padding: 2px 6px"
                      :class="
                        hasUserReacted(option.type)
                          ? 'bg-primary text-white'
                          : 'bg-body text-body'
                      "
                      :data-testid="`reaction-count-${option.type}`"
                    >
                      {{ reactionCountsByType[option.type] ?? 0 }}
                    </span>
                  </button>
                </div>
              </section>

              <!-- 6. Discussion Timeline (Vertical conversational flow, no bordered blocks) -->
              <section class="mb-2" aria-label="Discussion comments">
                <div class="d-flex align-items-center justify-content-between mb-3">
                  <span class="text-body-secondary fw-semibold text-uppercase tracking-wider" style="font-size: 11px">
                    Discussion Activity
                    <span class="badge rounded-pill bg-body-secondary text-body ms-1" style="font-size: 10px" data-testid="post-comment-count">
                      {{ comments.length }}
                    </span>
                  </span>

                  <button
                    type="button"
                    class="btn btn-link text-body-secondary text-decoration-none p-0 small"
                    style="font-size: 11.5px"
                    :disabled="isLoadingComments"
                    @click="refreshThreadState"
                  >
                    <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>Refresh
                  </button>
                </div>

                <!-- Comments Loading Indicator -->
                <div
                  v-if="isLoadingComments && comments.length === 0"
                  class="text-center py-3 text-body-secondary small"
                >
                  <span class="spinner-border spinner-border-sm me-1" role="status"></span>
                  Loading comments…
                </div>

                <!-- Empty Comments State -->
                <div
                  v-else-if="comments.length === 0"
                  class="py-3 text-center text-body-secondary small rounded-3 bg-body-tertiary"
                  style="font-size: 12px"
                  data-testid="post-comments-empty"
                >
                  No comments yet. Start the conversation below.
                </div>

                <!-- Vertical Discussion Timeline (Clean conversational spacing) -->
                <div
                  v-else
                  class="discussion-timeline d-flex flex-column gap-3 mb-2"
                  style="max-height: 280px; overflow-y: auto"
                  data-testid="post-comments-list"
                >
                  <div
                    v-for="comment in comments"
                    :key="comment.id"
                    class="d-flex align-items-start gap-2.5"
                    data-testid="post-comment-item"
                  >
                    <!-- Author Avatar Circle -->
                    <div
                      class="avatar-circle rounded-circle bg-secondary-subtle text-secondary-emphasis fw-semibold flex-shrink-0 d-inline-flex align-items-center justify-content-center mt-0.5"
                      style="width: 28px; height: 28px; font-size: 10.5px"
                      aria-hidden="true"
                    >
                      {{ formatCommentAuthorInitials(comment) }}
                    </div>

                    <!-- Comment Body Flow -->
                    <div class="flex-grow-1 min-w-0">
                      <div class="d-flex align-items-baseline gap-2 mb-0.5">
                        <span class="fw-semibold text-body small" data-testid="comment-author">
                          {{ formatCommentAuthor(comment) }}
                        </span>
                        <time
                          class="text-body-tertiary font-monospace"
                          style="font-size: 11px"
                          :datetime="comment.createdAt"
                          data-testid="comment-created-at"
                        >
                          {{ formatDateTime(comment.createdAt) }}
                        </time>
                      </div>

                      <p
                        class="mb-0 text-body"
                        style="white-space: pre-wrap; font-size: 13px; line-height: 1.5"
                        data-testid="comment-content"
                      >
                        {{ comment.content }}
                      </p>
                    </div>
                  </div>
                </div>
              </section>
            </template>
          </div>

          <!-- 7 & 9. Sticky Comment Composer & Simplified Bottom Actions (Slack / GitHub style) -->
          <div class="modal-footer p-3 px-md-4 border-0 bg-body d-block">
            <form
              class="sticky-composer-container rounded-3 border bg-body-tertiary p-2 transition-all shadow-xs"
              data-testid="post-comment-form"
              @submit.prevent="handleCommentSubmit"
            >
              <textarea
                id="newPostCommentTextarea"
                v-model="newCommentContent"
                class="form-control form-control-sm border-0 bg-transparent shadow-none px-1 py-1"
                rows="2"
                placeholder="Write a comment..."
                :disabled="isSubmittingComment || isArchived"
                required
                style="font-size: 13px; resize: none"
                data-testid="post-comment-textarea"
                @keydown.enter.ctrl.prevent="handleCommentSubmit"
              ></textarea>

              <div class="d-flex align-items-center justify-content-between pt-1 border-top border-light-subtle">
                <span v-if="isArchived" class="text-muted small ps-1" style="font-size: 11px">
                  Post archived (read-only).
                </span>
                <span v-else class="text-body-tertiary small ps-1" style="font-size: 11px">
                  Press Ctrl+Enter to post
                </span>

                <div class="d-flex align-items-center gap-2">
                  <button
                    type="button"
                    class="btn btn-sm btn-link text-body-secondary text-decoration-none py-0 px-2"
                    style="font-size: 12px"
                    data-testid="post-modal-footer-close-btn"
                    @click="handleClose"
                  >
                    Close
                  </button>

                  <button
                    type="submit"
                    class="btn btn-primary btn-sm py-1 px-3 rounded-2 fw-medium d-inline-flex align-items-center gap-1.5"
                    style="font-size: 12px"
                    :disabled="isSubmittingComment || !newCommentContent.trim() || isArchived"
                    data-testid="post-comment-submit"
                  >
                    <span
                      v-if="isSubmittingComment"
                      class="spinner-border spinner-border-sm"
                      role="status"
                      aria-hidden="true"
                    ></span>
                    <i v-else class="bi bi-send-fill" aria-hidden="true" style="font-size: 10px"></i>
                    Post Comment
                  </button>
                </div>
              </div>
            </form>
          </div>
        </div>
      </div>
    </div>

    <!-- Bootstrap 5 Modal Backdrop -->
    <div class="modal-backdrop fade show" @click="handleClose"></div>
  </div>
</template>
