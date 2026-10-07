<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'

import { httpClient } from '@/api/http'
import { useAuthStore } from '@/stores/auth'
import type { FeedItem } from '@/views/FeedView.vue'

/**
 * SCR-FEED-001: Pinned Operational Decision & Context Dossier (SCR-FEED-001 Split Command Mode)
 * Strictly aligned with CAKRA Operational Principles (Manifesto v3):
 * - Principle 1 & 4: Surfaces "What decision is needed?" and "What is the expected impact?".
 * - Principle 3: Projects authoritative current state of the referenced operational object.
 * - Principle 6: Distinguishes observable FACTS from engineering ASSESSMENTS/OPINIONS.
 * - Principle 12: Enforces accountable problem ownership and provides Claim Ownership affordance.
 * - Post Domain §5: Provides structured Operational Signals (ACK, EXPERIENCED, HAVE_IDEA, NEED_CLARIFICATION).
 */

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
  commentType?: 'FACT' | 'ASSESSMENT' | string
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
  shortLabel: string
  iconClass: string
  badgeClass: string
  description: string
}

const props = withDefaults(
  defineProps<{
    item: FeedItem | null
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
  (e: 'claim-ownership', item: FeedItem): void
}>()

const router = useRouter()
const authStore = useAuthStore()

const OPERATIONAL_SIGNALS: readonly ReactionOptionMeta[] = [
  {
    type: 'SEEN',
    label: 'Acknowledge',
    shortLabel: 'ACK',
    iconClass: 'bi-check-circle-fill',
    badgeClass: 'btn-outline-success',
    description: 'Acknowledge on-call receipt and situational awareness',
  },
  {
    type: 'EXPERIENCED',
    label: 'Experienced',
    shortLabel: 'EXP',
    iconClass: 'bi-lightning-charge-fill',
    badgeClass: 'btn-outline-primary',
    description: 'Corroborate first-hand occurrence across instances',
  },
  {
    type: 'HAVE_IDEA',
    label: 'Suggestion',
    shortLabel: 'IDEA',
    iconClass: 'bi-lightbulb-fill',
    badgeClass: 'btn-outline-warning',
    description: 'Signal that a workaround or remediation plan is available',
  },
  {
    type: 'NEED_CLARIFICATION',
    label: 'Clarify',
    shortLabel: 'CLARIFY',
    iconClass: 'bi-question-diamond-fill',
    badgeClass: 'btn-outline-info',
    description: 'Flag missing operational facts or reproduction details (Principle 6)',
  },
] as const

// Local state
const comments = ref<PostCommentItem[]>([])
const isLoadingComments = ref<boolean>(false)
const isSubmittingNote = ref<boolean>(false)
const newNoteContent = ref<string>('')
const newNoteType = ref<'ASSESSMENT' | 'FACT'>('ASSESSMENT')
const noteError = ref<string | null>(null)

const localReactionCounts = ref<Record<string, number>>({})
const activeUserReaction = ref<ReactionTypeCode | null>(null)
const isTogglingReaction = ref<boolean>(false)

const resolvedPostId = computed<string>(() => {
  if (!props.item) return ''
  return (props.item.postId || props.item.id || props.item.feedItemId || '').trim()
})

const effectiveRequestId = computed<string | null>(() => {
  if (!props.item) return null
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
  if (!props.item) return ''
  if (props.item.referenceDisplay && props.item.referenceDisplay.trim().length > 0) {
    return props.item.referenceDisplay.trim()
  }
  const reqId = effectiveRequestId.value
  return reqId ? `Request #${reqId.slice(0, 8)}` : 'View Request'
})

const customerDisplay = computed<string>(() => {
  if (!props.item) return '—'
  const name = props.item.customer ?? props.item.customerName
  if (name && name.trim().length > 0) return name.trim()
  if (props.item.customerId && props.customerNameMap[props.item.customerId]) {
    return props.customerNameMap[props.item.customerId]
  }
  return props.item.customerId ? `Customer #${props.item.customerId.slice(0, 8)}` : '—'
})

const productDisplay = computed<string>(() => {
  if (!props.item) return '—'
  const name = props.item.product ?? props.item.productName
  if (name && name.trim().length > 0) return name.trim()
  if (props.item.productId && props.productNameMap[props.item.productId]) {
    return props.productNameMap[props.item.productId]
  }
  return props.item.productId ? `Product #${props.item.productId.slice(0, 8)}` : '—'
})

const ownerDisplay = computed<string>(() => {
  if (!props.item) return '—'
  const explicitOwner = props.item.ownerName
  if (explicitOwner && explicitOwner.trim().length > 0) {
    return explicitOwner.trim()
  }
  const author = props.item.author ?? props.item.authorName
  if (author && author.trim().length > 0 && author.toUpperCase() !== 'SYSTEM') {
    return author.trim()
  }
  return 'Unassigned'
})

const isUnowned = computed<boolean>(() => {
  if (!props.item) return false
  return ownerDisplay.value === 'Unassigned' || !props.item.ownerName
})

// Principles 1 & 4: Derive explicit operational decision required
const decisionNeeded = computed<string | null>(() => {
  if (!props.item) return null
  if (props.item.decisionNeeded && props.item.decisionNeeded.trim().length > 0) {
    return props.item.decisionNeeded
  }
  if (props.item.isException) {
    const exType = (props.item.exceptionType || 'EXCEPTION').toUpperCase()
    if (exType === 'ESCALATION') {
      return 'Critical Escalation: SRE & Management decision required on resource reallocation or customer SLA waiver.'
    }
    if (exType === 'STALLED') {
      return 'Work Stalled: Root-cause triage required to unblock dependency or assign specialist.'
    }
    if (exType === 'REJECTION') {
      return 'Request Rejected: Stakeholder review required on scope revision or decommissioning.'
    }
    return 'Abnormal Condition: On-call triage required to determine remediation pathway.'
  }
  if (isUnowned.value) {
    return 'Ownership Required (Principle 12): Incident must have an assigned accountable engineer.'
  }
  return null
})

// Principles 1 & 16: Derive expected blast radius / impact
const expectedImpact = computed<string>(() => {
  if (!props.item) return '—'
  if (props.item.expectedImpact && props.item.expectedImpact.trim().length > 0) {
    return props.item.expectedImpact
  }
  if (props.item.isException) {
    return `Operational SLA at risk for ${customerDisplay.value} on ${productDisplay.value}.`
  }
  return `Normal lifecycle progression within standard capacity envelope.`
})

function formatTimestamp(value?: string | null): string {
  if (!value) return '—'
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return value
  return parsed.toLocaleString(undefined, {
    month: 'short',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function formatRelativeTime(value?: string | null): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  const now = new Date()
  const diffSec = Math.floor((now.getTime() - date.getTime()) / 1000)
  if (diffSec < 45) return 'just now'
  if (diffSec < 3600) return `${Math.floor(diffSec / 60)}m ago`
  if (diffSec < 86400) return `${Math.floor(diffSec / 3600)}h ago`
  return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
}

function initReactionCounts(): void {
  localReactionCounts.value = {}
  activeUserReaction.value = null
  if (!props.item) return

  let parsed: Record<string, number> = {}
  if (props.item.reactionCounts && typeof props.item.reactionCounts === 'object') {
    parsed = { ...props.item.reactionCounts }
  } else if (props.item.reactionCountsJson) {
    try {
      parsed = JSON.parse(props.item.reactionCountsJson) as Record<string, number>
    } catch {
      parsed = {}
    }
  }

  for (const opt of OPERATIONAL_SIGNALS) {
    const rawVal = parsed[opt.type] ?? parsed[opt.type.toLowerCase()] ?? 0
    localReactionCounts.value[opt.type] = typeof rawVal === 'number' ? rawVal : 0
  }
}

async function loadReactions(): Promise<void> {
  const postId = resolvedPostId.value
  if (!postId) return

  try {
    const res = await httpClient.get<Array<{ reactionType: string; personId?: string; isActive?: boolean }>>(
      `/posts/${postId}/reactions`,
    )
    const list = Array.isArray(res.data) ? res.data : []
    const myPersonId = (authStore.currentUser?.personId ?? '').trim().toLowerCase()

    let foundActive: ReactionTypeCode | null = null
    const counts: Record<string, number> = {}
    for (const opt of OPERATIONAL_SIGNALS) {
      counts[opt.type] = 0
    }

    for (const r of list) {
      if (r.isActive !== false) {
        const typeUpper = (r.reactionType || '').toUpperCase() as ReactionTypeCode
        if (counts[typeUpper] !== undefined) {
          counts[typeUpper] += 1
        }
        if (myPersonId && (r.personId ?? '').trim().toLowerCase() === myPersonId) {
          foundActive = typeUpper
        }
      }
    }

    localReactionCounts.value = counts
    activeUserReaction.value = foundActive
  } catch {
    // Retain initial projection counts
  }
}

async function toggleSignal(type: ReactionTypeCode): Promise<void> {
  const postId = resolvedPostId.value
  if (!postId || isTogglingReaction.value || !props.item) return

  const prevReaction = activeUserReaction.value
  const isRemoving = prevReaction === type
  isTogglingReaction.value = true

  // Optimistic update
  const updatedCounts = { ...localReactionCounts.value }
  if (isRemoving) {
    updatedCounts[type] = Math.max(0, (updatedCounts[type] ?? 0) - 1)
    activeUserReaction.value = null
  } else {
    if (prevReaction && updatedCounts[prevReaction] > 0) {
      updatedCounts[prevReaction] = Math.max(0, updatedCounts[prevReaction] - 1)
    }
    updatedCounts[type] = (updatedCounts[type] ?? 0) + 1
    activeUserReaction.value = type
  }
  localReactionCounts.value = updatedCounts

  try {
    if (isRemoving) {
      await httpClient.delete(`/posts/${postId}/reactions/${encodeURIComponent(type)}`)
    } else {
      await httpClient.post(`/posts/${postId}/reactions`, { reactionType: type })
    }
    emit('post-updated', {
      ...props.item,
      reactionCounts: localReactionCounts.value,
    })
  } catch {
    // Rollback
    initReactionCounts()
    void loadReactions()
  } finally {
    isTogglingReaction.value = false
  }
}

async function loadComments(): Promise<void> {
  const postId = resolvedPostId.value
  if (!postId) return

  isLoadingComments.value = true
  noteError.value = null
  try {
    const res = await httpClient.get<PostCommentItem[]>(`/posts/${postId}/comments`)
    const raw = Array.isArray(res.data) ? res.data : []
    comments.value = raw.filter((c) => c.isActive !== false)
  } catch (err) {
    if (err instanceof AxiosError && err.response?.status === 404) {
      comments.value = []
    } else {
      noteError.value = 'Failed to load operational discussion.'
    }
  } finally {
    isLoadingComments.value = false
  }
}

async function submitWorkNote(): Promise<void> {
  const text = newNoteContent.value.trim()
  const postId = resolvedPostId.value
  if (!text || !postId || isSubmittingNote.value || !props.item) return

  isSubmittingNote.value = true
  noteError.value = null

  // Tag note with Fact vs Assessment (Principle 6)
  const prefix = newNoteType.value === 'FACT' ? '[FACT] ' : '[ASSESSMENT] '
  const payload = prefix + text

  try {
    const res = await httpClient.post<PostCommentItem>(`/posts/${postId}/comments`, {
      content: payload,
    })
    newNoteContent.value = ''
    if (res.data) {
      comments.value.push(res.data)
      emit('post-updated', {
        ...props.item,
        commentCount: (props.item.commentCount ?? 0) + 1,
      })
    } else {
      void loadComments()
    }
  } catch (err: unknown) {
    if (err instanceof AxiosError && err.response?.data?.detail) {
      noteError.value = err.response.data.detail
    } else {
      noteError.value = 'Failed to record work note to operational ledger.'
    }
  } finally {
    isSubmittingNote.value = false
  }
}

function handleClaim(): void {
  if (!props.item) return
  emit('claim-ownership', props.item)
}

function navigateToRequest(): void {
  if (effectiveRequestId.value) {
    void router.push(`/requests/${effectiveRequestId.value}`)
  }
}

watch(
  () => props.item?.id,
  () => {
    initReactionCounts()
    void loadReactions()
    void loadComments()
  },
  { immediate: true },
)

onMounted(() => {
  initReactionCounts()
  void loadReactions()
  void loadComments()
})
</script>

<template>
  <div v-if="item" class="bg-slate-50 dark:bg-slate-900/90 border border-slate-200 dark:border-slate-800 rounded-xl shadow-sm dark:shadow-lg h-100 d-flex flex-column text-slate-900 dark:text-slate-100 overflow-hidden" data-testid="feed-dossier-panel">
    <!-- Dossier Header: Telemetry & State Machine (Principle 3) -->
    <div class="bg-slate-50 dark:bg-slate-950/60 border-b border-slate-200 dark:border-slate-800 py-3 px-4">
      <div class="d-flex flex-wrap align-items-center justify-content-between gap-2">
        <div class="d-flex flex-wrap align-items-center gap-1.5 min-w-0">
          <!-- Exception badge -->
          <span
            v-if="item.isException"
            class="badge bg-rose-500/15 text-rose-400 border border-rose-500/30 font-mono fs-11"
          >
            <i class="bi bi-exclamation-triangle-fill me-1" aria-hidden="true"></i>
            {{ item.exceptionType || 'EXCEPTION' }}
          </span>

          <!-- Source badge -->
          <span
            class="badge font-mono fs-11"
            :class="item.source === 'SYSTEM_GENERATED' ? 'bg-cyan-500/15 text-cyan-400 border border-cyan-500/30' : 'bg-indigo-500/15 text-indigo-400 border border-indigo-500/30'"
          >
            {{ item.source === 'SYSTEM_GENERATED' ? 'STATE FACT' : 'OPS POST' }}
          </span>

          <!-- Reference ID -->
          <span class="badge bg-slate-100 dark:bg-slate-800 text-cyan-600 dark:text-cyan-400 border border-slate-300 dark:border-slate-700 font-mono fw-bold fs-11">
            {{ effectiveRequestDisplay }}
          </span>

          <!-- Authoritative State -->
          <span class="badge bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 border border-slate-300 dark:border-slate-700 font-mono fs-11">
            STATE: {{ item.status || 'ACTIVE' }}
          </span>
        </div>

        <div class="d-flex align-items-center gap-2 font-mono fs-11 text-slate-500 dark:text-slate-400">
          <i class="bi bi-clock me-0.5" aria-hidden="true"></i>
          {{ formatTimestamp(item.createdAt) }} ({{ formatRelativeTime(item.createdAt) }})
        </div>
      </div>

      <!-- Post Title -->
      <h2 class="h6 fw-bold text-slate-900 dark:text-white mt-2 mb-0 text-break">
        {{ item.title }}
      </h2>
    </div>

    <!-- Dossier Body: Decision Callout, Context Grid, Observation, Signals & Ledger -->
    <div class="p-4 overflow-y-auto flex-grow-1 d-flex flex-column gap-3 text-slate-900 dark:text-slate-100">
      <!-- PRINCIPLE 1 & 4: DECISION REQUIRED CALLOUT -->
      <div
        v-if="decisionNeeded"
        class="p-3 rounded-lg border border-cyan-500/30 bg-cyan-500/10 text-slate-900 dark:text-slate-100"
      >
        <div class="d-flex align-items-center gap-1.5 text-cyan-600 dark:text-cyan-400 fw-bold fs-11 text-uppercase font-mono mb-1">
          <i class="bi bi-signpost-2-fill" aria-hidden="true"></i>
          Decision Required (Principles 1 &amp; 4)
        </div>
        <p class="fs-12 fw-semibold text-slate-900 dark:text-white mb-1">
          {{ decisionNeeded }}
        </p>
        <div class="fs-11 text-slate-500 dark:text-slate-400">
          <strong class="text-slate-700 dark:text-slate-300">Expected Impact:</strong> {{ expectedImpact }}
        </div>
      </div>

      <!-- CONTEXT & ACCOUNTABILITY GRID (PRINCIPLE 12 & 16) -->
      <div class="p-3 rounded-lg bg-slate-50 dark:bg-slate-950/60 border border-slate-200 dark:border-slate-800 fs-12">
        <div class="row g-2">
          <div class="col-6 col-md-3">
            <span class="text-slate-500 dark:text-slate-400 d-block fs-11">Customer:</span>
            <strong class="text-slate-900 dark:text-white text-truncate d-block fw-semibold" :title="customerDisplay">{{ customerDisplay }}</strong>
          </div>
          <div class="col-6 col-md-3">
            <span class="text-slate-500 dark:text-slate-400 d-block fs-11">Product:</span>
            <strong class="text-slate-900 dark:text-white text-truncate d-block fw-semibold" :title="productDisplay">{{ productDisplay }}</strong>
          </div>
          <div class="col-6 col-md-3">
            <span class="text-slate-500 dark:text-slate-400 d-block fs-11">Accountable Owner (P.12):</span>
            <span v-if="isUnowned" class="badge bg-amber-500/15 text-amber-400 border border-amber-500/30 font-mono fs-11">
              ⚠️ UNOWNED
            </span>
            <strong v-else class="text-slate-900 dark:text-white text-truncate d-block fw-semibold" :title="ownerDisplay">{{ ownerDisplay }}</strong>
          </div>
          <div class="col-6 col-md-3">
            <span class="text-slate-500 dark:text-slate-400 d-block fs-11">Logged By:</span>
            <span class="text-slate-700 dark:text-slate-300 text-truncate d-block">{{ item.author || item.authorName || 'SYSTEM' }}</span>
          </div>
        </div>
      </div>

      <!-- OPERATIONAL NARRATIVE -->
      <div>
        <span class="fs-11 fw-bold text-slate-500 dark:text-slate-400 text-uppercase d-block mb-1">Operational Observation</span>
        <div class="p-3 rounded-lg bg-slate-50 dark:bg-slate-950/60 border border-slate-200 dark:border-slate-800 fs-12 text-slate-700 dark:text-slate-300 text-break leading-normal">
          {{ item.contentExcerpt || item.summary || 'No detailed excerpt provided.' }}
        </div>
      </div>

      <!-- STRUCTURED OPERATIONAL SIGNALS (Post Domain §5) -->
      <div>
        <div class="d-flex justify-content-between align-items-center mb-1.5">
          <span class="fs-11 fw-bold text-slate-500 dark:text-slate-400 text-uppercase">Operational Signals (Post Domain §5)</span>
          <span class="fs-11 text-slate-500 dark:text-slate-400">Click to register operational signal</span>
        </div>

        <div class="row g-1.5">
          <div v-for="sig in OPERATIONAL_SIGNALS" :key="sig.type" class="col-6 col-sm-3">
            <button
              type="button"
              class="btn btn-sm w-100 text-start p-2 rounded-lg border d-flex flex-column justify-content-between transition"
              :class="activeUserReaction === sig.type ? 'bg-cyan-600 text-white dark:bg-cyan-500/20 dark:text-cyan-300 dark:border-cyan-500/40 shadow-sm' : 'bg-slate-50 dark:bg-slate-800/80 text-slate-700 dark:text-slate-300 border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-700'"
              :title="sig.description"
              :disabled="isTogglingReaction"
              @click="toggleSignal(sig.type)"
            >
              <div class="d-flex justify-content-between align-items-center w-100">
                <span class="fs-11 fw-bold">
                  <i class="bi me-1" :class="sig.iconClass" aria-hidden="true"></i>
                  {{ sig.shortLabel }}
                </span>
                <span class="badge font-mono" :class="activeUserReaction === sig.type ? 'bg-white/20 text-white dark:bg-cyan-500/40 dark:text-cyan-200' : 'bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300'">
                  {{ localReactionCounts[sig.type] || 0 }}
                </span>
              </div>
              <span class="fs-11 mt-1 opacity-75 text-truncate d-block">{{ sig.label }}</span>
            </button>
          </div>
        </div>
      </div>

      <!-- PRINCIPLE 6: FACTS BEFORE OPINIONS (Audited Discussion Thread) -->
      <div class="d-flex flex-column gap-2 flex-grow-1">
        <div class="d-flex justify-content-between align-items-center">
          <span class="fs-11 fw-bold text-slate-500 dark:text-slate-400 text-uppercase">
            Audited Thread: Facts vs Assessments (Principle 6 &amp; 14)
          </span>
          <span class="fs-11 text-slate-500 dark:text-slate-400 font-mono">{{ comments.length }} entries</span>
        </div>

        <!-- Comments List -->
        <div class="d-flex flex-column gap-2 overflow-y-auto max-h-48" style="max-height: 220px;">
          <div v-if="isLoadingComments" class="text-center py-3 text-slate-400 fs-11">
            <div class="spinner-border spinner-border-sm text-cyan-500 me-1" role="status"></div>
            Loading operational thread...
          </div>

          <div v-else-if="comments.length === 0" class="text-slate-400 dark:text-slate-500 fs-11 text-center py-2.5 bg-slate-50 dark:bg-slate-950/60 rounded-lg border border-slate-200 dark:border-slate-800">
            No audited work notes or factual telemetry attached yet.
          </div>

          <div
            v-for="comment in comments"
            :key="comment.id || comment.commentId"
            class="p-2.5 rounded-lg bg-slate-50 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700/80 fs-12 shadow-xs"
          >
            <div class="d-flex justify-content-between align-items-center font-mono fs-11 text-slate-500 dark:text-slate-400 mb-1">
              <span class="d-flex align-items-center gap-1.5">
                <!-- Fact vs Assessment Badge -->
                <span
                  v-if="(comment.content || '').startsWith('[FACT]')"
                  class="badge bg-cyan-500/15 text-cyan-400 border border-cyan-500/30 font-mono"
                >
                  FACT
                </span>
                <span
                  v-else-if="(comment.content || '').startsWith('[ASSESSMENT]')"
                  class="badge bg-amber-500/15 text-amber-400 border border-amber-500/30 font-mono"
                >
                  ASSESSMENT
                </span>
                <span v-else class="badge bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300 border border-slate-300 dark:border-slate-600 font-mono">NOTE</span>

                <strong class="text-slate-900 dark:text-white">{{ comment.author || comment.authorName || 'Engineer' }}</strong>
              </span>
              <span>{{ formatRelativeTime(comment.createdAt) }}</span>
            </div>
            <p class="mb-0 text-slate-700 dark:text-slate-300 text-break">
              {{ (comment.content || '').replace(/^\[(FACT|ASSESSMENT)\]\s*/, '') }}
            </p>
          </div>
        </div>

        <!-- Note Composer -->
        <form class="d-flex gap-1.5 mt-auto pt-2 border-top border-slate-200 dark:border-slate-800" @submit.prevent="submitWorkNote">
          <select
            v-model="newNoteType"
            class="form-select form-select-sm fs-11 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 text-slate-900 dark:text-slate-100 rounded-lg"
            style="width: 130px;"
            aria-label="Note classification type"
          >
            <option value="ASSESSMENT">Assessment</option>
            <option value="FACT">Observable Fact</option>
          </select>
          <input
            v-model="newNoteContent"
            type="text"
            class="form-control form-control-sm fs-12 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 text-slate-900 dark:text-slate-100 placeholder:text-slate-500 rounded-lg"
            placeholder="Record observable fact or engineering assessment..."
            :disabled="isSubmittingNote"
          />
          <button
            type="submit"
            class="btn btn-sm btn-primary flex-shrink-0"
            :disabled="!newNoteContent.trim() || isSubmittingNote"
          >
            <span v-if="isSubmittingNote" class="spinner-border spinner-border-sm" role="status"></span>
            <span v-else>Post</span>
          </button>
        </form>

        <div v-if="noteError" class="text-rose-400 fs-11">
          <i class="bi bi-exclamation-circle me-1" aria-hidden="true"></i>
          {{ noteError }}
        </div>
      </div>
    </div>

    <!-- Dossier Footer: Quick Operational Actions -->
    <div class="bg-slate-50 dark:bg-slate-950/60 border-t border-slate-200 dark:border-slate-800 py-2.5 px-4 d-flex flex-wrap justify-content-between align-items-center gap-2">
      <div>
        <button
          v-if="effectiveRequestId"
          type="button"
          class="btn btn-sm btn-link text-decoration-none p-0 fw-semibold text-cyan-600 dark:text-cyan-400 hover:underline"
          @click="navigateToRequest"
        >
          <i class="bi bi-box-arrow-up-right me-1" aria-hidden="true"></i>
          Open Authoritative Request Detail ({{ effectiveRequestDisplay }}) &rarr;
        </button>
      </div>

      <div class="d-flex align-items-center gap-2">
        <button
          v-if="isUnowned"
          type="button"
          class="btn btn-sm border border-amber-500/30 text-amber-500 dark:text-amber-400 hover:bg-amber-500/10 font-mono"
          @click="handleClaim"
        >
          <i class="bi bi-person-check-fill me-1" aria-hidden="true"></i>
          Claim Ownership
        </button>

        <button
          type="button"
          class="btn btn-sm btn-outline-secondary dark:bg-slate-800 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-700"
          @click="emit('open-modal', item)"
        >
          <i class="bi bi-arrows-angle-expand me-1" aria-hidden="true"></i>
          Full Modal Thread
        </button>
      </div>
    </div>
  </div>

  <!-- Empty state when no item selected -->
  <div v-else class="bg-white dark:bg-slate-900/90 border border-slate-200 dark:border-slate-800 rounded-xl shadow-sm dark:shadow-lg h-100 d-flex align-items-center justify-content-center p-5 text-center text-slate-400 dark:text-slate-500">
    <div>
      <i class="bi bi-activity fs-2 d-block mb-2 text-cyan-500 opacity-60" aria-hidden="true"></i>
      <p class="fs-12 fw-semibold mb-0">Select an operational event to view its decision context &amp; dossier.</p>
    </div>
  </div>
</template>
