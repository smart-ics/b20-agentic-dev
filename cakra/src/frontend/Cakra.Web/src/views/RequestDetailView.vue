<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { httpClient } from '@/api/http'
import type { RequestSubTask } from '@/api/requests'
import {
  addSubTask as apiAddSubTask,
  completeSubTask as apiCompleteSubTask,
  reopenSubTask as apiReopenSubTask,
  removeSubTask as apiRemoveSubTask,
  startWork as apiStartWork,
  pauseWork as apiPauseWork,
  cancelRequest as apiCancelRequest,
  assignRequestOwner as apiAssignRequestOwner,
  reassignRequestOwner as apiReassignRequestOwner,
  completeRequest as apiCompleteRequest,
  updateRequestCoreAttributes,
} from '@/api/requests'
import { useAuthStore } from '@/stores/auth'

/**
 * SCR-REQ-003: Request Detail Screen
 * (Architecture §7, §8 — UC-REQ-002..008, UC-COL-003, UC-MGT-001, §9 — FEAT-REQ-001..008, CR-016 TD-001..005).
 *
 * - Renders a Bootstrap 5 card displaying authoritative request details:
 *   Title, Description, Customer, Product, Status, Assignee, CreatedAt, UpdatedAt,
 *   plus Resolution when present.
 * - Renders simplified lifecycle actions card and contextual buttons:
 *   * CAPTURED: Assign Owner, Cancel Request
 *   * ASSIGNED: Start Work (enabled for assigned owner), Reassign Owner, Cancel Request
 *   * IN_PROGRESS: Pause Work, Complete Request, Reassign Owner, Cancel Request
 *   * PAUSED: Start / Resume Work (enabled for assigned owner), Reassign Owner, Cancel Request
 *   * COMPLETED / CANCELLED: Closed terminal badge
 * - Renders dedicated Pause Work and Cancel Request modals/forms.
 * - Embeds a dedicated Comments & Discussion thread directly on the screen linked to the request's post.
 * - Renders chronological state history timeline below the card.
 */

export interface RequestResolutionDetail {
  id: string
  requestId: string
  outcome: string
  description: string
  resolvedBy: string
  resolvedByName?: string | null
  resolvedAt: string
  createdAt: string
  updatedAt?: string | null
}

export interface RequestAssignmentHistoryItem {
  id: string
  requestId: string
  previousOwnerPersonId?: string | null
  previousOwnerName?: string | null
  assignedOwnerPersonId?: string | null
  assignedOwnerName?: string | null
  actorPersonId: string
  actorName?: string | null
  previousStatus?: string | null
  newStatus: string
  assignedAtUtc: string
  timestamp?: string
  notes?: string | null
  createdAt: string
  updatedAt?: string | null
}

export interface RequestCommentItem {
  id: string
  postId: string
  authorPersonId: string
  authorName?: string | null
  author?: string | null
  content: string
  status?: string
  createdAt: string
  updatedAt?: string | null
}

export interface RequestDetail {
  id: string
  requestId?: string
  title: string
  description: string
  requestType: string
  status:
    | 'CAPTURED'
    | 'ASSIGNED'
    | 'IN_PROGRESS'
    | 'PAUSED'
    | 'COMPLETED'
    | 'CANCELLED'
    | string
  priority: 'LOW' | 'NORMAL' | 'HIGH' | 'URGENT' | string
  complexity?: number
  deadline?: string | null
  ownerPersonId: string | null
  assigneePersonId?: string | null
  ownerName?: string | null
  assigneeName?: string | null
  customerId: string | null
  customerName?: string | null
  customerCode?: string | null
  productId: string | null
  productName?: string | null
  productCode?: string | null
  workPackageId?: string | null
  evaluationNotes?: string | null
  escalationReason?: string | null
  managementDecisionNotes?: string | null
  totalSubTasksCount?: number
  completedSubTasksCount?: number
  completionPercentage?: number
  subTasks?: RequestSubTask[]
  resolution?: RequestResolutionDetail | null
  assignments?: RequestAssignmentHistoryItem[]
  createdAt: string
  updatedAt?: string | null
}

export interface ActivePersonOption {
  id: string
  personId?: string
  firstName: string
  lastName: string
  fullName: string
  email: string
  status: string
}

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const route = useRoute()
const router = useRouter()

const requestId = computed(() => String(route.params.id ?? '').trim())

const request = ref<RequestDetail | null>(null)
const stateHistory = ref<RequestAssignmentHistoryItem[]>([])
const activePersons = ref<ActivePersonOption[]>([])

const isLoading = ref(false)
const isHistoryLoading = ref(false)
const isSubmittingAction = ref(false)
const errorMessage = ref<string | null>(null)
const actionSuccessMessage = ref<string | null>(null)

// Action Forms State
const assignForm = reactive({
  ownerPersonId: '',
  notes: '',
})

const startForm = reactive({
  notes: '',
})

const pauseForm = reactive({
  note: '',
})

const completeForm = reactive({
  resolutionDescription: '',
})

const reassignForm = reactive({
  newOwnerPersonId: '',
  notes: '',
})

const cancelForm = reactive({
  reason: '',
})

// Modal visibility toggles
const showAssignModal = ref(false)
const showPauseModal = ref(false)
const showCancelModal = ref(false)
const showStartModal = ref(false)
const showCompleteModal = ref(false)
const showReassignModal = ref(false)
const showEditModal = ref(false)
const isSubmittingEdit = ref(false)
const editErrorMessage = ref<string | null>(null)
const editForm = reactive<{
  title: string
  description: string
  requestType: string
  priority: string
  deadline: string | null
}>({
  title: '',
  description: '',
  requestType: 'GENERAL',
  priority: 'NORMAL',
  deadline: null,
})

const REQUEST_TYPES = ['GENERAL', 'BUG', 'FEATURE', 'SUPPORT', 'CHANGE_REQUEST', 'INCIDENT'] as const
const PRIORITIES = ['LOW', 'NORMAL', 'HIGH', 'URGENT'] as const

// Comments & Discussion State
const associatedPostId = ref<string | null>(null)
const comments = ref<RequestCommentItem[]>([])
const isCommentsLoading = ref(false)
const isSubmittingComment = ref(false)
const commentErrorMessage = ref<string | null>(null)
const newCommentContent = ref('')

const normalizedStatus = computed(() => (request.value?.status ?? '').toUpperCase())

const isCaptured = computed(() => normalizedStatus.value === 'CAPTURED')
const isAssigned = computed(() => normalizedStatus.value === 'ASSIGNED')
const isInProgress = computed(() => normalizedStatus.value === 'IN_PROGRESS')
const isPaused = computed(() => normalizedStatus.value === 'PAUSED')
const isCompleted = computed(() => normalizedStatus.value === 'COMPLETED')
const isCancelled = computed(() => normalizedStatus.value === 'CANCELLED')
const isClosed = computed(
  () => isCompleted.value || isCancelled.value || normalizedStatus.value === 'REJECTED',
)

const isOverdue = computed(() => {
  if (!request.value?.deadline) return false
  const status = normalizedStatus.value
  if (status === 'COMPLETED' || status === 'CANCELLED') return false
  const raw = request.value.deadline
  const match = raw.match(/^(\d{4})-(\d{2})-(\d{2})/)
  const deadlineDate = match
    ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]))
    : new Date(raw)
  if (Number.isNaN(deadlineDate.getTime())) return false
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  deadlineDate.setHours(0, 0, 0, 0)
  return deadlineDate < today
})

const authStore = useAuthStore()

// Core attributes edit authorization (CR-018)
const canEditCoreAttributes = computed(() => {
  if (isClosed.value) {
    return false
  }
  const userRoles = authStore.roles.map((r) => r.toUpperCase())
  if (userRoles.includes('ADMINISTRATOR') || userRoles.includes('ADMIN') || userRoles.includes('MANAGER')) {
    return true
  }
  if (isCaptured.value && !request.value?.ownerPersonId) {
    return true
  }
  const currentPersonId = authStore.currentUser?.personId
  if (
    currentPersonId &&
    request.value?.ownerPersonId &&
    currentPersonId.toLowerCase() === request.value.ownerPersonId.toLowerCase()
  ) {
    return true
  }
  return false
})

// Owner accountability check (CR-016 TD-002)
const isAssignedOwner = computed(() => {
  const ownerId = request.value?.ownerPersonId ?? request.value?.assigneePersonId
  if (!ownerId) {
    return false
  }
  const currentPersonId = authStore.currentUser?.personId
  if (!currentPersonId) {
    return true
  }
  return currentPersonId.toLowerCase() === ownerId.toLowerCase()
})

// Visibility rules for conditional lifecycle actions (CR-016 TD-001..003)
const canAssign = computed(() => isCaptured.value)
const canStartWork = computed(() => isAssigned.value || isPaused.value)
const canPauseWork = computed(() => isInProgress.value)
const canComplete = computed(() => isInProgress.value)
const canReassign = computed(
  () => !isClosed.value && (isAssigned.value || isInProgress.value || isPaused.value),
)
const canCancel = computed(() => !isClosed.value)

const AUTHORIZED_COMPLEXITY_ROLES = [
  'PROGRAMMER',
  'ADMINISTRATOR',
  'ADMIN',
  'DEVELOPER',
  'TEAM LEAD',
  'TEAM_LEAD',
  'MANAGER',
] as const

const canEditComplexity = computed(() => {
  if (isClosed.value) {
    return false
  }
  if (!authStore.isAuthenticated) {
    return true
  }
  const userRoles = authStore.roles.map((r) => r.toUpperCase())
  if (userRoles.length === 0) {
    return true
  }
  return userRoles.some((role) => AUTHORIZED_COMPLEXITY_ROLES.includes(role as (typeof AUTHORIZED_COMPLEXITY_ROLES)[number]))
})

const complexityForm = reactive({
  complexity: 1,
  reason: '',
  isEditing: false,
  isSubmitting: false,
})

function initComplexityForm(): void {
  complexityForm.complexity = request.value?.complexity ?? 1
  complexityForm.reason = ''
}

const personNameById = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const person of activePersons.value) {
    const label = person.fullName || `${person.firstName} ${person.lastName}`.trim()
    map[person.id] = label
  }
  return map
})

// Sub-Tasks Checklist State & Actions (CR-006, Architecture TD-001..TD-003, TD-010)
const newSubTaskForm = reactive({
  title: '',
  assigneePersonId: '',
})
const isSubTaskLoading = ref(false)
const subTaskOperatingId = ref<string | null>(null)

const hasUnfinishedSubTasks = computed(() => {
  const total = request.value?.totalSubTasksCount ?? 0
  const completed = request.value?.completedSubTasksCount ?? 0
  return total > 0 && completed < total
})

const unfinishedSubTasksCount = computed(() => {
  const total = request.value?.totalSubTasksCount ?? 0
  const completed = request.value?.completedSubTasksCount ?? 0
  return Math.max(0, total - completed)
})

function resolveSubTaskAssigneeDisplay(subTask: RequestSubTask): string {
  if (subTask.assigneeName && subTask.assigneeName.trim().length > 0) {
    return subTask.assigneeName
  }
  if (subTask.assigneePersonId) {
    return personNameById.value[subTask.assigneePersonId] ?? subTask.assigneePersonId
  }
  return 'Unassigned'
}

function resolveSubTaskCompletedByDisplay(subTask: RequestSubTask): string {
  if (subTask.completedByName && subTask.completedByName.trim().length > 0) {
    return subTask.completedByName
  }
  if (subTask.completedByPersonId) {
    return personNameById.value[subTask.completedByPersonId] ?? subTask.completedByPersonId
  }
  return ''
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
  return fallback
}

function statusBadgeClass(status: string | null | undefined): string {
  switch ((status ?? '').toUpperCase()) {
    case 'CAPTURED':
      return 'text-bg-secondary'
    case 'ASSIGNED':
      return 'text-bg-info text-dark'
    case 'IN_PROGRESS':
      return 'text-bg-primary'
    case 'PAUSED':
      return 'text-bg-warning text-dark'
    case 'COMPLETED':
      return 'text-bg-success'
    case 'CANCELLED':
      return 'text-bg-danger'
    case 'EVALUATING':
      return 'text-bg-info text-dark'
    case 'ACCEPTED':
      return 'text-bg-info text-dark'
    case 'ESCALATED':
      return 'text-bg-warning text-dark'
    case 'REJECTED':
      return 'text-bg-danger'
    default:
      return 'text-bg-secondary'
  }
}

function priorityBadgeClass(priority: string | null | undefined): string {
  switch ((priority ?? '').toUpperCase()) {
    case 'URGENT':
      return 'text-bg-danger'
    case 'HIGH':
      return 'text-bg-warning'
    case 'LOW':
      return 'text-bg-light border text-secondary'
    default:
      return 'text-bg-light border text-body-secondary'
  }
}

function complexityBadgeClass(complexity?: number): string {
  switch (complexity) {
    case 1:
      return 'text-bg-light border text-secondary'
    case 2:
      return 'text-bg-info text-dark'
    case 3:
      return 'text-bg-primary'
    case 4:
      return 'text-bg-warning text-dark'
    case 5:
      return 'text-bg-danger'
    default:
      return 'text-bg-light border text-secondary'
  }
}

function formatComplexity(complexity?: number): string {
  const val = complexity ?? 1
  switch (val) {
    case 1:
      return '1 (Very Low)'
    case 2:
      return '2 (Low)'
    case 3:
      return '3 (Medium)'
    case 4:
      return '4 (High)'
    case 5:
      return '5 (Very High)'
    default:
      return `${val}`
  }
}

function resolveCustomerDisplay(item: RequestDetail | null): string {
  if (!item) {
    return '—'
  }
  if (item.customerName && item.customerName.trim().length > 0) {
    return item.customerCode ? `${item.customerName} (${item.customerCode})` : item.customerName
  }
  if (item.customerCode && item.customerCode.trim().length > 0) {
    return item.customerCode
  }
  return item.customerId ?? '—'
}

function resolveProductDisplay(item: RequestDetail | null): string {
  if (!item) {
    return '—'
  }
  if (item.productName && item.productName.trim().length > 0) {
    return item.productCode ? `${item.productName} (${item.productCode})` : item.productName
  }
  if (item.productCode && item.productCode.trim().length > 0) {
    return item.productCode
  }
  return item.productId ?? '—'
}

function resolveAssigneeDisplay(item: RequestDetail | null): string {
  if (!item) {
    return 'Unassigned'
  }
  if (item.assigneeName && item.assigneeName.trim().length > 0) {
    return item.assigneeName
  }
  if (item.ownerName && item.ownerName.trim().length > 0) {
    return item.ownerName
  }
  const assigneeId = item.assigneePersonId ?? item.ownerPersonId
  if (assigneeId) {
    return personNameById.value[assigneeId] ?? assigneeId
  }
  return 'Unassigned'
}

function resolvePersonDisplay(personId: string | null | undefined, personName?: string | null): string {
  if (personName && personName.trim().length > 0) {
    return personName
  }
  if (personId) {
    return personNameById.value[personId] ?? personId
  }
  return '—'
}

function formatTimestamp(value: string | null | undefined): string {
  if (!value) {
    return '—'
  }
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }
  return parsed.toLocaleString()
}

function formatDeadline(value: string | null | undefined): string {
  if (!value) {
    return 'None'
  }
  const match = value.match(/^(\d{4})-(\d{2})-(\d{2})/)
  if (match) {
    return `${match[1]}-${match[2]}-${match[3]}`
  }
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }
  return parsed.toISOString().slice(0, 10)
}

async function loadActivePersons(): Promise<void> {
  try {
    const response = await httpClient.get<ActivePersonOption[]>('/organization/persons/active')
    activePersons.value = response.data
  } catch {
    activePersons.value = []
  }
}

async function loadRequestDetail(): Promise<void> {
  if (!requestId.value) {
    errorMessage.value = 'Request ID is missing from route.'
    return
  }

  isLoading.value = true
  errorMessage.value = null

  try {
    const response = await httpClient.get<RequestDetail>(`/requests/${requestId.value}`)
    request.value = response.data
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load request details.')
  } finally {
    isLoading.value = false
  }
}

async function loadRequestComments(): Promise<void> {
  if (!requestId.value) {
    return
  }

  isCommentsLoading.value = true
  commentErrorMessage.value = null

  try {
    const response = await httpClient.get<Array<{ id: string; comments?: RequestCommentItem[] }>>(
      '/posts/by-reference',
      {
        params: { requestId: requestId.value },
      },
    )

    if (Array.isArray(response.data) && response.data.length > 0) {
      const post = response.data[0]
      associatedPostId.value = post.id

      try {
        const commentsResponse = await httpClient.get<RequestCommentItem[]>(
          `/posts/${post.id}/comments`,
        )
        if (Array.isArray(commentsResponse.data)) {
          comments.value = commentsResponse.data
        } else if (Array.isArray(post.comments)) {
          comments.value = post.comments
        }
      } catch {
        if (Array.isArray(post.comments)) {
          comments.value = post.comments
        }
      }
    } else {
      associatedPostId.value = null
      comments.value = []
    }
  } catch (err) {
    commentErrorMessage.value = extractErrorMessage(err, 'Failed to load comments.')
  } finally {
    isCommentsLoading.value = false
  }
}

async function handleAddComment(): Promise<void> {
  const content = newCommentContent.value.trim()
  if (!content || !associatedPostId.value || isSubmittingComment.value) {
    return
  }

  isSubmittingComment.value = true
  commentErrorMessage.value = null

  try {
    const response = await httpClient.post<RequestCommentItem>(
      `/posts/${associatedPostId.value}/comments`,
      {
        content,
        authorPersonId: authStore.currentUser?.personId || null,
      },
    )

    if (response.data) {
      comments.value.push(response.data)
    } else {
      await loadRequestComments()
    }
    newCommentContent.value = ''
  } catch (err) {
    commentErrorMessage.value = extractErrorMessage(err, 'Failed to post comment.')
  } finally {
    isSubmittingComment.value = false
  }
}

async function loadStateHistory(): Promise<void> {
  if (!requestId.value) {
    return
  }

  isHistoryLoading.value = true

  try {
    const response = await httpClient.get<RequestAssignmentHistoryItem[]>(
      `/requests/${requestId.value}/history`,
    )
    stateHistory.value = response.data
  } catch (err) {
    if (!errorMessage.value) {
      errorMessage.value = extractErrorMessage(err, 'Failed to load request state history.')
    }
  } finally {
    isHistoryLoading.value = false
  }
}

async function refreshAll(): Promise<void> {
  actionSuccessMessage.value = null
  await Promise.all([loadRequestDetail(), loadStateHistory(), loadRequestComments()])
}

function openEditModal(): void {
  if (!request.value) {
    return
  }
  editErrorMessage.value = null
  editForm.title = request.value.title ?? ''
  editForm.description = request.value.description ?? ''
  editForm.requestType = request.value.requestType ?? 'GENERAL'
  editForm.priority = request.value.priority ?? 'NORMAL'
  if (request.value.deadline) {
    const match = request.value.deadline.match(/^(\d{4})-(\d{2})-(\d{2})/)
    if (match) {
      editForm.deadline = `${match[1]}-${match[2]}-${match[3]}`
    } else {
      const d = new Date(request.value.deadline)
      editForm.deadline = Number.isNaN(d.getTime()) ? null : d.toISOString().slice(0, 10)
    }
  } else {
    editForm.deadline = null
  }
  showEditModal.value = true
}

async function handleSaveEdit(): Promise<void> {
  const trimmedTitle = editForm.title.trim()
  const trimmedDescription = editForm.description.trim()

  if (!trimmedTitle) {
    editErrorMessage.value = 'Title is required.'
    return
  }
  if (!trimmedDescription) {
    editErrorMessage.value = 'Description is required.'
    return
  }
  if (!requestId.value) {
    return
  }

  isSubmittingEdit.value = true
  editErrorMessage.value = null

  try {
    const updated = await updateRequestCoreAttributes(requestId.value, {
      title: trimmedTitle,
      description: trimmedDescription,
      requestType: editForm.requestType,
      priority: editForm.priority,
      deadline: editForm.deadline ? editForm.deadline : null,
    })
    request.value = { ...request.value, ...updated } as RequestDetail
    actionSuccessMessage.value = 'Request details updated successfully.'
    showEditModal.value = false
    await loadStateHistory()
  } catch (err: unknown) {
    editErrorMessage.value = extractErrorMessage(err, 'Failed to update request details.')
  } finally {
    isSubmittingEdit.value = false
  }
}

async function handleAssign(): Promise<void> {
  if (!requestId.value) {
    return
  }

  const ownerPersonId = assignForm.ownerPersonId.trim()
  if (!ownerPersonId) {
    errorMessage.value = 'Please select an active person to assign this request.'
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const response = await apiAssignRequestOwner(
      requestId.value,
      ownerPersonId,
      assignForm.notes.trim() || null,
    )
    request.value = { ...request.value, ...response } as RequestDetail
    assignForm.ownerPersonId = ''
    assignForm.notes = ''
    showAssignModal.value = false
    actionSuccessMessage.value = 'Request assigned to owner.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to assign request owner.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleStartWork(): Promise<void> {
  if (!requestId.value) {
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const response = await apiStartWork(
      requestId.value,
      startForm.notes.trim() || null,
    )
    request.value = { ...request.value, ...response } as RequestDetail
    actionSuccessMessage.value = isPaused.value
      ? 'Work resumed on request.'
      : 'Work started on request.'
    startForm.notes = ''
    showStartModal.value = false
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to start work on request.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handlePauseWork(): Promise<void> {
  if (!requestId.value) {
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const response = await apiPauseWork(
      requestId.value,
      pauseForm.note.trim() || null,
    )
    request.value = { ...request.value, ...response } as RequestDetail
    pauseForm.note = ''
    showPauseModal.value = false
    actionSuccessMessage.value = 'Work paused on request.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to pause work on request.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleComplete(): Promise<void> {
  if (!requestId.value) {
    return
  }

  const resolutionDescription = completeForm.resolutionDescription.trim()
  if (!resolutionDescription) {
    errorMessage.value = 'Resolution description is required to complete the request.'
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const response = await apiCompleteRequest(
      requestId.value,
      resolutionDescription,
    )
    request.value = { ...request.value, ...response } as RequestDetail
    completeForm.resolutionDescription = ''
    showCompleteModal.value = false
    actionSuccessMessage.value = 'Request completed successfully.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to complete request.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleReassign(): Promise<void> {
  if (!requestId.value) {
    return
  }

  const newOwnerPersonId = reassignForm.newOwnerPersonId.trim()
  if (!newOwnerPersonId) {
    errorMessage.value = 'Please select a new owner to reassign this request.'
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const response = await apiReassignRequestOwner(
      requestId.value,
      newOwnerPersonId,
      reassignForm.notes.trim() || null,
    )
    request.value = { ...request.value, ...response } as RequestDetail
    reassignForm.newOwnerPersonId = ''
    reassignForm.notes = ''
    showReassignModal.value = false
    actionSuccessMessage.value = 'Request ownership reassigned.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to reassign request ownership.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleCancelRequest(): Promise<void> {
  if (!requestId.value) {
    return
  }

  const reason = cancelForm.reason.trim()
  if (!reason) {
    errorMessage.value = 'Cancellation reason is required.'
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const response = await apiCancelRequest(
      requestId.value,
      reason,
    )
    request.value = { ...request.value, ...response } as RequestDetail
    cancelForm.reason = ''
    showCancelModal.value = false
    actionSuccessMessage.value = 'Request cancelled.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to cancel request.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleUpdateComplexity(): Promise<void> {
  if (!requestId.value) {
    return
  }
  complexityForm.isSubmitting = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const payload: { complexity: number; reason?: string; actorPersonId?: string } = {
      complexity: Number(complexityForm.complexity),
      reason: complexityForm.reason.trim() || undefined,
      actorPersonId: authStore.currentUser?.personId || undefined,
    }

    const response = await httpClient.patch<RequestDetail>(
      `/requests/${requestId.value}/complexity`,
      payload,
    )
    request.value = response.data
    complexityForm.isEditing = false
    complexityForm.reason = ''
    actionSuccessMessage.value = 'Request complexity updated successfully.'
    await loadStateHistory()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to update request complexity.')
  } finally {
    complexityForm.isSubmitting = false
  }
}

async function handleAddSubTask(): Promise<void> {
  const title = newSubTaskForm.title.trim()
  if (!title || !request.value || isClosed.value) {
    return
  }
  isSubTaskLoading.value = true
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const updated = await apiAddSubTask(
      request.value.id,
      title,
      newSubTaskForm.assigneePersonId || null,
    )
    request.value = { ...request.value, ...updated } as RequestDetail
    newSubTaskForm.title = ''
    newSubTaskForm.assigneePersonId = ''
    actionSuccessMessage.value = 'Sub-task added successfully.'
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to add sub-task.')
  } finally {
    isSubTaskLoading.value = false
  }
}

async function handleToggleSubTask(subTask: RequestSubTask): Promise<void> {
  if (!request.value || isClosed.value) {
    return
  }
  subTaskOperatingId.value = subTask.id
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    let updated
    if (subTask.isCompleted) {
      updated = await apiReopenSubTask(request.value.id, subTask.id)
      actionSuccessMessage.value = `Sub-task "${subTask.title}" reopened.`
    } else {
      updated = await apiCompleteSubTask(request.value.id, subTask.id)
      actionSuccessMessage.value = `Sub-task "${subTask.title}" completed.`
    }
    request.value = { ...request.value, ...updated } as RequestDetail
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to update sub-task status.')
  } finally {
    subTaskOperatingId.value = null
  }
}

async function handleRemoveSubTask(subTaskId: string): Promise<void> {
  if (!request.value || isClosed.value) {
    return
  }
  subTaskOperatingId.value = subTaskId
  errorMessage.value = null
  actionSuccessMessage.value = null

  try {
    const updated = await apiRemoveSubTask(request.value.id, subTaskId)
    request.value = { ...request.value, ...updated } as RequestDetail
    actionSuccessMessage.value = 'Sub-task removed successfully.'
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to remove sub-task.')
  } finally {
    subTaskOperatingId.value = null
  }
}

async function handleBack(): Promise<void> {
  if (window.history.length > 1) {
    router.back()
  } else {
    await router.push('/feed')
  }
}

watch(
  () => route.params.id,
  async (newId, oldId) => {
    if (newId && newId !== oldId) {
      await Promise.all([loadRequestDetail(), loadStateHistory(), loadRequestComments()])
    }
  },
)

onMounted(async () => {
  await Promise.all([loadActivePersons(), loadRequestDetail(), loadStateHistory(), loadRequestComments()])
})
</script>

<template>
  <section class="request-detail-view" data-screen-id="SCR-REQ-003">
    <!-- Screen Header -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2 flex-wrap">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm py-0 px-2"
          style="font-size: 12px; height: 26px; line-height: 24px"
          data-testid="back-button"
          @click="handleBack"
        >
          <i class="bi bi-arrow-left me-1" aria-hidden="true"></i>Back
        </button>
        <span class="font-monospace text-body-secondary small fw-medium" data-testid="request-detail-id">
          {{ request?.id || requestId }}
        </span>
        <span class="badge text-bg-secondary font-monospace" style="font-size: 11px">SCR-REQ-003</span>
        <template v-if="request">
          <span
            class="badge"
            style="font-size: 11px"
            :class="statusBadgeClass(request.status)"
            data-testid="request-detail-status"
          >
            {{ request.status }}
          </span>
          <span
            v-if="request.priority"
            class="badge"
            style="font-size: 11px"
            :class="priorityBadgeClass(request.priority)"
            data-testid="request-detail-priority"
          >
            {{ request.priority }}
          </span>
          <span
            class="badge"
            style="font-size: 11px"
            :class="complexityBadgeClass(request.complexity)"
            data-testid="request-detail-complexity"
          >
            Complexity: {{ formatComplexity(request.complexity) }}
          </span>
          <span
            v-if="request.requestType"
            class="badge text-bg-light border"
            style="font-size: 11px"
            data-testid="request-detail-type"
          >
            {{ request.requestType }}
          </span>
          <div
            v-if="(request.totalSubTasksCount ?? 0) > 0"
            class="d-inline-flex align-items-center gap-1 border rounded px-2 py-0 bg-light"
            style="height: 22px; font-size: 11px"
            data-testid="request-header-progress"
          >
            <span class="text-body-secondary small fw-medium">Progress:</span>
            <div class="progress" style="width: 50px; height: 8px;">
              <div
                class="progress-bar"
                :class="(request.completionPercentage ?? 0) === 100 ? 'bg-success' : 'bg-primary'"
                role="progressbar"
                :style="{ width: `${request.completionPercentage ?? 0}%` }"
                :aria-valuenow="request.completionPercentage ?? 0"
                aria-valuemin="0"
                aria-valuemax="100"
              ></div>
            </div>
            <span class="fw-semibold">
              {{ request.completionPercentage ?? 0 }}% ({{ request.completedSubTasksCount ?? 0 }}/{{ request.totalSubTasksCount ?? 0 }})
            </span>
          </div>
        </template>
      </div>

      <div class="d-flex align-items-center gap-1">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm py-0 px-2"
          style="font-size: 12px; height: 26px; line-height: 24px"
          :disabled="isLoading || isHistoryLoading"
          data-testid="refresh-request-detail-button"
          @click="refreshAll"
        >
          <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>Refresh
        </button>
      </div>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      role="alert"
      class="alert alert-danger alert-dismissible py-1 px-2 mb-2 small d-flex align-items-center gap-2"
      data-testid="request-detail-error-alert"
    >
      <i class="bi bi-exclamation-triangle-fill flex-shrink-0" aria-hidden="true"></i>
      <div>{{ errorMessage }}</div>
      <button
        type="button"
        class="btn-close py-1 px-2"
        aria-label="Close"
        @click="errorMessage = null"
      ></button>
    </div>

    <!-- Success Alert -->
    <div
      v-if="actionSuccessMessage"
      role="status"
      class="alert alert-success alert-dismissible py-1 px-2 mb-2 small d-flex align-items-center gap-2"
      data-testid="request-detail-success-alert"
    >
      <i class="bi bi-check-circle-fill flex-shrink-0" aria-hidden="true"></i>
      <div>{{ actionSuccessMessage }}</div>
      <button
        type="button"
        class="btn-close py-1 px-2"
        aria-label="Close"
        @click="actionSuccessMessage = null"
      ></button>
    </div>

    <!-- Loading Indicator -->
    <div v-if="isLoading && !request" class="text-center py-4">
      <div class="spinner-border spinner-border-sm text-primary" role="status">
        <span class="visually-hidden">Loading request details...</span>
      </div>
      <span class="text-body-secondary small ms-2">Loading request details...</span>
    </div>

    <!-- 2-Column Operational Workspace -->
    <div v-if="request" class="row g-2">
      <!-- Main Content Column (Left) -->
      <div class="col-12 col-lg-8">
        <!-- Request Detail Card -->
        <div class="card shadow-none bg-slate-900/90 border border-slate-800 text-slate-100 mb-2" data-testid="request-detail-card">
          <div class="card-header py-1 px-2 bg-slate-950/60 border-b border-slate-800 d-flex align-items-center justify-content-between flex-wrap gap-1">
            <h2 class="h6 mb-0 fw-bold" data-testid="request-detail-title">{{ request.title }}</h2>
            <button
              v-if="canEditCoreAttributes"
              type="button"
              class="btn btn-sm btn-outline-primary py-0 px-2"
              data-testid="edit-request-button"
              @click="openEditModal"
            >
              <i class="bi bi-pencil me-1" aria-hidden="true"></i>Edit
            </button>
          </div>
          <div class="card-body p-2">
            <!-- Description -->
            <div class="small fw-semibold text-body-secondary text-uppercase mb-1" style="font-size: 10.5px">
              Description
            </div>
            <div
              class="p-2 rounded bg-slate-950/60 border border-slate-800/80 mb-2 small text-slate-200"
              style="white-space: pre-wrap; font-size: 12.5px; line-height: 1.4"
              data-testid="request-detail-description"
            >
              {{ request.description }}
            </div>

            <!-- Evaluation Notes if present -->
            <div v-if="request.evaluationNotes" class="p-2 rounded bg-info-subtle border border-info-subtle mb-2 small">
              <div class="small fw-semibold text-info-emphasis mb-1">
                <i class="bi bi-journal-check me-1" aria-hidden="true"></i>Evaluation Notes
              </div>
              <div style="white-space: pre-wrap; font-size: 12px" data-testid="request-detail-evaluation-notes">
                {{ request.evaluationNotes }}
              </div>
            </div>

            <!-- Escalation Reason if present -->
            <div v-if="request.escalationReason" class="p-2 rounded bg-warning-subtle border border-warning-subtle mb-2 small">
              <div class="small fw-semibold text-warning-emphasis mb-1">
                <i class="bi bi-exclamation-octagon me-1" aria-hidden="true"></i>Escalation Reason
              </div>
              <div style="white-space: pre-wrap; font-size: 12px" data-testid="request-detail-escalation-reason">
                {{ request.escalationReason }}
              </div>
            </div>

            <!-- Management Decision Notes if present -->
            <div v-if="request.managementDecisionNotes" class="p-2 rounded bg-primary-subtle border border-primary-subtle mb-2 small">
              <div class="small fw-semibold text-primary-emphasis mb-1">
                <i class="bi bi-diagram-3 me-1" aria-hidden="true"></i>Management Decision Notes
              </div>
              <div style="white-space: pre-wrap; font-size: 12px" data-testid="request-detail-management-decision-notes">
                {{ request.managementDecisionNotes }}
              </div>
            </div>

            <!-- Resolution Details if present -->
            <div
              v-if="request.resolution"
              class="p-2 rounded border mb-2 small"
              :class="
                request.resolution.outcome === 'REJECTED' || request.resolution.outcome === 'CANCELLED'
                  ? 'bg-danger-subtle border-danger-subtle'
                  : 'bg-success-subtle border-success-subtle'
              "
              data-testid="request-detail-resolution"
            >
              <div class="d-flex flex-wrap justify-content-between align-items-center gap-1 mb-1">
                <span class="small fw-semibold">
                  <i class="bi bi-check2-circle me-1" aria-hidden="true"></i>
                  Resolution ({{ request.resolution.outcome }})
                </span>
                <span class="text-body-secondary" style="font-size: 11px">
                  Resolved by
                  {{ resolvePersonDisplay(request.resolution.resolvedBy, request.resolution.resolvedByName) }}
                  on {{ formatTimestamp(request.resolution.resolvedAt) }}
                </span>
              </div>
              <div style="white-space: pre-wrap; font-size: 12px" data-testid="request-detail-resolution-description">
                {{ request.resolution.description }}
              </div>
            </div>
          </div>
        </div>

        <!-- Sub-Tasks Checklist Card (CR-006, Architecture §4 TD-001..TD-003, TD-010) -->
        <div class="card shadow-none bg-slate-900/90 border border-slate-800 text-slate-100 mb-2" data-testid="request-subtasks-card">
          <div class="card-header py-1 px-2 bg-slate-950/60 border-b border-slate-800 d-flex align-items-center justify-content-between flex-wrap gap-1">
            <div class="d-flex align-items-center gap-2">
              <span class="fw-semibold small">
                <i class="bi bi-check2-square me-1 text-primary" aria-hidden="true"></i>
                Sub-Tasks Checklist
              </span>
              <span class="badge text-bg-secondary" data-testid="subtasks-count-badge">
                {{ request.completedSubTasksCount ?? 0 }} / {{ request.totalSubTasksCount ?? 0 }}
              </span>
            </div>

            <!-- Progress Bar inside Card Header -->
            <div
              v-if="(request.totalSubTasksCount ?? 0) > 0"
              class="d-flex align-items-center gap-2"
              data-testid="subtasks-progress-header"
            >
              <div class="progress" style="width: 100px; height: 10px;">
                <div
                  class="progress-bar"
                  :class="(request.completionPercentage ?? 0) === 100 ? 'bg-success' : 'bg-primary'"
                  role="progressbar"
                  :style="{ width: `${request.completionPercentage ?? 0}%` }"
                  :aria-valuenow="request.completionPercentage ?? 0"
                  aria-valuemin="0"
                  aria-valuemax="100"
                ></div>
              </div>
              <span class="fw-bold small text-body-secondary" style="font-size: 11px;">
                {{ request.completionPercentage ?? 0 }}%
              </span>
            </div>
          </div>

          <div class="card-body p-2">
            <!-- Sub-tasks items list -->
            <div
              v-if="!request.subTasks || request.subTasks.length === 0"
              class="text-body-secondary small py-2 px-1 text-center"
              data-testid="no-subtasks-message"
            >
              No sub-tasks defined for this request.
            </div>

            <ul v-else class="list-group list-group-flush mb-2" data-testid="subtasks-list">
              <li
                v-for="subTask in request.subTasks"
                :key="subTask.id"
                class="list-group-item px-2 py-2 d-flex align-items-start justify-content-between gap-2 border rounded mb-1 bg-body-subtle"
                data-testid="subtask-item"
                :data-subtask-id="subTask.id"
              >
                <div class="d-flex align-items-start gap-2 flex-grow-1 min-w-0">
                  <div class="form-check mt-0 mb-0 pt-0">
                    <input
                      :id="`subtask-chk-${subTask.id}`"
                      type="checkbox"
                      class="form-check-input"
                      style="cursor: pointer;"
                      :checked="subTask.isCompleted"
                      :disabled="isClosed || subTaskOperatingId === subTask.id"
                      data-testid="subtask-checkbox"
                      @change="handleToggleSubTask(subTask)"
                    />
                  </div>
                  <div class="flex-grow-1 min-w-0">
                    <label
                      :for="`subtask-chk-${subTask.id}`"
                      class="form-check-label d-block fw-medium mb-0 text-break"
                      :class="{ 'text-decoration-line-through text-body-secondary': subTask.isCompleted }"
                      style="font-size: 13px; cursor: pointer;"
                      data-testid="subtask-title"
                    >
                      {{ subTask.title }}
                    </label>

                    <!-- Subtask Metadata -->
                    <div class="d-flex flex-wrap align-items-center gap-2 mt-1 text-body-secondary" style="font-size: 11px;" data-testid="subtask-meta">
                      <span v-if="subTask.assigneePersonId || subTask.assigneeName" class="badge text-bg-light border text-secondary">
                        <i class="bi bi-person me-1" aria-hidden="true"></i>{{ resolveSubTaskAssigneeDisplay(subTask) }}
                      </span>
                      <span v-else class="badge text-bg-light border text-body-tertiary">
                        <i class="bi bi-person me-1" aria-hidden="true"></i>Unassigned
                      </span>

                      <span v-if="subTask.isCompleted" class="text-success small">
                        <i class="bi bi-check-circle me-1" aria-hidden="true"></i>Completed
                        <span v-if="resolveSubTaskCompletedByDisplay(subTask)">by {{ resolveSubTaskCompletedByDisplay(subTask) }}</span>
                        <span v-if="subTask.completedAt"> on {{ formatTimestamp(subTask.completedAt) }}</span>
                      </span>
                    </div>
                  </div>
                </div>

                <!-- Actions: Delete Sub-Task -->
                <div v-if="!isClosed" class="flex-shrink-0">
                  <button
                    type="button"
                    class="btn btn-outline-danger btn-sm py-0 px-1"
                    style="font-size: 11px; height: 24px; line-height: 22px;"
                    title="Remove Sub-Task"
                    :disabled="subTaskOperatingId === subTask.id"
                    data-testid="delete-subtask-button"
                    @click="handleRemoveSubTask(subTask.id)"
                  >
                    <i class="bi bi-trash" aria-hidden="true"></i>
                  </button>
                </div>
              </li>
            </ul>

            <!-- Inline Add Sub-Task Form (only when active) -->
            <form
              v-if="!isClosed"
              class="border rounded p-2 bg-body-tertiary"
              data-testid="add-subtask-form"
              @submit.prevent="handleAddSubTask"
            >
              <div class="small fw-semibold mb-1" style="font-size: 11.5px;">
                <i class="bi bi-plus-circle me-1 text-primary" aria-hidden="true"></i>
                Add Sub-Task
              </div>
              <div class="row g-2 align-items-center">
                <div class="col-12 col-md-7">
                  <input
                    v-model="newSubTaskForm.title"
                    type="text"
                    class="form-control form-control-sm"
                    placeholder="Enter sub-task title..."
                    maxlength="255"
                    required
                    :disabled="isSubTaskLoading"
                    data-testid="add-subtask-title-input"
                  />
                </div>
                <div class="col-8 col-md-3">
                  <select
                    v-model="newSubTaskForm.assigneePersonId"
                    class="form-select form-select-sm"
                    :disabled="isSubTaskLoading"
                    data-testid="add-subtask-assignee-select"
                  >
                    <option value="">(Assignee: Optional)</option>
                    <option
                      v-for="person in activePersons"
                      :key="person.id"
                      :value="person.id"
                    >
                      {{ person.fullName }}
                    </option>
                  </select>
                </div>
                <div class="col-4 col-md-2 text-end">
                  <button
                    type="submit"
                    class="btn btn-primary btn-sm w-100 py-1"
                    :disabled="isSubTaskLoading || !newSubTaskForm.title.trim()"
                    data-testid="add-subtask-button"
                  >
                    <span v-if="isSubTaskLoading" class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
                    <i v-else class="bi bi-plus-lg me-1" aria-hidden="true"></i>Add
                  </button>
                </div>
              </div>
            </form>
          </div>
        </div>

        <!-- Lifecycle Actions Card (CR-016 TD-001..003) -->
        <div class="card shadow-none bg-slate-900/90 border border-slate-800 text-slate-100 mb-2" data-testid="request-actions-card">
          <div class="card-header py-1 px-2 bg-slate-950/60 border-b border-slate-800 d-flex align-items-center justify-content-between">
            <span class="fw-semibold small">
              <i class="bi bi-sliders me-1 text-primary" aria-hidden="true"></i>
              Lifecycle Actions
            </span>
            <span class="badge" style="font-size: 10px" :class="statusBadgeClass(request.status)">
              Status: {{ request.status }}
            </span>
          </div>

          <!-- When Closed: Terminal Closed Badge -->
          <div v-if="isClosed" class="card-body p-2" data-testid="closed-actions-section">
            <div
              class="alert alert-secondary py-2 px-3 small d-flex align-items-center gap-2 mb-0"
              data-testid="request-closed-badge"
            >
              <i class="bi bi-lock-fill text-secondary flex-shrink-0" aria-hidden="true"></i>
              <span>
                This request is closed (<strong class="font-monospace">{{ request.status }}</strong>). No further lifecycle actions are available.
              </span>
            </div>
          </div>

          <!-- When Active: Contextual Action Buttons & Forms -->
          <div v-else class="card-body p-2">
            <!-- Contextual Action Buttons Bar -->
            <div class="d-flex flex-wrap align-items-center gap-2 mb-3 pb-2 border-bottom" data-testid="lifecycle-action-buttons-bar">
              <!-- When CAPTURED: [Assign Owner], [Cancel Request] -->
              <template v-if="isCaptured">
                <button
                  type="button"
                  class="btn btn-primary btn-sm py-1 px-2.5"
                  :disabled="isSubmittingAction"
                  data-testid="open-assign-button"
                  @click="showAssignModal = true"
                >
                  <i class="bi bi-person-check me-1" aria-hidden="true"></i>Assign Owner
                </button>
                <button
                  type="button"
                  class="btn btn-outline-danger btn-sm py-1 px-2.5"
                  :disabled="isSubmittingAction"
                  data-testid="open-cancel-button"
                  @click="showCancelModal = true"
                >
                  <i class="bi bi-x-circle me-1" aria-hidden="true"></i>Cancel Request
                </button>
              </template>

              <!-- When ASSIGNED: [Start Work] (enabled for assigned owner), [Reassign Owner], [Cancel Request] -->
              <template v-else-if="isAssigned">
                <button
                  type="button"
                  class="btn btn-primary btn-sm py-1 px-2.5"
                  :disabled="!isAssignedOwner || isSubmittingAction"
                  :title="!isAssignedOwner ? 'Only the assigned owner can start work on this request.' : 'Start work'"
                  data-testid="start-work-action-button"
                  @click="handleStartWork"
                >
                  <i class="bi bi-play-circle me-1" aria-hidden="true"></i>Start Work
                </button>
                <button
                  type="button"
                  class="btn btn-outline-secondary btn-sm py-1 px-2.5"
                  :disabled="isSubmittingAction"
                  data-testid="open-reassign-button"
                  @click="showReassignModal = true"
                >
                  <i class="bi bi-arrow-left-right me-1" aria-hidden="true"></i>Reassign Owner
                </button>
                <button
                  type="button"
                  class="btn btn-outline-danger btn-sm py-1 px-2.5"
                  :disabled="isSubmittingAction"
                  data-testid="open-cancel-button"
                  @click="showCancelModal = true"
                >
                  <i class="bi bi-x-circle me-1" aria-hidden="true"></i>Cancel Request
                </button>
              </template>

              <!-- When IN_PROGRESS: [Pause Work], [Complete Request], [Reassign Owner], [Cancel Request] -->
              <template v-else-if="isInProgress">
                <button
                  type="button"
                  class="btn btn-warning btn-sm py-1 px-2.5"
                  :disabled="isSubmittingAction"
                  data-testid="pause-work-action-button"
                  @click="showPauseModal = true"
                >
                  <i class="bi bi-pause-circle me-1" aria-hidden="true"></i>Pause Work
                </button>
                <button
                  type="button"
                  class="btn btn-success btn-sm py-1 px-2.5"
                  :disabled="isSubmittingAction || hasUnfinishedSubTasks"
                  :title="hasUnfinishedSubTasks ? 'All sub-tasks must be completed or removed before completing the request' : 'Complete request'"
                  data-testid="complete-request-action-button"
                  @click="showCompleteModal = true"
                >
                  <i class="bi bi-check2-all me-1" aria-hidden="true"></i>Complete Request
                </button>
                <button
                  type="button"
                  class="btn btn-outline-secondary btn-sm py-1 px-2.5"
                  :disabled="isSubmittingAction"
                  data-testid="open-reassign-button"
                  @click="showReassignModal = true"
                >
                  <i class="bi bi-arrow-left-right me-1" aria-hidden="true"></i>Reassign Owner
                </button>
                <button
                  type="button"
                  class="btn btn-outline-danger btn-sm py-1 px-2.5"
                  :disabled="isSubmittingAction"
                  data-testid="open-cancel-button"
                  @click="showCancelModal = true"
                >
                  <i class="bi bi-x-circle me-1" aria-hidden="true"></i>Cancel Request
                </button>
              </template>

              <!-- When PAUSED: [Start / Resume Work] (enabled for assigned owner), [Reassign Owner], [Cancel Request] -->
              <template v-else-if="isPaused">
                <button
                  type="button"
                  class="btn btn-primary btn-sm py-1 px-2.5"
                  :disabled="!isAssignedOwner || isSubmittingAction"
                  :title="!isAssignedOwner ? 'Only the assigned owner can resume work on this request.' : 'Resume work'"
                  data-testid="start-work-action-button"
                  @click="handleStartWork"
                >
                  <i class="bi bi-play-circle me-1" aria-hidden="true"></i>Start / Resume Work
                </button>
                <button
                  type="button"
                  class="btn btn-outline-secondary btn-sm py-1 px-2.5"
                  :disabled="isSubmittingAction"
                  data-testid="open-reassign-button"
                  @click="showReassignModal = true"
                >
                  <i class="bi bi-arrow-left-right me-1" aria-hidden="true"></i>Reassign Owner
                </button>
                <button
                  type="button"
                  class="btn btn-outline-danger btn-sm py-1 px-2.5"
                  :disabled="isSubmittingAction"
                  data-testid="open-cancel-button"
                  @click="showCancelModal = true"
                >
                  <i class="bi bi-x-circle me-1" aria-hidden="true"></i>Cancel Request
                </button>
              </template>
            </div>

            <!-- Warning if In Progress and unfinished subtasks remain -->
            <div
              v-if="isInProgress && hasUnfinishedSubTasks"
              class="alert alert-warning py-1 px-2 mb-2 small d-flex align-items-center gap-1"
              data-testid="complete-subtasks-warning"
            >
              <i class="bi bi-exclamation-triangle-fill text-warning flex-shrink-0" aria-hidden="true"></i>
              <span>
                Cannot complete request: <strong>{{ unfinishedSubTasksCount }} unfinished sub-task(s)</strong> remain. All sub-tasks must be completed or removed before completion.
              </span>
            </div>

            <!-- Active Lifecycle Forms Grid -->
            <div class="row g-2">
              <!-- CAPTURED: Assign Request Owner Form Section -->
              <div v-if="canAssign" class="col-12" data-testid="assign-action-section">
                <form class="border rounded p-2 bg-body-tertiary" @submit.prevent="handleAssign">
                  <div class="small fw-bold mb-1">
                    <i class="bi bi-person-plus me-1 text-primary"></i>Assign Request Owner
                  </div>
                  <div class="row g-1 align-items-end">
                    <div class="col-12 col-md-5">
                      <label for="assignOwnerSelect" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                        Assignee <span class="text-danger">*</span>
                      </label>
                      <select
                        id="assignOwnerSelect"
                        v-model="assignForm.ownerPersonId"
                        class="form-select form-select-sm"
                        required
                        :disabled="isSubmittingAction"
                        data-testid="assign-owner-select"
                      >
                        <option value="">Select active person...</option>
                        <option
                          v-for="person in activePersons"
                          :key="person.id"
                          :value="person.id"
                        >
                          {{ person.fullName }} ({{ person.email }})
                        </option>
                      </select>
                    </div>
                    <div class="col-12 col-md-5">
                      <label for="assignNotesInput" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                        Assignment Notes
                      </label>
                      <input
                        id="assignNotesInput"
                        v-model="assignForm.notes"
                        type="text"
                        class="form-control form-control-sm"
                        placeholder="Optional instructions"
                        :disabled="isSubmittingAction"
                        data-testid="assign-notes-input"
                      />
                    </div>
                    <div class="col-12 col-md-2">
                      <button
                        type="submit"
                        class="btn btn-primary btn-sm w-100"
                        :disabled="isSubmittingAction || !assignForm.ownerPersonId"
                        data-testid="assign-request-button"
                      >
                        <i class="bi bi-person-check me-1" aria-hidden="true"></i>Assign
                      </button>
                    </div>
                  </div>
                </form>
              </div>

              <!-- ASSIGNED / PAUSED: Start / Resume Work Section -->
              <div v-if="canStartWork" class="col-12 col-md-6" data-testid="start-action-section">
                <form class="border rounded p-2 h-100 bg-body-tertiary" @submit.prevent="handleStartWork">
                  <div class="small fw-bold mb-1">
                    <i class="bi bi-play-circle me-1 text-primary"></i>
                    {{ isPaused ? 'Resume Work' : 'Start Work' }}
                  </div>
                  <div class="mb-1">
                    <label for="startNotesInput" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                      Work Notes <span class="text-body-secondary">(Optional)</span>
                    </label>
                    <input
                      id="startNotesInput"
                      v-model="startForm.notes"
                      type="text"
                      class="form-control form-control-sm"
                      placeholder="Optional notes..."
                      :disabled="isSubmittingAction || !isAssignedOwner"
                      data-testid="start-notes-input"
                    />
                  </div>
                  <div v-if="!isAssignedOwner" class="alert alert-warning py-1 px-2 mb-1 small" style="font-size: 11px">
                    <i class="bi bi-shield-lock me-1"></i>Only the assigned owner can start work on this request.
                  </div>
                  <button
                    type="submit"
                    class="btn btn-primary btn-sm"
                    :disabled="isSubmittingAction || !isAssignedOwner"
                    :title="!isAssignedOwner ? 'Only the assigned owner can start work on this request.' : 'Start Work'"
                    data-testid="start-work-button"
                  >
                    <i class="bi bi-play-circle me-1" aria-hidden="true"></i>
                    {{ isPaused ? 'Resume Work' : 'Start Work' }}
                  </button>
                </form>
              </div>

              <!-- IN_PROGRESS: Pause Work Section -->
              <div v-if="canPauseWork" class="col-12 col-md-6" data-testid="pause-action-section">
                <form class="border rounded p-2 h-100 bg-body-tertiary" @submit.prevent="handlePauseWork">
                  <div class="small fw-bold mb-1">
                    <i class="bi bi-pause-circle me-1 text-warning"></i>Pause Work
                  </div>
                  <div class="mb-1">
                    <label for="pauseNoteInput" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                      Pause Note <span class="text-body-secondary">(Optional)</span>
                    </label>
                    <textarea
                      id="pauseNoteInput"
                      v-model="pauseForm.note"
                      class="form-control form-control-sm"
                      rows="2"
                      placeholder="Document reason for suspension or external blocker..."
                      :disabled="isSubmittingAction"
                      data-testid="pause-note-input"
                    ></textarea>
                  </div>
                  <button
                    type="submit"
                    class="btn btn-warning btn-sm"
                    :disabled="isSubmittingAction"
                    data-testid="pause-work-button"
                  >
                    <i class="bi bi-pause-circle me-1" aria-hidden="true"></i>Pause Work
                  </button>
                </form>
              </div>

              <!-- IN_PROGRESS: Complete Request Section -->
              <div v-if="canComplete" class="col-12 col-md-6" data-testid="complete-action-section">
                <form class="border rounded p-2 h-100 bg-body-tertiary" @submit.prevent="handleComplete">
                  <div class="small fw-bold mb-1">
                    <i class="bi bi-check2-all me-1 text-success"></i>Complete Request
                  </div>
                  <div class="mb-1">
                    <label for="completeResolutionInput" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                      Resolution Description <span class="text-danger">*</span>
                    </label>
                    <textarea
                      id="completeResolutionInput"
                      v-model="completeForm.resolutionDescription"
                      class="form-control form-control-sm"
                      rows="2"
                      placeholder="Describe completed solution..."
                      required
                      :disabled="isSubmittingAction || hasUnfinishedSubTasks"
                      data-testid="complete-resolution-input"
                    ></textarea>
                  </div>
                  <button
                    type="submit"
                    class="btn btn-success btn-sm"
                    :disabled="isSubmittingAction || !completeForm.resolutionDescription.trim() || hasUnfinishedSubTasks"
                    :title="hasUnfinishedSubTasks ? 'All sub-tasks must be completed or removed before completing the request' : 'Complete Request'"
                    data-testid="complete-request-button"
                  >
                    <i class="bi bi-check2-all me-1" aria-hidden="true"></i>Complete
                  </button>
                </form>
              </div>

              <!-- ASSIGNED / IN_PROGRESS / PAUSED: Reassign Ownership Section -->
              <div v-if="canReassign" class="col-12 col-md-6" data-testid="reassign-action-section">
                <form class="border rounded p-2 h-100 bg-body-tertiary" @submit.prevent="handleReassign">
                  <div class="small fw-bold mb-1">
                    <i class="bi bi-arrow-left-right me-1 text-secondary"></i>Reassign Ownership
                  </div>
                  <div class="row g-1 mb-1">
                    <div class="col-12">
                      <select
                        id="reassignOwnerSelect"
                        v-model="reassignForm.newOwnerPersonId"
                        class="form-select form-select-sm"
                        required
                        :disabled="isSubmittingAction"
                        data-testid="reassign-owner-select"
                      >
                        <option value="">Select new assignee...</option>
                        <option
                          v-for="person in activePersons"
                          :key="person.id"
                          :value="person.id"
                        >
                          {{ person.fullName }} ({{ person.email }})
                        </option>
                      </select>
                    </div>
                    <div class="col-12">
                      <input
                        id="reassignNotesInput"
                        v-model="reassignForm.notes"
                        type="text"
                        class="form-control form-control-sm"
                        placeholder="Reassignment notes..."
                        :disabled="isSubmittingAction"
                        data-testid="reassign-notes-input"
                      />
                    </div>
                  </div>
                  <button
                    type="submit"
                    class="btn btn-outline-secondary btn-sm"
                    :disabled="isSubmittingAction || !reassignForm.newOwnerPersonId"
                    data-testid="reassign-request-button"
                  >
                    <i class="bi bi-arrow-left-right me-1" aria-hidden="true"></i>Reassign
                  </button>
                </form>
              </div>

              <!-- ALL ACTIVE: Cancel Request Section -->
              <div v-if="canCancel" class="col-12 col-md-6" data-testid="cancel-action-section">
                <form class="border rounded p-2 h-100 bg-body-tertiary" @submit.prevent="handleCancelRequest">
                  <div class="small fw-bold mb-1 text-danger">
                    <i class="bi bi-x-circle me-1 text-danger"></i>Cancel Request
                  </div>
                  <div class="mb-1">
                    <label for="cancelReasonInput" class="form-label mb-0 small fw-medium text-danger" style="font-size: 11px">
                      Cancellation Reason <span class="text-danger">*</span>
                    </label>
                    <textarea
                      id="cancelReasonInput"
                      v-model="cancelForm.reason"
                      class="form-control form-control-sm"
                      rows="2"
                      placeholder="Mandatory cancellation reason..."
                      required
                      :disabled="isSubmittingAction"
                      data-testid="cancel-reason-input"
                    ></textarea>
                  </div>
                  <button
                    type="submit"
                    class="btn btn-danger btn-sm"
                    :disabled="isSubmittingAction || !cancelForm.reason.trim()"
                    data-testid="cancel-request-button"
                  >
                    <i class="bi bi-x-circle me-1" aria-hidden="true"></i>Cancel Request
                  </button>
                </form>
              </div>
            </div>
          </div>
        </div>

        <!-- Pause Work Modal Dialog -->
        <div
          v-if="showPauseModal"
          class="modal fade show d-block"
          tabindex="-1"
          style="background-color: rgba(2, 6, 23, 0.8); backdrop-filter: blur(4px);"
          data-testid="pause-work-modal"
          role="dialog"
          aria-modal="true"
        >
          <div class="modal-dialog modal-dialog-centered">
            <div class="modal-content bg-slate-900 border border-slate-800 text-slate-100">
              <div class="modal-header py-2 bg-slate-950/60 border-b border-slate-800">
                <h5 class="modal-title h6 mb-0 text-amber-400">
                  <i class="bi bi-pause-circle me-1 text-warning"></i>Pause Work
                </h5>
                <button
                  type="button"
                  class="btn-close"
                  aria-label="Close"
                  data-testid="close-pause-modal-button"
                  @click="showPauseModal = false"
                ></button>
              </div>
              <form @submit.prevent="handlePauseWork">
                <div class="modal-body py-2">
                  <p class="small text-body-secondary mb-2">
                    Suspend active progress on this request. You can resume work at any time or discuss blockers in the comments thread.
                  </p>
                  <label for="modalPauseNote" class="form-label small fw-medium">
                    Pause Note <span class="text-body-secondary">(Optional)</span>
                  </label>
                  <textarea
                    id="modalPauseNote"
                    v-model="pauseForm.note"
                    class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                    rows="3"
                    placeholder="Document reason for pause or external blocker..."
                    :disabled="isSubmittingAction"
                    data-testid="modal-pause-note-input"
                  ></textarea>
                </div>
                <div class="modal-footer py-1 bg-slate-950/60 border-t border-slate-800">
                  <button
                    type="button"
                    class="btn btn-outline-secondary btn-sm"
                    :disabled="isSubmittingAction"
                    @click="showPauseModal = false"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    class="btn btn-warning btn-sm"
                    :disabled="isSubmittingAction"
                    data-testid="modal-submit-pause-button"
                  >
                    <span v-if="isSubmittingAction" class="spinner-border spinner-border-sm me-1" role="status"></span>
                    <i v-else class="bi bi-pause-circle me-1"></i>Pause Work
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>

        <!-- Cancel Request Modal Dialog -->
        <div
          v-if="showCancelModal"
          class="modal fade show d-block"
          tabindex="-1"
          style="background-color: rgba(2, 6, 23, 0.8); backdrop-filter: blur(4px);"
          data-testid="cancel-request-modal"
          role="dialog"
          aria-modal="true"
        >
          <div class="modal-dialog modal-dialog-centered">
            <div class="modal-content bg-slate-900 border border-slate-800 text-slate-100">
              <div class="modal-header py-2 bg-slate-950/60 border-b border-slate-800">
                <h5 class="modal-title h6 mb-0 text-rose-400">
                  <i class="bi bi-x-circle me-1 text-danger"></i>Cancel Request
                </h5>
                <button
                  type="button"
                  class="btn-close"
                  aria-label="Close"
                  data-testid="close-cancel-modal-button"
                  @click="showCancelModal = false"
                ></button>
              </div>
              <form @submit.prevent="handleCancelRequest">
                <div class="modal-body py-2">
                  <p class="small text-body-secondary mb-2">
                    Cancelling will move this request into terminal <strong class="text-danger">CANCELLED</strong> state. A cancellation reason is mandatory.
                  </p>
                  <label for="modalCancelReason" class="form-label small fw-medium">
                    Cancellation Reason <span class="text-danger">*</span>
                  </label>
                  <textarea
                    id="modalCancelReason"
                    v-model="cancelForm.reason"
                    class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                    rows="3"
                    placeholder="Explain why this request is being cancelled..."
                    required
                    :disabled="isSubmittingAction"
                    data-testid="modal-cancel-reason-input"
                  ></textarea>
                </div>
                <div class="modal-footer py-1 bg-slate-950/60 border-t border-slate-800">
                  <button
                    type="button"
                    class="btn btn-outline-secondary btn-sm"
                    :disabled="isSubmittingAction"
                    @click="showCancelModal = false"
                  >
                    Back
                  </button>
                  <button
                    type="submit"
                    class="btn btn-danger btn-sm"
                    :disabled="isSubmittingAction || !cancelForm.reason.trim()"
                    data-testid="modal-submit-cancel-button"
                  >
                    <span v-if="isSubmittingAction" class="spinner-border spinner-border-sm me-1" role="status"></span>
                    <i v-else class="bi bi-x-circle me-1"></i>Confirm Cancellation
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>

        <!-- Edit Request Modal Dialog (CR-018) -->
        <div
          v-if="showEditModal"
          class="modal fade show d-block"
          tabindex="-1"
          style="background-color: rgba(2, 6, 23, 0.8); backdrop-filter: blur(4px);"
          data-testid="edit-request-modal"
          role="dialog"
          aria-modal="true"
        >
          <div class="modal-dialog modal-dialog-centered">
            <div class="modal-content bg-slate-900 border border-slate-800 text-slate-100">
              <div class="modal-header py-2 bg-slate-950/60 border-b border-slate-800">
                <h5 class="modal-title h6 mb-0 text-cyan-400">
                  <i class="bi bi-pencil-square me-1 text-primary"></i>Edit Request Details
                </h5>
                <button
                  type="button"
                  class="btn-close"
                  aria-label="Close"
                  :disabled="isSubmittingEdit"
                  @click="showEditModal = false"
                ></button>
              </div>
              <form @submit.prevent="handleSaveEdit">
                <div class="modal-body py-2">
                  <div
                    v-if="editErrorMessage"
                    class="alert alert-danger py-1 px-2 small mb-2"
                    role="alert"
                    data-testid="edit-error-alert"
                  >
                    <i class="bi bi-exclamation-triangle-fill me-1"></i>
                    {{ editErrorMessage }}
                  </div>

                  <div class="mb-2">
                    <label for="editTitleInput" class="form-label small fw-medium">
                      Title <span class="text-danger">*</span>
                    </label>
                    <input
                      id="editTitleInput"
                      v-model="editForm.title"
                      type="text"
                      class="form-control form-control-sm"
                      placeholder="Request Title"
                      required
                      maxlength="255"
                      :disabled="isSubmittingEdit"
                      data-testid="edit-title-input"
                    />
                  </div>

                  <div class="mb-2">
                    <label for="editDescriptionInput" class="form-label small fw-medium">
                      Description <span class="text-danger">*</span>
                    </label>
                    <textarea
                      id="editDescriptionInput"
                      v-model="editForm.description"
                      class="form-control form-control-sm"
                      placeholder="Request Description"
                      rows="4"
                      required
                      :disabled="isSubmittingEdit"
                      data-testid="edit-description-input"
                    ></textarea>
                  </div>

                  <div class="row g-2 mb-2">
                    <div class="col-6">
                      <label for="editTypeSelect" class="form-label small fw-medium">
                        Request Type <span class="text-danger">*</span>
                      </label>
                      <select
                        id="editTypeSelect"
                        v-model="editForm.requestType"
                        class="form-select form-select-sm"
                        required
                        :disabled="isSubmittingEdit"
                        data-testid="edit-type-select"
                      >
                        <option v-for="type in REQUEST_TYPES" :key="type" :value="type">
                          {{ type }}
                        </option>
                      </select>
                    </div>

                    <div class="col-6">
                      <label for="editPrioritySelect" class="form-label small fw-medium">
                        Priority <span class="text-danger">*</span>
                      </label>
                      <select
                        id="editPrioritySelect"
                        v-model="editForm.priority"
                        class="form-select form-select-sm"
                        required
                        :disabled="isSubmittingEdit"
                        data-testid="edit-priority-select"
                      >
                        <option v-for="priority in PRIORITIES" :key="priority" :value="priority">
                          {{ priority }}
                        </option>
                      </select>
                    </div>
                  </div>

                  <div class="mb-2">
                    <label for="editDeadlineInput" class="form-label small fw-medium">
                      Deadline <span class="text-body-secondary">(Optional)</span>
                    </label>
                    <div class="input-group">
                      <input
                        id="editDeadlineInput"
                        v-model="editForm.deadline"
                        type="date"
                        class="form-control"
                        :disabled="isSubmittingEdit"
                        data-testid="edit-deadline-input"
                      />
                      <button
                        type="button"
                        class="btn btn-outline-secondary"
                        :disabled="isSubmittingEdit || !editForm.deadline"
                        data-testid="clear-deadline-button"
                        @click="editForm.deadline = null"
                      >
                        Clear Deadline
                      </button>
                    </div>
                  </div>
                </div>

                <div class="modal-footer py-1 bg-slate-950/60 border-t border-slate-800">
                  <button
                    type="button"
                    class="btn btn-outline-secondary btn-sm"
                    :disabled="isSubmittingEdit"
                    data-testid="close-edit-modal-button"
                    @click="showEditModal = false"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    class="btn btn-primary btn-sm"
                    :disabled="isSubmittingEdit"
                    data-testid="save-edit-button"
                  >
                    <span v-if="isSubmittingEdit" class="spinner-border spinner-border-sm me-1" role="status"></span>
                    <i v-else class="bi bi-check2 me-1"></i>Save Changes
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>

      <!-- Properties & History Column (Right) -->
      <div class="col-12 col-lg-4">
        <!-- Properties Card -->
        <div class="card shadow-none bg-slate-900/90 border border-slate-800 text-slate-100 mb-2" data-testid="request-information-card">
          <div class="card-header py-1 px-2 bg-slate-950/60 border-b border-slate-800 fw-semibold small">
            <i class="bi bi-info-circle me-1 text-primary" aria-hidden="true"></i>
            Properties
          </div>
          <div class="card-body p-2">
            <dl class="row mb-0 g-1 small" style="font-size: 12px">
              <dt class="col-4 text-body-secondary fw-normal">Customer:</dt>
              <dd class="col-8 fw-semibold mb-1" data-testid="request-detail-customer">
                {{ resolveCustomerDisplay(request) }}
              </dd>

              <dt class="col-4 text-body-secondary fw-normal">Product:</dt>
              <dd class="col-8 fw-semibold mb-1" data-testid="request-detail-product">
                {{ resolveProductDisplay(request) }}
              </dd>

              <dt class="col-4 text-body-secondary fw-normal">Assignee:</dt>
              <dd class="col-8 fw-semibold mb-1" data-testid="request-detail-assignee">
                <i class="bi bi-person me-1 text-secondary" aria-hidden="true"></i>
                {{ resolveAssigneeDisplay(request) }}
              </dd>

              <dt class="col-4 text-body-secondary fw-normal">Status:</dt>
              <dd class="col-8 mb-1">
                <span class="badge" style="font-size: 10px" :class="statusBadgeClass(request.status)">
                  {{ request.status }}
                </span>
              </dd>

              <dt class="col-4 text-body-secondary fw-normal">Complexity:</dt>
              <dd class="col-8 mb-1" data-testid="request-detail-complexity-property">
                <div class="d-flex align-items-center gap-1">
                  <span
                    class="badge"
                    style="font-size: 10px"
                    :class="complexityBadgeClass(request.complexity)"
                  >
                    {{ formatComplexity(request.complexity) }}
                  </span>
                  <button
                    v-if="canEditComplexity && !complexityForm.isEditing"
                    type="button"
                    class="btn btn-link btn-sm p-0 text-decoration-none"
                    style="font-size: 11px"
                    title="Change Complexity"
                    data-testid="edit-complexity-button"
                    @click="initComplexityForm(); complexityForm.isEditing = true"
                  >
                    <i class="bi bi-pencil ms-1" aria-hidden="true"></i>Edit
                  </button>
                </div>
              </dd>

              <dt class="col-4 text-body-secondary fw-normal">Deadline:</dt>
              <dd class="col-8 mb-1" data-testid="request-detail-deadline">
                <div class="d-flex align-items-center gap-2">
                  <span>{{ formatDeadline(request.deadline) }}</span>
                  <span v-if="isOverdue" class="badge bg-danger" data-testid="request-overdue-badge">
                    <i class="bi bi-exclamation-triangle-fill me-1"></i>Overdue
                  </span>
                </div>
              </dd>

              <dt class="col-4 text-body-secondary fw-normal">Created:</dt>
              <dd class="col-8 mb-1 text-body-secondary" style="font-size: 11px" data-testid="request-detail-created-at">
                {{ formatTimestamp(request.createdAt) }}
              </dd>

              <dt class="col-4 text-body-secondary fw-normal">Updated:</dt>
              <dd class="col-8 mb-0 text-body-secondary" style="font-size: 11px" data-testid="request-detail-updated-at">
                {{ formatTimestamp(request.updatedAt) }}
              </dd>
            </dl>

            <!-- Inline Complexity Editor -->
            <div
              v-if="complexityForm.isEditing"
              class="border rounded p-2 bg-body-tertiary mt-2"
              data-testid="edit-complexity-form"
            >
              <div class="fw-semibold small mb-1" style="font-size: 11px">
                <i class="bi bi-sliders me-1 text-primary" aria-hidden="true"></i>Update Complexity (1 - 5)
              </div>
              <div class="mb-1">
                <select
                  v-model.number="complexityForm.complexity"
                  class="form-select form-select-sm"
                  :disabled="complexityForm.isSubmitting"
                  data-testid="edit-complexity-select"
                >
                  <option :value="1">1 (Very Low)</option>
                  <option :value="2">2 (Low)</option>
                  <option :value="3">3 (Medium)</option>
                  <option :value="4">4 (High)</option>
                  <option :value="5">5 (Very High)</option>
                </select>
              </div>
              <div class="mb-1">
                <input
                  v-model="complexityForm.reason"
                  type="text"
                  class="form-control form-control-sm"
                  placeholder="Optional reason (max 500 chars)"
                  maxlength="500"
                  :disabled="complexityForm.isSubmitting"
                  data-testid="edit-complexity-reason-input"
                />
              </div>
              <div class="d-flex gap-1 justify-content-end">
                <button
                  type="button"
                  class="btn btn-outline-secondary btn-sm py-0 px-2"
                  style="font-size: 11px"
                  :disabled="complexityForm.isSubmitting"
                  @click="complexityForm.isEditing = false"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  class="btn btn-primary btn-sm py-0 px-2"
                  style="font-size: 11px"
                  :disabled="complexityForm.isSubmitting"
                  data-testid="save-complexity-button"
                  @click="handleUpdateComplexity"
                >
                  <span
                    v-if="complexityForm.isSubmitting"
                    class="spinner-border spinner-border-sm me-1"
                    role="status"
                    aria-hidden="true"
                  ></span>
                  Save
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- State History Timeline Card -->
        <div class="card card-table shadow-none bg-slate-900/90 border border-slate-800 text-slate-100 mb-2" data-testid="request-history-card">
          <div class="card-header py-1 px-2 bg-slate-950/60 border-b border-slate-800 d-flex justify-content-between align-items-center">
            <span class="fw-semibold small">
              <i class="bi bi-clock-history me-1 text-primary" aria-hidden="true"></i>
              State History
            </span>
            <span class="badge text-bg-secondary" style="font-size: 11px" data-testid="request-history-count">
              {{ stateHistory.length }}
            </span>
          </div>

          <div class="card-body p-0" style="max-height: 520px; overflow-y: auto">
            <div v-if="isHistoryLoading" class="text-center py-3 text-body-secondary small">
              <span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
              Loading timeline...
            </div>

            <div
              v-else-if="stateHistory.length === 0"
              class="text-center py-3 text-body-secondary small"
              data-testid="empty-history-message"
            >
              No state transitions recorded yet.
            </div>

            <ul
              v-else
              class="list-group list-group-flush"
              data-testid="request-history-timeline"
            >
              <li
                v-for="entry in stateHistory"
                :key="entry.id"
                class="list-group-item px-2 py-1.5"
                data-testid="request-history-item"
              >
                <div class="d-flex flex-wrap justify-content-between align-items-start gap-1">
                  <div class="d-flex align-items-center gap-1 flex-wrap">
                    <span
                      v-if="entry.previousStatus"
                      class="badge"
                      style="font-size: 10px; padding: 2px 4px"
                      :class="statusBadgeClass(entry.previousStatus)"
                    >
                      {{ entry.previousStatus }}
                    </span>
                    <i
                      v-if="entry.previousStatus"
                      class="bi bi-arrow-right text-body-secondary"
                      style="font-size: 10px"
                      aria-hidden="true"
                    ></i>
                    <span class="badge" style="font-size: 10px; padding: 2px 4px" :class="statusBadgeClass(entry.newStatus)">
                      {{ entry.newStatus }}
                    </span>
                  </div>

                  <span class="text-body-secondary font-monospace" style="font-size: 10.5px">
                    {{ formatTimestamp(entry.assignedAtUtc || entry.timestamp || entry.createdAt) }}
                  </span>
                </div>

                <div class="small text-body-secondary mt-1" style="font-size: 11.5px">
                  by <strong>{{ resolvePersonDisplay(entry.actorPersonId, entry.actorName) }}</strong>
                  <span v-if="entry.assignedOwnerPersonId">
                    &rarr; {{ resolvePersonDisplay(entry.assignedOwnerPersonId, entry.assignedOwnerName) }}
                  </span>
                </div>

                <div
                  v-if="entry.notes"
                  class="mt-1 small text-body-secondary font-monospace p-1 rounded bg-body-tertiary"
                  style="white-space: pre-wrap; font-size: 11px"
                >
                  {{ entry.notes }}
                </div>
              </li>
            </ul>
          </div>
        </div>

        <!-- Embedded Comments & Discussion Card (CR-016 TD-005) -->
        <div class="card shadow-none bg-slate-900/90 border border-slate-800 text-slate-100 mb-2" data-testid="request-comments-card">
          <div class="card-header py-1 px-2 bg-slate-950/60 border-b border-slate-800 d-flex justify-content-between align-items-center">
            <span class="fw-semibold small">
              <i class="bi bi-chat-left-text me-1 text-primary" aria-hidden="true"></i>
              Comments &amp; Discussion
            </span>
            <div class="d-flex align-items-center gap-1">
              <span class="badge text-bg-secondary" style="font-size: 11px" data-testid="request-comments-count">
                {{ comments.length }}
              </span>
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm py-0 px-1"
                style="font-size: 11px; height: 22px; line-height: 20px"
                title="Refresh comments"
                :disabled="isCommentsLoading"
                data-testid="refresh-comments-button"
                @click="loadRequestComments"
              >
                <i class="bi bi-arrow-clockwise" aria-hidden="true"></i>
              </button>
            </div>
          </div>

          <div class="card-body p-2">
            <!-- Comment Error Alert -->
            <div
              v-if="commentErrorMessage"
              role="alert"
              class="alert alert-danger py-1 px-2 mb-2 small d-flex align-items-center gap-1"
              data-testid="comment-error-alert"
            >
              <i class="bi bi-exclamation-triangle-fill flex-shrink-0" aria-hidden="true"></i>
              <div class="flex-grow-1">{{ commentErrorMessage }}</div>
              <button
                type="button"
                class="btn-close py-1 px-2"
                aria-label="Close"
                @click="commentErrorMessage = null"
              ></button>
            </div>

            <!-- Loading Comments Indicator -->
            <div v-if="isCommentsLoading && comments.length === 0" class="text-center py-3 text-body-secondary small">
              <span class="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
              Loading discussion...
            </div>

            <!-- Empty Comments State -->
            <div
              v-else-if="comments.length === 0"
              class="text-center py-3 text-body-secondary small"
              data-testid="empty-comments-message"
            >
              No comments yet. Start a discussion or coordinate blockers.
            </div>

            <!-- Chronological Comments Thread -->
            <div
              v-else
              class="comments-list mb-2"
              style="max-height: 380px; overflow-y: auto"
              data-testid="request-comments-list"
            >
              <div
                v-for="c in comments"
                :key="c.id"
                class="p-2 border rounded mb-1 bg-body-subtle"
                data-testid="request-comment-item"
              >
                <div class="d-flex justify-content-between align-items-center mb-1">
                  <span class="fw-semibold small text-primary" style="font-size: 11.5px">
                    <i class="bi bi-person-fill me-1" aria-hidden="true"></i>{{ c.authorName || c.author || 'User' }}
                  </span>
                  <span class="text-body-secondary font-monospace" style="font-size: 10px">
                    {{ formatTimestamp(c.createdAt) }}
                  </span>
                </div>
                <div
                  class="small text-body"
                  style="white-space: pre-wrap; font-size: 12px; line-height: 1.4"
                  data-testid="request-comment-content"
                >
                  {{ c.content }}
                </div>
              </div>
            </div>

            <!-- Add Comment Form -->
            <form class="mt-2" data-testid="add-comment-form" @submit.prevent="handleAddComment">
              <div class="mb-1">
                <textarea
                  v-model="newCommentContent"
                  class="form-control form-control-sm"
                  rows="2"
                  placeholder="Write a comment or discuss blocker..."
                  required
                  :disabled="isSubmittingComment"
                  data-testid="add-comment-input"
                ></textarea>
              </div>
              <div class="d-flex justify-content-end">
                <button
                  type="submit"
                  class="btn btn-primary btn-sm py-0 px-2"
                  style="font-size: 12px; height: 26px"
                  :disabled="isSubmittingComment || !newCommentContent.trim()"
                  data-testid="add-comment-button"
                >
                  <span
                    v-if="isSubmittingComment"
                    class="spinner-border spinner-border-sm me-1"
                    role="status"
                    aria-hidden="true"
                  ></span>
                  <i v-else class="bi bi-send me-1" aria-hidden="true"></i>Add Comment
                </button>
              </div>
            </form>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>
