<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { httpClient } from '@/api/http'

/**
 * SCR-WP-001: Work Package Screen
 * (Architecture §7, §8 — UC-WP-001..003, §9 — FEAT-WP-001, §11, §19.4, §19.6, §20, §21).
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
 *     with remove buttons (`DELETE /api/v1/work-packages/${id}/requests/${requestId}`).
 */

export interface WorkPackageScopeItem {
  id: string
  membershipId?: string
  workPackageId: string
  requestId: string
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

function openCreateModal(): void {
  errorMessage.value = null
  successMessage.value = null
  createForm.name = ''
  createForm.objective = ''
  createForm.ownerPersonId = activePersons.value[0]?.id ?? ''
  createForm.customerId = ''
  createForm.productId = ''
  showCreateForm.value = true
}

function closeCreateModal(): void {
  showCreateForm.value = false
}

async function handleCreateWorkPackage(): Promise<void> {
  if (isCreateDisabled.value) {
    return
  }

  isSubmittingCreate.value = true
  errorMessage.value = null
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
    showCreateForm.value = false
    successMessage.value = `Work Package "${created.objective || created.name}" created in ${created.status} status.`

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

watch(
  routeWorkPackageId,
  async (newId) => {
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
  <section class="container-fluid py-2" data-screen-id="SCR-WP-001">
    <!-- Screen Header -->
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-3 mb-4">
      <div>
        <div class="d-flex align-items-center gap-2">
          <h1 class="h3 mb-0 fw-bold">Work Packages</h1>
          <span class="badge text-bg-light border text-secondary">SCR-WP-001</span>
        </div>
        <p class="text-body-secondary small mb-0 mt-1">
          Create and manage work packages, coordinate ownership and lifecycle status, and review grouped operational request scope.
        </p>
      </div>

      <div class="d-flex align-items-center gap-2">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm"
          :disabled="isLoadingList"
          data-testid="refresh-work-packages-button"
          @click="loadWorkPackages"
        >
          <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>
          Refresh
        </button>

        <button
          type="button"
          class="btn btn-primary"
          data-testid="create-work-package-button"
          @click="openCreateModal"
        >
          <i class="bi bi-plus-lg me-1" aria-hidden="true"></i>
          Create Work Package
        </button>
      </div>
    </div>

    <!-- Feedback Alerts -->
    <div
      v-if="errorMessage"
      role="alert"
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center gap-2 mb-4"
      data-testid="work-package-error-alert"
    >
      <i class="bi bi-exclamation-triangle-fill flex-shrink-0" aria-hidden="true"></i>
      <div>{{ errorMessage }}</div>
      <button
        type="button"
        class="btn-close"
        aria-label="Close"
        @click="errorMessage = null"
      ></button>
    </div>

    <div
      v-if="successMessage"
      role="status"
      class="alert alert-success alert-dismissible fade show d-flex align-items-center gap-2 mb-4"
      data-testid="work-package-success-alert"
    >
      <i class="bi bi-check-circle-fill flex-shrink-0" aria-hidden="true"></i>
      <div>{{ successMessage }}</div>
      <button
        type="button"
        class="btn-close"
        aria-label="Close"
        @click="successMessage = null"
      ></button>
    </div>

    <!-- Create Work Package Form / Modal Card -->
    <div
      v-if="showCreateForm"
      class="card shadow-sm border-primary mb-4"
      data-testid="create-work-package-modal"
    >
      <div class="card-header bg-primary text-white d-flex justify-content-between align-items-center">
        <span class="fw-semibold">
          <i class="bi bi-kanban me-2" aria-hidden="true"></i>
          Create New Work Package
        </span>
        <button
          type="button"
          class="btn-close btn-close-white"
          aria-label="Close"
          @click="closeCreateModal"
        ></button>
      </div>

      <div class="card-body">
        <form
          novalidate
          data-testid="create-work-package-form"
          @submit.prevent="handleCreateWorkPackage"
        >
          <div class="row g-3">
            <!-- Objective (Required) -->
            <div class="col-12 col-md-8">
              <label for="createWorkPackageObjective" class="form-label fw-medium">
                Objective <span class="text-danger">*</span>
              </label>
              <textarea
                id="createWorkPackageObjective"
                v-model="createForm.objective"
                class="form-control"
                rows="2"
                placeholder="Describe the shared operational objective for this work package"
                required
                :disabled="isSubmittingCreate"
                data-testid="create-work-package-objective-input"
              ></textarea>
            </div>

            <!-- Optional Short Name / Title -->
            <div class="col-12 col-md-4">
              <label for="createWorkPackageName" class="form-label fw-medium">
                Package Name / Title
              </label>
              <input
                id="createWorkPackageName"
                v-model="createForm.name"
                type="text"
                class="form-control"
                maxlength="255"
                placeholder="Optional short title (defaults to objective)"
                :disabled="isSubmittingCreate"
                data-testid="create-work-package-name-input"
              />
            </div>

            <!-- Owner Select (Required) -->
            <div class="col-12 col-md-4">
              <label for="createWorkPackageOwner" class="form-label fw-medium">
                Owner <span class="text-danger">*</span>
              </label>
              <select
                id="createWorkPackageOwner"
                v-model="createForm.ownerPersonId"
                class="form-select"
                required
                :disabled="isSubmittingCreate"
                data-testid="create-work-package-owner-select"
              >
                <option value="" disabled>Select an active person...</option>
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
              <label for="createWorkPackageCustomer" class="form-label fw-medium">
                Customer
              </label>
              <select
                id="createWorkPackageCustomer"
                v-model="createForm.customerId"
                class="form-select"
                :disabled="isSubmittingCreate"
                data-testid="create-work-package-customer-select"
              >
                <option value="">All / No specific customer (optional)</option>
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
              <label for="createWorkPackageProduct" class="form-label fw-medium">
                Product
              </label>
              <select
                id="createWorkPackageProduct"
                v-model="createForm.productId"
                class="form-select"
                :disabled="isSubmittingCreate"
                data-testid="create-work-package-product-select"
              >
                <option value="">All / No specific product (optional)</option>
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

          <div class="d-flex justify-content-end gap-2 mt-3">
            <button
              type="button"
              class="btn btn-outline-secondary"
              :disabled="isSubmittingCreate"
              @click="closeCreateModal"
            >
              Cancel
            </button>
            <button
              type="submit"
              class="btn btn-primary"
              :disabled="isCreateDisabled"
              data-testid="create-work-package-submit-button"
            >
              <span
                v-if="isSubmittingCreate"
                class="spinner-border spinner-border-sm me-2"
                role="status"
                aria-hidden="true"
              ></span>
              Save Work Package
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Filter Controls Row -->
    <div class="card shadow-sm border-0 mb-3">
      <div class="card-body py-3">
        <div class="row g-3 align-items-end">
          <!-- Status Filter -->
          <div class="col-12 col-sm-6 col-md-3">
            <label for="wpStatusFilter" class="form-label small fw-medium mb-1">
              Status
            </label>
            <select
              id="wpStatusFilter"
              v-model="filters.status"
              class="form-select form-select-sm"
              :disabled="isLoadingList"
              data-testid="status-filter-select"
              @change="handleFilterChange"
            >
              <option value="">All Statuses</option>
              <option v-for="status in WORK_PACKAGE_STATUSES" :key="status" :value="status">
                {{ status }}
              </option>
            </select>
          </div>

          <!-- Owner Filter -->
          <div class="col-12 col-sm-6 col-md-3">
            <label for="wpOwnerFilter" class="form-label small fw-medium mb-1">
              Owner
            </label>
            <select
              id="wpOwnerFilter"
              v-model="filters.ownerPersonId"
              class="form-select form-select-sm"
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
                {{ person.fullName }} ({{ person.email }})
              </option>
            </select>
          </div>

          <!-- Customer Filter -->
          <div class="col-12 col-sm-6 col-md-3">
            <label for="wpCustomerFilter" class="form-label small fw-medium mb-1">
              Customer
            </label>
            <select
              id="wpCustomerFilter"
              v-model="filters.customerId"
              class="form-select form-select-sm"
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
          <div class="col-12 col-sm-6 col-md-3">
            <label for="wpProductFilter" class="form-label small fw-medium mb-1">
              Product
            </label>
            <div class="d-flex align-items-center gap-2">
              <select
                id="wpProductFilter"
                v-model="filters.productId"
                class="form-select form-select-sm"
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

              <button
                v-if="hasActiveFilters"
                type="button"
                class="btn btn-outline-secondary btn-sm text-nowrap"
                :disabled="isLoadingList"
                data-testid="clear-filters-button"
                @click="handleResetFilters"
              >
                <i class="bi bi-x-circle me-1" aria-hidden="true"></i>
                Clear
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Main Layout: Work Packages Table + Detail Panel -->
    <div class="row g-4">
      <!-- Work Packages Table Column -->
      <div :class="selectedWorkPackage || isLoadingDetail ? 'col-12 col-xl-7' : 'col-12'">
        <div class="card shadow-sm border-0">
          <div class="card-body p-0">
            <div class="table-responsive">
              <table
                class="table table-hover align-middle mb-0"
                data-testid="work-packages-table"
              >
                <thead class="table-light">
                  <tr>
                    <th scope="col" class="ps-4">ID</th>
                    <th scope="col">Objective</th>
                    <th scope="col">Owner</th>
                    <th scope="col">Customer</th>
                    <th scope="col">Product</th>
                    <th scope="col" class="pe-4">Status</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-if="isLoadingList">
                    <td colspan="6" class="text-center py-5 text-body-secondary">
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
                      class="text-center py-5 text-body-secondary"
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
                    <td class="ps-4">
                      <router-link
                        :to="`/work-packages/${wp.id}`"
                        class="font-monospace small text-decoration-none"
                        data-testid="work-package-id-link"
                        @click.stop="selectWorkPackage(wp.id)"
                      >
                        {{ wp.id }}
                      </router-link>
                    </td>

                    <!-- Objective Column -->
                    <td>
                      <div class="fw-semibold" data-testid="work-package-objective-cell">
                        {{ wp.objective || wp.name }}
                      </div>
                      <div
                        v-if="wp.name && wp.name !== wp.objective"
                        class="text-body-secondary small"
                      >
                        {{ wp.name }}
                      </div>
                    </td>

                    <!-- Owner Column -->
                    <td>
                      <i class="bi bi-person-badge text-secondary me-1" aria-hidden="true"></i>
                      {{ resolveOwnerDisplay(wp) }}
                    </td>

                    <!-- Customer Column -->
                    <td>
                      {{ resolveCustomerDisplay(wp) }}
                    </td>

                    <!-- Product Column -->
                    <td>
                      {{ resolveProductDisplay(wp) }}
                    </td>

                    <!-- Status Column -->
                    <td class="pe-4">
                      <span
                        class="badge"
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
        <div class="card shadow-sm border-0">
          <div class="card-header bg-body-tertiary d-flex justify-content-between align-items-center py-3">
            <div class="d-flex align-items-center gap-2">
              <i class="bi bi-kanban text-primary" aria-hidden="true"></i>
              <span class="fw-bold">Work Package Detail</span>
              <span
                v-if="selectedWorkPackage"
                class="badge"
                :class="statusBadgeClass(selectedWorkPackage.status)"
                data-testid="detail-status-badge"
              >
                {{ selectedWorkPackage.status }}
              </span>
            </div>

            <button
              type="button"
              class="btn-close"
              aria-label="Close detail panel"
              data-testid="close-detail-panel-button"
              @click="closeDetailPanel"
            ></button>
          </div>

          <div v-if="isLoadingDetail" class="card-body py-5 text-center text-body-secondary">
            <span
              class="spinner-border spinner-border-sm me-2"
              role="status"
              aria-hidden="true"
            ></span>
            Loading work package details...
          </div>

          <div v-else-if="selectedWorkPackage" class="card-body">
            <!-- Summary Metadata -->
            <div class="mb-3 pb-3 border-bottom">
              <div class="small text-body-secondary font-monospace mb-1" data-testid="detail-work-package-id">
                ID: {{ selectedWorkPackage.id }}
              </div>
              <h2 class="h5 fw-bold mb-1" data-testid="detail-objective-display">
                {{ selectedWorkPackage.objective }}
              </h2>
              <div
                v-if="selectedWorkPackage.name && selectedWorkPackage.name !== selectedWorkPackage.objective"
                class="text-body-secondary small mb-2"
              >
                Title: {{ selectedWorkPackage.name }}
              </div>

              <div class="row g-2 small mt-2">
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
                class="alert alert-secondary py-2 px-3 small mt-3 mb-0"
                data-testid="detail-closed-reason"
              >
                <strong>Closed Reason:</strong> {{ selectedWorkPackage.closedReason }}
                <span v-if="selectedWorkPackage.closedAt" class="text-body-secondary ms-1">
                  ({{ formatTimestamp(selectedWorkPackage.closedAt) }})
                </span>
              </div>
            </div>

            <!-- Objective Edit Section (PUT /api/v1/work-packages/${id}/objective) -->
            <div class="mb-4 pb-3 border-bottom" data-testid="detail-objective-section">
              <h3 class="h6 fw-semibold mb-2">
                <i class="bi bi-bullseye me-1 text-primary" aria-hidden="true"></i>
                Objective
              </h3>
              <form novalidate data-testid="update-objective-form" @submit.prevent="handleUpdateObjective">
                <div class="mb-2">
                  <label for="detailObjectiveInput" class="form-label small fw-medium mb-1">
                    Objective Statement
                  </label>
                  <textarea
                    id="detailObjectiveInput"
                    v-model="objectiveForm.objective"
                    class="form-control form-control-sm"
                    rows="2"
                    :disabled="!canModifyPackage || isSubmittingAction"
                    data-testid="detail-objective-input"
                  ></textarea>
                </div>
                <div class="d-flex justify-content-end">
                  <button
                    type="submit"
                    class="btn btn-sm btn-outline-primary"
                    :disabled="!canModifyPackage || isSubmittingAction || !objectiveForm.objective.trim()"
                    data-testid="save-objective-button"
                  >
                    <i class="bi bi-check2 me-1" aria-hidden="true"></i>
                    Update Objective
                  </button>
                </div>
              </form>
            </div>

            <!-- Owner Assignment Section (POST /api/v1/work-packages/${id}/assign-owner) -->
            <div class="mb-4 pb-3 border-bottom" data-testid="detail-owner-section">
              <h3 class="h6 fw-semibold mb-2">
                <i class="bi bi-person-check me-1 text-primary" aria-hidden="true"></i>
                Work Package Owner
              </h3>
              <form
                novalidate
                class="d-flex align-items-center gap-2"
                data-testid="assign-owner-form"
                @submit.prevent="handleAssignOwner"
              >
                <select
                  id="detailOwnerSelect"
                  v-model="assignOwnerForm.newOwnerPersonId"
                  class="form-select form-select-sm"
                  :disabled="!canModifyPackage || isSubmittingAction"
                  data-testid="detail-owner-select"
                >
                  <option value="" disabled>Select owner...</option>
                  <option
                    v-for="person in activePersons"
                    :key="person.id"
                    :value="person.id"
                  >
                    {{ person.fullName }} ({{ person.email }})
                  </option>
                </select>
                <button
                  type="submit"
                  class="btn btn-sm btn-outline-primary text-nowrap"
                  :disabled="!canModifyPackage || isSubmittingAction || !assignOwnerForm.newOwnerPersonId"
                  data-testid="assign-owner-submit-button"
                >
                  Assign Owner
                </button>
              </form>
            </div>

            <!-- Lifecycle Action Buttons (Activate in DRAFT, Close in DRAFT/ACTIVE) -->
            <div class="mb-4 pb-3 border-bottom" data-testid="detail-lifecycle-section">
              <h3 class="h6 fw-semibold mb-2">
                <i class="bi bi-arrow-repeat me-1 text-primary" aria-hidden="true"></i>
                Lifecycle Actions
              </h3>

              <div v-if="isSelectedClosed" class="small text-body-secondary">
                This work package is <strong>CLOSED</strong> and cannot transition to another state.
              </div>

              <div v-else class="d-flex flex-column gap-3">
                <div class="d-flex flex-wrap align-items-center gap-2">
                  <!-- Activate button: visible only in DRAFT -->
                  <button
                    v-if="canActivate"
                    type="button"
                    class="btn btn-success btn-sm"
                    :disabled="isSubmittingAction"
                    data-testid="activate-work-package-button"
                    @click="handleActivateWorkPackage"
                  >
                    <i class="bi bi-play-fill me-1" aria-hidden="true"></i>
                    Activate
                  </button>

                  <!-- Close button: visible in DRAFT and ACTIVE -->
                  <div v-if="canClose" class="d-flex flex-grow-1 align-items-center gap-2">
                    <input
                      v-model="closePackageForm.reason"
                      type="text"
                      class="form-control form-control-sm"
                      placeholder="Close reason (optional)"
                      :disabled="isSubmittingAction"
                      data-testid="close-work-package-reason-input"
                    />
                    <button
                      type="button"
                      class="btn btn-outline-danger btn-sm text-nowrap"
                      :disabled="isSubmittingAction"
                      data-testid="close-work-package-button"
                      @click="handleCloseWorkPackage"
                    >
                      <i class="bi bi-lock-fill me-1" aria-hidden="true"></i>
                      Close
                    </button>
                  </div>
                </div>
              </div>
            </div>

            <!-- Scope Management Section (GET /scope, POST /requests, DELETE /requests/{requestId}) -->
            <div data-testid="detail-scope-section">
              <div class="d-flex justify-content-between align-items-center mb-2">
                <h3 class="h6 fw-semibold mb-0">
                  <i class="bi bi-list-check me-1 text-primary" aria-hidden="true"></i>
                  Scope — Linked Requests
                  <span class="badge text-bg-secondary ms-1" data-testid="active-scope-count">
                    {{ activeScopeItems.length }}
                  </span>
                </h3>

                <button
                  type="button"
                  class="btn btn-link btn-sm p-0 text-decoration-none"
                  :disabled="isLoadingScope"
                  data-testid="refresh-scope-button"
                  @click="loadWorkPackageScope(selectedWorkPackage.id)"
                >
                  <i class="bi bi-arrow-clockwise" aria-hidden="true"></i>
                  Refresh Scope
                </button>
              </div>

              <!-- Add Request Controls -->
              <form
                v-if="canModifyPackage"
                novalidate
                class="card bg-body-tertiary border-0 p-2 mb-3"
                data-testid="add-request-form"
                @submit.prevent="handleAddRequestToScope"
              >
                <label for="addRequestSelect" class="form-label small fw-medium mb-1">
                  Add Request to Scope
                </label>
                <div class="d-flex flex-column gap-2">
                  <select
                    id="addRequestSelect"
                    v-model="addRequestForm.selectedRequestId"
                    class="form-select form-select-sm"
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
                      placeholder="Or enter Request ID (UUID)..."
                      :disabled="isSubmittingAction"
                      data-testid="add-request-id-input"
                    />
                    <button
                      type="submit"
                      class="btn btn-primary"
                      :disabled="isSubmittingAction || !resolvedRequestIdToAdd"
                      data-testid="add-request-button"
                    >
                      <i class="bi bi-plus-circle me-1" aria-hidden="true"></i>
                      Add Request
                    </button>
                  </div>
                </div>
              </form>

              <!-- Linked Requests List -->
              <div v-if="isLoadingScope" class="text-center py-3 text-body-secondary small">
                <span
                  class="spinner-border spinner-border-sm me-2"
                  role="status"
                  aria-hidden="true"
                ></span>
                Loading linked requests...
              </div>

              <div
                v-else-if="activeScopeItems.length === 0"
                class="text-center py-3 text-body-secondary small border rounded"
                data-testid="empty-scope-message"
              >
                No active requests are currently linked to this work package.
              </div>

              <ul
                v-else
                class="list-group list-group-flush border rounded mb-3"
                data-testid="scope-requests-list"
              >
                <li
                  v-for="item in activeScopeItems"
                  :key="item.id || item.requestId"
                  class="list-group-item d-flex justify-content-between align-items-start gap-2"
                  :data-request-id="item.requestId"
                  data-testid="scope-request-item"
                >
                  <div class="me-auto">
                    <div class="d-flex align-items-center gap-2 flex-wrap">
                      <router-link
                        :to="`/requests/${item.requestId}`"
                        class="fw-semibold text-decoration-none"
                        data-testid="scope-request-link"
                      >
                        {{ item.title || item.requestTitle || item.requestId }}
                      </router-link>
                      <span
                        v-if="item.status || item.requestStatus"
                        class="badge"
                        :class="requestStatusBadgeClass(item.status || item.requestStatus)"
                      >
                        {{ item.status || item.requestStatus }}
                      </span>
                    </div>
                    <div class="small text-body-secondary font-monospace">
                      {{ item.requestId }}
                    </div>
                    <div v-if="item.ownerName || item.customerName || item.productName" class="small text-body-secondary">
                      <span v-if="item.ownerName">Owner: {{ item.ownerName }}</span>
                      <span v-if="item.customerName" class="ms-2">Customer: {{ item.customerName }}</span>
                      <span v-if="item.productName" class="ms-2">Product: {{ item.productName }}</span>
                    </div>
                  </div>

                  <button
                    v-if="canModifyPackage"
                    type="button"
                    class="btn btn-outline-danger btn-sm"
                    :disabled="removingRequestId === item.requestId || isSubmittingAction"
                    data-testid="remove-request-button"
                    @click="handleRemoveRequestFromScope(item.requestId)"
                  >
                    <i class="bi bi-x-lg me-1" aria-hidden="true"></i>
                    Remove
                  </button>
                </li>
              </ul>

              <!-- Historical / Removed Scope Items -->
              <div v-if="historicalScopeItems.length > 0" class="mt-3">
                <div class="small fw-semibold text-body-secondary mb-1">
                  Historical (Removed) Scope Memberships
                </div>
                <ul class="list-group list-group-flush border rounded small" data-testid="historical-scope-list">
                  <li
                    v-for="hist in historicalScopeItems"
                    :key="hist.id || `${hist.requestId}-${hist.removedAt}`"
                    class="list-group-item text-body-secondary d-flex justify-content-between align-items-center"
                  >
                    <div>
                      <router-link
                        :to="`/requests/${hist.requestId}`"
                        class="text-decoration-none text-secondary"
                      >
                        {{ hist.title || hist.requestTitle || hist.requestId }}
                      </router-link>
                      <span class="ms-2 badge text-bg-light border text-secondary">Removed</span>
                    </div>
                    <span class="small">{{ formatTimestamp(hist.removedAt) }}</span>
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
