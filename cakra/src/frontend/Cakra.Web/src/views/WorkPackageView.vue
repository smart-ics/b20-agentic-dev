<script lang="ts">
export { normalizeTaskLine, parseTaskListText } from '@/api/requests'
</script>

<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { httpClient } from '@/api/http'
import {
  normalizeTaskLine,
  parseTaskListText,
  recordRequest,
} from '@/api/requests'
import { reorderWorkPackageRequests } from '@/api/workpackages'

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
  ownerPersonId?: string | null
  requestOwnerPersonId?: string | null
  ownerName?: string | null
  requestOwnerName?: string | null
  customerId?: string | null
  customerName?: string | null
  productId?: string | null
  productName?: string | null
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
})
// Quick capture candidate tasks for create modal (CR-019)
const createRawTasks = ref('')
const createCandidateTasks = ref<string[]>([])

// Detail Panel forms
const objectiveForm = reactive({
  name: '',
  objective: '',
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

// Scope bulk quick add state (CR-019)
const scopeAddMode = ref<'existing' | 'quick'>('existing')
const scopeRawTasks = ref('')
const scopeCandidateTasks = ref<string[]>([])
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
      return 'text-bg-secondary'
    case 'ACTIVE':
      return 'text-bg-success'
    case 'CLOSED':
      return 'text-bg-dark'
    default:
      return 'text-bg-secondary'
  }
}

function requestStatusBadgeClass(status: string | null | undefined): string {
  switch ((status ?? '').toUpperCase()) {
    case 'CAPTURED':
      return 'text-bg-secondary'
    case 'EVALUATING':
      return 'text-bg-info'
    case 'ACCEPTED':
    case 'IN_PROGRESS':
      return 'text-bg-primary'
    case 'ESCALATED':
      return 'text-bg-warning'
    case 'COMPLETED':
      return 'text-bg-success'
    case 'REJECTED':
      return 'text-bg-danger'
    default:
      return 'text-bg-secondary'
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

function deriveNameFromObjective(objective: string, explicitName?: string): string {
  if (explicitName && explicitName.trim().length > 0) {
    return explicitName.trim().slice(0, 255)
  }
  return objective.trim().slice(0, 255)
}

function syncDetailForms(wp: WorkPackageItem): void {
  objectiveForm.name = wp.name ?? ''
  objectiveForm.objective = wp.objective ?? ''
  assignOwnerForm.newOwnerPersonId = wp.ownerPersonId ?? ''
  closePackageForm.reason = wp.closedReason ?? ''
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

    const response = await httpClient.get<WorkPackageItem[]>('/work-packages', { params })
    workPackages.value = response.data
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
  const parsed = parseTaskListText(createRawTasks.value).map((line) => normalizeTaskLine(line))
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
  createRawTasks.value = ''
  createCandidateTasks.value = []
  showCreateForm.value = true
}

function closeCreateModal(): void {
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
    })

    const created = response.data

    const tasksToRecord = [...createCandidateTasks.value]
    if (tasksToRecord.length === 0 && createRawTasks.value.trim().length > 0) {
      tasksToRecord.push(...parseTaskListText(createRawTasks.value))
    }

    const unrecordedTitles: string[] = []
    let recordedCount = 0

    if (tasksToRecord.length > 0) {
      const results = await Promise.allSettled(
        tasksToRecord.map((taskTitle) =>
          recordRequest({
            title: taskTitle,
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
          unrecordedTitles.push(tasksToRecord[idx])
        }
      })
    }

    showCreateForm.value = false
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
  const parsed = parseTaskListText(scopeRawTasks.value).map((line) => normalizeTaskLine(line))
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
  const tasksToAdd = [...scopeCandidateTasks.value]
  if (tasksToAdd.length === 0 && scopeRawTasks.value.trim().length > 0) {
    tasksToAdd.push(...parseTaskListText(scopeRawTasks.value))
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
      tasksToAdd.map((title) =>
        recordRequest({
          title,
          description: '',
          customerId: wp.customerId || null,
          productId: wp.productId || null,
          workPackageId: wp.id,
          requestType: 'GENERAL',
          priority: 'NORMAL',
        }),
      ),
    )

    const unrecordedTitles: string[] = []
    let recordedCount = 0

    results.forEach((res, idx) => {
      if (res.status === 'fulfilled') {
        recordedCount++
      } else {
        unrecordedTitles.push(tasksToAdd[idx])
      }
    })

    if (unrecordedTitles.length > 0) {
      scopeCandidateTasks.value = unrecordedTitles
      scopeRawTasks.value = ''
      warningMessage.value = `${recordedCount} task(s) added to scope, but ${unrecordedTitles.length} failed: "${unrecordedTitles.join('", "')}".`
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
  <section class="work-package-view" data-screen-id="SCR-WP-001">
    <!-- Screen Header -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2">
        <h1 class="h6 mb-0 fw-bold">Work Packages</h1>
        <span class="badge text-bg-secondary font-monospace" style="font-size: 11px">SCR-WP-001</span>
        <span class="text-body-secondary small d-none d-md-inline">| Grouped operational demand &amp; lifecycle orchestration</span>
      </div>

      <div class="d-flex align-items-center gap-1">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm py-0 px-2"
          style="font-size: 12px; height: 26px; line-height: 24px"
          :disabled="isLoadingList"
          data-testid="refresh-work-packages-button"
          @click="loadWorkPackages"
        >
          <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>Refresh
        </button>

        <button
          type="button"
          class="btn btn-primary btn-sm py-0 px-2"
          style="font-size: 12px; height: 26px; line-height: 24px"
          data-testid="create-work-package-button"
          @click="openCreateModal"
        >
          <i class="bi bi-plus-lg me-1" aria-hidden="true"></i>New Package
        </button>
      </div>
    </div>

    <!-- Feedback Alerts -->
    <div
      v-if="errorMessage"
      role="alert"
      class="alert alert-danger alert-dismissible py-1 px-2 mb-2 small d-flex align-items-center gap-2"
      data-testid="work-package-error-alert"
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

    <div
      v-if="warningMessage"
      role="alert"
      class="alert alert-warning alert-dismissible py-1 px-2 mb-2 small d-flex align-items-center gap-2"
      data-testid="work-package-warning-alert"
    >
      <i class="bi bi-exclamation-circle-fill flex-shrink-0" aria-hidden="true"></i>
      <div>{{ warningMessage }}</div>
      <button
        type="button"
        class="btn-close py-1 px-2"
        aria-label="Close"
        @click="warningMessage = null"
      ></button>
    </div>

    <div
      v-if="successMessage"
      role="status"
      class="alert alert-success alert-dismissible py-1 px-2 mb-2 small d-flex align-items-center gap-2"
      data-testid="work-package-success-alert"
    >
      <i class="bi bi-check-circle-fill flex-shrink-0" aria-hidden="true"></i>
      <div>{{ successMessage }}</div>
      <button
        type="button"
        class="btn-close py-1 px-2"
        aria-label="Close"
        @click="successMessage = null"
      ></button>
    </div>

    <!-- Create Work Package Form Card (Collapsible) -->
    <div
      v-if="showCreateForm"
      class="card shadow-none border border-primary mb-2"
      data-testid="create-work-package-modal"
    >
      <div class="card-header py-1 px-2 bg-primary text-white d-flex justify-content-between align-items-center">
        <span class="fw-semibold small">
          <i class="bi bi-kanban me-1" aria-hidden="true"></i>Create New Work Package
        </span>
        <button
          type="button"
          class="btn-close btn-close-white py-1 px-2"
          aria-label="Close"
          @click="closeCreateModal"
        ></button>
      </div>

      <div class="card-body p-2">
        <form
          novalidate
          data-testid="create-work-package-form"
          @submit.prevent="handleCreateWorkPackage"
        >
          <div class="row g-2">
            <!-- Objective (Required) -->
            <div class="col-12 col-md-8">
              <label for="createWorkPackageObjective" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Objective <span class="text-danger">*</span>
              </label>
              <input
                id="createWorkPackageObjective"
                v-model="createForm.objective"
                type="text"
                class="form-control form-control-sm"
                placeholder="Describe shared operational objective..."
                required
                :disabled="isSubmittingCreate"
                data-testid="create-work-package-objective-input"
              />
            </div>

            <!-- Optional Short Name / Title -->
            <div class="col-12 col-md-4">
              <label for="createWorkPackageName" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Package Name / Title
              </label>
              <input
                id="createWorkPackageName"
                v-model="createForm.name"
                type="text"
                class="form-control form-control-sm"
                maxlength="255"
                placeholder="Optional short title"
                :disabled="isSubmittingCreate"
                data-testid="create-work-package-name-input"
              />
            </div>

            <!-- Owner Select (Required) -->
            <div class="col-12 col-md-4">
              <label for="createWorkPackageOwner" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Owner <span class="text-danger">*</span>
              </label>
              <select
                id="createWorkPackageOwner"
                v-model="createForm.ownerPersonId"
                class="form-select form-select-sm"
                required
                :disabled="isSubmittingCreate"
                data-testid="create-work-package-owner-select"
              >
                <option value="" disabled>Select active person...</option>
                <option
                  v-for="person in activePersons"
                  :key="person.id"
                  :value="person.id"
                >
                  {{ person.fullName }} ({{ person.email }})
                </option>
              </select>
            </div>

            <!-- Customer Select (Optional) -->
            <div class="col-12 col-md-4">
              <label for="createWorkPackageCustomer" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Customer (Optional)
              </label>
              <select
                id="createWorkPackageCustomer"
                v-model="createForm.customerId"
                class="form-select form-select-sm"
                :disabled="isSubmittingCreate"
                data-testid="create-work-package-customer-select"
              >
                <option value="">All / No specific customer</option>
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
            <div class="col-12 col-md-4">
              <label for="createWorkPackageProduct" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Product (Optional)
              </label>
              <select
                id="createWorkPackageProduct"
                v-model="createForm.productId"
                class="form-select form-select-sm"
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
          </div>

          <!-- Quick Capture Tasks (Optional) (CR-019) -->
          <div class="mt-2 pt-2 border-top" data-testid="create-quick-capture-section">
            <div class="d-flex justify-content-between align-items-center mb-1">
              <label for="createQuickTasksInput" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                <i class="bi bi-lightning-charge me-1 text-warning"></i>Quick Capture Tasks (Optional)
              </label>
              <div class="d-flex align-items-center gap-1">
                <span
                  v-if="createCandidateTasks.length > 0"
                  class="badge text-bg-primary"
                  style="font-size: 10px"
                  data-testid="create-candidate-count-badge"
                >
                  {{ createCandidateTasks.length }} parsed
                </span>
                <button
                  v-if="createCandidateTasks.length > 0"
                  type="button"
                  class="btn btn-link btn-sm p-0 text-decoration-none small text-danger"
                  style="font-size: 10.5px"
                  :disabled="isSubmittingCreate"
                  data-testid="create-clear-candidates-button"
                  @click="handleClearCreateCandidates"
                >
                  Clear Parsed
                </button>
              </div>
            </div>

            <div class="mb-1">
              <textarea
                id="createQuickTasksInput"
                v-model="createRawTasks"
                class="form-control form-control-sm font-monospace"
                style="font-size: 11.5px; resize: vertical"
                rows="3"
                placeholder="Paste or type task list (bullets, numbers, markdown checklists)&#10;- Setup database schema&#10;- Configure API endpoints&#10;- Build user interface"
                :disabled="isSubmittingCreate"
                data-testid="create-quick-tasks-textarea"
              ></textarea>
            </div>

            <div class="d-flex justify-content-between align-items-center mb-2">
              <span class="text-body-secondary small" style="font-size: 10.5px">
                One task per line. Bullets, numbers, and checkboxes are automatically cleaned.
              </span>
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm py-0 px-2"
                style="font-size: 11px; height: 24px; line-height: 22px"
                :disabled="isSubmittingCreate || !createRawTasks.trim()"
                data-testid="create-parse-tasks-button"
                @click="handleParseCreateTasks"
              >
                <i class="bi bi-arrow-down-circle me-1"></i>Parse Tasks
              </button>
            </div>

            <!-- Candidate Tasks Preview List -->
            <div
              v-if="createCandidateTasks.length > 0"
              class="border rounded p-1 bg-body-tertiary mb-1"
              data-testid="create-candidate-preview-container"
            >
              <div class="small fw-semibold text-body-secondary mb-1 px-1" style="font-size: 10.5px">
                Candidate Tasks to Create:
              </div>
              <ul class="list-group list-group-flush small" style="max-height: 150px; overflow-y: auto" data-testid="create-candidate-tasks-list">
                <li
                  v-for="(task, idx) in createCandidateTasks"
                  :key="idx"
                  class="list-group-item d-flex justify-content-between align-items-center py-1 px-2 bg-transparent"
                  data-testid="create-candidate-task-item"
                >
                  <div class="d-flex align-items-center text-truncate me-2">
                    <span class="badge text-bg-light border text-secondary me-2 font-monospace" style="font-size: 9px">
                      #{{ idx + 1 }}
                    </span>
                    <span class="text-truncate" style="font-size: 11.5px">{{ task }}</span>
                  </div>
                  <button
                    type="button"
                    class="btn btn-outline-danger btn-xs py-0 px-1"
                    style="font-size: 10px; height: 20px; line-height: 18px"
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

          <div class="d-flex justify-content-end gap-1 mt-2 pt-2 border-top">
            <button
              type="button"
              class="btn btn-outline-secondary btn-sm"
              :disabled="isSubmittingCreate"
              @click="closeCreateModal"
            >
              Cancel
            </button>
            <button
              type="submit"
              class="btn btn-primary btn-sm"
              :disabled="isCreateDisabled"
              data-testid="create-work-package-submit-button"
            >
              <span
                v-if="isSubmittingCreate"
                class="spinner-border spinner-border-sm me-1"
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
    <div class="op-toolbar mb-2">
      <div class="d-flex flex-wrap align-items-center gap-1 w-100">
        <!-- Status Filter -->
        <div class="d-flex align-items-center gap-1">
          <label for="wpStatusFilter" class="small text-body-secondary mb-0 fw-medium text-nowrap" style="font-size: 11px">Status:</label>
          <select
            id="wpStatusFilter"
            v-model="filters.status"
            class="form-select form-select-sm py-0"
            style="width: 110px; font-size: 12px; height: 26px"
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
        <div class="d-flex align-items-center gap-1">
          <label for="wpOwnerFilter" class="small text-body-secondary mb-0 fw-medium text-nowrap" style="font-size: 11px">Owner:</label>
          <select
            id="wpOwnerFilter"
            v-model="filters.ownerPersonId"
            class="form-select form-select-sm py-0"
            style="max-width: 180px; font-size: 12px; height: 26px"
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
        <div class="d-flex align-items-center gap-1">
          <label for="wpCustomerFilter" class="small text-body-secondary mb-0 fw-medium text-nowrap" style="font-size: 11px">Customer:</label>
          <select
            id="wpCustomerFilter"
            v-model="filters.customerId"
            class="form-select form-select-sm py-0"
            style="max-width: 180px; font-size: 12px; height: 26px"
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
        <div class="d-flex align-items-center gap-1">
          <label for="wpProductFilter" class="small text-body-secondary mb-0 fw-medium text-nowrap" style="font-size: 11px">Product:</label>
          <select
            id="wpProductFilter"
            v-model="filters.productId"
            class="form-select form-select-sm py-0"
            style="max-width: 170px; font-size: 12px; height: 26px"
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

        <button
          v-if="hasActiveFilters"
          type="button"
          class="btn btn-outline-secondary btn-sm py-0 px-2 ms-auto"
          style="font-size: 11px; height: 26px; line-height: 24px"
          :disabled="isLoadingList"
          data-testid="clear-filters-button"
          @click="handleResetFilters"
        >
          <i class="bi bi-x-circle me-1" aria-hidden="true"></i>Reset
        </button>
      </div>
    </div>

    <!-- Main Layout: Work Packages Table + Detail Panel -->
    <div class="row g-2">
      <!-- Work Packages Table Column -->
      <div :class="selectedWorkPackage || isLoadingDetail ? 'col-12 col-xl-7' : 'col-12'">
        <div class="card card-table shadow-none border mb-2">
          <div class="card-body p-0">
            <div class="table-responsive">
              <table
                class="table table-hover align-middle mb-0 text-nowrap"
                data-testid="work-packages-table"
              >
                <thead class="table-light">
                  <tr>
                    <th scope="col" style="width: 100px">ID</th>
                    <th scope="col" style="min-width: 200px">Objective</th>
                    <th scope="col">Owner</th>
                    <th scope="col">Customer</th>
                    <th scope="col">Product</th>
                    <th scope="col" class="text-center" style="width: 80px">Status</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-if="isLoadingList">
                    <td colspan="6" class="text-center py-4 text-body-secondary small">
                      <span
                        class="spinner-border spinner-border-sm me-2"
                        role="status"
                        aria-hidden="true"
                      ></span>
                      Loading work packages...
                    </td>
                  </tr>

                  <tr v-else-if="workPackages.length === 0">
                    <td
                      colspan="6"
                      class="text-center py-4 text-body-secondary small"
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
                    :class="{ 'table-active': selectedWorkPackage?.id === wp.id }"
                    style="cursor: pointer"
                    data-testid="work-package-row"
                    @click="selectWorkPackage(wp.id)"
                  >
                    <!-- ID Column -->
                    <td>
                      <router-link
                        :to="`/work-packages/${wp.id}`"
                        class="font-monospace text-decoration-none small"
                        style="font-size: 11px"
                        data-testid="work-package-id-link"
                        @click.stop="selectWorkPackage(wp.id)"
                      >
                        {{ wp.id.slice(0, 8) }}
                      </router-link>
                    </td>

                    <!-- Objective Column -->
                    <td>
                      <div class="fw-medium text-truncate d-inline-block" style="max-width: 260px" data-testid="work-package-objective-cell">
                        {{ wp.objective || wp.name }}
                      </div>
                      <div
                        v-if="wp.name && wp.name !== wp.objective"
                        class="text-body-secondary font-monospace"
                        style="font-size: 10.5px"
                      >
                        {{ wp.name }}
                      </div>
                    </td>

                    <!-- Owner Column -->
                    <td>
                      <span class="small">{{ resolveOwnerDisplay(wp) }}</span>
                    </td>

                    <!-- Customer Column -->
                    <td>
                      <span class="small">{{ resolveCustomerDisplay(wp) }}</span>
                    </td>

                    <!-- Product Column -->
                    <td>
                      <span class="small">{{ resolveProductDisplay(wp) }}</span>
                    </td>

                    <!-- Status Column -->
                    <td class="text-center">
                      <span
                        class="badge"
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
          </div>
        </div>
      </div>

      <!-- Work Package Detail & Scope Panel -->
      <div
        v-if="selectedWorkPackage || isLoadingDetail"
        class="col-12 col-xl-5"
        data-testid="work-package-detail-panel"
      >
        <div class="card shadow-none border mb-2">
          <div class="card-header bg-body-tertiary d-flex justify-content-between align-items-center py-1 px-2">
            <div class="d-flex align-items-center gap-1">
              <i class="bi bi-kanban text-primary" aria-hidden="true"></i>
              <span class="fw-bold small">Work Package Detail</span>
              <span
                v-if="selectedWorkPackage"
                class="badge ms-1"
                style="font-size: 10px; padding: 2px 6px"
                :class="statusBadgeClass(selectedWorkPackage.status)"
                data-testid="detail-status-badge"
              >
                {{ selectedWorkPackage.status }}
              </span>
            </div>

            <button
              type="button"
              class="btn-close py-1 px-2"
              aria-label="Close detail panel"
              data-testid="close-detail-panel-button"
              @click="closeDetailPanel"
            ></button>
          </div>

          <div v-if="isLoadingDetail" class="card-body py-4 text-center text-body-secondary small">
            <span
              class="spinner-border spinner-border-sm me-1"
              role="status"
              aria-hidden="true"
            ></span>
            Loading details...
          </div>

          <div v-else-if="selectedWorkPackage" class="card-body p-2">
            <!-- Summary Metadata -->
            <div class="mb-2 pb-2 border-bottom">
              <div class="d-flex justify-content-between align-items-start gap-1">
                <h2 class="h6 fw-bold mb-1" data-testid="detail-objective-display">
                  {{ selectedWorkPackage.objective }}
                </h2>
                <span class="font-monospace text-body-secondary small flex-shrink-0" style="font-size: 10.5px" data-testid="detail-work-package-id">
                  {{ selectedWorkPackage.id }}
                </span>
              </div>
              <div
                v-if="selectedWorkPackage.name && selectedWorkPackage.name !== selectedWorkPackage.objective"
                class="text-body-secondary small mb-1"
                style="font-size: 11.5px"
              >
                Title: {{ selectedWorkPackage.name }}
              </div>

              <div class="row g-1 small" style="font-size: 11.5px">
                <div class="col-6">
                  <span class="text-body-secondary">Owner:</span>
                  <strong class="ms-1" data-testid="detail-owner-display">
                    {{ resolveOwnerDisplay(selectedWorkPackage) }}
                  </strong>
                </div>
                <div class="col-6">
                  <span class="text-body-secondary">Created:</span>
                  <span class="ms-1">{{ formatTimestamp(selectedWorkPackage.createdAt) }}</span>
                </div>
                <div class="col-6">
                  <span class="text-body-secondary">Customer:</span>
                  <span class="ms-1">{{ resolveCustomerDisplay(selectedWorkPackage) }}</span>
                </div>
                <div class="col-6">
                  <span class="text-body-secondary">Product:</span>
                  <span class="ms-1">{{ resolveProductDisplay(selectedWorkPackage) }}</span>
                </div>
              </div>

              <div
                v-if="isSelectedClosed && selectedWorkPackage.closedReason"
                class="alert alert-secondary py-1 px-2 small mt-1 mb-0"
                style="font-size: 11px"
                data-testid="detail-closed-reason"
              >
                <strong>Closed Reason:</strong> {{ selectedWorkPackage.closedReason }}
                <span v-if="selectedWorkPackage.closedAt" class="text-body-secondary ms-1">
                  ({{ formatTimestamp(selectedWorkPackage.closedAt) }})
                </span>
              </div>
            </div>

            <!-- Objective Edit Section (PUT /api/v1/work-packages/${id}/objective) -->
            <div class="mb-2 pb-2 border-bottom" data-testid="detail-objective-section">
              <form novalidate data-testid="update-objective-form" @submit.prevent="handleUpdateObjective">
                <div class="d-flex align-items-center gap-1 mb-1">
                  <label for="detailObjectiveInput" class="small fw-semibold text-body-secondary mb-0" style="font-size: 11px">
                    <i class="bi bi-bullseye me-1 text-primary"></i>Objective:
                  </label>
                  <button
                    type="submit"
                    class="btn btn-xs btn-outline-primary ms-auto py-0 px-2"
                    style="font-size: 11px; height: 22px; line-height: 20px"
                    :disabled="!canModifyPackage || isSubmittingAction || !objectiveForm.objective.trim()"
                    data-testid="save-objective-button"
                  >
                    Update
                  </button>
                </div>
                <textarea
                  id="detailObjectiveInput"
                  v-model="objectiveForm.objective"
                  class="form-control form-control-sm"
                  rows="2"
                  :disabled="!canModifyPackage || isSubmittingAction"
                  data-testid="detail-objective-input"
                ></textarea>
              </form>
            </div>

            <!-- Owner Assignment Section (POST /api/v1/work-packages/${id}/assign-owner) -->
            <div class="mb-2 pb-2 border-bottom" data-testid="detail-owner-section">
              <form
                novalidate
                class="d-flex align-items-center gap-1"
                data-testid="assign-owner-form"
                @submit.prevent="handleAssignOwner"
              >
                <label for="detailOwnerSelect" class="small fw-semibold text-body-secondary mb-0 text-nowrap" style="font-size: 11px">
                  <i class="bi bi-person-check me-1 text-primary"></i>Owner:
                </label>
                <select
                  id="detailOwnerSelect"
                  v-model="assignOwnerForm.newOwnerPersonId"
                  class="form-select form-select-sm"
                  style="font-size: 12px; height: 26px"
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
                  class="btn btn-sm btn-outline-primary text-nowrap py-0 px-2"
                  style="font-size: 11px; height: 26px; line-height: 24px"
                  :disabled="!canModifyPackage || isSubmittingAction || !assignOwnerForm.newOwnerPersonId"
                  data-testid="assign-owner-submit-button"
                >
                  Reassign
                </button>
              </form>
            </div>

            <!-- Lifecycle Action Buttons (Activate in DRAFT, Close in DRAFT/ACTIVE) -->
            <div class="mb-2 pb-2 border-bottom" data-testid="detail-lifecycle-section">
              <div v-if="isSelectedClosed" class="small text-body-secondary" style="font-size: 11px">
                Work package is <strong>CLOSED</strong> (terminal state).
              </div>

              <div v-else class="d-flex flex-wrap align-items-center gap-1">
                <!-- Activate button: visible only in DRAFT -->
                <button
                  v-if="canActivate"
                  type="button"
                  class="btn btn-success btn-sm py-0 px-2"
                  style="font-size: 11px; height: 26px; line-height: 24px"
                  :disabled="isSubmittingAction"
                  data-testid="activate-work-package-button"
                  @click="handleActivateWorkPackage"
                >
                  <i class="bi bi-play-fill me-1" aria-hidden="true"></i>Activate
                </button>

                <!-- Close button: visible in DRAFT and ACTIVE -->
                <div v-if="canClose" class="d-flex flex-grow-1 align-items-center gap-1">
                  <input
                    v-model="closePackageForm.reason"
                    type="text"
                    class="form-control form-control-sm"
                    style="font-size: 12px; height: 26px"
                    placeholder="Close reason (optional)"
                    :disabled="isSubmittingAction"
                    data-testid="close-work-package-reason-input"
                  />
                  <button
                    type="button"
                    class="btn btn-outline-danger btn-sm text-nowrap py-0 px-2"
                    style="font-size: 11px; height: 26px; line-height: 24px"
                    :disabled="isSubmittingAction"
                    data-testid="close-work-package-button"
                    @click="handleCloseWorkPackage"
                  >
                    <i class="bi bi-lock-fill me-1" aria-hidden="true"></i>Close
                  </button>
                </div>
              </div>
            </div>

            <!-- Scope Management Section (GET /scope, POST /requests, DELETE /requests/{requestId}) -->
            <div data-testid="detail-scope-section">
              <div class="d-flex justify-content-between align-items-center mb-1">
                <span class="small fw-semibold text-body-secondary" style="font-size: 11px">
                  <i class="bi bi-list-check me-1 text-primary" aria-hidden="true"></i>
                  Scope (Linked Requests)
                  <span class="badge text-bg-secondary ms-1" style="font-size: 10px" data-testid="active-scope-count">
                    {{ activeScopeItems.length }}
                  </span>
                </span>

                <button
                  type="button"
                  class="btn btn-link btn-sm p-0 text-decoration-none small"
                  style="font-size: 11px"
                  :disabled="isLoadingScope"
                  data-testid="refresh-scope-button"
                  @click="loadWorkPackageScope(selectedWorkPackage.id)"
                >
                  <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>Refresh
                </button>
              </div>

              <!-- Scope Add Controls Toggle Pill (CR-019) -->
              <div v-if="canModifyPackage" class="d-flex align-items-center mb-1" data-testid="scope-add-mode-toggle">
                <div class="btn-group btn-group-sm w-100" role="group">
                  <button
                    type="button"
                    class="btn py-0"
                    :class="scopeAddMode === 'existing' ? 'btn-primary' : 'btn-outline-secondary'"
                    style="font-size: 11px; height: 24px; line-height: 22px"
                    data-testid="scope-mode-existing-button"
                    @click="scopeAddMode = 'existing'"
                  >
                    Existing Request
                  </button>
                  <button
                    type="button"
                    class="btn py-0"
                    :class="scopeAddMode === 'quick' ? 'btn-primary' : 'btn-outline-secondary'"
                    style="font-size: 11px; height: 24px; line-height: 22px"
                    data-testid="scope-mode-quick-button"
                    @click="scopeAddMode = 'quick'"
                  >
                    ⚡ Quick Bulk Add
                  </button>
                </div>
              </div>

              <!-- Existing Request Form -->
              <form
                v-if="canModifyPackage && scopeAddMode === 'existing'"
                novalidate
                class="p-1 mb-1 rounded bg-body-tertiary border"
                data-testid="add-request-form"
                @submit.prevent="handleAddRequestToScope"
              >
                <div class="d-flex flex-column gap-1">
                  <select
                    id="addRequestSelect"
                    v-model="addRequestForm.selectedRequestId"
                    class="form-select form-select-sm"
                    style="font-size: 11.5px; height: 26px"
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

                  <div class="input-group input-group-sm">
                    <input
                      v-model="addRequestForm.manualRequestId"
                      type="text"
                      class="form-control"
                      style="font-size: 11.5px; height: 26px"
                      placeholder="Or enter Request ID (UUID)..."
                      :disabled="isSubmittingAction"
                      data-testid="add-request-id-input"
                    />
                    <button
                      type="submit"
                      class="btn btn-primary btn-sm py-0 px-2"
                      style="font-size: 11px; height: 26px; line-height: 24px"
                      :disabled="isSubmittingAction || !resolvedRequestIdToAdd"
                      data-testid="add-request-button"
                    >
                      <i class="bi bi-plus-circle me-1" aria-hidden="true"></i>Add
                    </button>
                  </div>
                </div>
              </form>

              <!-- Quick Bulk Add Form (CR-019) -->
              <div
                v-if="canModifyPackage && scopeAddMode === 'quick'"
                class="p-2 mb-2 rounded bg-body-tertiary border"
                data-testid="scope-quick-bulk-form"
              >
                <div class="d-flex justify-content-between align-items-center mb-1">
                  <label for="scopeQuickTasksInput" class="small fw-semibold text-body-secondary mb-0" style="font-size: 11px">
                    <i class="bi bi-lightning-charge-fill me-1 text-warning"></i>Bulk Quick Add Tasks
                  </label>
                  <div class="d-flex align-items-center gap-1">
                    <span
                      v-if="scopeCandidateTasks.length > 0"
                      class="badge text-bg-primary"
                      style="font-size: 10px"
                      data-testid="scope-candidate-count-badge"
                    >
                      {{ scopeCandidateTasks.length }} parsed
                    </span>
                    <button
                      v-if="scopeCandidateTasks.length > 0"
                      type="button"
                      class="btn btn-link btn-sm p-0 text-decoration-none small text-danger"
                      style="font-size: 10.5px"
                      :disabled="isSubmittingScopeBulk"
                      data-testid="scope-clear-candidates-button"
                      @click="handleClearScopeCandidates"
                    >
                      Clear Parsed
                    </button>
                  </div>
                </div>

                <div class="mb-1">
                  <textarea
                    id="scopeQuickTasksInput"
                    v-model="scopeRawTasks"
                    class="form-control form-control-sm font-monospace"
                    style="font-size: 11.5px; resize: vertical"
                    rows="3"
                    placeholder="Paste or type task list (bullets, numbers, markdown checklists)&#10;- Sub-system integration&#10;- Validation checks"
                    :disabled="isSubmittingScopeBulk"
                    data-testid="scope-quick-tasks-textarea"
                  ></textarea>
                </div>

                <div class="d-flex justify-content-between align-items-center mb-2">
                  <span class="text-body-secondary small" style="font-size: 10.5px">
                    One task per line. Bullets, numbers, and checkboxes are cleaned.
                  </span>
                  <button
                    type="button"
                    class="btn btn-outline-secondary btn-sm py-0 px-2"
                    style="font-size: 11px; height: 24px; line-height: 22px"
                    :disabled="isSubmittingScopeBulk || !scopeRawTasks.trim()"
                    data-testid="scope-parse-tasks-button"
                    @click="handleParseScopeTasks"
                  >
                    <i class="bi bi-arrow-down-circle me-1"></i>Parse Tasks
                  </button>
                </div>

                <!-- Scope Candidate Tasks Preview List -->
                <div
                  v-if="scopeCandidateTasks.length > 0"
                  class="border rounded p-1 bg-white mb-2"
                  data-testid="scope-candidate-preview-container"
                >
                  <div class="small fw-semibold text-body-secondary mb-1 px-1" style="font-size: 10.5px">
                    Candidate Tasks to Add to Scope:
                  </div>
                  <ul class="list-group list-group-flush small" style="max-height: 150px; overflow-y: auto" data-testid="scope-candidate-tasks-list">
                    <li
                      v-for="(task, idx) in scopeCandidateTasks"
                      :key="idx"
                      class="list-group-item d-flex justify-content-between align-items-center py-1 px-2"
                      data-testid="scope-candidate-task-item"
                    >
                      <div class="d-flex align-items-center text-truncate me-2">
                        <span class="badge text-bg-light border text-secondary me-2 font-monospace" style="font-size: 9px">
                          #{{ idx + 1 }}
                        </span>
                        <span class="text-truncate" style="font-size: 11.5px">{{ task }}</span>
                      </div>
                      <button
                        type="button"
                        class="btn btn-outline-danger btn-xs py-0 px-1"
                        style="font-size: 10px; height: 20px; line-height: 18px"
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

                <div class="d-flex justify-content-end">
                  <button
                    type="button"
                    class="btn btn-primary btn-sm py-0 px-2"
                    style="font-size: 11px; height: 26px; line-height: 24px"
                    :disabled="isSubmittingScopeBulk || (scopeCandidateTasks.length === 0 && !scopeRawTasks.trim())"
                    data-testid="scope-submit-bulk-button"
                    @click="handleBulkAddTasksToScope"
                  >
                    <span
                      v-if="isSubmittingScopeBulk"
                      class="spinner-border spinner-border-sm me-1"
                      role="status"
                      aria-hidden="true"
                    ></span>
                    <i v-else class="bi bi-plus-circle me-1" aria-hidden="true"></i>
                    Add Tasks to Scope
                  </button>
                </div>
              </div>

              <!-- Linked Requests List -->
              <div v-if="isLoadingScope" class="text-center py-2 text-body-secondary small">
                <span
                  class="spinner-border spinner-border-sm me-1"
                  role="status"
                  aria-hidden="true"
                ></span>
                Loading linked requests...
              </div>

              <div
                v-else-if="activeScopeItems.length === 0"
                class="text-center py-2 text-body-secondary small border rounded"
                style="font-size: 11.5px"
                data-testid="empty-scope-message"
              >
                No active requests currently linked to this package.
              </div>

              <ul
                v-else
                class="list-group list-group-flush border rounded mb-2"
                data-testid="scope-requests-list"
              >
                <li
                  v-for="(item, index) in activeScopeItems"
                  :key="item.id || item.requestId"
                  class="list-group-item d-flex justify-content-between align-items-center gap-1 px-2 py-1 scope-request-item"
                  :class="{
                    'is-dragging': draggedIndex === index,
                    'drop-target': dropTargetIndex === index && draggedIndex !== index,
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
                  <div class="d-flex align-items-center me-auto text-truncate" style="max-width: 78%">
                    <span
                      v-if="canModifyPackage"
                      class="drag-handle text-body-secondary me-2 flex-shrink-0"
                      title="Drag to reorder"
                      aria-label="Drag to reorder"
                      data-testid="drag-handle"
                    >
                      <i class="bi bi-grip-vertical" aria-hidden="true"></i>
                    </span>
                    <div class="text-truncate">
                      <div class="d-flex align-items-center gap-1 flex-wrap">
                        <router-link
                          :to="`/requests/${item.requestId}`"
                          class="fw-medium text-decoration-none small text-truncate"
                          style="font-size: 12px; max-width: 220px"
                          data-testid="scope-request-link"
                        >
                          {{ item.title || item.requestTitle || item.requestId }}
                        </router-link>
                        <span
                          v-if="item.status || item.requestStatus"
                          class="badge"
                          style="font-size: 9.5px; padding: 2px 4px"
                          :class="requestStatusBadgeClass(item.status || item.requestStatus)"
                        >
                          {{ item.status || item.requestStatus }}
                        </span>
                      </div>
                      <div class="text-body-secondary font-monospace" style="font-size: 10px">
                        {{ item.requestId }}
                      </div>
                    </div>
                  </div>

                  <button
                    v-if="canModifyPackage"
                    type="button"
                    class="btn btn-outline-danger btn-xs py-0 px-1"
                    style="font-size: 10px; height: 22px; line-height: 20px"
                    :disabled="removingRequestId === item.requestId || isSubmittingAction || isReordering"
                    data-testid="remove-request-button"
                    @click="handleRemoveRequestFromScope(item.requestId)"
                  >
                    <i class="bi bi-x-lg" aria-hidden="true"></i>
                  </button>
                </li>
              </ul>

              <!-- Historical / Removed Scope Items -->
              <div v-if="historicalScopeItems.length > 0">
                <div class="small fw-semibold text-body-secondary mb-1" style="font-size: 10.5px">
                  Historical Removed Scope Memberships
                </div>
                <ul class="list-group list-group-flush border rounded small" data-testid="historical-scope-list">
                  <li
                    v-for="hist in historicalScopeItems"
                    :key="hist.id || `${hist.requestId}-${hist.removedAt}`"
                    class="list-group-item text-body-secondary d-flex justify-content-between align-items-center px-2 py-1"
                    style="font-size: 11px"
                  >
                    <div>
                      <router-link
                        :to="`/requests/${hist.requestId}`"
                        class="text-decoration-none text-secondary"
                      >
                        {{ hist.title || hist.requestTitle || hist.requestId }}
                      </router-link>
                      <span class="ms-1 badge text-bg-light border text-secondary" style="font-size: 9px">Removed</span>
                    </div>
                    <span class="font-monospace" style="font-size: 10px">{{ formatTimestamp(hist.removedAt) }}</span>
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
  background-color: var(--bs-tertiary-bg, #f8f9fa);
}

.scope-request-item.drop-target {
  border-top: 2px solid var(--bs-primary, #0d6efd) !important;
  background-color: rgba(13, 110, 253, 0.05);
}
</style>
