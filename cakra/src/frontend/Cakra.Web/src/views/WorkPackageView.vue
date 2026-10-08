<script lang="ts">
export {
  normalizeTaskLine,
  parseTaskListText,
  parseTaskListTasks,
  type CandidateTaskItem,
} from '@/api/requests'
</script>

<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { httpClient } from '@/api/http'
import {
  parseTaskListTasks,
  recordRequest,
  type CandidateTaskItem,
  type RequestDto,
} from '@/api/requests'
import {
  getOperationsCockpit,
  reorderWorkPackageRequests,
  updateWorkPackageContext,
  updateWorkPackageDeadline,
  type FlowBarcodeDayDto,
  type WorkPackageTelemetryDto,
} from '@/api/workpackages'

/**
 * SCR-WP-001: Work Package Screen
 * (Architecture §7, §8 — UC-WP-001..003, §9 — FEAT-WP-001, §11, §19.4, §19.6, §20, §21; CR-015).
 *
 * - Renders a Bootstrap 5 table listing work packages with columns:
 *   ID, Objective, Owner, Customer, Product, Status.
 * - Provides filter dropdowns for `status`, `owner` (`GET /api/v1/organization/persons/active`),
 *   `customer` (`GET /api/v1/customers/active`), and `product` (`GET /api/v1/products/active`).
 * - Provides a "Create Work Package" button opening a form/modal (`POST /api/v1/work-packages`
 *   with `objective`, `ownerPersonId`, optional `customerId`, optional `productId`).
 * - Renders a detail panel when a work package is selected (via row click or route `:id` param)
 *   showing:
 *   * Objective display & edit (`PUT /api/v1/work-packages/${id}/objective`)
 *   * Owner selector dropdown (`POST /api/v1/work-packages/${id}/assign-owner`)
 *   * Conditional lifecycle buttons (`Activate` in DRAFT via `POST /api/v1/work-packages/${id}/activate`,
 *     `Close` in DRAFT/ACTIVE via `POST /api/v1/work-packages/${id}/close`)
 *   * Scope management section (`GET /api/v1/work-packages/${id}/scope`) with "Add Request"
 *     selector/input (`POST /api/v1/work-packages/${id}/requests`) and linked request list
 *     with remove buttons (`DELETE /api/v1/work-packages/${id}/requests/${requestId}`) and
 *     HTML5 drag-and-drop reordering with optimistic updates and background auto-save (`PUT /api/v1/work-packages/${id}/requests/reorder`).
 */

export interface WorkPackageScopeItem {
  id: string
  membershipId?: string
  workPackageId: string
  requestId: string
  sortOrder?: number
  addedAt: string
  removedAt?: string | null
  isActive: boolean
  createdAt: string
  updatedAt?: string | null
  title: string
  requestTitle?: string
  description?: string
  requestType?: string
  status: string
  requestStatus?: string
  priority?: string
  complexity?: number
  blockerNote?: string | null
  ownerPersonId?: string | null
  requestOwnerPersonId?: string | null
  ownerName?: string | null
  requestOwnerName?: string | null
  customerId?: string | null
  customerName?: string | null
  productId?: string | null
  productName?: string | null
  request?: RequestDto | null
}

export interface WorkPackageItem {
  id: string
  workPackageId?: string
  name: string
  objective: string
  status: 'DRAFT' | 'ACTIVE' | 'CLOSED' | string
  ownerPersonId: string
  ownerName?: string | null
  customerId?: string | null
  customerName?: string | null
  customerCode?: string | null
  productId?: string | null
  productName?: string | null
  productCode?: string | null
  deadline?: string | null
  closedReason?: string | null
  closedAt?: string | null
  createdAt: string
  updatedAt?: string | null
  requests?: WorkPackageScopeItem[]
  activeRequests?: WorkPackageScopeItem[]
  activeRequestCount?: number
  totalRequestCount?: number
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

export interface ActiveCustomerOption {
  id: string
  customerId?: string
  customerCode: string
  code: string
  customerName: string
  name: string
  status: string
  hasActiveMaintenanceContract?: boolean
  isActive?: boolean
}

export interface ActiveProductOption {
  id: string
  productId?: string
  code: string
  productCode?: string
  name: string
  productName?: string
  description?: string | null
  ownerPersonId: string
  ownerName?: string | null
  status: string
  isActive?: boolean
}

export interface RequestOptionItem {
  id: string
  requestId?: string
  title: string
  status: string
  priority?: string
  customerName?: string | null
  productName?: string | null
}

interface PagedRequestGridPayload {
  items?: RequestOptionItem[]
  requests?: RequestOptionItem[]
  totalCount?: number
}

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const WORK_PACKAGE_STATUSES = ['DRAFT', 'ACTIVE', 'CLOSED'] as const

const route = useRoute()
const router = useRouter()

const workPackages = ref<WorkPackageItem[]>([])
const activePersons = ref<ActivePersonOption[]>([])
const activeCustomers = ref<ActiveCustomerOption[]>([])
const activeProducts = ref<ActiveProductOption[]>([])
const availableRequests = ref<RequestOptionItem[]>([])

const selectedWorkPackage = ref<WorkPackageItem | null>(null)
const scopeItems = ref<WorkPackageScopeItem[]>([])

const isLoadingList = ref(false)
const isLoadingDetail = ref(false)
const isLoadingScope = ref(false)
const isSubmittingCreate = ref(false)
const isSubmittingAction = ref(false)
const removingRequestId = ref<string | null>(null)

const errorMessage = ref<string | null>(null)
const warningMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)

// Operational Telemetry state (CR-025 P4-S07)
const telemetryByPackageId = ref<Record<string, WorkPackageTelemetryDto>>({})
const isLoadingTelemetry = ref(false)
const viewMode = ref<'table' | 'cards'>('table')

// Decision Workbench & Simulation state (CR-025 P4-S08, Architecture §4 TD-007)
const portfolioOrgThroughput = ref<number>(1.0)
const simulatedDeadline = ref<string>('')
const descopedRequestIds = ref<string[]>([])

// Filter state for GET /api/v1/work-packages
const filters = reactive({
  status: '',
  ownerPersonId: '',
  customerId: '',
  productId: '',
})

// Create Work Package modal/form state
const showCreateForm = ref(false)
const createForm = reactive({
  name: '',
  objective: '',
  ownerPersonId: '',
  customerId: '',
  productId: '',
  deadline: '',
})
// Quick capture candidate tasks for create modal (CR-019, CR-028)
const createRawTasks = ref('')
const createCandidateTasks = ref<CandidateTaskItem[]>([])

// Detail Panel forms
const objectiveForm = reactive({
  name: '',
  objective: '',
})

const deadlineForm = reactive({
  deadline: '',
})

const contextForm = reactive({
  customerId: '',
  productId: '',
})

const assignOwnerForm = reactive({
  newOwnerPersonId: '',
})

const closePackageForm = reactive({
  reason: '',
})

const addRequestForm = reactive({
  selectedRequestId: '',
  manualRequestId: '',
})

// Scope bulk quick add state (CR-019, CR-028)
const scopeAddMode = ref<'existing' | 'quick'>('existing')
const scopeRawTasks = ref('')
const scopeCandidateTasks = ref<CandidateTaskItem[]>([])
const isSubmittingScopeBulk = ref(false)

const routeWorkPackageId = computed(() => String(route.params.id ?? '').trim())

const personNameById = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const person of activePersons.value) {
    const label = person.fullName || `${person.firstName} ${person.lastName}`.trim()
    map[person.id] = label
  }
  return map
})

const customerNameById = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const customer of activeCustomers.value) {
    const name = customer.customerName || customer.name
    const code = customer.customerCode || customer.code
    map[customer.id] = code ? `${name} (${code})` : name
  }
  return map
})

const productNameById = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const product of activeProducts.value) {
    const name = product.name || product.productName || ''
    const code = product.code || product.productCode || ''
    map[product.id] = code ? `${name} (${code})` : name
  }
  return map
})

const hasActiveFilters = computed(
  () =>
    filters.status.trim().length > 0 ||
    filters.ownerPersonId.trim().length > 0 ||
    filters.customerId.trim().length > 0 ||
    filters.productId.trim().length > 0,
)

const isCreateDisabled = computed(
  () =>
    isSubmittingCreate.value ||
    createForm.objective.trim().length === 0 ||
    createForm.ownerPersonId.trim().length === 0,
)

const selectedStatus = computed(() => (selectedWorkPackage.value?.status ?? '').toUpperCase())
const isSelectedDraft = computed(() => selectedStatus.value === 'DRAFT')
const isSelectedActive = computed(() => selectedStatus.value === 'ACTIVE')
const isSelectedClosed = computed(() => selectedStatus.value === 'CLOSED')

// Conditional lifecycle visibility per completion criteria
const canActivate = computed(() => isSelectedDraft.value)
const canClose = computed(() => isSelectedDraft.value || isSelectedActive.value)
const canModifyPackage = computed(() => !isSelectedClosed.value)

const isContextDirty = computed(() => {
  if (!selectedWorkPackage.value) return false
  const currentCust = selectedWorkPackage.value.customerId ?? ''
  const currentProd = selectedWorkPackage.value.productId ?? ''
  return contextForm.customerId !== currentCust || contextForm.productId !== currentProd
})

const activeScopeItems = computed(() =>
  scopeItems.value.filter((item) => item.isActive && !item.removedAt),
)

const historicalScopeItems = computed(() =>
  scopeItems.value.filter((item) => !item.isActive || Boolean(item.removedAt)),
)

const resolvedRequestIdToAdd = computed(() =>
  (addRequestForm.manualRequestId.trim() || addRequestForm.selectedRequestId.trim()),
)

const selectableRequests = computed(() => {
  const activeIds = new Set(activeScopeItems.value.map((s) => s.requestId.toLowerCase()))
  return availableRequests.value.filter((r) => !activeIds.has(r.id.toLowerCase()))
})

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
    case 'DRAFT':
      return 'bg-slate-800 text-slate-400 border border-slate-700'
    case 'ACTIVE':
      return 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30'
    case 'CLOSED':
      return 'bg-slate-950 text-slate-500 border border-slate-800'
    default:
      return 'bg-slate-800 text-slate-400 border border-slate-700'
  }
}

function requestStatusBadgeClass(status: string | null | undefined): string {
  switch ((status ?? '').toUpperCase()) {
    case 'CAPTURED':
      return 'bg-slate-800 text-slate-400 border border-slate-700'
    case 'EVALUATING':
    case 'ASSIGNED':
      return 'bg-cyan-500/15 text-cyan-400 border border-cyan-500/30'
    case 'ACCEPTED':
    case 'IN_PROGRESS':
      return 'bg-indigo-500/15 text-indigo-400 border border-indigo-500/30'
    case 'PAUSED':
      return 'bg-rose-500/15 text-rose-400 border border-rose-500/30'
    case 'ESCALATED':
      return 'bg-amber-500/15 text-amber-400 border border-amber-500/30'
    case 'COMPLETED':
      return 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30'
    case 'REJECTED':
      return 'bg-rose-500/15 text-rose-400 border border-rose-500/30'
    case 'CANCELLED':
      return 'bg-slate-950 text-slate-500 border border-slate-800'
    default:
      return 'bg-slate-800 text-slate-400 border border-slate-700'
  }
}

function resolveCustomerOptionLabel(customer: ActiveCustomerOption): string {
  const name = customer.customerName || customer.name
  const code = customer.customerCode || customer.code
  return code ? `${name} (${code})` : name
}

function resolveProductOptionLabel(product: ActiveProductOption): string {
  const name = product.name || product.productName || ''
  const code = product.code || product.productCode || ''
  return code ? `${name} (${code})` : name
}

function resolveOwnerDisplay(wp: WorkPackageItem): string {
  if (wp.ownerName && wp.ownerName.trim().length > 0) {
    return wp.ownerName
  }
  return personNameById.value[wp.ownerPersonId] ?? wp.ownerPersonId
}

function resolveCustomerDisplay(wp: WorkPackageItem): string {
  if (wp.customerName && wp.customerName.trim().length > 0) {
    return wp.customerCode ? `${wp.customerName} (${wp.customerCode})` : wp.customerName
  }
  if (wp.customerCode && wp.customerCode.trim().length > 0) {
    return wp.customerCode
  }
  if (wp.customerId) {
    return customerNameById.value[wp.customerId] ?? wp.customerId
  }
  return '—'
}

function resolveProductDisplay(wp: WorkPackageItem): string {
  if (wp.productName && wp.productName.trim().length > 0) {
    return wp.productCode ? `${wp.productName} (${wp.productCode})` : wp.productName
  }
  if (wp.productCode && wp.productCode.trim().length > 0) {
    return wp.productCode
  }
  if (wp.productId) {
    return productNameById.value[wp.productId] ?? wp.productId
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
    return '—'
  }
  if (value.length >= 10 && /^\d{4}-\d{2}-\d{2}/.test(value)) {
    return value.slice(0, 10)
  }
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }
  return parsed.toISOString().slice(0, 10)
}

function isWorkPackageOverdue(wp: WorkPackageItem | null | undefined): boolean {
  if (!wp?.deadline) return false
  if (wp.status === 'CLOSED') return false
  const dateStr = wp.deadline.slice(0, 10)
  const parts = dateStr.split('-').map(Number)
  const deadlineDate =
    parts.length === 3 && !parts.some(isNaN)
      ? new Date(parts[0], parts[1] - 1, parts[2], 0, 0, 0, 0)
      : new Date(wp.deadline)
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  deadlineDate.setHours(0, 0, 0, 0)
  return deadlineDate < today
}

function deriveNameFromObjective(objective: string, explicitName?: string): string {
  if (explicitName && explicitName.trim().length > 0) {
    return explicitName.trim().slice(0, 255)
  }
  return objective.trim().slice(0, 255)
}

function syncDetailForms(wp: WorkPackageItem): void {
  objectiveForm.name = wp.name ?? ''
  objectiveForm.objective = wp.objective ?? ''
  deadlineForm.deadline = wp.deadline ? wp.deadline.slice(0, 10) : ''
  contextForm.customerId = wp.customerId ?? ''
  contextForm.productId = wp.productId ?? ''
  assignOwnerForm.newOwnerPersonId = wp.ownerPersonId ?? ''
  closePackageForm.reason = wp.closedReason ?? ''
  simulatedDeadline.value = wp.deadline ? wp.deadline.slice(0, 10) : ''
  descopedRequestIds.value = []
}

async function loadLookups(): Promise<void> {
  const [personsResult, customersResult, productsResult, requestsResult] =
    await Promise.allSettled([
      httpClient.get<ActivePersonOption[]>('/organization/persons/active'),
      httpClient.get<ActiveCustomerOption[]>('/customers/active'),
      httpClient.get<ActiveProductOption[]>('/products/active'),
      httpClient.get<PagedRequestGridPayload | RequestOptionItem[]>('/requests', {
        params: { page: 1, pageSize: 100 },
      }),
    ])

  if (personsResult.status === 'fulfilled') {
    activePersons.value = personsResult.value.data
  }
  if (customersResult.status === 'fulfilled') {
    activeCustomers.value = customersResult.value.data
  }
  if (productsResult.status === 'fulfilled') {
    activeProducts.value = productsResult.value.data
  }
  if (requestsResult.status === 'fulfilled') {
    const data = requestsResult.value.data
    if (Array.isArray(data)) {
      availableRequests.value = data
    } else {
      availableRequests.value = data.items ?? data.requests ?? []
    }
  }
}

async function fetchTelemetry(): Promise<void> {
  isLoadingTelemetry.value = true
  try {
    const cockpit = await getOperationsCockpit()
    const map: Record<string, WorkPackageTelemetryDto> = {}
    if (cockpit?.packages) {
      for (const pkg of cockpit.packages) {
        map[pkg.id] = pkg
      }
    }
    telemetryByPackageId.value = map
    if (cockpit?.portfolioMetrics?.orgDailyThroughput) {
      portfolioOrgThroughput.value = cockpit.portfolioMetrics.orgDailyThroughput
    }
  } catch (err) {
    console.warn('Failed to fetch operations cockpit telemetry:', err)
  } finally {
    isLoadingTelemetry.value = false
  }
}

function getTelemetry(wpId: string): WorkPackageTelemetryDto | null {
  return telemetryByPackageId.value[wpId] ?? null
}

const selectedTelemetry = computed<WorkPackageTelemetryDto | null>(() => {
  if (!selectedWorkPackage.value) return null
  return telemetryByPackageId.value[selectedWorkPackage.value.id] ?? null
})

function getPressureBadgeClass(tier: string | undefined): string {
  switch ((tier ?? '').toUpperCase()) {
    case 'NOMINAL':
      return 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30'
    case 'ELEVATED':
      return 'bg-indigo-500/15 text-indigo-400 border border-indigo-500/30'
    case 'CRITICAL':
      return 'bg-amber-500/15 text-amber-400 border border-amber-500/30'
    case 'IMPOSSIBLE':
      return 'bg-rose-500/20 text-rose-400 border border-rose-500/30 font-bold'
    case 'UNPLANNED':
    default:
      return 'bg-slate-800 text-slate-400 border border-slate-700'
  }
}

function getScopeDemandLabel(t?: WorkPackageTelemetryDto | null): string {
  if (!t || t.pressureTier === 'UNPLANNED' || !t.deadline) {
    return '[UNPLANNED]'
  }
  const share = t.orgCapacityShare != null ? Math.round(t.orgCapacityShare) : 0
  return `[${share}% Org Output]`
}

function getWpScopeDemandBadge(wp: WorkPackageItem): { label: string; class: string; tooltip: string } {
  const t = telemetryByPackageId.value[wp.id]
  if (!t || t.pressureTier === 'UNPLANNED' || !wp.deadline) {
    return {
      label: '[UNPLANNED]',
      class: 'bg-slate-800 text-slate-400 border border-slate-700',
      tooltip: 'No target deadline assigned (Unplanned pressure)',
    }
  }

  const share = t.orgCapacityShare != null ? Math.round(t.orgCapacityShare) : 0
  const label = `[${share}% Org Output]`
  const tierClass = getPressureBadgeClass(t.pressureTier)
  const burn = t.requiredDailyBurn != null ? t.requiredDailyBurn.toFixed(1) : '0.0'
  const tooltip = `${burn} pts/day | ${share}% Org Output [${t.pressureTier}]`

  return { label, class: tierClass, tooltip }
}

function getBarClass(day: FlowBarcodeDayDto): string {
  if (day.blockedCount > 0) {
    return 'bg-rose-500'
  }
  if (day.closedCount > 0) {
    return 'bg-emerald-400'
  }
  if (day.stateMutationCount > 0) {
    return 'bg-cyan-400'
  }
  return 'bg-slate-700'
}

function getBarHeight(day: FlowBarcodeDayDto, isLarge = false): string {
  if (isLarge) {
    if (day.blockedCount > 0 || day.closedCount > 0) {
      return '26px'
    }
    if (day.stateMutationCount > 0) {
      const h = Math.min(26, 10 + day.stateMutationCount * 4)
      return `${h}px`
    }
    return '6px'
  }

  if (day.blockedCount > 0 || day.closedCount > 0) {
    return '14px'
  }
  if (day.stateMutationCount > 0) {
    const h = Math.min(14, 6 + day.stateMutationCount * 2)
    return `${h}px`
  }
  return '4px'
}

function getBarTooltip(day: FlowBarcodeDayDto): string {
  return `${day.date}: ${day.closedCount} closed, ${day.stateMutationCount} mutations, ${day.blockedCount} blocked`
}

// Decision Workbench & Simulation engine (CR-025 P4-S08, Architecture §4 TD-007)
function getRequestComplexity(item: WorkPackageScopeItem): number {
  if (typeof item.complexity === 'number' && item.complexity > 0) {
    return item.complexity
  }
  if (typeof item.request?.complexity === 'number' && item.request.complexity > 0) {
    return item.request.complexity
  }
  return 1
}

function isRequestPausedOrBlocked(item: WorkPackageScopeItem): boolean {
  const s = (item.status || item.requestStatus || item.request?.status || '').toUpperCase()
  return s === 'PAUSED'
}

function isRequestCompleted(item: WorkPackageScopeItem): boolean {
  const s = (item.status || item.requestStatus || item.request?.status || '').toUpperCase()
  return s === 'COMPLETED' || s === 'CANCELLED'
}

function getRequestStateAgingDays(item: WorkPackageScopeItem): number {
  const dateStr =
    item.updatedAt ||
    item.request?.updatedAt ||
    item.createdAt ||
    item.request?.createdAt
  if (!dateStr) return 0
  const parsed = new Date(dateStr)
  if (Number.isNaN(parsed.getTime())) return 0
  const diffMs = Math.max(0, Date.now() - parsed.getTime())
  return Math.floor(diffMs / (1000 * 60 * 60 * 24))
}

function getRequestBlockerNote(item: WorkPackageScopeItem): string {
  if (item.blockerNote && item.blockerNote.trim().length > 0) {
    return item.blockerNote.trim()
  }
  if (item.request?.assignments && item.request.assignments.length > 0) {
    const paused = [...item.request.assignments]
      .reverse()
      .find((a) => (a.newStatus || '').toUpperCase() === 'PAUSED')
    if (paused?.notes && paused.notes.trim().length > 0) {
      return paused.notes.trim()
    }
  }
  if (item.request?.evaluationNotes && item.request.evaluationNotes.trim().length > 0) {
    return item.request.evaluationNotes.trim()
  }
  return 'Work paused on request'
}

function computeWorkingDays(startDate: Date, targetDate: Date): number {
  const start = new Date(Date.UTC(startDate.getUTCFullYear(), startDate.getUTCMonth(), startDate.getUTCDate()))
  const target = new Date(Date.UTC(targetDate.getUTCFullYear(), targetDate.getUTCMonth(), targetDate.getUTCDate()))
  if (target <= start) {
    return 0
  }
  let workingDays = 0
  const current = new Date(start)
  current.setUTCDate(current.getUTCDate() + 1)
  while (current <= target) {
    const day = current.getUTCDay()
    if (day !== 0 && day !== 6) {
      workingDays++
    }
    current.setUTCDate(current.getUTCDate() + 1)
  }
  return workingDays
}

function determinePressureTier(orgCapacityShare: number | null, hasDeadline: boolean): string {
  if (!hasDeadline || orgCapacityShare === null) {
    return 'UNPLANNED'
  }
  if (orgCapacityShare < 20.0) {
    return 'NOMINAL'
  }
  if (orgCapacityShare <= 50.0) {
    return 'ELEVATED'
  }
  if (orgCapacityShare <= 100.0) {
    return 'CRITICAL'
  }
  return 'IMPOSSIBLE'
}

function isDescoped(requestId: string): boolean {
  return descopedRequestIds.value.includes(requestId)
}

function toggleDescope(requestId: string): void {
  if (descopedRequestIds.value.includes(requestId)) {
    descopedRequestIds.value = descopedRequestIds.value.filter((id) => id !== requestId)
  } else {
    descopedRequestIds.value = [...descopedRequestIds.value, requestId]
  }
}

function resetSimulation(): void {
  descopedRequestIds.value = []
  simulatedDeadline.value = selectedWorkPackage.value?.deadline
    ? selectedWorkPackage.value.deadline.slice(0, 10)
    : ''
}

const uncompletedActiveScopeItems = computed(() =>
  activeScopeItems.value.filter((item) => !isRequestCompleted(item)),
)

const baselineRemainingComplexity = computed(() => {
  if (selectedTelemetry.value?.remainingComplexity != null) {
    return selectedTelemetry.value.remainingComplexity
  }
  return uncompletedActiveScopeItems.value.reduce(
    (sum, item) => sum + getRequestComplexity(item),
    0,
  )
})

const descopedComplexity = computed(() => {
  const descopedSet = new Set(descopedRequestIds.value)
  return uncompletedActiveScopeItems.value
    .filter((item) => descopedSet.has(item.requestId))
    .reduce((sum, item) => sum + getRequestComplexity(item), 0)
})

const simulatedRemainingComplexity = computed(() =>
  Math.max(0, baselineRemainingComplexity.value - descopedComplexity.value),
)

const simulatedWorkingDaysRemaining = computed<number | null>(() => {
  if (!simulatedDeadline.value) {
    return null
  }
  const parts = simulatedDeadline.value.split('-').map(Number)
  if (parts.length !== 3 || parts.some(isNaN)) {
    return null
  }
  const targetDate = new Date(Date.UTC(parts[0], parts[1] - 1, parts[2]))
  return computeWorkingDays(new Date(), targetDate)
})

const simulatedRequiredDailyBurn = computed<number | null>(() => {
  if (!simulatedDeadline.value) {
    return null
  }
  const days = simulatedWorkingDaysRemaining.value ?? 0
  if (days <= 0) {
    return simulatedRemainingComplexity.value
  }
  return Math.round((simulatedRemainingComplexity.value / days) * 100) / 100
})

const simulatedOrgCapacityShare = computed<number | null>(() => {
  if (simulatedRequiredDailyBurn.value == null) {
    return null
  }
  const effectiveCOrg = portfolioOrgThroughput.value > 0 ? portfolioOrgThroughput.value : 1.0
  return Math.round((simulatedRequiredDailyBurn.value / effectiveCOrg) * 1000) / 10
})

const simulatedPressureTier = computed<string>(() =>
  determinePressureTier(simulatedOrgCapacityShare.value, Boolean(simulatedDeadline.value)),
)

const isSimulationActive = computed(() => {
  const currentDeadline = selectedWorkPackage.value?.deadline
    ? selectedWorkPackage.value.deadline.slice(0, 10)
    : ''
  return descopedRequestIds.value.length > 0 || simulatedDeadline.value !== currentDeadline
})

const simulatedComparisonText = computed(() => {
  const ptsText = `${simulatedRemainingComplexity.value} pts`
  const descopeText = descopedComplexity.value > 0 ? ` (-${descopedComplexity.value} pts descope)` : ''
  const burnText = simulatedRequiredDailyBurn.value != null ? `${simulatedRequiredDailyBurn.value.toFixed(1)} pts/day` : '— pts/day'
  const shareText = simulatedOrgCapacityShare.value != null ? `${Math.round(simulatedOrgCapacityShare.value)}%` : '—%'
  const tierText = simulatedPressureTier.value
  return `Simulated: ${ptsText}${descopeText} | ${burnText} -> ${shareText} Org Output [${tierText}]`
})

async function loadWorkPackages(): Promise<void> {
  isLoadingList.value = true
  errorMessage.value = null

  try {
    const params: Record<string, string> = {}
    if (filters.status.trim().length > 0) {
      params.status = filters.status.trim()
    }
    if (filters.ownerPersonId.trim().length > 0) {
      params.ownerPersonId = filters.ownerPersonId.trim()
    }
    if (filters.customerId.trim().length > 0) {
      params.customerId = filters.customerId.trim()
    }
    if (filters.productId.trim().length > 0) {
      params.productId = filters.productId.trim()
    }

    const [packagesResponse] = await Promise.all([
      httpClient.get<WorkPackageItem[]>('/work-packages', { params }),
      fetchTelemetry(),
    ])
    workPackages.value = packagesResponse.data
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load work packages.')
  } finally {
    isLoadingList.value = false
  }
}

async function loadWorkPackageScope(workPackageId: string): Promise<void> {
  if (!workPackageId) {
    scopeItems.value = []
    return
  }

  isLoadingScope.value = true
  try {
    const response = await httpClient.get<WorkPackageScopeItem[]>(
      `/work-packages/${workPackageId}/scope`,
    )
    scopeItems.value = response.data
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load work package scope.')
  } finally {
    isLoadingScope.value = false
  }
}

async function loadWorkPackageDetail(workPackageId: string): Promise<void> {
  if (!workPackageId) {
    selectedWorkPackage.value = null
    scopeItems.value = []
    return
  }

  isLoadingDetail.value = true
  errorMessage.value = null

  try {
    const [detailResponse, scopeResponse] = await Promise.all([
      httpClient.get<WorkPackageItem>(`/work-packages/${workPackageId}`),
      httpClient.get<WorkPackageScopeItem[]>(`/work-packages/${workPackageId}/scope`),
    ])

    selectedWorkPackage.value = detailResponse.data
    scopeItems.value = scopeResponse.data
    syncDetailForms(detailResponse.data)
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load work package details.')
  } finally {
    isLoadingDetail.value = false
  }
}

async function handleFilterChange(): Promise<void> {
  await loadWorkPackages()
}

async function handleResetFilters(): Promise<void> {
  filters.status = ''
  filters.ownerPersonId = ''
  filters.customerId = ''
  filters.productId = ''
  await loadWorkPackages()
}

function handleParseCreateTasks(): void {
  const parsed = parseTaskListTasks(createRawTasks.value)
  if (parsed.length > 0) {
    createCandidateTasks.value = [...createCandidateTasks.value, ...parsed]
    createRawTasks.value = ''
  }
}

function handleRemoveCreateCandidate(index: number): void {
  createCandidateTasks.value.splice(index, 1)
}

function handleClearCreateCandidates(): void {
  createCandidateTasks.value = []
  createRawTasks.value = ''
}

function openCreateModal(): void {
  errorMessage.value = null
  warningMessage.value = null
  successMessage.value = null
  createForm.name = ''
  createForm.objective = ''
  createForm.ownerPersonId = activePersons.value[0]?.id ?? ''
  createForm.customerId = ''
  createForm.productId = ''
  createForm.deadline = ''
  createRawTasks.value = ''
  createCandidateTasks.value = []
  showCreateForm.value = true
}

function closeCreateModal(): void {
  createForm.deadline = ''
  createRawTasks.value = ''
  createCandidateTasks.value = []
  showCreateForm.value = false
}

async function handleCreateWorkPackage(): Promise<void> {
  if (isCreateDisabled.value) {
    return
  }

  isSubmittingCreate.value = true
  errorMessage.value = null
  warningMessage.value = null
  successMessage.value = null

  const trimmedObjective = createForm.objective.trim()
  const resolvedName = deriveNameFromObjective(trimmedObjective, createForm.name)

  try {
    const response = await httpClient.post<WorkPackageItem>('/work-packages', {
      name: resolvedName,
      objective: trimmedObjective,
      ownerPersonId: createForm.ownerPersonId,
      customerId: createForm.customerId.trim() || null,
      productId: createForm.productId.trim() || null,
      deadline: createForm.deadline ? createForm.deadline : null,
    })

    const created = response.data

    const tasksToRecord: CandidateTaskItem[] = [...createCandidateTasks.value]
    if (tasksToRecord.length === 0 && createRawTasks.value.trim().length > 0) {
      tasksToRecord.push(...parseTaskListTasks(createRawTasks.value))
    }

    const unrecordedTitles: string[] = []
    let recordedCount = 0

    if (tasksToRecord.length > 0) {
      const results = await Promise.allSettled(
        tasksToRecord.map((task) =>
          recordRequest({
            title: task.title,
            complexity: task.complexity,
            description: '',
            customerId: createForm.customerId.trim() || null,
            productId: createForm.productId.trim() || null,
            workPackageId: created.id,
            requestType: 'GENERAL',
            priority: 'NORMAL',
          }),
        ),
      )

      results.forEach((res, idx) => {
        if (res.status === 'fulfilled') {
          recordedCount++
        } else {
          unrecordedTitles.push(tasksToRecord[idx].title)
        }
      })
    }

    showCreateForm.value = false
    createForm.deadline = ''
    createRawTasks.value = ''
    createCandidateTasks.value = []

    if (unrecordedTitles.length > 0) {
      warningMessage.value = `Work package "${created.objective || created.name}" created, but ${unrecordedTitles.length} task(s) failed to record: "${unrecordedTitles.join('", "')}". You can re-try adding them in Scope Management.`
    } else if (recordedCount > 0) {
      successMessage.value = `Work Package "${created.objective || created.name}" created with ${recordedCount} task(s) in ${created.status} status.`
    } else {
      successMessage.value = `Work Package "${created.objective || created.name}" created in ${created.status} status.`
    }

    await loadWorkPackages()
    await selectWorkPackage(created.id)
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to create work package.')
  } finally {
    isSubmittingCreate.value = false
  }
}

async function selectWorkPackage(workPackageId: string): Promise<void> {
  if (!workPackageId) {
    return
  }

  if (routeWorkPackageId.value !== workPackageId) {
    await router.push(`/work-packages/${workPackageId}`)
  } else {
    await loadWorkPackageDetail(workPackageId)
  }
}

async function closeDetailPanel(): Promise<void> {
  selectedWorkPackage.value = null
  scopeItems.value = []
  resetScopeBulkState()
  resetSimulation()
  if (routeWorkPackageId.value) {
    await router.push('/work-packages')
  }
}

async function handleUpdateObjective(): Promise<void> {
  if (!selectedWorkPackage.value || !canModifyPackage.value) {
    return
  }

  const trimmedObjective = objectiveForm.objective.trim()
  if (!trimmedObjective) {
    errorMessage.value = 'Work package objective is required.'
    return
  }

  const resolvedName = deriveNameFromObjective(
    trimmedObjective,
    objectiveForm.name || selectedWorkPackage.value.name,
  )

  isSubmittingAction.value = true
  errorMessage.value = null
  successMessage.value = null

  try {
    const wpId = selectedWorkPackage.value.id
    const response = await httpClient.put<WorkPackageItem>(`/work-packages/${wpId}/objective`, {
      name: resolvedName,
      objective: trimmedObjective,
    })

    selectedWorkPackage.value = response.data
    syncDetailForms(response.data)
    successMessage.value = 'Work package objective updated successfully.'
    await loadWorkPackages()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to update work package objective.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleUpdateDeadline(newDeadline?: string | null): Promise<void> {
  if (!selectedWorkPackage.value || !canModifyPackage.value) {
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  successMessage.value = null

  const targetDeadline =
    newDeadline !== undefined
      ? newDeadline
      : deadlineForm.deadline
        ? deadlineForm.deadline
        : null

  try {
    const wpId = selectedWorkPackage.value.id
    const updated = await updateWorkPackageDeadline(wpId, {
      deadline: targetDeadline,
    })

    selectedWorkPackage.value = updated
    syncDetailForms(updated)
    successMessage.value = targetDeadline
      ? 'Work package deadline updated successfully.'
      : 'Work package deadline cleared successfully.'
    await loadWorkPackages()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to update work package deadline.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleClearDeadline(): Promise<void> {
  deadlineForm.deadline = ''
  await handleUpdateDeadline(null)
}

async function handleUpdateContext(): Promise<void> {
  if (!selectedWorkPackage.value || !canModifyPackage.value) {
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  successMessage.value = null

  try {
    const wpId = selectedWorkPackage.value.id
    const updated = await updateWorkPackageContext(wpId, {
      customerId: contextForm.customerId || null,
      productId: contextForm.productId || null,
    })

    selectedWorkPackage.value = updated
    const index = workPackages.value.findIndex((wp) => wp.id === updated.id)
    if (index !== -1) {
      workPackages.value[index] = { ...workPackages.value[index], ...updated }
    }
    syncDetailForms(updated)
    successMessage.value = 'Work package customer and product context updated successfully.'
    await loadWorkPackages()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to update work package customer and product context.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleAssignOwner(): Promise<void> {
  if (!selectedWorkPackage.value || !canModifyPackage.value) {
    return
  }

  const targetOwnerId = assignOwnerForm.newOwnerPersonId.trim()
  if (!targetOwnerId) {
    errorMessage.value = 'Please select an active person as the Work Package Owner.'
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  successMessage.value = null

  try {
    const wpId = selectedWorkPackage.value.id
    const response = await httpClient.post<WorkPackageItem>(
      `/work-packages/${wpId}/assign-owner`,
      {
        newOwnerPersonId: targetOwnerId,
        ownerPersonId: targetOwnerId,
      },
    )

    selectedWorkPackage.value = response.data
    syncDetailForms(response.data)
    successMessage.value = 'Work package owner assigned successfully.'
    await loadWorkPackages()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to assign work package owner.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleActivateWorkPackage(): Promise<void> {
  if (!selectedWorkPackage.value || !canActivate.value) {
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  successMessage.value = null

  try {
    const wpId = selectedWorkPackage.value.id
    const response = await httpClient.post<WorkPackageItem>(`/work-packages/${wpId}/activate`)

    selectedWorkPackage.value = response.data
    syncDetailForms(response.data)
    successMessage.value = 'Work package activated (status is now ACTIVE).'
    await loadWorkPackages()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to activate work package.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleCloseWorkPackage(): Promise<void> {
  if (!selectedWorkPackage.value || !canClose.value) {
    return
  }

  const reason = closePackageForm.reason.trim() || 'Completed work package objective.'

  isSubmittingAction.value = true
  errorMessage.value = null
  successMessage.value = null

  try {
    const wpId = selectedWorkPackage.value.id
    const response = await httpClient.post<WorkPackageItem>(`/work-packages/${wpId}/close`, {
      reason,
    })

    selectedWorkPackage.value = response.data
    syncDetailForms(response.data)
    successMessage.value = 'Work package closed (status is now CLOSED).'
    await loadWorkPackages()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to close work package.')
  } finally {
    isSubmittingAction.value = false
  }
}

async function handleAddRequestToScope(): Promise<void> {
  if (!selectedWorkPackage.value || !canModifyPackage.value) {
    return
  }

  const requestId = resolvedRequestIdToAdd.value
  if (!requestId) {
    errorMessage.value = 'Please select or enter a Request ID to add to the work package scope.'
    return
  }

  isSubmittingAction.value = true
  errorMessage.value = null
  successMessage.value = null

  try {
    const wpId = selectedWorkPackage.value.id
    const response = await httpClient.post<WorkPackageItem>(`/work-packages/${wpId}/requests`, {
      requestId,
    })

    selectedWorkPackage.value = response.data
    addRequestForm.selectedRequestId = ''
    addRequestForm.manualRequestId = ''
    successMessage.value = 'Request added to work package scope.'

    await Promise.all([loadWorkPackageScope(wpId), loadWorkPackages()])
  } catch (err) {
    errorMessage.value = extractErrorMessage(
      err,
      'Failed to add request to work package scope.',
    )
  } finally {
    isSubmittingAction.value = false
  }
}

function handleParseScopeTasks(): void {
  const parsed = parseTaskListTasks(scopeRawTasks.value)
  if (parsed.length > 0) {
    scopeCandidateTasks.value = [...scopeCandidateTasks.value, ...parsed]
    scopeRawTasks.value = ''
  }
}

function handleRemoveScopeCandidate(index: number): void {
  scopeCandidateTasks.value.splice(index, 1)
}

function handleClearScopeCandidates(): void {
  scopeCandidateTasks.value = []
  scopeRawTasks.value = ''
}

function resetScopeBulkState(): void {
  scopeRawTasks.value = ''
  scopeCandidateTasks.value = []
  scopeAddMode.value = 'existing'
}

async function handleBulkAddTasksToScope(): Promise<void> {
  if (!selectedWorkPackage.value || !canModifyPackage.value) {
    return
  }

  const wp = selectedWorkPackage.value
  const tasksToAdd: CandidateTaskItem[] = [...scopeCandidateTasks.value]
  if (tasksToAdd.length === 0 && scopeRawTasks.value.trim().length > 0) {
    tasksToAdd.push(...parseTaskListTasks(scopeRawTasks.value))
  }

  if (tasksToAdd.length === 0) {
    errorMessage.value = 'Please enter or parse at least one task title to add.'
    return
  }

  isSubmittingScopeBulk.value = true
  errorMessage.value = null
  warningMessage.value = null
  successMessage.value = null

  try {
    const results = await Promise.allSettled(
      tasksToAdd.map((task) =>
        recordRequest({
          title: task.title,
          complexity: task.complexity,
          description: '',
          customerId: wp.customerId || null,
          productId: wp.productId || null,
          workPackageId: wp.id,
          requestType: 'GENERAL',
          priority: 'NORMAL',
        }),
      ),
    )

    const unrecordedTasks: CandidateTaskItem[] = []
    let recordedCount = 0

    results.forEach((res, idx) => {
      if (res.status === 'fulfilled') {
        recordedCount++
      } else {
        unrecordedTasks.push(tasksToAdd[idx])
      }
    })

    if (unrecordedTasks.length > 0) {
      scopeCandidateTasks.value = unrecordedTasks
      scopeRawTasks.value = ''
      const failedTitles = unrecordedTasks.map((t) => t.title)
      warningMessage.value = `${recordedCount} task(s) added to scope, but ${unrecordedTasks.length} failed: "${failedTitles.join('", "')}".`
    } else {
      scopeCandidateTasks.value = []
      scopeRawTasks.value = ''
      successMessage.value = `Successfully added ${recordedCount} task(s) to scope.`
    }

    await Promise.all([loadWorkPackageScope(wp.id), loadWorkPackages()])
  } catch (err) {
    errorMessage.value = extractErrorMessage(
      err,
      'Failed to add tasks to work package scope.',
    )
  } finally {
    isSubmittingScopeBulk.value = false
  }
}

async function handleRemoveRequestFromScope(requestId: string): Promise<void> {
  if (!selectedWorkPackage.value || !canModifyPackage.value || !requestId) {
    return
  }

  removingRequestId.value = requestId
  errorMessage.value = null
  successMessage.value = null

  try {
    const wpId = selectedWorkPackage.value.id
    const response = await httpClient.delete<WorkPackageItem>(
      `/work-packages/${wpId}/requests/${requestId}`,
    )

    selectedWorkPackage.value = response.data
    successMessage.value = 'Request removed from work package scope.'

    await Promise.all([loadWorkPackageScope(wpId), loadWorkPackages()])
  } catch (err) {
    errorMessage.value = extractErrorMessage(
      err,
      'Failed to remove request from work package scope.',
    )
  } finally {
    removingRequestId.value = null
  }
}

// Drag and drop state and event handlers for active scope requests (CR-015)
const draggedIndex = ref<number | null>(null)
const dropTargetIndex = ref<number | null>(null)
const isReordering = ref(false)

function onDragStart(event: DragEvent, index: number): void {
  if (!canModifyPackage.value || isReordering.value) {
    event.preventDefault()
    return
  }
  draggedIndex.value = index
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move'
    event.dataTransfer.setData('text/plain', String(index))
  }
}

function onDragOver(event: DragEvent, index: number): void {
  if (!canModifyPackage.value || draggedIndex.value === null || isReordering.value) {
    return
  }
  event.preventDefault()
  if (event.dataTransfer) {
    event.dataTransfer.dropEffect = 'move'
  }
  dropTargetIndex.value = index
}

function onDragLeave(_event: DragEvent, index: number): void {
  if (dropTargetIndex.value === index) {
    dropTargetIndex.value = null
  }
}

function onDragEnd(): void {
  draggedIndex.value = null
  dropTargetIndex.value = null
}

async function onDrop(event: DragEvent, targetIndex: number): Promise<void> {
  if (!canModifyPackage.value || draggedIndex.value === null || isReordering.value) {
    return
  }
  event.preventDefault()

  const fromIndex = draggedIndex.value
  const toIndex = targetIndex

  draggedIndex.value = null
  dropTargetIndex.value = null

  if (fromIndex === toIndex) {
    return
  }

  const wpId = selectedWorkPackage.value?.id
  if (!wpId) {
    return
  }

  // Snapshot previous scope items for rollback on error
  const previousScopeItems = [...scopeItems.value]

  // Optimistically reorder active items
  const activeItems = [...activeScopeItems.value]
  const [movedItem] = activeItems.splice(fromIndex, 1)
  activeItems.splice(toIndex, 0, movedItem)

  // Update sortOrder on active items
  activeItems.forEach((item, idx) => {
    item.sortOrder = idx
  })

  // Combine with inactive/historical items
  const inactiveItems = scopeItems.value.filter((item) => !item.isActive || Boolean(item.removedAt))
  scopeItems.value = [...activeItems, ...inactiveItems]

  const orderedRequestIds = activeItems.map((item) => item.requestId)

  isReordering.value = true
  errorMessage.value = null

  try {
    await reorderWorkPackageRequests(wpId, orderedRequestIds)
  } catch (err) {
    // Rollback to prior snapshot
    scopeItems.value = previousScopeItems
    errorMessage.value = extractErrorMessage(err, 'Failed to reorder requests.')
  } finally {
    isReordering.value = false
  }
}

watch(
  routeWorkPackageId,
  async (newId) => {
    resetScopeBulkState()
    resetSimulation()
    if (newId) {
      await loadWorkPackageDetail(newId)
    } else {
      selectedWorkPackage.value = null
      scopeItems.value = []
    }
  },
)

onMounted(async () => {
  await Promise.all([loadLookups(), loadWorkPackages()])
  if (routeWorkPackageId.value) {
    await loadWorkPackageDetail(routeWorkPackageId.value)
  }
})
</script>

<template>
  <section class="work-package-view space-y-4" data-screen-id="SCR-WP-001">
    <!-- Screen Header -->
    <div class="op-screen-header p-4 bg-slate-900/90 border border-slate-800 rounded-xl shadow-md flex flex-wrap items-center justify-between gap-3 text-slate-100">
      <div class="flex items-center gap-2.5 flex-wrap">
        <h1 class="text-lg font-bold text-white tracking-tight flex items-center gap-2 m-0">Work Packages</h1>
        <span class="px-2 py-0.5 text-xs font-mono font-semibold rounded bg-slate-950/80 text-cyan-400 border border-slate-800" style="font-size: 11px">SCR-WP-001</span>
        <span class="text-slate-400 text-xs hidden md:inline">| Grouped operational demand &amp; lifecycle orchestration</span>
      </div>

      <div class="flex items-center gap-2">
        <button
          type="button"
          class="px-3 py-1.5 text-xs font-semibold rounded-lg border border-slate-700 bg-slate-800/80 hover:bg-slate-700 text-slate-200 transition inline-flex items-center gap-1.5 shadow-sm"
          :disabled="isLoadingList"
          data-testid="refresh-work-packages-button"
          @click="loadWorkPackages"
        >
          <i class="bi bi-arrow-clockwise text-cyan-400" aria-hidden="true"></i>
          <span>Refresh</span>
        </button>

        <button
          type="button"
          class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white transition inline-flex items-center gap-1.5 shadow-md shadow-cyan-500/20"
          data-testid="create-work-package-button"
          @click="openCreateModal"
        >
          <i class="bi bi-plus-lg" aria-hidden="true"></i>
          <span>New Package</span>
        </button>
      </div>
    </div>

    <!-- Feedback Alerts -->
    <div
      v-if="errorMessage"
      role="alert"
      class="bg-rose-950/40 border border-rose-500/30 text-rose-300 rounded-xl p-3 text-xs flex items-center justify-between shadow-lg"
      data-testid="work-package-error-alert"
    >
      <div class="flex items-center gap-2">
        <i class="bi bi-exclamation-triangle-fill text-rose-400 text-base flex-shrink-0" aria-hidden="true"></i>
        <span>{{ errorMessage }}</span>
      </div>
      <button
        type="button"
        class="text-rose-400 hover:text-rose-200 p-1"
        aria-label="Close"
        @click="errorMessage = null"
      >
        <i class="bi bi-x-lg"></i>
      </button>
    </div>

    <div
      v-if="warningMessage"
      role="alert"
      class="bg-amber-950/40 border border-amber-500/30 text-amber-300 rounded-xl p-3 text-xs flex items-center justify-between shadow-lg"
      data-testid="work-package-warning-alert"
    >
      <div class="flex items-center gap-2">
        <i class="bi bi-exclamation-circle-fill text-amber-400 text-base flex-shrink-0" aria-hidden="true"></i>
        <span>{{ warningMessage }}</span>
      </div>
      <button
        type="button"
        class="text-amber-400 hover:text-amber-200 p-1"
        aria-label="Close"
        @click="warningMessage = null"
      >
        <i class="bi bi-x-lg"></i>
      </button>
    </div>

    <div
      v-if="successMessage"
      role="status"
      class="bg-emerald-950/40 border border-emerald-500/30 text-emerald-300 rounded-xl p-3 text-xs flex items-center justify-between shadow-lg"
      data-testid="work-package-success-alert"
    >
      <div class="flex items-center gap-2">
        <i class="bi bi-check-circle-fill text-emerald-400 text-base flex-shrink-0" aria-hidden="true"></i>
        <span>{{ successMessage }}</span>
      </div>
      <button
        type="button"
        class="text-emerald-400 hover:text-emerald-200 p-1"
        aria-label="Close"
        @click="successMessage = null"
      >
        <i class="bi bi-x-lg"></i>
      </button>
    </div>

    <!-- Create Work Package Form Card (Collapsible) -->
    <div
      v-if="showCreateForm"
      class="bg-slate-900/95 border border-slate-800 rounded-xl shadow-2xl overflow-hidden text-slate-100 mb-3"
      data-testid="create-work-package-modal"
    >
      <div class="bg-slate-950/80 border-b border-slate-800 px-4 py-3 flex justify-between items-center text-cyan-300 font-bold text-xs uppercase tracking-wider">
        <span class="flex items-center gap-1.5">
          <i class="bi bi-kanban text-cyan-400" aria-hidden="true"></i>Create New Work Package
        </span>
        <button
          type="button"
          class="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-slate-800 transition"
          aria-label="Close"
          @click="closeCreateModal"
        >
          <i class="bi bi-x-lg text-xs"></i>
        </button>
      </div>

      <div class="p-4">
        <form
          novalidate
          data-testid="create-work-package-form"
          @submit.prevent="handleCreateWorkPackage"
        >
          <div class="grid grid-cols-1 md:grid-cols-12 gap-3">
            <!-- Objective (Required) -->
            <div class="col-span-12 md:col-span-8">
              <label for="createWorkPackageObjective" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 mb-1 block">
                Objective <span class="text-rose-400">*</span>
              </label>
              <input
                id="createWorkPackageObjective"
                v-model="createForm.objective"
                type="text"
                class="bg-slate-950 border border-slate-700 text-slate-100 placeholder-slate-500 rounded-lg px-3 py-1.5 text-xs focus:border-cyan-400 focus:outline-none w-full"
                placeholder="Describe shared operational objective..."
                required
                :disabled="isSubmittingCreate"
                data-testid="create-work-package-objective-input"
              />
            </div>

            <!-- Optional Short Name / Title -->
            <div class="col-span-12 md:col-span-4">
              <label for="createWorkPackageName" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 mb-1 block">
                Package Name / Title
              </label>
              <input
                id="createWorkPackageName"
                v-model="createForm.name"
                type="text"
                class="bg-slate-950 border border-slate-700 text-slate-100 placeholder-slate-500 rounded-lg px-3 py-1.5 text-xs focus:border-cyan-400 focus:outline-none w-full"
                placeholder="e.g. Auth Service Refactoring"
                :disabled="isSubmittingCreate"
                data-testid="create-work-package-name-input"
              />
            </div>

            <!-- Owner Select (Required) -->
            <div class="col-span-12 md:col-span-3">
              <label for="createWorkPackageOwner" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 mb-1 block">
                Owner <span class="text-rose-400">*</span>
              </label>
              <select
                id="createWorkPackageOwner"
                v-model="createForm.ownerPersonId"
                class="bg-slate-950 border border-slate-700 text-slate-100 rounded-lg px-2.5 py-1.5 text-xs focus:border-cyan-400 focus:outline-none w-full"
                required
                :disabled="isSubmittingCreate"
                data-testid="create-work-package-owner-select"
              >
                <option value="" disabled>Select owner...</option>
                <option
                  v-for="person in activePersons"
                  :key="person.id"
                  :value="person.id"
                >
                  {{ person.fullName }}
                </option>
              </select>
            </div>

            <!-- Customer Select (Optional) -->
            <div class="col-span-12 md:col-span-3">
              <label for="createWorkPackageCustomer" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 mb-1 block">
                Customer (Optional)
              </label>
              <select
                id="createWorkPackageCustomer"
                v-model="createForm.customerId"
                class="bg-slate-950 border border-slate-700 text-slate-100 rounded-lg px-2.5 py-1.5 text-xs focus:border-cyan-400 focus:outline-none w-full"
                :disabled="isSubmittingCreate"
                data-testid="create-work-package-customer-select"
              >
                <option value="">All / Internal / None</option>
                <option
                  v-for="customer in activeCustomers"
                  :key="customer.id"
                  :value="customer.id"
                >
                  {{ resolveCustomerOptionLabel(customer) }}
                </option>
              </select>
            </div>

            <!-- Product Select (Optional) -->
            <div class="col-span-12 md:col-span-3">
              <label for="createWorkPackageProduct" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 mb-1 block">
                Product (Optional)
              </label>
              <select
                id="createWorkPackageProduct"
                v-model="createForm.productId"
                class="bg-slate-950 border border-slate-700 text-slate-100 rounded-lg px-2.5 py-1.5 text-xs focus:border-cyan-400 focus:outline-none w-full"
                :disabled="isSubmittingCreate"
                data-testid="create-work-package-product-select"
              >
                <option value="">All / No specific product</option>
                <option
                  v-for="product in activeProducts"
                  :key="product.id"
                  :value="product.id"
                >
                  {{ resolveProductOptionLabel(product) }}
                </option>
              </select>
            </div>

            <!-- Target Deadline (Optional) -->
            <div class="col-span-12 md:col-span-3">
              <label for="createWorkPackageDeadline" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 mb-1 block">
                Target Deadline (Optional)
              </label>
              <input
                id="createWorkPackageDeadline"
                v-model="createForm.deadline"
                type="date"
                class="bg-slate-950 border border-slate-700 text-slate-100 rounded-lg px-2.5 py-1.5 text-xs font-mono focus:border-cyan-400 focus:outline-none w-full"
                :disabled="isSubmittingCreate"
                data-testid="create-deadline-input"
              />
            </div>
          </div>

          <!-- Quick Capture Tasks (Optional) (CR-019) -->
          <div class="mt-3 pt-3 border-t border-slate-800" data-testid="create-quick-capture-section">
            <div class="flex justify-between items-center mb-1.5">
              <label for="createQuickTasksInput" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 flex items-center gap-1 mb-0">
                <i class="bi bi-lightning-charge text-amber-400"></i>Quick Capture Tasks (Optional)
              </label>
              <div class="flex items-center gap-2">
                <span
                  v-if="createCandidateTasks.length > 0"
                  class="px-2 py-0.5 text-xs font-mono rounded bg-cyan-500/20 text-cyan-300 border border-cyan-500/30"
                  data-testid="create-candidate-count-badge"
                >
                  {{ createCandidateTasks.length }} parsed
                </span>
                <button
                  v-if="createCandidateTasks.length > 0"
                  type="button"
                  class="text-xs text-rose-400 hover:text-rose-300 bg-transparent border-0 cursor-pointer p-0"
                  :disabled="isSubmittingCreate"
                  data-testid="create-clear-candidates-button"
                  @click="handleClearCreateCandidates"
                >
                  Clear Parsed
                </button>
              </div>
            </div>

            <div class="mb-2">
              <textarea
                id="createQuickTasksInput"
                v-model="createRawTasks"
                class="bg-slate-950 border border-slate-700 text-slate-100 placeholder-slate-500 rounded-lg p-2.5 text-xs font-mono focus:border-cyan-400 focus:outline-none w-full"
                rows="3"
                placeholder="Paste or type task list (bullets, numbers, markdown checklists)&#10;- Setup database schema&#10;- Configure API endpoints&#10;- Build user interface"
                :disabled="isSubmittingCreate"
                data-testid="create-quick-tasks-textarea"
              ></textarea>
            </div>

            <div class="flex justify-between items-center mb-2">
              <span class="text-slate-400 text-[10.5px]">
                One task per line. Bullets, numbers, and checkboxes are automatically cleaned.
              </span>
              <button
                type="button"
                class="px-2.5 py-1 text-xs font-semibold rounded-lg border border-slate-700 bg-slate-800 hover:bg-slate-700 text-slate-200 transition inline-flex items-center gap-1 shadow-sm"
                :disabled="isSubmittingCreate || !createRawTasks.trim()"
                data-testid="create-parse-tasks-button"
                @click="handleParseCreateTasks"
              >
                <i class="bi bi-arrow-down-circle text-cyan-400"></i>Parse Tasks
              </button>
            </div>

            <!-- Candidate Tasks Preview List -->
            <div
              v-if="createCandidateTasks.length > 0"
              class="border border-slate-800 rounded-lg p-2 bg-slate-950/60 mb-2"
              data-testid="create-candidate-preview-container"
            >
              <div class="text-[10.5px] uppercase tracking-wider font-semibold text-slate-400 mb-1 px-1">
                Candidate Tasks to Create:
              </div>
              <ul class="divide-y divide-slate-800/60 max-h-36 overflow-y-auto m-0 p-0 list-none text-xs" data-testid="create-candidate-tasks-list">
                <li
                  v-for="(task, idx) in createCandidateTasks"
                  :key="idx"
                  class="flex justify-between items-center py-1.5 px-2 bg-transparent text-slate-200"
                  data-testid="create-candidate-task-item"
                >
                  <div class="flex items-center truncate me-2 gap-1.5">
                    <span class="px-1.5 py-0.2 rounded bg-slate-800 text-slate-400 border border-slate-700 font-mono text-[10px] shrink-0">
                      #{{ idx + 1 }}
                    </span>
                    <span
                      class="px-1.5 py-0.5 text-[10px] font-mono rounded bg-amber-500/15 text-amber-300 border border-amber-500/30 font-semibold shrink-0"
                      data-testid="create-candidate-complexity-badge"
                    >
                      {{ task.complexity }} pt{{ task.complexity > 1 ? 's' : '' }}
                    </span>
                    <span class="truncate text-xs">{{ task.title }}</span>
                  </div>
                  <button
                    type="button"
                    class="text-rose-400 hover:text-rose-300 p-0.5 bg-transparent border-0 cursor-pointer text-xs"
                    title="Remove candidate task"
                    aria-label="Remove candidate task"
                    :disabled="isSubmittingCreate"
                    data-testid="create-candidate-remove-button"
                    @click="handleRemoveCreateCandidate(idx)"
                  >
                    ✕
                  </button>
                </li>
              </ul>
            </div>
          </div>

          <div class="flex justify-end gap-2 mt-3 pt-3 border-t border-slate-800">
            <button
              type="button"
              class="px-3 py-1.5 text-xs font-semibold rounded-lg border border-slate-700 bg-slate-800 hover:bg-slate-700 text-slate-300 transition"
              :disabled="isSubmittingCreate"
              @click="closeCreateModal"
            >
              Cancel
            </button>
            <button
              type="submit"
              class="px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white transition inline-flex items-center gap-1.5 shadow-md shadow-cyan-500/20"
              :disabled="isCreateDisabled"
              data-testid="create-work-package-submit-button"
            >
              <span
                v-if="isSubmittingCreate"
                class="inline-block w-3 h-3 border-2 border-white border-t-transparent rounded-full animate-spin me-1"
                role="status"
                aria-hidden="true"
              ></span>
              Save Package
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Filter Controls Toolbar -->
    <div class="op-toolbar p-3 bg-slate-900/90 border border-slate-800 rounded-xl shadow-md text-slate-200">
      <div class="flex flex-wrap items-center gap-3 w-full">
        <!-- Status Filter -->
        <div class="flex items-center gap-1.5">
          <label for="wpStatusFilter" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 mb-0 whitespace-nowrap">Status:</label>
          <select
            id="wpStatusFilter"
            v-model="filters.status"
            class="bg-slate-950 border border-slate-700 text-slate-200 rounded-lg px-2.5 py-1 text-xs focus:border-cyan-400 focus:outline-none"
            style="width: 110px; height: 28px"
            :disabled="isLoadingList"
            data-testid="status-filter-select"
            @change="handleFilterChange"
          >
            <option value="">All</option>
            <option v-for="status in WORK_PACKAGE_STATUSES" :key="status" :value="status">
              {{ status }}
            </option>
          </select>
        </div>

        <!-- Owner Filter -->
        <div class="flex items-center gap-1.5">
          <label for="wpOwnerFilter" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 mb-0 whitespace-nowrap">Owner:</label>
          <select
            id="wpOwnerFilter"
            v-model="filters.ownerPersonId"
            class="bg-slate-950 border border-slate-700 text-slate-200 rounded-lg px-2.5 py-1 text-xs focus:border-cyan-400 focus:outline-none"
            style="max-width: 180px; height: 28px"
            :disabled="isLoadingList"
            data-testid="owner-filter-select"
            @change="handleFilterChange"
          >
            <option value="">All Owners</option>
            <option
              v-for="person in activePersons"
              :key="person.id"
              :value="person.id"
            >
              {{ person.fullName }}
            </option>
          </select>
        </div>

        <!-- Customer Filter -->
        <div class="flex items-center gap-1.5">
          <label for="wpCustomerFilter" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 mb-0 whitespace-nowrap">Customer:</label>
          <select
            id="wpCustomerFilter"
            v-model="filters.customerId"
            class="bg-slate-950 border border-slate-700 text-slate-200 rounded-lg px-2.5 py-1 text-xs focus:border-cyan-400 focus:outline-none"
            style="max-width: 180px; height: 28px"
            :disabled="isLoadingList"
            data-testid="customer-filter-select"
            @change="handleFilterChange"
          >
            <option value="">All Customers</option>
            <option
              v-for="customer in activeCustomers"
              :key="customer.id"
              :value="customer.id"
            >
              {{ resolveCustomerOptionLabel(customer) }}
            </option>
          </select>
        </div>

        <!-- Product Filter -->
        <div class="flex items-center gap-1.5">
          <label for="wpProductFilter" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 mb-0 whitespace-nowrap">Product:</label>
          <select
            id="wpProductFilter"
            v-model="filters.productId"
            class="bg-slate-950 border border-slate-700 text-slate-200 rounded-lg px-2.5 py-1 text-xs focus:border-cyan-400 focus:outline-none"
            style="max-width: 170px; height: 28px"
            :disabled="isLoadingList"
            data-testid="product-filter-select"
            @change="handleFilterChange"
          >
            <option value="">All Products</option>
            <option
              v-for="product in activeProducts"
              :key="product.id"
              :value="product.id"
            >
              {{ resolveProductOptionLabel(product) }}
            </option>
          </select>
        </div>

        <div class="flex items-center gap-2 ms-auto">
          <button
            v-if="hasActiveFilters"
            type="button"
            class="px-2.5 py-1 text-xs font-semibold rounded-lg border border-slate-700 bg-slate-800 hover:bg-slate-700 text-slate-300 transition inline-flex items-center gap-1 shadow-sm"
            style="height: 28px"
            :disabled="isLoadingList"
            data-testid="clear-filters-button"
            @click="handleResetFilters"
          >
            <i class="bi bi-x-circle text-cyan-400" aria-hidden="true"></i>Reset
          </button>

          <div class="inline-flex rounded-lg border border-slate-700 p-0.5 bg-slate-950" role="group" data-testid="wp-view-mode-toggle">
            <button
              type="button"
              class="px-2.5 py-1 text-xs rounded-md font-medium transition inline-flex items-center gap-1"
              :class="viewMode === 'table' ? 'bg-cyan-500/20 text-cyan-300 border border-cyan-500/30' : 'text-slate-400 hover:text-white border border-transparent'"
              style="height: 26px"
              data-testid="view-mode-table-button"
              @click="viewMode = 'table'"
            >
              <i class="bi bi-table me-1"></i>Table
            </button>
            <button
              type="button"
              class="px-2.5 py-1 text-xs rounded-md font-medium transition inline-flex items-center gap-1"
              :class="viewMode === 'cards' ? 'bg-cyan-500/20 text-cyan-300 border border-cyan-500/30' : 'text-slate-400 hover:text-white border border-transparent'"
              style="height: 26px"
              data-testid="view-mode-cards-button"
              @click="viewMode = 'cards'"
            >
              <i class="bi bi-grid me-1"></i>Cards
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Main Layout: Work Packages Table + Detail Panel -->
    <div class="row g-3">
      <!-- Work Packages Table Column -->
      <div :class="selectedWorkPackage || isLoadingDetail ? 'col-12 col-xl-7' : 'col-12'">
        <div class="card card-table bg-slate-900/90 border border-slate-800 rounded-xl shadow-lg overflow-hidden">
          <div class="card-body p-0">
            <!-- Table View -->
            <div v-if="viewMode === 'table'" class="table-responsive">
              <table
                class="table align-middle mb-0 text-nowrap w-full"
                data-testid="work-packages-table"
              >
                <thead class="bg-slate-950/80 border-b border-slate-800 text-slate-400 font-semibold tracking-wider uppercase text-[11px]">
                  <tr>
                    <th scope="col" class="py-3 px-3 text-start" style="width: 85px">ID</th>
                    <th scope="col" class="py-3 px-3 text-start" style="min-width: 220px">Objective &amp; Demand</th>
                    <th scope="col" class="py-3 px-3 text-start" style="width: 140px">14d Flow</th>
                    <th scope="col" class="py-3 px-3 text-start">Owner</th>
                    <th scope="col" class="py-3 px-3 text-start">Customer</th>
                    <th scope="col" class="py-3 px-3 text-start">Product</th>
                    <th scope="col" class="py-3 px-3 text-start" style="width: 110px">Deadline</th>
                    <th scope="col" class="py-3 px-3 text-center" style="width: 75px">Status</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/80">
                  <tr v-if="isLoadingList">
                    <td colspan="8" class="text-center py-6 text-slate-400 text-xs">
                      <div class="inline-block w-4 h-4 border-2 border-cyan-400 border-t-transparent rounded-full animate-spin me-2" role="status"></div>
                      Loading work packages...
                    </td>
                  </tr>

                  <tr v-else-if="workPackages.length === 0">
                    <td
                      colspan="8"
                      class="text-center py-6 text-slate-400 text-xs"
                      data-testid="empty-work-packages-row"
                    >
                      No work packages found matching the current filters.
                    </td>
                  </tr>

                  <tr
                    v-for="wp in workPackages"
                    v-else
                    :key="wp.id"
                    :data-work-package-id="wp.id"
                    class="transition cursor-pointer"
                    :class="selectedWorkPackage?.id === wp.id ? 'bg-cyan-950/30 border-l-2 border-l-cyan-400 text-slate-100' : 'hover:bg-slate-850/80 text-slate-200'"
                    data-testid="work-package-row"
                    @click="selectWorkPackage(wp.id)"
                  >
                    <!-- ID Column -->
                    <td class="py-2.5 px-3">
                      <router-link
                        :to="`/work-packages/${wp.id}`"
                        class="font-mono text-cyan-400 hover:text-cyan-300 font-semibold text-xs no-underline"
                        data-testid="work-package-id-link"
                        @click.stop="selectWorkPackage(wp.id)"
                      >
                        {{ wp.id.slice(0, 8) }}
                      </router-link>
                    </td>

                    <!-- Objective & Demand Column -->
                    <td class="py-2.5 px-3">
                      <div class="font-medium text-white truncate inline-block max-w-[260px] text-xs" data-testid="work-package-objective-cell">
                        {{ wp.objective || wp.name }}
                      </div>
                      <div
                        v-if="wp.name && wp.name !== wp.objective"
                        class="text-slate-400 font-mono text-[10.5px] truncate"
                      >
                        {{ wp.name }}
                      </div>

                      <!-- Scope Demand Badge & Health Invariant Pills -->
                      <div class="flex flex-wrap items-center gap-1 mt-1" data-testid="wp-telemetry-strip">
                        <!-- Scope Demand Badge -->
                        <span
                          class="badge font-mono rounded px-1.5 py-0.5 text-[9.5px]"
                          :class="getWpScopeDemandBadge(wp).class"
                          :title="getWpScopeDemandBadge(wp).tooltip"
                          data-testid="wp-scope-demand-badge"
                        >
                          {{ getWpScopeDemandBadge(wp).label }}
                        </span>

                        <!-- Health Invariant Pills -->
                        <template v-if="getTelemetry(wp.id)">
                          <span
                            v-if="getTelemetry(wp.id)?.deadlineBreached"
                            class="px-1.5 py-0.5 rounded text-[9.5px] font-semibold bg-rose-500/20 text-rose-300 border border-rose-500/30"
                            data-testid="wp-health-breached-pill"
                          >
                            ! Deadline Breached
                          </span>
                          <span
                            v-if="(getTelemetry(wp.id)?.blockedRequestsCount ?? 0) > 0"
                            class="px-1.5 py-0.5 rounded text-[9.5px] font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/20"
                            data-testid="wp-health-blocked-pill"
                          >
                            ! {{ getTelemetry(wp.id)?.blockedRequestsCount }} Blocked
                          </span>
                          <span
                            v-if="getTelemetry(wp.id)?.isDormant"
                            class="px-1.5 py-0.5 rounded text-[9.5px] font-semibold bg-amber-500/10 text-amber-400 border border-amber-500/20"
                            data-testid="wp-health-dormant-pill"
                          >
                            Dormant: {{ Math.round(getTelemetry(wp.id)?.dormantDays ?? 0) }}d
                          </span>
                          <span
                            v-if="(getTelemetry(wp.id)?.activeWipCount ?? 0) > 0"
                            class="px-1.5 py-0.5 rounded text-[9.5px] font-semibold bg-cyan-500/10 text-cyan-400 border border-cyan-500/20"
                            data-testid="wp-health-wip-pill"
                          >
                            WIP: {{ getTelemetry(wp.id)?.activeWipCount }}
                          </span>
                          <span
                            v-if="getTelemetry(wp.id)?.healthState === 'FLOWING'"
                            class="px-1.5 py-0.5 rounded text-[9.5px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20"
                            data-testid="wp-health-flowing-pill"
                          >
                            Flowing
                          </span>
                        </template>
                      </div>
                    </td>

                    <!-- 14d Flow Barcode Column -->
                    <td class="py-2.5 px-3" data-testid="work-package-flow-cell">
                      <div v-if="getTelemetry(wp.id)" class="flex flex-col gap-1">
                        <!-- Barcode Strip -->
                        <div
                          class="flex items-end gap-1 flow-barcode-strip bg-slate-950 border border-slate-800 px-1.5 py-0.5 rounded"
                          style="height: 18px"
                          data-testid="flow-barcode-strip"
                          :title="`14-day flow activity (Outflow: ${getTelemetry(wp.id)?.outflow14dCount}, Active WIP: ${getTelemetry(wp.id)?.activeWipCount})`"
                        >
                          <div
                            v-for="(day, idx) in getTelemetry(wp.id)!.flowBarcode"
                            :key="idx"
                            class="barcode-bar rounded-sm"
                            :class="getBarClass(day)"
                            :style="{ height: getBarHeight(day), width: '4px' }"
                            :title="getBarTooltip(day)"
                            data-testid="flow-barcode-segment"
                          ></div>
                        </div>

                        <!-- Mini Flow Inventory Stats -->
                        <div class="text-slate-400 font-mono text-[9.5px]" data-testid="wp-flow-inventory-stats">
                          <span>WIP: {{ getTelemetry(wp.id)?.activeWipCount }}</span>
                          <span class="mx-1">•</span>
                          <span>14d: {{ getTelemetry(wp.id)?.outflow14dCount }}</span>
                        </div>
                      </div>
                      <span v-else class="text-slate-500 font-mono text-[10px]">—</span>
                    </td>

                    <!-- Owner Column -->
                    <td class="py-2.5 px-3">
                      <span class="text-xs text-slate-300">{{ resolveOwnerDisplay(wp) }}</span>
                    </td>

                    <!-- Customer Column -->
                    <td class="py-2.5 px-3">
                      <span class="text-xs text-slate-300">{{ resolveCustomerDisplay(wp) }}</span>
                    </td>

                    <!-- Product Column -->
                    <td class="py-2.5 px-3">
                      <span class="text-xs text-slate-300">{{ resolveProductDisplay(wp) }}</span>
                    </td>

                    <!-- Deadline Column -->
                    <td class="py-2.5 px-3">
                      <div v-if="wp.deadline" class="flex items-center gap-1.5">
                        <span class="text-xs font-mono text-slate-200">{{ formatDeadline(wp.deadline) }}</span>
                        <span v-if="isWorkPackageOverdue(wp)" class="px-1.5 py-0.5 rounded text-[9.5px] font-semibold bg-rose-500/20 text-rose-300 border border-rose-500/30" data-testid="wp-overdue-badge">
                          Overdue
                        </span>
                      </div>
                      <span v-else class="text-slate-500 text-xs">—</span>
                    </td>

                    <!-- Status Column -->
                    <td class="py-2.5 px-3 text-center">
                      <span
                        class="badge rounded font-mono"
                        style="font-size: 10px; padding: 2px 6px"
                        :class="statusBadgeClass(wp.status)"
                        data-testid="work-package-status-badge"
                      >
                        {{ wp.status }}
                      </span>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Card View Grid -->
            <div v-else class="row g-3 p-3" data-testid="work-packages-cards-grid">
              <div
                v-if="isLoadingList"
                class="col-12 text-center py-6 text-slate-400 text-xs"
              >
                <div class="inline-block w-4 h-4 border-2 border-cyan-400 border-t-transparent rounded-full animate-spin me-2" role="status"></div>
                Loading work packages...
              </div>

              <div
                v-else-if="workPackages.length === 0"
                class="col-12 text-center py-6 text-slate-400 text-xs"
              >
                No work packages found matching the current filters.
              </div>

              <div
                v-for="wp in workPackages"
                v-else
                :key="wp.id"
                class="col-12 col-md-6"
              >
                <div
                  class="bg-slate-900/90 border border-slate-800 hover:border-slate-700 rounded-xl p-3.5 shadow-lg transition text-slate-100 flex flex-col justify-between h-full"
                  :class="{ 'border-cyan-400 ring-1 ring-cyan-400/50 bg-slate-900': selectedWorkPackage?.id === wp.id }"
                  style="cursor: pointer"
                  data-testid="work-package-card"
                  @click="selectWorkPackage(wp.id)"
                >
                  <div>
                    <!-- Card Header: Title & Status -->
                    <div class="flex justify-between items-start gap-1 mb-1">
                      <div class="truncate">
                        <router-link
                          :to="`/work-packages/${wp.id}`"
                          class="font-bold text-white hover:text-cyan-300 no-underline text-xs block truncate"
                          data-testid="card-work-package-id-link"
                          @click.stop="selectWorkPackage(wp.id)"
                        >
                          {{ wp.objective || wp.name }}
                        </router-link>
                        <span class="font-mono text-cyan-400 text-[10px]">
                          #{{ wp.id.slice(0, 8) }}
                        </span>
                      </div>
                      <span
                        class="badge rounded flex-shrink-0 font-mono"
                        style="font-size: 10px; padding: 2px 6px"
                        :class="statusBadgeClass(wp.status)"
                        data-testid="card-work-package-status-badge"
                      >
                        {{ wp.status }}
                      </span>
                    </div>

                    <!-- Scope Demand Badge & Health Invariant Pills -->
                    <div class="flex flex-wrap items-center gap-1 mb-2">
                      <span
                        class="badge font-mono rounded px-1.5 py-0.5 text-[9.5px]"
                        :class="getWpScopeDemandBadge(wp).class"
                        :title="getWpScopeDemandBadge(wp).tooltip"
                        data-testid="card-scope-demand-badge"
                      >
                        {{ getWpScopeDemandBadge(wp).label }}
                      </span>

                      <template v-if="getTelemetry(wp.id)">
                        <span
                          v-if="getTelemetry(wp.id)?.deadlineBreached"
                          class="px-1.5 py-0.5 rounded text-[9.5px] font-semibold bg-rose-500/20 text-rose-300 border border-rose-500/30"
                        >
                          ! Deadline Breached
                        </span>
                        <span
                          v-if="(getTelemetry(wp.id)?.blockedRequestsCount ?? 0) > 0"
                          class="px-1.5 py-0.5 rounded text-[9.5px] font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/20"
                        >
                          ! {{ getTelemetry(wp.id)?.blockedRequestsCount }} Blocked
                        </span>
                        <span
                          v-if="getTelemetry(wp.id)?.isDormant"
                          class="px-1.5 py-0.5 rounded text-[9.5px] font-semibold bg-amber-500/10 text-amber-400 border border-amber-500/20"
                        >
                          Dormant: {{ Math.round(getTelemetry(wp.id)?.dormantDays ?? 0) }}d
                        </span>
                        <span
                          v-if="(getTelemetry(wp.id)?.activeWipCount ?? 0) > 0"
                          class="px-1.5 py-0.5 rounded text-[9.5px] font-semibold bg-cyan-500/10 text-cyan-400 border border-cyan-500/20"
                        >
                          WIP: {{ getTelemetry(wp.id)?.activeWipCount }}
                        </span>
                        <span
                          v-if="getTelemetry(wp.id)?.healthState === 'FLOWING'"
                          class="px-1.5 py-0.5 rounded text-[9.5px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20"
                        >
                          Flowing
                        </span>
                      </template>
                    </div>

                    <!-- Metadata: Owner & Deadline -->
                    <div class="row g-1 text-slate-400 text-xs mb-2">
                      <div class="col-6 truncate">
                        <i class="bi bi-person text-cyan-400 me-1"></i>{{ resolveOwnerDisplay(wp) }}
                      </div>
                      <div class="col-6 truncate">
                        <i class="bi bi-calendar-event text-indigo-400 me-1"></i>
                        <span :class="{ 'text-rose-400 font-bold': isWorkPackageOverdue(wp) }">
                          {{ wp.deadline ? formatDeadline(wp.deadline) : 'No Deadline' }}
                        </span>
                      </div>
                    </div>
                  </div>

                  <!-- Flow Activity Barcode & Flow Inventory Stats -->
                  <div v-if="getTelemetry(wp.id)" class="pt-2 border-t border-slate-800">
                    <div class="flex justify-between items-center mb-1">
                      <span class="text-slate-400 text-[10px] font-medium">14-Day Activity Barcode</span>
                      <div class="text-slate-400 font-mono text-[9.5px]" data-testid="card-flow-stats">
                        <span>WIP: {{ getTelemetry(wp.id)?.activeWipCount }}</span>
                        <span class="mx-1">•</span>
                        <span>Outflow: {{ getTelemetry(wp.id)?.outflow14dCount }}</span>
                      </div>
                    </div>
                    <div
                      class="flex items-end gap-1 flow-barcode-strip bg-slate-950 border border-slate-800 p-1.5 rounded-lg w-full"
                      style="height: 22px"
                      data-testid="card-flow-barcode-strip"
                      :title="`14-day flow activity (Outflow: ${getTelemetry(wp.id)?.outflow14dCount}, Active WIP: ${getTelemetry(wp.id)?.activeWipCount})`"
                    >
                      <div
                        v-for="(day, idx) in getTelemetry(wp.id)!.flowBarcode"
                        :key="idx"
                        class="barcode-bar flex-grow rounded-sm"
                        :class="getBarClass(day)"
                        :style="{ height: getBarHeight(day) }"
                        :title="getBarTooltip(day)"
                      ></div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Work Package Detail & Scope Panel -->
      <div
        v-if="selectedWorkPackage || isLoadingDetail"
        class="col-12 col-xl-5"
        data-testid="work-package-detail-panel"
      >
        <div class="card shadow-2xl bg-slate-900/95 border border-slate-800 rounded-xl overflow-hidden text-slate-100 mb-3">
          <div class="card-header bg-slate-950/80 border-b border-slate-800 flex justify-between items-center py-2.5 px-3.5">
            <div class="flex items-center gap-1.5">
              <i class="bi bi-kanban text-cyan-400" aria-hidden="true"></i>
              <span class="font-bold text-sm text-white">Work Package Detail</span>
              <span
                v-if="selectedWorkPackage"
                class="badge ms-1 rounded font-mono"
                style="font-size: 10px; padding: 2px 6px"
                :class="statusBadgeClass(selectedWorkPackage.status)"
                data-testid="detail-status-badge"
              >
                {{ selectedWorkPackage.status }}
              </span>
            </div>

            <button
              type="button"
              class="text-slate-400 hover:text-white p-1 rounded-lg hover:bg-slate-800 transition bg-transparent border-0 cursor-pointer"
              aria-label="Close detail panel"
              data-testid="close-detail-panel-button"
              @click="closeDetailPanel"
            >
              <i class="bi bi-x-lg text-xs"></i>
            </button>
          </div>

          <div v-if="isLoadingDetail" class="card-body py-8 text-center text-slate-400 text-xs">
            <div class="inline-block w-4 h-4 border-2 border-cyan-400 border-t-transparent rounded-full animate-spin me-2" role="status"></div>
            Loading details...
          </div>

          <div v-else-if="selectedWorkPackage" class="card-body p-3.5 space-y-3">
            <!-- Summary Metadata -->
            <div class="pb-3 border-b border-slate-800">
              <div class="flex justify-between items-start gap-1">
                <h2 class="text-base font-bold text-white tracking-tight mb-1" data-testid="detail-objective-display">
                  {{ selectedWorkPackage.objective }}
                </h2>
                <span class="font-mono text-cyan-400 text-xs flex-shrink-0" data-testid="detail-work-package-id">
                  #{{ selectedWorkPackage.id.slice(0, 8) }}
                </span>
              </div>
              <div
                v-if="selectedWorkPackage.name && selectedWorkPackage.name !== selectedWorkPackage.objective"
                class="text-slate-400 text-xs mb-1.5"
              >
                Title: {{ selectedWorkPackage.name }}
              </div>

              <div class="row g-2 text-xs text-slate-400">
                <div class="col-6">
                  <span>Owner:</span>
                  <strong class="ms-1 text-slate-200" data-testid="detail-owner-display">
                    {{ resolveOwnerDisplay(selectedWorkPackage) }}
                  </strong>
                </div>
                <div class="col-6">
                  <span>Created:</span>
                  <span class="ms-1 text-slate-300 font-mono text-[11px]">{{ formatTimestamp(selectedWorkPackage.createdAt) }}</span>
                </div>
                <div class="col-6">
                  <span>Customer:</span>
                  <span class="ms-1 text-slate-300">{{ resolveCustomerDisplay(selectedWorkPackage) }}</span>
                </div>
                <div class="col-6">
                  <span>Product:</span>
                  <span class="ms-1 text-slate-300">{{ resolveProductDisplay(selectedWorkPackage) }}</span>
                </div>
                <div class="col-6">
                  <span>Deadline:</span>
                  <span v-if="selectedWorkPackage.deadline" class="ms-1 font-mono text-slate-200" data-testid="detail-deadline-display">
                    {{ formatDeadline(selectedWorkPackage.deadline) }}
                    <span
                      v-if="isWorkPackageOverdue(selectedWorkPackage)"
                      class="px-1.5 py-0.5 rounded text-[9.5px] font-semibold bg-rose-500/20 text-rose-300 border border-rose-500/30 ms-1"
                      data-testid="wp-overdue-badge"
                    >
                      Overdue
                    </span>
                  </span>
                  <span v-else class="text-slate-500 ms-1">—</span>
                </div>
              </div>

              <div
                v-if="isSelectedClosed && selectedWorkPackage.closedReason"
                class="bg-slate-950/60 border border-slate-800 text-slate-300 rounded-lg p-2.5 text-xs mt-2"
                data-testid="detail-closed-reason"
              >
                <strong class="text-white">Closed Reason:</strong> {{ selectedWorkPackage.closedReason }}
                <span v-if="selectedWorkPackage.closedAt" class="text-slate-400 ms-1 font-mono">
                  ({{ formatTimestamp(selectedWorkPackage.closedAt) }})
                </span>
              </div>
            </div>

            <!-- Operational Telemetry Spotlight (CR-025 P4-S07) -->
            <div v-if="selectedTelemetry" class="pb-3 border-b border-slate-800" data-testid="detail-telemetry-spotlight">
              <div class="flex justify-between items-center mb-2">
                <span class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 flex items-center gap-1">
                  <i class="bi bi-speedometer2 text-cyan-400"></i>Operational Telemetry &amp; Invariants
                </span>
                <span
                  class="badge font-mono rounded"
                  style="font-size: 10px; padding: 2px 6px"
                  :class="getPressureBadgeClass(selectedTelemetry.pressureTier)"
                  data-testid="detail-scope-demand-badge"
                >
                  {{ getScopeDemandLabel(selectedTelemetry) }}
                </span>
              </div>

              <!-- Scope Demand Density Card -->
              <div class="p-3 rounded-xl bg-slate-950/60 border border-slate-800 mb-2.5" data-testid="detail-scope-demand-card">
                <div class="flex justify-between items-center mb-1.5">
                  <span class="text-xs font-semibold text-white">Scope Demand Density</span>
                  <span class="text-xs font-mono text-slate-400">
                    Tier: <strong class="text-cyan-400">{{ selectedTelemetry.pressureTier }}</strong>
                  </span>
                </div>

                <div class="row g-2 text-xs">
                  <div class="col-6">
                    <div class="text-[10px] uppercase tracking-wider font-semibold text-slate-400">Required Daily Burn:</div>
                    <div class="font-mono font-bold text-white text-xs mt-0.5">
                      {{ selectedTelemetry.requiredDailyBurn != null ? `${selectedTelemetry.requiredDailyBurn.toFixed(1)} pts/day` : '— (UNPLANNED)' }}
                    </div>
                  </div>
                  <div class="col-6">
                    <div class="text-[10px] uppercase tracking-wider font-semibold text-slate-400">Org Capacity Share:</div>
                    <div class="font-mono font-bold text-cyan-400 text-xs mt-0.5">
                      {{ selectedTelemetry.orgCapacityShare != null ? `${selectedTelemetry.orgCapacityShare.toFixed(1)}% of C_org` : '—' }}
                    </div>
                  </div>
                  <div class="col-6">
                    <div class="text-[10px] uppercase tracking-wider font-semibold text-slate-400">Working Days Remaining:</div>
                    <div class="font-mono font-bold text-white text-xs mt-0.5">
                      {{ selectedTelemetry.workingDaysRemaining != null ? `${selectedTelemetry.workingDaysRemaining} business days` : '—' }}
                    </div>
                  </div>
                  <div class="col-6">
                    <div class="text-[10px] uppercase tracking-wider font-semibold text-slate-400">Remaining Complexity:</div>
                    <div class="font-mono font-bold text-white text-xs mt-0.5">
                      {{ selectedTelemetry.remainingComplexity }} / {{ selectedTelemetry.totalComplexity }} pts
                    </div>
                  </div>
                </div>
              </div>

              <!-- Health Invariants & Flow Inventory Facts -->
              <div class="p-3 rounded-xl bg-slate-950/60 border border-slate-800 mb-2.5" data-testid="detail-health-invariants-card">
                <div class="flex justify-between items-center mb-1.5">
                  <span class="text-xs font-semibold text-white">Observable Health Invariants</span>
                  <span
                    class="badge font-mono rounded"
                    style="font-size: 9.5px"
                    :class="selectedTelemetry.healthState === 'FLOWING' ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30' : 'bg-amber-500/15 text-amber-400 border border-amber-500/30'"
                  >
                    State: {{ selectedTelemetry.healthState }}
                  </span>
                </div>

                <!-- Invariant Pills -->
                <div class="flex flex-wrap items-center gap-1.5 mb-2" data-testid="detail-health-pills">
                  <span
                    v-if="selectedTelemetry.deadlineBreached"
                    class="px-2 py-0.5 rounded text-[9.5px] font-semibold bg-rose-500/20 text-rose-300 border border-rose-500/30"
                  >
                    ! Deadline Breached
                  </span>
                  <span
                    v-if="selectedTelemetry.blockedRequestsCount > 0"
                    class="px-2 py-0.5 rounded text-[9.5px] font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/20"
                  >
                    ! {{ selectedTelemetry.blockedRequestsCount }} Blocked
                  </span>
                  <span
                    v-if="selectedTelemetry.isDormant"
                    class="px-2 py-0.5 rounded text-[9.5px] font-semibold bg-amber-500/10 text-amber-400 border border-amber-500/20"
                  >
                    Dormant: {{ Math.round(selectedTelemetry.dormantDays) }}d
                  </span>
                  <span
                    v-if="selectedTelemetry.activeWipCount > 0"
                    class="px-2 py-0.5 rounded text-[9.5px] font-semibold bg-cyan-500/10 text-cyan-400 border border-cyan-500/20"
                  >
                    WIP: {{ selectedTelemetry.activeWipCount }}
                    <template v-if="selectedTelemetry.isWipStagnant">
                      (Stagnant: {{ Math.round(selectedTelemetry.oldestActiveWipDays) }}d)
                    </template>
                  </span>
                  <span
                    v-if="selectedTelemetry.healthState === 'FLOWING'"
                    class="px-2 py-0.5 rounded text-[9.5px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20"
                  >
                    Flowing (Zero Invariant Violations)
                  </span>
                </div>

                <!-- Flow Inventory Counters -->
                <div class="row g-2 border-t border-slate-800/80 pt-2 mt-1 text-xs">
                  <div class="col-4">
                    <div class="text-[10px] uppercase tracking-wider font-semibold text-slate-400">Active WIP:</div>
                    <div class="font-mono font-bold text-white text-xs mt-0.5" data-testid="detail-active-wip-count">
                      {{ selectedTelemetry.activeWipCount }} reqs
                    </div>
                  </div>
                  <div class="col-4">
                    <div class="text-[10px] uppercase tracking-wider font-semibold text-slate-400">Oldest In-Flight:</div>
                    <div class="font-mono font-bold text-white text-xs mt-0.5" data-testid="detail-oldest-wip-days">
                      {{ selectedTelemetry.activeWipCount > 0 ? `${Math.round(selectedTelemetry.oldestActiveWipDays)}d` : '—' }}
                    </div>
                  </div>
                  <div class="col-4">
                    <div class="text-[10px] uppercase tracking-wider font-semibold text-slate-400">14d Outflow:</div>
                    <div class="font-mono font-bold text-emerald-400 text-xs mt-0.5" data-testid="detail-outflow-count">
                      {{ selectedTelemetry.outflow14dCount }} done
                    </div>
                  </div>
                </div>
              </div>

              <!-- 14-Day Flow Activity Barcode Spotlight -->
              <div class="p-3 rounded-xl bg-slate-950/60 border border-slate-800" data-testid="detail-flow-barcode-card">
                <div class="flex justify-between items-center mb-1.5">
                  <span class="text-xs font-semibold text-white">14-Day Flow Activity Barcode</span>
                  <div class="flex items-center gap-2 text-slate-400 text-[10px]">
                    <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-emerald-400"></span>Closed</span>
                    <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-cyan-400"></span>Mutated</span>
                    <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-rose-500"></span>Blocked</span>
                  </div>
                </div>

                <div
                  class="flex items-end gap-1 flow-barcode-strip py-1.5 bg-slate-950 border border-slate-800 rounded-lg px-2 w-full"
                  style="height: 36px"
                  data-testid="detail-flow-barcode-strip"
                >
                  <div
                    v-for="(day, idx) in selectedTelemetry.flowBarcode"
                    :key="idx"
                    class="barcode-bar flex-grow rounded-sm"
                    :class="getBarClass(day)"
                    :style="{ height: getBarHeight(day, true) }"
                    :title="getBarTooltip(day)"
                    data-testid="flow-barcode-segment"
                  ></div>
                </div>
                <div class="flex justify-between text-slate-400 font-mono mt-1 px-1 text-[9px]">
                  <span>{{ selectedTelemetry.flowBarcode[0]?.date || 'T-13' }}</span>
                  <span>{{ selectedTelemetry.flowBarcode[selectedTelemetry.flowBarcode.length - 1]?.date || 'Today' }}</span>
                </div>
              </div>
            </div>

            <!-- Interactive Decision Workbench (CR-025 P4-S08, Architecture §4 TD-007) -->
            <div class="pb-3 border-b border-slate-800" data-testid="decision-workbench">
              <div class="flex justify-between items-center mb-2">
                <div class="flex items-center gap-1.5">
                  <span class="text-[11px] uppercase tracking-wider font-bold text-white flex items-center gap-1">
                    <i class="bi bi-sliders text-cyan-400"></i>Decision Workbench
                  </span>
                  <span class="px-2 py-0.5 rounded text-[9.5px] font-mono bg-cyan-500/10 text-cyan-400 border border-cyan-500/20">
                    Live Simulation
                  </span>
                </div>
                <button
                  type="button"
                  class="px-2.5 py-1 text-xs font-semibold rounded-lg border border-slate-700 bg-slate-800 hover:bg-slate-700 text-slate-300 disabled:opacity-40 transition"
                  :disabled="!isSimulationActive"
                  data-testid="reset-simulation-button"
                  @click="resetSimulation"
                >
                  <i class="bi bi-arrow-counterclockwise me-1"></i>Reset
                </button>
              </div>

              <!-- Simulation Controls: Target Deadline Extension & De-Scope Selector -->
              <div class="p-3 rounded-xl bg-slate-950/70 border border-cyan-500/30 mb-2.5 space-y-2.5">
                <div class="row g-2">
                  <!-- Deadline Extension Simulator -->
                  <div class="col-12 col-sm-6">
                    <label for="simulateDeadlineInput" class="text-[10px] uppercase tracking-wider font-semibold text-slate-400 mb-1 block">
                      <i class="bi bi-calendar-event text-cyan-400 me-1"></i>Simulated Target Deadline:
                    </label>
                    <div class="flex gap-1">
                      <input
                        id="simulateDeadlineInput"
                        v-model="simulatedDeadline"
                        type="date"
                        class="bg-slate-900 border border-slate-700 text-slate-100 font-mono rounded-lg px-2.5 py-1 text-xs focus:border-cyan-400 focus:outline-none flex-grow"
                        style="height: 28px"
                        data-testid="simulate-deadline-input"
                      />
                      <button
                        v-if="simulatedDeadline"
                        type="button"
                        class="px-2 py-0.5 text-xs rounded-lg border border-slate-700 bg-slate-800 hover:bg-slate-700 text-slate-300"
                        title="Clear simulated deadline"
                        @click="simulatedDeadline = ''"
                      >
                        ✕
                      </button>
                    </div>
                    <div class="text-slate-400 font-mono text-[10px] mt-1">
                      {{ simulatedWorkingDaysRemaining != null ? `${simulatedWorkingDaysRemaining} working days left` : 'No deadline (UNPLANNED)' }}
                    </div>
                  </div>

                  <!-- De-Scope Simulation Counter -->
                  <div class="col-12 col-sm-6">
                    <div class="text-[10px] uppercase tracking-wider font-semibold text-slate-400 mb-1">
                      <i class="bi bi-dash-circle text-amber-400 me-1"></i>Simulated De-Scope:
                    </div>
                    <div class="p-1.5 rounded-lg bg-slate-900 border border-slate-800 flex justify-between items-center text-xs font-mono" style="min-height: 28px">
                      <span class="text-slate-300">
                        <strong class="text-white">{{ descopedRequestIds.length }}</strong> / {{ uncompletedActiveScopeItems.length }} uncompleted
                      </span>
                      <span v-if="descopedComplexity > 0" class="px-1.5 py-0.5 rounded text-[9.5px] font-mono bg-amber-500/20 text-amber-300 border border-amber-500/30">
                        -{{ descopedComplexity }} pts
                      </span>
                      <span v-else class="text-slate-500 text-[10px]">
                        0 pts
                      </span>
                    </div>
                    <div class="text-slate-400 text-[10px] mt-1">
                      Toggle checkboxes below to simulate scope reduction.
                    </div>
                  </div>
                </div>

                <!-- Simulation Request Selector Checklist -->
                <div v-if="uncompletedActiveScopeItems.length > 0">
                  <div class="flex justify-between items-center mb-1">
                    <span class="text-[10px] uppercase tracking-wider font-semibold text-slate-400">
                      De-Scope Candidate Selection:
                    </span>
                    <span class="text-slate-400 font-mono text-[10px]">
                      {{ uncompletedActiveScopeItems.length }} candidates
                    </span>
                  </div>
                  <div class="flex flex-col gap-1 bg-slate-900 border border-slate-800 rounded-lg p-1.5 max-h-32 overflow-y-auto">
                    <label
                      v-for="req in uncompletedActiveScopeItems"
                      :key="req.requestId"
                      class="flex items-center gap-2 mb-0 p-1 rounded hover:bg-slate-850 cursor-pointer text-xs text-slate-300"
                    >
                      <input
                        type="checkbox"
                        class="rounded border-slate-700 bg-slate-950 text-cyan-500 focus:ring-cyan-400"
                        :checked="isDescoped(req.requestId)"
                        data-testid="descope-request-checkbox"
                        @change="toggleDescope(req.requestId)"
                      />
                      <span
                        class="truncate flex-grow"
                        :class="{ 'line-through text-slate-500': isDescoped(req.requestId) }"
                      >
                        {{ req.title || req.requestTitle || req.requestId }}
                      </span>
                      <span class="px-1.5 py-0.2 rounded bg-slate-800 text-slate-400 font-mono text-[9.5px]">
                        C{{ getRequestComplexity(req) }}
                      </span>
                    </label>
                  </div>
                </div>

                <!-- Real-Time Simulated Metrics Grid -->
                <div class="grid grid-cols-3 gap-2">
                  <div class="p-2 rounded-lg bg-slate-900 border border-slate-800 text-center">
                    <div class="text-[10px] uppercase tracking-wider font-semibold text-slate-400">Remaining Pts</div>
                    <div class="font-mono font-bold text-white text-xs mt-0.5" data-testid="simulated-remaining-complexity">
                      {{ simulatedRemainingComplexity }} pts
                      <span v-if="descopedComplexity > 0" class="text-rose-400 text-[9px]">
                        (-{{ descopedComplexity }})
                      </span>
                    </div>
                  </div>
                  <div class="p-2 rounded-lg bg-slate-900 border border-slate-800 text-center">
                    <div class="text-[10px] uppercase tracking-wider font-semibold text-slate-400">Req. Burn</div>
                    <div class="font-mono font-bold text-white text-xs mt-0.5" data-testid="simulated-daily-burn">
                      {{ simulatedRequiredDailyBurn != null ? `${simulatedRequiredDailyBurn.toFixed(1)} pts/d` : '—' }}
                    </div>
                  </div>
                  <div class="p-2 rounded-lg bg-slate-900 border border-slate-800 text-center">
                    <div class="text-[10px] uppercase tracking-wider font-semibold text-slate-400">Capacity Share</div>
                    <div class="font-mono font-bold text-cyan-400 text-xs mt-0.5" data-testid="simulated-org-capacity-share">
                      {{ simulatedOrgCapacityShare != null ? `${simulatedOrgCapacityShare.toFixed(1)}%` : '—' }}
                    </div>
                  </div>
                </div>

                <!-- Live Comparison Display Banner -->
                <div
                  class="p-2.5 rounded-xl border font-mono text-xs"
                  :class="isSimulationActive ? 'bg-gradient-to-r from-cyan-950/60 via-slate-900 to-indigo-950/60 border-cyan-500/40 text-cyan-200' : 'bg-slate-900 border-slate-800 text-slate-300'"
                >
                  <div class="flex justify-between items-center flex-wrap gap-1">
                    <div data-testid="decision-simulation-comparison">
                      {{ simulatedComparisonText }}
                    </div>
                    <span
                      class="badge font-mono rounded"
                      style="font-size: 10px; padding: 2px 6px"
                      :class="getPressureBadgeClass(simulatedPressureTier)"
                      data-testid="simulated-pressure-badge"
                    >
                      [{{ simulatedPressureTier }}]
                    </span>
                  </div>

                  <!-- Baseline Footnote when Simulation is Active -->
                  <div v-if="isSimulationActive" class="text-slate-400 text-[9.5px] mt-1 pt-1 border-t border-slate-800 font-mono">
                    Baseline: {{ baselineRemainingComplexity }} pts |
                    {{ selectedTelemetry?.requiredDailyBurn != null ? `${selectedTelemetry.requiredDailyBurn.toFixed(1)} pts/day` : '—' }} -&gt;
                    {{ selectedTelemetry?.orgCapacityShare != null ? `${selectedTelemetry.orgCapacityShare.toFixed(1)}% Org Output` : '—' }}
                    [{{ selectedTelemetry?.pressureTier ?? 'UNPLANNED' }}]
                  </div>
                </div>
              </div>
            </div>

            <!-- Objective Edit Section (PUT /api/v1/work-packages/${id}/objective) -->
            <div class="pb-3 border-b border-slate-800" data-testid="detail-objective-section">
              <form novalidate data-testid="update-objective-form" @submit.prevent="handleUpdateObjective">
                <div class="flex items-center gap-2 mb-1">
                  <label for="detailObjectiveInput" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 mb-0 flex items-center gap-1">
                    <i class="bi bi-bullseye text-cyan-400"></i>Objective:
                  </label>
                  <button
                    type="submit"
                    class="ms-auto px-2.5 py-0.5 text-xs font-semibold rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white transition shadow-sm"
                    :disabled="!canModifyPackage || isSubmittingAction || !objectiveForm.objective.trim()"
                    data-testid="save-objective-button"
                  >
                    Update
                  </button>
                </div>
                <textarea
                  id="detailObjectiveInput"
                  v-model="objectiveForm.objective"
                  class="bg-slate-950 border border-slate-700 text-slate-100 rounded-lg p-2 text-xs focus:border-cyan-400 focus:outline-none w-full"
                  rows="2"
                  :disabled="!canModifyPackage || isSubmittingAction"
                  data-testid="detail-objective-input"
                ></textarea>
              </form>
            </div>

            <!-- Owner Assignment Section (POST /api/v1/work-packages/${id}/assign-owner) -->
            <div class="pb-3 border-b border-slate-800" data-testid="detail-owner-section">
              <form
                novalidate
                class="flex items-center gap-2"
                data-testid="assign-owner-form"
                @submit.prevent="handleAssignOwner"
              >
                <label for="detailOwnerSelect" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 mb-0 whitespace-nowrap flex items-center gap-1">
                  <i class="bi bi-person-check text-cyan-400"></i>Owner:
                </label>
                <select
                  id="detailOwnerSelect"
                  v-model="assignOwnerForm.newOwnerPersonId"
                  class="bg-slate-950 border border-slate-700 text-slate-100 rounded-lg px-2.5 py-1 text-xs focus:border-cyan-400 focus:outline-none flex-grow"
                  style="height: 28px"
                  :disabled="!canModifyPackage || isSubmittingAction"
                  data-testid="detail-owner-select"
                >
                  <option value="" disabled>Select owner...</option>
                  <option
                    v-for="person in activePersons"
                    :key="person.id"
                    :value="person.id"
                  >
                    {{ person.fullName }}
                  </option>
                </select>
                <button
                  type="submit"
                  class="px-2.5 py-1 text-xs font-semibold rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 whitespace-nowrap transition"
                  style="height: 28px"
                  :disabled="!canModifyPackage || isSubmittingAction || !assignOwnerForm.newOwnerPersonId"
                  data-testid="assign-owner-submit-button"
                >
                  Reassign
                </button>
              </form>
            </div>

            <!-- Target Deadline Section (PUT /api/v1/work-packages/${id}/deadline) -->
            <div v-if="canModifyPackage" class="pb-3 border-b border-slate-800" data-testid="detail-deadline-section">
              <form
                novalidate
                class="flex items-center gap-2"
                data-testid="update-deadline-form"
                @submit.prevent="handleUpdateDeadline()"
              >
                <label for="detailDeadlineInput" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 mb-0 whitespace-nowrap flex items-center gap-1">
                  <i class="bi bi-calendar-event text-cyan-400"></i>Deadline:
                </label>
                <input
                  id="detailDeadlineInput"
                  v-model="deadlineForm.deadline"
                  type="date"
                  class="bg-slate-950 border border-slate-700 text-slate-100 rounded-lg px-2.5 py-1 text-xs font-mono focus:border-cyan-400 focus:outline-none flex-grow"
                  style="height: 28px"
                  :disabled="!canModifyPackage || isSubmittingAction"
                  data-testid="detail-deadline-input"
                />
                <button
                  type="submit"
                  class="px-2.5 py-1 text-xs font-semibold rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white whitespace-nowrap transition shadow-sm"
                  style="height: 28px"
                  :disabled="!canModifyPackage || isSubmittingAction"
                  data-testid="save-deadline-button"
                >
                  Save
                </button>
                <button
                  type="button"
                  class="px-2.5 py-1 text-xs font-semibold rounded-lg border border-slate-700 bg-slate-800 hover:bg-slate-700 text-slate-300 whitespace-nowrap transition"
                  style="height: 28px"
                  :disabled="!canModifyPackage || isSubmittingAction || (!selectedWorkPackage?.deadline && !deadlineForm.deadline)"
                  data-testid="clear-deadline-button"
                  @click="handleClearDeadline"
                >
                  Clear
                </button>
              </form>
            </div>

            <!-- Customer & Product Context Section (PUT /api/v1/work-packages/${id}/context) -->
            <div v-if="canModifyPackage" class="pb-3 border-b border-slate-800" data-testid="detail-context-section">
              <form
                novalidate
                data-testid="update-context-form"
                @submit.prevent="handleUpdateContext"
              >
                <div class="flex items-center justify-between mb-1.5">
                  <span class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 flex items-center gap-1">
                    <i class="bi bi-tags text-cyan-400"></i>Customer &amp; Product:
                  </span>
                  <button
                    type="submit"
                    class="px-2.5 py-1 text-xs font-semibold rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white whitespace-nowrap transition shadow-sm"
                    :disabled="!canModifyPackage || isSubmittingAction || !isContextDirty"
                    data-testid="save-context-button"
                  >
                    Save Context
                  </button>
                </div>
                <div class="row g-2">
                  <div class="col-12 col-sm-6">
                    <label for="detailCustomerSelect" class="text-[10px] uppercase tracking-wider font-semibold text-slate-400 mb-1 block">Customer</label>
                    <select
                      id="detailCustomerSelect"
                      v-model="contextForm.customerId"
                      class="bg-slate-950 border border-slate-700 text-slate-100 rounded-lg px-2.5 py-1 text-xs focus:border-cyan-400 focus:outline-none w-full"
                      style="height: 28px"
                      :disabled="!canModifyPackage || isSubmittingAction"
                      data-testid="detail-customer-select"
                    >
                      <option value="">-- None / Unassigned --</option>
                      <option
                        v-for="customer in activeCustomers"
                        :key="customer.id"
                        :value="customer.id"
                      >
                        {{ resolveCustomerOptionLabel(customer) }}
                      </option>
                    </select>
                  </div>
                  <div class="col-12 col-sm-6">
                    <label for="detailProductSelect" class="text-[10px] uppercase tracking-wider font-semibold text-slate-400 mb-1 block">Product</label>
                    <select
                      id="detailProductSelect"
                      v-model="contextForm.productId"
                      class="bg-slate-950 border border-slate-700 text-slate-100 rounded-lg px-2.5 py-1 text-xs focus:border-cyan-400 focus:outline-none w-full"
                      style="height: 28px"
                      :disabled="!canModifyPackage || isSubmittingAction"
                      data-testid="detail-product-select"
                    >
                      <option value="">-- None / Unassigned --</option>
                      <option
                        v-for="product in activeProducts"
                        :key="product.id"
                        :value="product.id"
                      >
                        {{ resolveProductOptionLabel(product) }}
                      </option>
                    </select>
                  </div>
                </div>
              </form>
            </div>

            <!-- Lifecycle Action Buttons (Activate in DRAFT, Close in DRAFT/ACTIVE) -->
            <div class="pb-3 border-b border-slate-800" data-testid="detail-lifecycle-section">
              <div v-if="isSelectedClosed" class="text-slate-400 text-xs">
                Work package is <strong class="text-white">CLOSED</strong> (terminal state).
              </div>

              <div v-else class="flex flex-wrap items-center gap-2">
                <!-- Activate button: visible only in DRAFT -->
                <button
                  v-if="canActivate"
                  type="button"
                  class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition inline-flex items-center gap-1 shadow-sm"
                  :disabled="isSubmittingAction"
                  data-testid="activate-work-package-button"
                  @click="handleActivateWorkPackage"
                >
                  <i class="bi bi-play-fill"></i>Activate
                </button>

                <!-- Close button: visible in DRAFT and ACTIVE -->
                <div v-if="canClose" class="flex flex-grow items-center gap-2">
                  <input
                    v-model="closePackageForm.reason"
                    type="text"
                    class="bg-slate-950 border border-slate-700 text-slate-100 placeholder-slate-500 rounded-lg px-2.5 py-1 text-xs focus:border-cyan-400 focus:outline-none flex-grow"
                    style="height: 28px"
                    placeholder="Close reason (optional)"
                    :disabled="isSubmittingAction"
                    data-testid="close-work-package-reason-input"
                  />
                  <button
                    type="button"
                    class="px-3 py-1 text-xs font-semibold rounded-lg border border-rose-500/40 bg-rose-950/40 text-rose-300 hover:bg-rose-900/50 whitespace-nowrap transition shadow-sm"
                    style="height: 28px"
                    :disabled="isSubmittingAction"
                    data-testid="close-work-package-button"
                    @click="handleCloseWorkPackage"
                  >
                    <i class="bi bi-lock-fill me-1"></i>Close
                  </button>
                </div>
              </div>
            </div>

            <!-- Scope Management Section (GET /scope, POST /requests, DELETE /requests/{requestId}) -->
            <div data-testid="detail-scope-section">
              <div class="flex justify-between items-center mb-2">
                <span class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 flex items-center gap-1">
                  <i class="bi bi-list-check text-cyan-400" aria-hidden="true"></i>
                  Scope (Linked Requests)
                  <span class="px-2 py-0.5 text-[10px] font-mono rounded bg-slate-800 text-slate-300 border border-slate-700 ms-1" data-testid="active-scope-count">
                    {{ activeScopeItems.length }}
                  </span>
                </span>

                <button
                  type="button"
                  class="text-xs text-cyan-400 hover:text-cyan-300 bg-transparent border-0 cursor-pointer p-0 inline-flex items-center gap-1"
                  :disabled="isLoadingScope"
                  data-testid="refresh-scope-button"
                  @click="loadWorkPackageScope(selectedWorkPackage.id)"
                >
                  <i class="bi bi-arrow-clockwise" aria-hidden="true"></i>Refresh
                </button>
              </div>

              <!-- Scope Add Controls Toggle Pill (CR-019) -->
              <div v-if="canModifyPackage" class="inline-flex rounded-lg border border-slate-700 p-0.5 bg-slate-950 w-full mb-2" data-testid="scope-add-mode-toggle">
                <button
                  type="button"
                  class="flex-1 py-1 text-xs rounded-md font-medium transition text-center"
                  :class="scopeAddMode === 'existing' ? 'bg-cyan-500/20 text-cyan-300 border border-cyan-500/30' : 'text-slate-400 hover:text-white border border-transparent'"
                  data-testid="scope-mode-existing-button"
                  @click="scopeAddMode = 'existing'"
                >
                  Existing Request
                </button>
                <button
                  type="button"
                  class="flex-1 py-1 text-xs rounded-md font-medium transition text-center"
                  :class="scopeAddMode === 'quick' ? 'bg-cyan-500/20 text-cyan-300 border border-cyan-500/30' : 'text-slate-400 hover:text-white border border-transparent'"
                  data-testid="scope-mode-quick-button"
                  @click="scopeAddMode = 'quick'"
                >
                  ⚡ Quick Bulk Add
                </button>
              </div>

              <!-- Existing Request Form -->
              <form
                v-if="canModifyPackage && scopeAddMode === 'existing'"
                novalidate
                class="p-2.5 mb-2.5 rounded-xl bg-slate-950/60 border border-slate-800"
                data-testid="add-request-form"
                @submit.prevent="handleAddRequestToScope"
              >
                <div class="flex flex-col gap-2">
                  <select
                    id="addRequestSelect"
                    v-model="addRequestForm.selectedRequestId"
                    class="bg-slate-900 border border-slate-700 text-slate-100 rounded-lg px-2.5 py-1 text-xs focus:border-cyan-400 focus:outline-none w-full"
                    style="height: 28px"
                    :disabled="isSubmittingAction"
                    data-testid="add-request-select"
                  >
                    <option value="">Select an operational request...</option>
                    <option
                      v-for="req in selectableRequests"
                      :key="req.id"
                      :value="req.id"
                    >
                      {{ req.title }} ({{ req.status }}) — {{ req.id.slice(0, 8) }}
                    </option>
                  </select>

                  <div class="flex gap-1.5">
                    <input
                      v-model="addRequestForm.manualRequestId"
                      type="text"
                      class="bg-slate-900 border border-slate-700 text-slate-100 font-mono rounded-lg px-2.5 py-1 text-xs focus:border-cyan-400 focus:outline-none flex-grow"
                      style="height: 28px"
                      placeholder="Or enter Request ID (UUID)..."
                      :disabled="isSubmittingAction"
                      data-testid="add-request-id-input"
                    />
                    <button
                      type="submit"
                      class="px-3 py-1 text-xs font-semibold rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white transition inline-flex items-center gap-1 shadow-sm"
                      style="height: 28px"
                      :disabled="isSubmittingAction || !resolvedRequestIdToAdd"
                      data-testid="add-request-button"
                    >
                      <i class="bi bi-plus-circle"></i>Add
                    </button>
                  </div>
                </div>
              </form>

              <!-- Quick Bulk Add Form (CR-019) -->
              <div
                v-if="canModifyPackage && scopeAddMode === 'quick'"
                class="p-3 mb-2.5 rounded-xl bg-slate-950/60 border border-slate-800"
                data-testid="scope-quick-bulk-form"
              >
                <div class="flex justify-between items-center mb-1.5">
                  <label for="scopeQuickTasksInput" class="text-[11px] uppercase tracking-wider font-semibold text-slate-400 mb-0 flex items-center gap-1">
                    <i class="bi bi-lightning-charge-fill text-amber-400"></i>Bulk Quick Add Tasks
                  </label>
                  <div class="flex items-center gap-2">
                    <span
                      v-if="scopeCandidateTasks.length > 0"
                      class="px-2 py-0.5 text-xs font-mono rounded bg-cyan-500/20 text-cyan-300 border border-cyan-500/30"
                      data-testid="scope-candidate-count-badge"
                    >
                      {{ scopeCandidateTasks.length }} parsed
                    </span>
                    <button
                      v-if="scopeCandidateTasks.length > 0"
                      type="button"
                      class="text-xs text-rose-400 hover:text-rose-300 bg-transparent border-0 cursor-pointer p-0"
                      :disabled="isSubmittingScopeBulk"
                      data-testid="scope-clear-candidates-button"
                      @click="handleClearScopeCandidates"
                    >
                      Clear Parsed
                    </button>
                  </div>
                </div>

                <div class="mb-2">
                  <textarea
                    id="scopeQuickTasksInput"
                    v-model="scopeRawTasks"
                    class="bg-slate-900 border border-slate-700 text-slate-100 placeholder-slate-500 rounded-lg p-2.5 text-xs font-mono focus:border-cyan-400 focus:outline-none w-full"
                    rows="3"
                    placeholder="Paste or type task list (bullets, numbers, markdown checklists)&#10;- Sub-system integration&#10;- Validation checks"
                    :disabled="isSubmittingScopeBulk"
                    data-testid="scope-quick-tasks-textarea"
                  ></textarea>
                </div>

                <div class="flex justify-between items-center mb-2">
                  <span class="text-slate-400 text-[10.5px]">
                    One task per line. Bullets, numbers, and checkboxes are cleaned.
                  </span>
                  <button
                    type="button"
                    class="px-2.5 py-1 text-xs font-semibold rounded-lg border border-slate-700 bg-slate-800 hover:bg-slate-700 text-slate-200 transition inline-flex items-center gap-1 shadow-sm"
                    :disabled="isSubmittingScopeBulk || !scopeRawTasks.trim()"
                    data-testid="scope-parse-tasks-button"
                    @click="handleParseScopeTasks"
                  >
                    <i class="bi bi-arrow-down-circle text-cyan-400"></i>Parse Tasks
                  </button>
                </div>

                <!-- Scope Candidate Tasks Preview List -->
                <div
                  v-if="scopeCandidateTasks.length > 0"
                  class="border border-slate-800 rounded-lg p-2 bg-slate-900 mb-2"
                  data-testid="scope-candidate-preview-container"
                >
                  <div class="text-[10.5px] uppercase tracking-wider font-semibold text-slate-400 mb-1 px-1">
                    Candidate Tasks to Add to Scope:
                  </div>
                  <ul class="divide-y divide-slate-800 max-h-36 overflow-y-auto m-0 p-0 list-none text-xs" data-testid="scope-candidate-tasks-list">
                    <li
                      v-for="(task, idx) in scopeCandidateTasks"
                      :key="idx"
                      class="flex justify-between items-center py-1.5 px-2 bg-transparent text-slate-200"
                      data-testid="scope-candidate-task-item"
                    >
                      <div class="flex items-center truncate me-2 gap-1.5">
                        <span class="px-1.5 py-0.2 rounded bg-slate-800 text-slate-400 border border-slate-700 font-mono text-[10px] shrink-0">
                          #{{ idx + 1 }}
                        </span>
                        <span
                          class="px-1.5 py-0.5 text-[10px] font-mono rounded bg-amber-500/15 text-amber-300 border border-amber-500/30 font-semibold shrink-0"
                          data-testid="scope-candidate-complexity-badge"
                        >
                          {{ task.complexity }} pt{{ task.complexity > 1 ? 's' : '' }}
                        </span>
                        <span class="truncate text-xs">{{ task.title }}</span>
                      </div>
                      <button
                        type="button"
                        class="text-rose-400 hover:text-rose-300 p-0.5 bg-transparent border-0 cursor-pointer text-xs"
                        title="Remove candidate task"
                        aria-label="Remove candidate task"
                        :disabled="isSubmittingScopeBulk"
                        data-testid="scope-candidate-remove-button"
                        @click="handleRemoveScopeCandidate(idx)"
                      >
                        ✕
                      </button>
                    </li>
                  </ul>
                </div>

                <div class="flex justify-end">
                  <button
                    type="button"
                    class="px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white transition inline-flex items-center gap-1.5 shadow-md shadow-cyan-500/20"
                    :disabled="isSubmittingScopeBulk || (scopeCandidateTasks.length === 0 && !scopeRawTasks.trim())"
                    data-testid="scope-submit-bulk-button"
                    @click="handleBulkAddTasksToScope"
                  >
                    <span
                      v-if="isSubmittingScopeBulk"
                      class="inline-block w-3 h-3 border-2 border-white border-t-transparent rounded-full animate-spin me-1"
                      role="status"
                      aria-hidden="true"
                    ></span>
                    <i v-else class="bi bi-plus-circle"></i>
                    Add Tasks to Scope
                  </button>
                </div>
              </div>

              <!-- Linked Requests List -->
              <div v-if="isLoadingScope" class="text-center py-4 text-slate-400 text-xs">
                <div class="inline-block w-4 h-4 border-2 border-cyan-400 border-t-transparent rounded-full animate-spin me-2" role="status"></div>
                Loading linked requests...
              </div>

              <div
                v-else-if="activeScopeItems.length === 0"
                class="text-center py-4 text-slate-400 text-xs border border-slate-800 rounded-xl bg-slate-950/40"
                data-testid="empty-scope-message"
              >
                No active requests currently linked to this package.
              </div>

              <ul
                v-else
                class="space-y-1.5 m-0 p-0 list-none mb-3"
                data-testid="scope-requests-list"
              >
                <li
                  v-for="(item, index) in activeScopeItems"
                  :key="item.id || item.requestId"
                  class="bg-slate-950/50 border border-slate-800/80 rounded-lg p-2.5 text-slate-200 transition flex flex-col scope-request-item"
                  :class="{
                    'is-dragging': draggedIndex === index,
                    'drop-target': dropTargetIndex === index && draggedIndex !== index,
                    'opacity-60 bg-slate-950/80': isDescoped(item.requestId),
                  }"
                  :draggable="canModifyPackage"
                  :data-request-id="item.requestId"
                  :data-sort-order="item.sortOrder"
                  data-testid="scope-request-item"
                  @dragstart="onDragStart($event, index)"
                  @dragover="onDragOver($event, index)"
                  @dragleave="onDragLeave($event, index)"
                  @dragend="onDragEnd"
                  @drop="onDrop($event, index)"
                >
                  <div class="flex justify-between items-center gap-2 w-full">
                    <div class="flex items-center me-auto truncate max-w-[85%]">
                      <span
                        v-if="canModifyPackage"
                        class="drag-handle text-slate-500 hover:text-slate-300 me-2 flex-shrink-0 cursor-grab"
                        title="Drag to reorder"
                        aria-label="Drag to reorder"
                        data-testid="drag-handle"
                      >
                        <i class="bi bi-grip-vertical" aria-hidden="true"></i>
                      </span>

                      <!-- Checkbox to simulate de-scoping -->
                      <div class="me-2 mb-0 flex-shrink-0" title="Simulate de-scoping this request">
                        <input
                          :id="`descope-chk-${item.requestId}`"
                          type="checkbox"
                          class="rounded border-slate-700 bg-slate-950 text-cyan-500 cursor-pointer"
                          :checked="isDescoped(item.requestId)"
                          :disabled="isRequestCompleted(item)"
                          data-testid="descope-request-checkbox"
                          @change="toggleDescope(item.requestId)"
                        />
                      </div>

                      <div class="truncate">
                        <div class="flex items-center gap-1.5 flex-wrap">
                          <router-link
                            :to="`/requests/${item.requestId}`"
                            class="font-medium text-cyan-400 hover:text-cyan-300 no-underline text-xs truncate max-w-[220px]"
                            :class="{ 'line-through text-slate-500': isDescoped(item.requestId) }"
                            data-testid="scope-request-link"
                          >
                            {{ item.title || item.requestTitle || item.requestId }}
                          </router-link>

                          <!-- Complexity Rating (1-5) -->
                          <span
                            class="px-1.5 py-0.2 rounded font-mono text-[9.5px] bg-slate-800 text-slate-300 border border-slate-700"
                            title="Complexity Rating (1-5)"
                            data-testid="request-complexity-badge"
                          >
                            C{{ getRequestComplexity(item) }}
                          </span>

                          <!-- Status Badge -->
                          <span
                            v-if="item.status || item.requestStatus"
                            class="badge rounded font-mono"
                            style="font-size: 9.5px; padding: 2px 5px"
                            :class="requestStatusBadgeClass(item.status || item.requestStatus)"
                            data-testid="request-status-badge"
                          >
                            {{ item.status || item.requestStatus }}
                          </span>

                          <!-- Blocker Badge if PAUSED -->
                          <span
                            v-if="isRequestPausedOrBlocked(item)"
                            class="px-1.5 py-0.5 rounded text-[9.5px] font-semibold bg-rose-500/20 text-rose-300 border border-rose-500/30"
                            data-testid="request-blocker-badge"
                          >
                            <i class="bi bi-pause-circle-fill me-1"></i>BLOCKED
                          </span>

                          <!-- State Aging in days -->
                          <span
                            class="px-1.5 py-0.5 rounded font-mono text-[9.5px] bg-slate-800 text-slate-400 border border-slate-700"
                            data-testid="request-aging-badge"
                            :title="`${getRequestStateAgingDays(item)} days in current state`"
                          >
                            <i class="bi bi-clock-history me-1"></i>{{ getRequestStateAgingDays(item) }}d in state
                          </span>

                          <!-- De-scope Simulated Indicator -->
                          <span
                            v-if="isDescoped(item.requestId)"
                            class="px-1.5 py-0.5 rounded text-[9px] font-mono bg-amber-500/20 text-amber-300 border border-amber-500/30"
                          >
                            De-scoped
                          </span>
                        </div>

                        <div class="text-slate-500 font-mono text-[10px]">
                          {{ item.requestId }}
                        </div>
                      </div>
                    </div>

                    <button
                      v-if="canModifyPackage"
                      type="button"
                      class="text-rose-400 hover:text-rose-300 p-1 bg-transparent border-0 cursor-pointer flex-shrink-0 text-xs"
                      :disabled="removingRequestId === item.requestId || isSubmittingAction || isReordering"
                      data-testid="remove-request-button"
                      @click="handleRemoveRequestFromScope(item.requestId)"
                    >
                      <i class="bi bi-x-lg" aria-hidden="true"></i>
                    </button>
                  </div>

                  <!-- Blocker Note if Paused/Blocked -->
                  <div
                    v-if="isRequestPausedOrBlocked(item) && getRequestBlockerNote(item)"
                    class="text-rose-400 bg-rose-500/10 border border-rose-500/20 rounded p-1.5 text-xs font-mono mt-1.5 ms-6"
                    data-testid="request-blocker-note"
                  >
                    <i class="bi bi-exclamation-octagon-fill me-1"></i>
                    <strong>Blocker Note:</strong> {{ getRequestBlockerNote(item) }}
                  </div>
                </li>
              </ul>

              <!-- Historical / Removed Scope Items -->
              <div v-if="historicalScopeItems.length > 0">
                <div class="text-[10.5px] uppercase tracking-wider font-semibold text-slate-400 mb-1">
                  Historical Removed Scope Memberships
                </div>
                <ul class="border border-slate-800 rounded-lg divide-y divide-slate-800 m-0 p-0 list-none text-xs bg-slate-950/40" data-testid="historical-scope-list">
                  <li
                    v-for="hist in historicalScopeItems"
                    :key="hist.id || `${hist.requestId}-${hist.removedAt}`"
                    class="text-slate-400 flex justify-between items-center px-3 py-2"
                  >
                    <div>
                      <router-link
                        :to="`/requests/${hist.requestId}`"
                        class="text-slate-400 hover:text-slate-300 no-underline"
                      >
                        {{ hist.title || hist.requestTitle || hist.requestId }}
                      </router-link>
                      <span class="ms-1 px-1.5 py-0.2 rounded bg-slate-800 text-slate-500 text-[9px]">Removed</span>
                    </div>
                    <span class="font-mono text-[10px]">{{ formatTimestamp(hist.removedAt) }}</span>
                  </li>
                </ul>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.drag-handle {
  cursor: grab;
  user-select: none;
  touch-action: none;
}

.drag-handle:active {
  cursor: grabbing;
}

.scope-request-item.is-dragging {
  opacity: 0.45;
  background-color: rgba(15, 23, 42, 0.95);
}

.scope-request-item.drop-target {
  border-top: 2px solid #22d3ee !important;
  background-color: rgba(34, 211, 238, 0.05);
}

.flow-barcode-strip {
  min-width: 65px;
}

.barcode-bar {
  transition: transform 0.15s ease, opacity 0.15s ease;
  min-width: 3.5px;
}

.barcode-bar:hover {
  transform: scaleY(1.15);
  opacity: 0.85;
}
</style>

