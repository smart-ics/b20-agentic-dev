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
  customerCode?: string | null
  customer?: string | null
  productId?: string | null
  productName?: string | null
  productCode?: string | null
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
    customerCodeMap?: Record<string, string>
    productCodeMap?: Record<string, string>
  }>(),
  {
    customerNameMap: () => ({}),
    productNameMap: () => ({}),
    customerCodeMap: () => ({}),
    productCodeMap: () => ({}),
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
const isCommentsOpen = ref<boolean>(false)
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

// Module-level cache to share lookups across FeedTimelineCard instances
const sharedCustomerCache = ref<Record<string, { code: string; name: string }>>({})
const sharedProductCache = ref<Record<string, { code: string; name: string }>>({})
let hasInitiatedLookup = false

async function ensureSharedLookups(): Promise<void> {
  if (hasInitiatedLookup) return
  hasInitiatedLookup = true
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
      sharedCustomerCache.value = cMap
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
      sharedProductCache.value = pMap
    }
  } catch {
    // Ignore error
  }
}

// Helper regex to extract code from formatted strings like "Name (CODE)"
function parseFormattedCode(str?: string | null): { code: string; name: string } | null {
  if (!str) return null
  const trimmed = str.trim()
  const match = trimmed.match(/^(.*?)\s*\(([^()]+)\)$/)
  if (match) {
    return { name: match[1].trim(), code: match[2].trim() }
  }
  return null
}

const customerDisplayCode = computed<string>(() => {
  // 1. Direct customerCode on item
  const direct = props.item.customerCode?.trim()
  if (direct) return direct

  // 2. From customerCodeMap by ID (case-insensitive)
  const custId = props.item.customerId?.trim().toLowerCase()
  if (custId) {
    const fromCodeMap = props.customerCodeMap[custId] || props.customerCodeMap[props.item.customerId!]
    if (fromCodeMap?.trim()) return fromCodeMap.trim()

    const fromNameMap = props.customerNameMap[custId] || props.customerNameMap[props.item.customerId!]
    if (fromNameMap) {
      const parsed = parseFormattedCode(fromNameMap)
      if (parsed?.code) return parsed.code
    }

    if (sharedCustomerCache.value[custId]?.code) {
      return sharedCustomerCache.value[custId].code
    }
  }

  // 3. From customer string if formatted with parenthesized code e.g. "Husada Jakarta (HJ-01)"
  const raw = props.item.customer ?? props.item.customerName
  if (raw) {
    const parsed = parseFormattedCode(raw)
    if (parsed?.code) return parsed.code

    // 4. Search customerNameMap by customer name
    const rawLower = raw.trim().toLowerCase()
    for (const [id, formatted] of Object.entries(props.customerNameMap)) {
      const p = parseFormattedCode(formatted)
      if (p && (p.name.toLowerCase() === rawLower || p.code.toLowerCase() === rawLower)) {
        return p.code
      }
      if (formatted.toLowerCase() === rawLower) {
        const cCode = props.customerCodeMap[id] || props.customerCodeMap[id.toLowerCase()]
        if (cCode) return cCode
      }
    }

    // 5. Search shared customer cache by name
    const matched = Object.values(sharedCustomerCache.value).find(
      (c) => c.name.toLowerCase() === rawLower,
    )
    if (matched?.code) {
      return matched.code
    }
  }

  // 6. If we have customerId, format short code identifier (NEVER full name)
  if (props.item.customerId) {
    return `CUST-${props.item.customerId.slice(0, 6).toUpperCase()}`
  }

  // 7. If we only have raw name, format short uppercase acronym (NEVER full name)
  if (raw && raw.trim().length > 0) {
    const words = raw.trim().split(/\s+/).filter(Boolean)
    if (words.length > 1) {
      return words.map(w => w[0]).join('').toUpperCase()
    }
    return raw.trim().slice(0, 6).toUpperCase()
  }

  return ''
})

const customerTooltipName = computed<string>(() => {
  const custId = props.item.customerId?.trim().toLowerCase()
  let fullName = ''

  if (custId) {
    const fromNameMap = props.customerNameMap[custId] || props.customerNameMap[props.item.customerId!]
    if (fromNameMap) {
      const parsed = parseFormattedCode(fromNameMap)
      fullName = parsed ? parsed.name : fromNameMap
    }
    if (!fullName && sharedCustomerCache.value[custId]?.name) {
      fullName = sharedCustomerCache.value[custId].name
    }
  }

  if (!fullName) {
    const raw = props.item.customer ?? props.item.customerName
    if (raw) {
      const parsed = parseFormattedCode(raw)
      fullName = parsed ? parsed.name : raw.trim()
    }
  }

  const code = customerDisplayCode.value.trim()
  if (fullName && code && fullName.toLowerCase() !== code.toLowerCase()) {
    return `${fullName} (${code})`
  }
  return fullName || code || 'Customer'
})

const productDisplayCode = computed<string>(() => {
  // 1. Direct productCode on item
  const direct = props.item.productCode?.trim()
  if (direct) return direct

  // 2. From productCodeMap by ID (case-insensitive)
  const prodId = props.item.productId?.trim().toLowerCase()
  if (prodId) {
    const fromCodeMap = props.productCodeMap[prodId] || props.productCodeMap[props.item.productId!]
    if (fromCodeMap?.trim()) return fromCodeMap.trim()

    const fromNameMap = props.productNameMap[prodId] || props.productNameMap[props.item.productId!]
    if (fromNameMap) {
      const parsed = parseFormattedCode(fromNameMap)
      if (parsed?.code) return parsed.code
    }

    if (sharedProductCache.value[prodId]?.code) {
      return sharedProductCache.value[prodId].code
    }
  }

  // 3. From product string if formatted with parenthesized code e.g. "My Hospital Web (MYHOSP-WEB)"
  const raw = props.item.product ?? props.item.productName
  if (raw) {
    const parsed = parseFormattedCode(raw)
    if (parsed?.code) return parsed.code

    // 4. Search productNameMap by product name
    const rawLower = raw.trim().toLowerCase()
    for (const [id, formatted] of Object.entries(props.productNameMap)) {
      const p = parseFormattedCode(formatted)
      if (p && (p.name.toLowerCase() === rawLower || p.code.toLowerCase() === rawLower)) {
        return p.code
      }
      if (formatted.toLowerCase() === rawLower) {
        const pCode = props.productCodeMap[id] || props.productCodeMap[id.toLowerCase()]
        if (pCode) return pCode
      }
    }

    // 5. Search shared product cache by name
    const matched = Object.values(sharedProductCache.value).find(
      (p) => p.name.toLowerCase() === rawLower,
    )
    if (matched?.code) {
      return matched.code
    }
  }

  // 6. If we have productId, format short code identifier (NEVER full name)
  if (props.item.productId) {
    return `PROD-${props.item.productId.slice(0, 6).toUpperCase()}`
  }

  // 7. If we only have raw name, format short uppercase acronym (NEVER full name)
  if (raw && raw.trim().length > 0) {
    const words = raw.trim().split(/\s+/).filter(Boolean)
    if (words.length > 1) {
      return words.map(w => w[0]).join('').toUpperCase()
    }
    return raw.trim().slice(0, 6).toUpperCase()
  }

  return ''
})

const productTooltipName = computed<string>(() => {
  const prodId = props.item.productId?.trim().toLowerCase()
  let fullName = ''

  if (prodId) {
    const fromNameMap = props.productNameMap[prodId] || props.productNameMap[props.item.productId!]
    if (fromNameMap) {
      const parsed = parseFormattedCode(fromNameMap)
      fullName = parsed ? parsed.name : fromNameMap
    }
    if (!fullName && sharedProductCache.value[prodId]?.name) {
      fullName = sharedProductCache.value[prodId].name
    }
  }

  if (!fullName) {
    const raw = props.item.product ?? props.item.productName
    if (raw) {
      const parsed = parseFormattedCode(raw)
      fullName = parsed ? parsed.name : raw.trim()
    }
  }

  const code = productDisplayCode.value.trim()
  if (fullName && code && fullName.toLowerCase() !== code.toLowerCase()) {
    return `${fullName} (${code})`
  }
  return fullName || code || 'Product'
})

const contentExcerpt = computed<string>(() => {
  const text = props.item.contentExcerpt ?? props.item.summary ?? ''
  return text.trim()
})

const shouldShowExcerpt = computed<boolean>(() => {
  if (!contentExcerpt.value) return false
  const title = (props.item.title || '').trim().toLowerCase()
  const excerpt = contentExcerpt.value.toLowerCase()
  return excerpt !== title
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
  const reqId = effectiveRequestId.value
  if (!reqId) return ''

  const rawRef = props.item.referenceDisplay?.trim()
  const title = props.item.title?.trim().toLowerCase()
  if (
    rawRef &&
    (props.item.referenceType ?? '').toUpperCase() === 'REQUEST' &&
    /^REQ[-#\s]?[0-9a-fA-F]+/i.test(rawRef) &&
    rawRef.toLowerCase() !== title
  ) {
    return rawRef
  }

  return `REQ #${reqId.slice(0, 8).toUpperCase()}`
})

const requestTooltipTitle = computed<string>(() => {
  const title = (props.item.referenceDisplay || props.item.title || '').trim()
  const reqId = effectiveRequestId.value
  if (reqId && title) {
    return `Request #${reqId.slice(0, 8)}: ${title}`
  }
  return title || (reqId ? `Request #${reqId.slice(0, 8)}` : 'Request')
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

function toggleComments(): void {
  isCommentsOpen.value = !isCommentsOpen.value
  if (isCommentsOpen.value) {
    if (comments.value.length === 0 && localCommentCount.value > 0) {
      void loadComments()
    }
    setTimeout(() => {
      commentTextareaRef.value?.focus()
    }, 50)
  }
}

function focusCommentInput(): void {
  isCommentsOpen.value = true
  if (comments.value.length === 0 && localCommentCount.value > 0) {
    void loadComments()
  }
  setTimeout(() => {
    commentTextareaRef.value?.focus()
  }, 50)
}

defineExpose({
  focusCommentInput,
})

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
  void ensureSharedLookups()
})
const isClosed = computed<boolean>(() => {
  const st = (props.item.status || '').toUpperCase()
  return st === 'CLOSED' || st === 'RESOLVED' || st === 'CANCELLED'
})

const isEscalation = computed<boolean>(() => {
  if (props.item.isException) return true
  const exc = (props.item.exceptionType || '').toUpperCase()
  const postT = (props.item.postType || '').toUpperCase()
  return exc.includes('ESCALAT') || postT.includes('ESCALAT')
})

const statusAccentClass = computed<string>(() => {
  if (isEscalation.value) {
    return 'status-accent-red'
  }
  if (isSystemGenerated.value) {
    return 'status-accent-cyan'
  }
  if (isClosed.value) {
    return 'status-accent-gray'
  }
  return 'status-accent-blue'
})
</script>

<template>
  <article
    class="op-feed-row op-feed-card p-3 mb-2.5 rounded-2 bg-white shadow-sm transition-all position-relative"
    :class="statusAccentClass"
    :data-testid="`feed-card-${resolvedPostId}`"
  >
    <!-- Row 1: Header - Streamlined Essential Badges & Meta -->
    <div class="d-flex flex-wrap align-items-center justify-content-between gap-2 mb-1.5">
      <div class="d-flex flex-wrap align-items-center gap-1.5 min-w-0">
        <!-- Event Type Badge (SYS / ESCALATION) with muted calm colors -->
        <span
          v-if="isEscalation"
          class="badge font-monospace fs-11 fw-medium bg-danger-subtle text-danger border border-danger-subtle"
          data-testid="feed-card-exception-badge"
        >
          <i class="bi bi-exclamation-triangle-fill me-1" aria-hidden="true"></i>
          {{ item.exceptionType || 'ESCALATION' }}
        </span>

        <span
          v-else-if="isSystemGenerated"
          class="badge font-monospace fs-11 fw-medium bg-info-subtle text-info-emphasis border border-info-subtle"
        >
          SYS
        </span>

        <!-- Request Number Badge / Link -->
        <RouterLink
          v-if="effectiveRequestId"
          :to="`/requests/${effectiveRequestId}`"
          class="badge font-monospace fs-11 fw-medium bg-light text-secondary border border-secondary-subtle text-decoration-none text-hover-primary"
          data-testid="feed-card-request-link"
          :title="requestTooltipTitle"
          @click.stop="navigateToRequest(effectiveRequestId)"
        >
          {{ effectiveRequestDisplay }}
        </RouterLink>

        <!-- Work Package Badge / Link -->
        <RouterLink
          v-if="effectiveWorkPackageId"
          :to="`/work-packages/${effectiveWorkPackageId}`"
          class="badge font-monospace fs-11 fw-medium bg-light text-secondary border border-secondary-subtle text-decoration-none"
          data-testid="feed-card-work-package-link"
          @click.stop
        >
          WP
        </RouterLink>
      </div>

      <!-- Right Header Actions: Author, Timestamp, and Quick Reaction Pill -->
      <div class="d-flex align-items-center gap-2 ms-auto flex-shrink-0 text-muted fs-11">
        <span class="d-none d-sm-inline" data-testid="feed-card-author">
          <i class="bi bi-person me-0.5" aria-hidden="true"></i>
          {{ authorDisplay }}
        </span>
        <span class="text-secondary-emphasis" data-testid="feed-card-created-at">
          <i class="bi bi-clock me-0.5" aria-hidden="true"></i>
          {{ formatTimestamp(item.createdAt) }}
        </span>

        <!-- React Button + Floating Palette Container -->
        <div
          class="position-relative d-inline-block"
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

          <!-- React Action Button (Compact Pill) -->
          <button
            type="button"
            class="btn btn-sm py-0.5 px-2 rounded-pill fs-11 font-medium d-inline-flex align-items-center gap-1 border border-light-subtle"
            :class="activeReactionMeta ? `${activeReactionMeta.colorClass} border-primary bg-primary bg-opacity-10 fw-semibold` : 'btn-light text-secondary'"
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
            <span v-if="totalReactionCount > 0" class="fw-bold font-monospace ms-0.5" data-testid="feed-card-reaction-count">({{ totalReactionCount }})</span>
          </button>
        </div>
      </div>
    </div>

    <!-- Row 2: Request Title (High Visual Priority) -->
    <div class="mb-1">
      <button
        type="button"
        class="btn btn-link text-dark fw-semibold text-start p-0 text-decoration-none feed-title-link w-100"
        data-testid="feed-card-title"
        :title="item.title"
        @click.stop="openModal"
      >
        {{ item.title }}
      </button>
    </div>

    <!-- Row 3: Feed Message / Content Excerpt (Clear & Readable) -->
    <div
      v-if="shouldShowExcerpt"
      class="text-body-secondary fs-12 mb-2 line-clamp-2 cursor-pointer lh-base"
      data-testid="feed-card-excerpt"
      :title="contentExcerpt"
      @click="openModal"
    >
      {{ contentExcerpt }}
    </div>

    <!-- Row 3: Metadata Footer & Engagement Summary (Customer, Product, Signals, Comment trigger) -->
    <div class="d-flex flex-wrap align-items-center justify-content-between gap-1.5 pt-1 border-top border-light-subtle">
      <!-- Left Metadata Chips -->
      <div class="d-flex flex-wrap align-items-center gap-1.5 min-w-0">
        <!-- Customer Tag -->
        <span
          v-if="customerDisplayCode"
          class="badge bg-light text-dark border text-truncate font-monospace fs-11"
          style="max-width: 140px; cursor: help;"
          data-testid="feed-card-customer"
          :title="customerTooltipName"
        >
          <i class="bi bi-building me-1 text-secondary" aria-hidden="true"></i>
          {{ customerDisplayCode }}
        </span>

        <!-- Product Tag -->
        <span
          v-if="productDisplayCode"
          class="badge bg-light text-dark border text-truncate font-monospace fs-11"
          style="max-width: 130px; cursor: help;"
          data-testid="feed-card-product"
          :title="productTooltipName"
        >
          <i class="bi bi-box-seam me-1 text-secondary" aria-hidden="true"></i>
          {{ productDisplayCode }}
        </span>

        <!-- Reactions breakdown badges -->
        <div class="d-flex align-items-center gap-1 text-muted fs-11 ms-1" data-testid="feed-card-reaction-summary">
          <span v-if="totalReactionCount > 0" class="d-flex align-items-center gap-0.5">
            <span
              v-for="opt in presentReactionOptions"
              :key="opt.type"
              class="d-inline-flex align-items-center"
              :title="`${opt.label}: ${localReactionCounts[opt.type]}`"
            >
              <span>{{ opt.emoji }}</span>
            </span>
          </span>
        </div>
      </div>

      <!-- Right Engagement & Action Triggers -->
      <div class="d-flex align-items-center gap-1.5 ms-auto flex-shrink-0">
        <!-- Comment Toggle Button (Expand/Collapse drawer) -->
        <button
          type="button"
          class="btn btn-sm py-0 px-2 rounded-pill fs-11 d-inline-flex align-items-center gap-1 border transition-colors"
          :class="isCommentsOpen ? 'btn-primary text-white' : 'btn-light text-secondary'"
          data-testid="feed-card-comment-btn"
          @click.stop="toggleComments"
          title="Toggle comments drawer"
        >
          <i class="bi bi-chat-left-text" aria-hidden="true"></i>
          <span data-testid="feed-card-comment-count">{{ localCommentCount }} {{ localCommentCount === 1 ? 'comment' : 'comments' }}</span>
        </button>

        <!-- View Full Thread Button -->
        <button
          type="button"
          class="btn btn-sm btn-link text-decoration-none py-0 px-1 fs-11 text-body-secondary"
          data-testid="feed-card-open-modal-btn"
          @click.stop="openModal"
        >
          <span>Detail &rarr;</span>
        </button>
      </div>
    </div>

    <!-- Collapsible Comments Section (Opens smoothly on demand) -->
    <div v-if="isCommentsOpen" class="pt-2 border-top mt-2 bg-light bg-opacity-50 rounded-2 p-2">
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
        class="d-flex flex-column gap-1.5 mb-2"
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
      <div v-else-if="!isLoadingComments && localCommentCount === 0" class="text-muted fs-11 mb-2 ps-1 fst-italic">
        No comments yet. Write an operational note below.
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
            class="form-control form-control-sm pe-5 fs-12 rounded-3 bg-white"
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

<style scoped>
.op-feed-card {
  border: 1px solid #e5e7eb !important;
  background-color: #ffffff;
  border-left-width: 4px !important;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}

.op-feed-card:hover {
  border-color: #d1d5db !important;
  box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.07), 0 2px 4px -2px rgba(0, 0, 0, 0.05) !important;
}

/* 4px Left Status Accent Bars */
.status-accent-red {
  border-left-color: #ef4444 !important;
}

.status-accent-cyan {
  border-left-color: #06b6d4 !important;
}

.status-accent-blue {
  border-left-color: #3b82f6 !important;
}

.status-accent-gray {
  border-left-color: #9ca3af !important;
}

.feed-title-link {
  color: #111827 !important;
  font-size: 0.875rem; /* 14px */
  line-height: 1.35;
  transition: color 0.15s ease;
}

.feed-title-link:hover {
  color: #2563eb !important;
}

.text-hover-primary:hover {
  color: #2563eb !important;
  text-decoration: underline !important;
}

.line-clamp-2 {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
</style>

