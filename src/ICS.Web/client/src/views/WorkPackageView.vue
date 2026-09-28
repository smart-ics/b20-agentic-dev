<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { apiClient } from '@/services/api'

interface WorkPackage {
  id: string
  name: string
  objective: string
  status: string
  isDraft: boolean
  isActive: boolean
  isClosed: boolean
  ownerPersonId: string
  ownerName?: string
  customerId?: string
  customerName?: string
  productId?: string
  productName?: string
  createdAt: string
  updatedAt?: string
  closedAt?: string
  closeReason?: string
  activeRequestCount: number
  requests: WorkPackageRequestMembership[]
}

interface WorkPackageRequestMembership {
  membershipId: string
  workPackageId: string
  requestId: string
  addedAt: string
  removedAt?: string
  isActive: boolean
}

interface Person {
  personId: string
  name: string
  email: string
  status: string
}

interface Customer {
  customerId: string
  customerCode: string
  customerName: string
  status: string
}

interface Product {
  productId: string
  code: string
  name: string
  status: string
  ownerPersonId: string
}

// State
const workPackages = ref<WorkPackage[]>([])
const eligibleOwners = ref<Person[]>([])
const eligibleCustomers = ref<Customer[]>([])
const eligibleProducts = ref<Product[]>([])
const isLoading = ref(false)
const isSaving = ref(false)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)

// Filtering & Search
const searchQuery = ref('')
const statusFilter = ref<'ALL' | 'DRAFT' | 'ACTIVE' | 'CLOSED'>('ALL')
const ownerFilter = ref('')
const customerFilter = ref('')
const productFilter = ref('')

// Modals
const showCreateModal = ref(false)
const showEditModal = ref(false)
const showOwnerModal = ref(false)
const showScopeModal = ref(false)

// Forms
interface CreateForm {
  name: string
  objective: string
  ownerPersonId: string
  customerId: string
  productId: string
}

const createForm = ref<CreateForm>({
  name: '',
  objective: '',
  ownerPersonId: '',
  customerId: '',
  productId: ''
})

const editForm = ref({
  id: '',
  name: '',
  objective: ''
})

const ownerForm = ref({
  workPackageId: '',
  workPackageName: '',
  workPackageCode: '',
  currentOwnerName: '',
  currentOwnerPersonId: '',
  newOwnerPersonId: ''
})

const scopeForm = ref({
  workPackageId: ''
})

// Computed
const filteredWorkPackages = computed(() => {
  return workPackages.value.filter(wp => {
    // Status filter
    if (statusFilter.value !== 'ALL' && wp.status !== statusFilter.value) {
      return false
    }

    // Owner filter
    if (ownerFilter.value && wp.ownerPersonId !== ownerFilter.value) {
      return false
    }

    // Customer filter
    if (customerFilter.value && wp.customerId !== customerFilter.value) {
      return false
    }

    // Product filter
    if (productFilter.value && wp.productId !== productFilter.value) {
      return false
    }

    // Search query
    if (searchQuery.value.trim() !== '') {
      const q = searchQuery.value.trim().toLowerCase()
      const matchName = wp.name.toLowerCase().includes(q)
      const matchObjective = wp.objective.toLowerCase().includes(q)
      const matchOwner = wp.ownerName?.toLowerCase().includes(q) ?? false
      const matchCustomer = wp.customerName?.toLowerCase().includes(q) ?? false
      const matchProduct = wp.productName?.toLowerCase().includes(q) ?? false
      return matchName || matchObjective || matchOwner || matchCustomer || matchProduct
    }

    return true
  })
})

const draftCount = computed(() => workPackages.value.filter(wp => wp.status === 'DRAFT').length)
const activeCount = computed(() => workPackages.value.filter(wp => wp.status === 'ACTIVE').length)
const closedCount = computed(() => workPackages.value.filter(wp => wp.status === 'CLOSED').length)

// Data fetching
async function loadWorkPackages() {
  isLoading.value = true
  errorMessage.value = null
  try {
    const [packagesRes, ownersRes, customersRes, productsRes] = await Promise.all([
      apiClient.get<WorkPackage[]>('/work-packages'),
      apiClient.get<Person[]>('/work-packages/owners'),
      apiClient.get<Customer[]>('/work-packages/customers'),
      apiClient.get<Product[]>('/work-packages/products')
    ])
    workPackages.value = packagesRes.data
    eligibleOwners.value = ownersRes.data
    eligibleCustomers.value = customersRes.data
    eligibleProducts.value = productsRes.data
  } catch (err: any) {
    const detail = err.response?.data?.detail || err.message || 'Failed to load work packages.'
    errorMessage.value = detail
  } finally {
    isLoading.value = false
  }
}

async function loadLookupData() {
  try {
    const [ownersRes, customersRes, productsRes] = await Promise.all([
      apiClient.get<Person[]>('/work-packages/owners'),
      apiClient.get<Customer[]>('/work-packages/customers'),
      apiClient.get<Product[]>('/work-packages/products')
    ])
    eligibleOwners.value = ownersRes.data
    eligibleCustomers.value = customersRes.data
    eligibleProducts.value = productsRes.data
  } catch (err: any) {
    console.error('Failed to load lookup dropdowns', err)
  }
}

// Modal open handlers
function openCreateModal() {
  createForm.value = {
    name: '',
    objective: '',
    ownerPersonId: eligibleOwners.value.length > 0 ? eligibleOwners.value[0].personId : '',
    customerId: '',
    productId: ''
  }
  errorMessage.value = null
  showCreateModal.value = true
}

function openEditModal(workPackage: WorkPackage) {
  editForm.value = {
    id: workPackage.id,
    name: workPackage.name,
    objective: workPackage.objective
  }
  errorMessage.value = null
  showEditModal.value = true
}

function openOwnerModal(workPackage: WorkPackage) {
  ownerForm.value = {
    workPackageId: workPackage.id,
    workPackageName: workPackage.name,
    workPackageCode: workPackage.name.substring(0, Math.min(8, workPackage.name.length)).toUpperCase(),
    currentOwnerName: workPackage.ownerName || 'Unassigned',
    currentOwnerPersonId: workPackage.ownerPersonId,
    newOwnerPersonId: workPackage.ownerPersonId
  }
  errorMessage.value = null
  showOwnerModal.value = true
}

function openScopeModal(workPackage: WorkPackage) {
  scopeForm.value = {
    workPackageId: workPackage.id
  }
  showScopeModal.value = true
}

// CRUD actions
async function handleCreate() {
  if (!createForm.value.name.trim() || !createForm.value.objective.trim() || !createForm.value.ownerPersonId) {
    errorMessage.value = 'Name, Objective, and Work Package Owner are required.'
    return
  }

  isSaving.value = true
  errorMessage.value = null
  try {
    const res = await apiClient.post<WorkPackage>('/work-packages', {
      name: createForm.value.name.trim(),
      objective: createForm.value.objective.trim(),
      ownerPersonId: createForm.value.ownerPersonId,
      customerId: createForm.value.customerId || null,
      productId: createForm.value.productId || null
    })

    workPackages.value.unshift(res.data)
    showCreateModal.value = false
    successMessage.value = `Work Package "${res.data.name}" created successfully.`
    setTimeout(() => { successMessage.value = null }, 5000)
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || err.message || 'Failed to create work package.'
  } finally {
    isSaving.value = false
  }
}

async function handleUpdate() {
  if (!editForm.value.name.trim() || !editForm.value.objective.trim()) {
    errorMessage.value = 'Name and Objective are required.'
    return
  }

  isSaving.value = true
  errorMessage.value = null
  try {
    const res = await apiClient.put<WorkPackage>(`/work-packages/${editForm.value.id}/objective`, {
      name: editForm.value.name.trim(),
      objective: editForm.value.objective.trim()
    })

    const idx = workPackages.value.findIndex(wp => wp.id === editForm.value.id)
    if (idx !== -1) {
      workPackages.value[idx] = res.data
    }
    showEditModal.value = false
    successMessage.value = `Work Package "${res.data.name}" updated successfully.`
    setTimeout(() => { successMessage.value = null }, 5000)
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || err.message || 'Failed to update work package.'
  } finally {
    isSaving.value = false
  }
}

async function handleAssignOwner() {
  if (!ownerForm.value.newOwnerPersonId) {
    errorMessage.value = 'Please select a work package owner.'
    return
  }

  isSaving.value = true
  errorMessage.value = null
  try {
    const res = await apiClient.put<WorkPackage>(`/work-packages/${ownerForm.value.workPackageId}/owner`, {
      newOwnerPersonId: ownerForm.value.newOwnerPersonId
    })

    const idx = workPackages.value.findIndex(wp => wp.id === ownerForm.value.workPackageId)
    if (idx !== -1) {
      workPackages.value[idx] = res.data
    }
    showOwnerModal.value = false
    successMessage.value = `Owner for "${res.data.name}" updated to ${res.data.ownerName || 'selected person'}.`
    setTimeout(() => { successMessage.value = null }, 5000)
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || err.message || 'Failed to assign work package owner.'
  } finally {
    isSaving.value = false
  }
}

async function handleActivate(workPackage: WorkPackage) {
  const confirmed = window.confirm(`Are you sure you want to activate work package "${workPackage.name}"?`)
  if (!confirmed) return

  isLoading.value = true
  errorMessage.value = null
  try {
    await apiClient.post(`/work-packages/${workPackage.id}/activate`, null)
    workPackage.status = 'ACTIVE'
    workPackage.isActive = true
    workPackage.isDraft = false
    successMessage.value = `Work Package "${workPackage.name}" activated.`
    setTimeout(() => { successMessage.value = null }, 5000)
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || err.message || 'Failed to activate work package.'
  } finally {
    isLoading.value = false
  }
}

async function handleClose(workPackage: WorkPackage) {
  const confirmed = window.confirm(`Are you sure you want to close work package "${workPackage.name}"?`)
  if (!confirmed) return

  isLoading.value = true
  errorMessage.value = null
  try {
    await apiClient.post(`/work-packages/${workPackage.id}/close`, { reason: 'Manually closed from UI' })
    workPackage.status = 'CLOSED'
    workPackage.isClosed = true
    workPackage.isActive = false
    successMessage.value = `Work Package "${workPackage.name}" closed.`
    setTimeout(() => { successMessage.value = null }, 5000)
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || err.message || 'Failed to close work package.'
  } finally {
    isLoading.value = false
  }
}

async function handleAddRequest(workPackage: WorkPackage) {
  const requestId = prompt('Enter Request ID to add to this work package:')
  if (!requestId) return

  isLoading.value = true
  errorMessage.value = null
  try {
    await apiClient.post(`/work-packages/${workPackage.id}/requests/${requestId}`, null)
    workPackage.activeRequestCount++
    successMessage.value = `Request "${requestId}" added to work package "${workPackage.name}".`
    setTimeout(() => { successMessage.value = null }, 5000)
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || err.message || 'Failed to add request to work package.'
  } finally {
    isLoading.value = false
  }
}

async function handleRemoveRequest(workPackage: WorkPackage) {
  const requestId = prompt('Enter Request ID to remove from this work package:')
  if (!requestId) return

  isLoading.value = true
  errorMessage.value = null
  try {
    await apiClient.delete(`/work-packages/${workPackage.id}/requests/${requestId}`)
    workPackage.activeRequestCount--
    successMessage.value = `Request "${requestId}" removed from work package "${workPackage.name}".`
    setTimeout(() => { successMessage.value = null }, 5000)
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || err.message || 'Failed to remove request from work package.'
  } finally {
    isLoading.value = false
  }
}

function viewScope(workPackageId: string) {
  showScopeModal.value = true
  scopeForm.value = { workPackageId }
}

function getStatusBadgeClass(status: string): string {
  switch (status) {
    case 'DRAFT':
      return 'bg-secondary'
    case 'ACTIVE':
      return 'bg-success'
    case 'CLOSED':
      return 'bg-dark'
    default:
      return 'bg-secondary'
  }
}

function formatDate(isoString: string | null): string {
  if (!isoString) return '-'
  try {
    return new Date(isoString).toLocaleDateString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    })
  } catch {
    return isoString
  }
}

onMounted(async () => {
  await Promise.all([loadLookupData(), loadWorkPackages()])
})
</script>

<template>
  <div class="container py-4">
    <!-- Screen Header & Architecture Tracking -->
    <div class="d-flex justify-content-between align-items-center mb-4 pb-2 border-bottom flex-wrap gap-2">
      <div>
        <div class="d-flex align-items-center gap-2 mb-1">
          <span class="badge bg-primary text-uppercase font-monospace px-2 py-1">SCR-WP-001</span>
          <h2 class="h4 mb-0 fw-bold">Work Package Management</h2>
        </div>
        <p class="text-muted small mb-0">
          Temporary grouping of related operational Requests that share a common objective.
          Manage work package creation, lifecycle, and scope review.
        </p>
      </div>
      <div class="d-flex align-items-center gap-2">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm d-flex align-items-center gap-1"
          @click="loadWorkPackages"
          :disabled="isLoading"
          title="Refresh Work Packages"
        >
          <i class="bi bi-arrow-clockwise" :class="{ 'spin-icon': isLoading }"></i>
          <span>Refresh</span>
        </button>
        <button
          type="button"
          class="btn btn-primary btn-sm d-flex align-items-center gap-1 shadow-sm"
          @click="openCreateModal"
        >
          <i class="bi bi-plus-lg"></i>
          <span>New Work Package</span>
        </button>
      </div>
    </div>

    <!-- Alert Banners -->
    <div v-if="errorMessage" class="alert alert-danger alert-dismissible fade show shadow-sm" role="alert">
      <div class="d-flex align-items-center">
        <i class="bi bi-exclamation-triangle-fill fs-5 me-2"></i>
        <div><strong>Error:</strong> {{ errorMessage }}</div>
      </div>
      <button type="button" class="btn-close" @click="errorMessage = null" aria-label="Close"></button>
    </div>

    <div v-if="successMessage" class="alert alert-success alert-dismissible fade show shadow-sm" role="alert">
      <div class="d-flex align-items-center">
        <i class="bi bi-check-circle-fill fs-5 me-2"></i>
        <div>{{ successMessage }}</div>
      </div>
      <button type="button" class="btn-close" @click="successMessage = null" aria-label="Close"></button>
    </div>

    <!-- Summary Metrics & Filter Bar -->
    <div class="card bg-white border-0 shadow-sm mb-4">
      <div class="card-body p-3">
        <div class="row g-3 align-items-center">
          <!-- Search input -->
          <div class="col-md-4">
            <div class="input-group input-group-sm">
              <span class="input-group-text bg-light border-end-0">
                <i class="bi bi-search text-muted"></i>
              </span>
              <input
                type="text"
                class="form-control border-start-0"
                placeholder="Search by name, objective, owner, customer, or product..."
                v-model="searchQuery"
              />
              <button
                v-if="searchQuery"
                class="btn btn-outline-secondary border-start-0"
                type="button"
                @click="searchQuery = ''"
              >
                <i class="bi bi-x"></i>
              </button>
            </div>
          </div>

          <!-- Status Filters -->
          <div class="col-md-8 d-flex justify-content-md-end align-items-center gap-2">
            <span class="text-muted small me-1">Status:</span>
            <div class="btn-group btn-group-sm" role="group" aria-label="Status filter">
              <button
                type="button"
                class="btn"
                :class="statusFilter === 'ALL' ? 'btn-primary' : 'btn-outline-secondary'"
                @click="statusFilter = 'ALL'"
              >
                All ({{ workPackages.length }})
              </button>
              <button
                type="button"
                class="btn"
                :class="statusFilter === 'DRAFT' ? 'btn-secondary' : 'btn-outline-secondary'"
                @click="statusFilter = 'DRAFT'"
              >
                Draft ({{ draftCount }})
              </button>
              <button
                type="button"
                class="btn"
                :class="statusFilter === 'ACTIVE' ? 'btn-success' : 'btn-outline-secondary'"
                @click="statusFilter = 'ACTIVE'"
              >
                Active ({{ activeCount }})
              </button>
              <button
                type="button"
                class="btn"
                :class="statusFilter === 'CLOSED' ? 'btn-dark' : 'btn-outline-secondary'"
                @click="statusFilter = 'CLOSED'"
              >
                Closed ({{ closedCount }})
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Work Package Cards -->
    <div class="row g-4">
      <div v-if="isLoading" class="col-12 text-center py-5">
        <div class="spinner-border text-primary me-2" role="status">
          <span class="visually-hidden">Loading...</span>
        </div>
        <span>Loading work packages...</span>
      </div>

      <div v-else-if="filteredWorkPackages.length === 0" class="col-12">
        <div class="card bg-white border-0 shadow-sm">
          <div class="card-body text-center py-5">
            <i class="bi bi-inbox fs-2 d-block mb-2 text-secondary"></i>
            <div class="fw-semibold">No work packages found</div>
            <div class="text-muted small mb-3">
              <span v-if="searchQuery || statusFilter !== 'ALL' || ownerFilter || customerFilter || productFilter">
                No work packages match your search criteria.
              </span>
              <span v-else>No work packages registered in the system yet. Click "New Work Package" above.</span>
            </div>
            <button
              type="button"
              class="btn btn-primary btn-sm"
              @click="showCreateModal = true"
            >
              <i class="bi bi-plus-lg me-1"></i> Create First Work Package
            </button>
          </div>
        </div>
      </div>

      <div v-for="wp in filteredWorkPackages" :key="wp.id" class="col-lg-6 col-xl-4">
        <div class="card border-0 shadow-sm h-100">
          <div class="card-header bg-white d-flex justify-content-between align-items-start border-0 pb-0">
            <div class="d-flex align-items-center gap-2 mb-2">
              <span class="badge" :class="getStatusBadgeClass(wp.status)">
                {{ wp.status }}
              </span>
              <span class="badge bg-light text-dark border font-monospace small">
                {{ wp.ownerName || 'Unassigned' }}
              </span>
            </div>
            <div class="dropdown">
              <button
                type="button"
                class="btn btn-sm btn-outline-secondary"
                data-bs-toggle="dropdown"
                aria-expanded="false"
              >
                <i class="bi bi-three-dots-vertical"></i>
              </button>
              <ul class="dropdown-menu dropdown-menu-end">
                <li>
                  <button class="dropdown-item" @click="openEditModal(wp)">
                    <i class="bi bi-pencil me-2"></i>Edit Objective
                  </button>
                </li>
                <li>
                  <button class="dropdown-item" @click="openOwnerModal(wp)">
                    <i class="bi bi-person-gear me-2"></i>Change Owner
                  </button>
                </li>
                <li><hr class="dropdown-divider" /></li>
                <li>
                  <button class="dropdown-item" @click="openScopeModal(wp)">
                    <i class="bi bi-list-ul me-2"></i>View Scope
                  </button>
                </li>
                <li>
                  <button class="dropdown-item" @click="handleAddRequest(wp)">
                    <i class="bi bi-plus-circle me-2"></i>Add Request
                  </button>
                </li>
                <li>
                  <button class="dropdown-item text-danger" @click="handleRemoveRequest(wp)">
                    <i class="bi bi-dash-circle me-2"></i>Remove Request
                  </button>
                </li>
                <li><hr class="dropdown-divider" /></li>
                <li>
                  <button class="dropdown-item" @click="handleActivate(wp)" :disabled="wp.status !== 'DRAFT'">
                    <i class="bi bi-play-circle me-2"></i>Activate
                  </button>
                </li>
                <li>
                  <button class="dropdown-item text-warning" @click="handleClose(wp)" :disabled="wp.status === 'DRAFT' || wp.status === 'CLOSED'">
                    <i class="bi bi-pause-circle me-2"></i>Close
                  </button>
                </li>
              </ul>
            </div>
          </div>
          <div class="card-body">
            <h5 class="card-title fw-bold">{{ wp.name }}</h5>
            <p class="card-text text-muted small mb-3">{{ wp.objective }}</p>

            <div class="small">
              <div class="d-flex align-items-center gap-1 mb-2">
                <i class="bi bi-person text-secondary"></i>
                <span>Owner: {{ wp.ownerName || 'Unassigned' }}</span>
              </div>
              <div class="d-flex align-items-center gap-1 mb-2">
                <i class="bi bi-building text-secondary"></i>
                <span>Customer: {{ wp.customerName || 'Not specified' }}</span>
              </div>
              <div class="d-flex align-items-center gap-1 mb-2">
                <i class="bi bi-box-seam text-secondary"></i>
                <span>Product: {{ wp.productName || 'Not specified' }}</span>
              </div>
              <div class="d-flex align-items-center gap-1 mb-2">
                <i class="bi bi-list-check text-secondary"></i>
                <span>Active Requests: {{ wp.activeRequestCount }}</span>
              </div>
              <div class="d-flex align-items-center gap-1">
                <i class="bi bi-clock text-secondary"></i>
                <span>Created: {{ formatDate(wp.createdAt) }}</span>
              </div>
            </div>
          </div>
          <div class="card-footer bg-white border-0 pt-0">
            <div class="d-flex gap-2">
              <button
                type="button"
                class="btn btn-outline-primary btn-sm flex-grow-1"
                @click="openEditModal(wp)"
              >
                <i class="bi bi-pencil"></i> Edit
              </button>
              <button
                type="button"
                class="btn btn-outline-success btn-sm flex-grow-1"
                @click="openOwnerModal(wp)"
              >
                <i class="bi bi-person-gear"></i> Owner
              </button>
              <button
                type="button"
                class="btn btn-outline-info btn-sm flex-grow-1"
                @click="viewScope(wp.id)"
              >
                <i class="bi bi-list-ul"></i> Scope
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Create Work Package Modal -->
    <div
      v-if="showCreateModal"
      class="modal fade show d-block"
      tabindex="-1"
      style="background: rgba(0, 0, 0, 0.5);"
      role="dialog"
      aria-modal="true"
    >
      <div class="modal-dialog modal-dialog-centered modal-lg">
        <div class="modal-content shadow border-0">
          <div class="modal-header">
            <h5 class="modal-title fw-bold">
              <i class="bi bi-plus-circle me-1 text-primary"></i> Create New Work Package
            </h5>
            <button type="button" class="btn-close" @click="showCreateModal = false" aria-label="Close"></button>
          </div>
          <form @submit.prevent="handleCreate">
            <div class="modal-body">
              <div class="row g-3">
                <div class="col-md-8">
                  <label for="createName" class="form-label small fw-semibold">Work Package Name <span class="text-danger">*</span></label>
                  <input
                    id="createName"
                    type="text"
                    class="form-control form-control-sm"
                    placeholder="e.g., Phase 1 Rollout, MyHospital Billing Improvement"
                    v-model="createForm.name"
                    maxlength="200"
                    required
                  />
                  <div class="form-text small">Clear, descriptive name for the work package.</div>
                </div>

                <div class="col-md-8">
                  <label for="createObjective" class="form-label small fw-semibold">Objective <span class="text-danger">*</span></label>
                  <textarea
                    id="createObjective"
                    class="form-control form-control-sm"
                    rows="3"
                    placeholder="The intended result that gives the Work Package its purpose..."
                    v-model="createForm.objective"
                    maxlength="500"
                    required
                  ></textarea>
                  <div class="form-text small">The goal or purpose of this work package.</div>
                </div>

                <div class="col-md-6">
                  <label for="createOwner" class="form-label small fw-semibold">Work Package Owner <span class="text-danger">*</span></label>
                  <select
                    id="createOwner"
                    class="form-select form-select-sm"
                    v-model="createForm.ownerPersonId"
                    required
                  >
                    <option value="" disabled>Select an active person...</option>
                    <option
                      v-for="person in eligibleOwners"
                      :key="person.personId"
                      :value="person.personId"
                    >
                      {{ person.name }} ({{ person.email }})
                    </option>
                  </select>
                  <div class="form-text small">Active Person from Organization domain designated as work package owner.</div>
                </div>

                <div class="col-md-6">
                  <label for="createCustomer" class="form-label small fw-semibold">Customer (Optional)</label>
                  <select
                    id="createCustomer"
                    class="form-select form-select-sm"
                    v-model="createForm.customerId"
                  >
                    <option value="" selected>None (optional)</option>
                    <option
                      v-for="customer in eligibleCustomers"
                      :key="customer.customerId"
                      :value="customer.customerId"
                    >
                      {{ customer.customerName }} ({{ customer.customerCode }})
                    </option>
                  </select>
                  <div class="form-text small">Optional customer association for this work package.</div>
                </div>

                <div class="col-md-6">
                  <label for="createProduct" class="form-label small fw-semibold">Product (Optional)</label>
                  <select
                    id="createProduct"
                    class="form-select form-select-sm"
                    v-model="createForm.productId"
                  >
                    <option value="" selected>None (optional)</option>
                    <option
                      v-for="product in eligibleProducts"
                      :key="product.productId"
                      :value="product.productId"
                    >
                      {{ product.name }} ({{ product.code }})
                    </option>
                  </select>
                  <div class="form-text small">Optional product association for this work package.</div>
                </div>
              </div>
            </div>
            <div class="modal-footer">
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm"
                @click="showCreateModal = false"
                :disabled="isSaving"
              >
                Cancel
              </button>
              <button
                type="submit"
                class="btn btn-primary btn-sm d-flex align-items-center gap-1"
                :disabled="isSaving"
              >
                <span v-if="isSaving" class="spinner-border spinner-border-sm" role="status"></span>
                <i v-else class="bi bi-check-lg"></i>
                <span>Create Work Package</span>
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Edit Work Package Modal -->
    <div
      v-if="showEditModal"
      class="modal fade show d-block"
      tabindex="-1"
      style="background: rgba(0, 0, 0, 0.5);"
      role="dialog"
      aria-modal="true"
    >
      <div class="modal-dialog modal-dialog-centered modal-lg">
        <div class="modal-content shadow border-0">
          <div class="modal-header">
            <h5 class="modal-title fw-bold">
              <i class="bi bi-pencil me-1 text-primary"></i> Edit Work Package
            </h5>
            <button type="button" class="btn-close" @click="showEditModal = false" aria-label="Close"></button>
          </div>
          <form @submit.prevent="handleUpdate">
            <div class="modal-body">
              <div class="row g-3">
                <div class="col-md-8">
                  <label for="editName" class="form-label small fw-semibold">Work Package Name <span class="text-danger">*</span></label>
                  <input
                    id="editName"
                    type="text"
                    class="form-control form-control-sm"
                    v-model="editForm.name"
                    maxlength="200"
                    required
                  />
                </div>

                <div class="col-md-8">
                  <label for="editObjective" class="form-label small fw-semibold">Objective <span class="text-danger">*</span></label>
                  <textarea
                    id="editObjective"
                    class="form-control form-control-sm"
                    rows="3"
                    v-model="editForm.objective"
                    maxlength="500"
                    required
                  ></textarea>
                </div>
              </div>
            </div>
            <div class="modal-footer">
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm"
                @click="showEditModal = false"
                :disabled="isSaving"
              >
                Cancel
              </button>
              <button
                type="submit"
                class="btn btn-primary btn-sm d-flex align-items-center gap-1"
                :disabled="isSaving"
              >
                <span v-if="isSaving" class="spinner-border spinner-border-sm" role="status"></span>
                <i v-else class="bi bi-check-lg"></i>
                <span>Save Changes</span>
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Assign Owner Modal -->
    <div
      v-if="showOwnerModal"
      class="modal fade show d-block"
      tabindex="-1"
      style="background: rgba(0, 0, 0, 0.5);"
      role="dialog"
      aria-modal="true"
    >
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content shadow border-0">
          <div class="modal-header">
            <h5 class="modal-title fw-bold">
              <i class="bi bi-person-gear me-1 text-primary"></i> Assign Work Package Owner
            </h5>
            <button type="button" class="btn-close" @click="showOwnerModal = false" aria-label="Close"></button>
          </div>
          <form @submit.prevent="handleAssignOwner">
            <div class="modal-body">
              <div class="mb-3">
                <label class="form-label small fw-semibold text-muted">Work Package</label>
                <div class="form-control-plaintext small fw-semibold">
                  {{ ownerForm.workPackageName }}
                  <span class="badge bg-light text-dark border font-monospace ms-1">{{ ownerForm.workPackageCode }}</span>
                </div>
              </div>

              <div class="mb-3">
                <label class="form-label small fw-semibold text-muted">Current Owner</label>
                <div class="form-control-plaintext small text-secondary">
                  <i class="bi bi-person me-1"></i> {{ ownerForm.currentOwnerName }}
                </div>
              </div>

              <div class="mb-3">
                <label for="newOwnerSelect" class="form-label small fw-semibold">
                  New Work Package Owner <span class="text-danger">*</span>
                </label>
                <select
                  id="newOwnerSelect"
                  class="form-select form-select-sm"
                  v-model="ownerForm.newOwnerPersonId"
                  required
                >
                  <option value="" disabled>Select an active person...</option>
                  <option
                    v-for="person in eligibleOwners"
                    :key="person.personId"
                    :value="person.personId"
                  >
                    {{ person.name }} ({{ person.email }})
                  </option>
                </select>
                <div class="form-text small">
                  Select a registered, active member from the Organization domain.
                </div>
              </div>
            </div>
            <div class="modal-footer">
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm"
                @click="showOwnerModal = false"
                :disabled="isSaving"
              >
                Cancel
              </button>
              <button
                type="submit"
                class="btn btn-primary btn-sm d-flex align-items-center gap-1"
                :disabled="isSaving"
              >
                <span v-if="isSaving" class="spinner-border spinner-border-sm" role="status"></span>
                <i v-else class="bi bi-check-lg"></i>
                <span>Update Owner</span>
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Scope View Modal -->
    <div
      v-if="showScopeModal"
      class="modal fade show d-block"
      tabindex="-1"
      style="background: rgba(0, 0, 0, 0.5);"
      role="dialog"
      aria-modal="true"
    >
      <div class="modal-dialog modal-dialog-centered modal-xl">
        <div class="modal-content shadow border-0">
          <div class="modal-header">
            <h5 class="modal-title fw-bold">
              <i class="bi bi-list-ul me-1 text-primary"></i> Work Package Scope
            </h5>
            <button type="button" class="btn-close" @click="showScopeModal = false" aria-label="Close"></button>
          </div>
          <div class="modal-body">
            <div class="row">
              <div class="col-md-6">
                <h6 class="fw-semibold mb-3">Active Requests</h6>
                <div v-if="scopeForm.workPackageId" class="small">
                  <!-- Scope content would be populated here -->
                  <div class="alert alert-info small">
                    Scope view is under construction. Full scope details will be displayed here.
                  </div>
                </div>
              </div>
              <div class="col-md-6">
                <h6 class="fw-semibold mb-3">Package Metadata</h6>
                <div v-if="scopeForm.workPackageId" class="small">
                  <div class="mb-2">
                    <strong>ID:</strong> {{ scopeForm.workPackageId }}
                  </div>
                  <div class="mb-2">
                    <strong>Status:</strong> {{ workPackages.find(wp => wp.id === scopeForm.workPackageId)?.status || 'Unknown' }}
                  </div>
                  <div class="mb-2">
                    <strong>Owner:</strong> {{ workPackages.find(wp => wp.id === scopeForm.workPackageId)?.ownerName || 'Unknown' }}
                  </div>
                  <div class="mb-2">
                    <strong>Customer:</strong> {{ workPackages.find(wp => wp.id === scopeForm.workPackageId)?.customerName || 'Not specified' }}
                  </div>
                  <div class="mb-2">
                    <strong>Product:</strong> {{ workPackages.find(wp => wp.id === scopeForm.workPackageId)?.productName || 'Not specified' }}
                  </div>
                </div>
              </div>
            </div>
          </div>
          <div class="modal-footer">
            <button type="button" class="btn btn-outline-secondary btn-sm" @click="showScopeModal = false">
              Close
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Backdrop for modals -->
    <div
      v-if="showCreateModal || showEditModal || showOwnerModal || showScopeModal"
      class="modal-backdrop fade show"
      @click="showCreateModal = showEditModal = showOwnerModal = showScopeModal = false"
      style="background-color: rgba(0, 0, 0, 0.5);"
    ></div>
  </div>

  <style scoped>
  .spin-icon {
    animation: spin 1s linear infinite;
  }

  @keyframes spin {
    from { transform: rotate(0deg); }
    to { transform: rotate(360deg); }
  }

  .hover-primary:hover {
    color: #0d6efd !important;
    text-decoration: none;
  }
  </style>
</template>