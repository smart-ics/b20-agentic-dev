<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { apiClient } from '@/services/api'

interface Product {
  productId: string
  code: string
  name: string
  description: string | null
  ownerPersonId: string
  ownerName: string | null
  status: string
  createdAt: string
  updatedAt: string | null
}

interface Person {
  personId: string
  name: string
  email: string
  status: string
}

// State
const products = ref<Product[]>([])
const eligibleOwners = ref<Person[]>([])
const isLoading = ref(false)
const isSaving = ref(false)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)

// Filtering & Search
const searchQuery = ref('')
const statusFilter = ref<'ALL' | 'ACTIVE' | 'INACTIVE'>('ALL')

// Modals
const showCreateModal = ref(false)
const showEditModal = ref(false)
const showOwnerModal = ref(false)

// Forms
const createForm = ref({
  code: '',
  name: '',
  description: '',
  ownerPersonId: ''
})

const editForm = ref({
  productId: '',
  code: '',
  name: '',
  description: ''
})

const ownerForm = ref({
  productId: '',
  productName: '',
  productCode: '',
  currentOwnerName: '',
  newOwnerPersonId: ''
})

// Computed
const filteredProducts = computed(() => {
  return products.value.filter(p => {
    // Status filter
    if (statusFilter.value !== 'ALL' && p.status !== statusFilter.value) {
      return false
    }

    // Search query
    if (searchQuery.value.trim() !== '') {
      const q = searchQuery.value.trim().toLowerCase()
      const matchCode = p.code.toLowerCase().includes(q)
      const matchName = p.name.toLowerCase().includes(q)
      const matchDesc = p.description?.toLowerCase().includes(q) ?? false
      const matchOwner = p.ownerName?.toLowerCase().includes(q) ?? false
      return matchCode || matchName || matchDesc || matchOwner
    }

    return true
  })
})

const activeCount = computed(() => products.value.filter(p => p.status === 'ACTIVE').length)
const inactiveCount = computed(() => products.value.filter(p => p.status === 'INACTIVE').length)

// Data fetching
async function loadCatalog() {
  isLoading.value = true
  errorMessage.value = null
  try {
    const [productsRes, ownersRes] = await Promise.all([
      apiClient.get<Product[]>('/products'),
      apiClient.get<Person[]>('/products/owners')
    ])
    products.value = productsRes.data
    eligibleOwners.value = ownersRes.data
  } catch (err: any) {
    const detail = err.response?.data?.detail || err.message || 'Failed to load product catalog.'
    errorMessage.value = detail
  } finally {
    isLoading.value = false
  }
}

// Modal open handlers
function openCreateModal() {
  createForm.value = {
    code: '',
    name: '',
    description: '',
    ownerPersonId: eligibleOwners.value.length > 0 ? eligibleOwners.value[0].personId : ''
  }
  errorMessage.value = null
  showCreateModal.value = true
}

function openEditModal(product: Product) {
  editForm.value = {
    productId: product.productId,
    code: product.code,
    name: product.name,
    description: product.description || ''
  }
  errorMessage.value = null
  showEditModal.value = true
}

function openOwnerModal(product: Product) {
  ownerForm.value = {
    productId: product.productId,
    productName: product.name,
    productCode: product.code,
    currentOwnerName: product.ownerName || 'Unassigned',
    newOwnerPersonId: product.ownerPersonId
  }
  errorMessage.value = null
  showOwnerModal.value = true
}

// CRUD actions
async function handleCreate() {
  if (!createForm.value.code.trim() || !createForm.value.name.trim() || !createForm.value.ownerPersonId) {
    errorMessage.value = 'Code, Name, and Product Owner are required.'
    return
  }

  isSaving.value = true
  errorMessage.value = null
  try {
    const res = await apiClient.post<Product>('/products', {
      code: createForm.value.code.trim().toUpperCase(),
      name: createForm.value.name.trim(),
      description: createForm.value.description.trim() || null,
      ownerPersonId: createForm.value.ownerPersonId
    })

    products.value.unshift(res.data)
    showCreateModal.value = false
    successMessage.value = `Product "${res.data.name}" (${res.data.code}) created successfully.`
    setTimeout(() => { successMessage.value = null }, 5000)
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || err.message || 'Failed to create product.'
  } finally {
    isSaving.value = false
  }
}

async function handleUpdate() {
  if (!editForm.value.name.trim()) {
    errorMessage.value = 'Product Name is required.'
    return
  }

  isSaving.value = true
  errorMessage.value = null
  try {
    const res = await apiClient.put<Product>(`/products/${editForm.value.productId}`, {
      name: editForm.value.name.trim(),
      description: editForm.value.description.trim() || null
    })

    const idx = products.value.findIndex(p => p.productId === editForm.value.productId)
    if (idx !== -1) {
      products.value[idx] = res.data
    }
    showEditModal.value = false
    successMessage.value = `Product "${res.data.name}" updated successfully.`
    setTimeout(() => { successMessage.value = null }, 5000)
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || err.message || 'Failed to update product.'
  } finally {
    isSaving.value = false
  }
}

async function handleAssignOwner() {
  if (!ownerForm.value.newOwnerPersonId) {
    errorMessage.value = 'Please select a product owner.'
    return
  }

  isSaving.value = true
  errorMessage.value = null
  try {
    const res = await apiClient.put<Product>(`/products/${ownerForm.value.productId}/owner`, {
      ownerPersonId: ownerForm.value.newOwnerPersonId
    })

    const idx = products.value.findIndex(p => p.productId === ownerForm.value.productId)
    if (idx !== -1) {
      products.value[idx] = res.data
    }
    showOwnerModal.value = false
    successMessage.value = `Owner for "${res.data.name}" updated to ${res.data.ownerName || 'selected person'}.`
    setTimeout(() => { successMessage.value = null }, 5000)
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || err.message || 'Failed to assign product owner.'
  } finally {
    isSaving.value = false
  }
}

async function handleToggleStatus(product: Product) {
  const isActivating = product.status !== 'ACTIVE'
  const actionName = isActivating ? 'activate' : 'deactivate'
  const confirmed = window.confirm(`Are you sure you want to ${actionName} product "${product.name}" (${product.code})?`)
  if (!confirmed) return

  isLoading.value = true
  errorMessage.value = null
  try {
    const endpoint = `/products/${product.productId}/${actionName}`
    const res = await apiClient.post<Product>(endpoint, null)

    const idx = products.value.findIndex(p => p.productId === product.productId)
    if (idx !== -1) {
      products.value[idx] = res.data
    }
    successMessage.value = `Product "${res.data.name}" status changed to ${res.data.status}.`
    setTimeout(() => { successMessage.value = null }, 5000)
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || err.message || `Failed to ${actionName} product.`
  } finally {
    isLoading.value = false
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

onMounted(() => {
  loadCatalog()
})
</script>

<template>
  <div class="container py-4">
    <!-- Screen Header & Architecture Tracking -->
    <div class="d-flex justify-content-between align-items-center mb-4 pb-2 border-bottom flex-wrap gap-2">
      <div>
        <div class="d-flex align-items-center gap-2 mb-1">
          <span class="badge bg-primary text-uppercase font-monospace px-2 py-1">SCR-PRD-001</span>
          <h2 class="h4 mb-0 fw-bold">Product Catalog</h2>
        </div>
        <p class="text-muted small mb-0">
          Authoritative business product registry, master metadata, ownership assignments, and lifecycle status.
        </p>
      </div>
      <div class="d-flex align-items-center gap-2">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm d-flex align-items-center gap-1"
          @click="loadCatalog"
          :disabled="isLoading"
          title="Refresh Catalog"
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
          <span>New Product</span>
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
          <div class="col-md-5 col-lg-6">
            <div class="input-group input-group-sm">
              <span class="input-group-text bg-light border-end-0">
                <i class="bi bi-search text-muted"></i>
              </span>
              <input
                type="text"
                class="form-control border-start-0"
                placeholder="Search by code, product name, or owner..."
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
          <div class="col-md-7 col-lg-6 d-flex justify-content-md-end align-items-center gap-2">
            <span class="text-muted small me-1">Status:</span>
            <div class="btn-group btn-group-sm" role="group" aria-label="Status filter">
              <button
                type="button"
                class="btn"
                :class="statusFilter === 'ALL' ? 'btn-primary' : 'btn-outline-secondary'"
                @click="statusFilter = 'ALL'"
              >
                All ({{ products.length }})
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
                :class="statusFilter === 'INACTIVE' ? 'btn-secondary' : 'btn-outline-secondary'"
                @click="statusFilter = 'INACTIVE'"
              >
                Inactive ({{ inactiveCount }})
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Product Grid / Table -->
    <div class="card bg-white border-0 shadow-sm">
      <div class="table-responsive">
        <table class="table table-hover align-middle mb-0">
          <thead class="table-light">
            <tr>
              <th scope="col" style="width: 140px;">Code</th>
              <th scope="col" style="min-width: 180px;">Name</th>
              <th scope="col" style="min-width: 220px;">Description</th>
              <th scope="col" style="min-width: 160px;">Product Owner</th>
              <th scope="col" style="width: 110px;">Status</th>
              <th scope="col" style="width: 120px;">Created</th>
              <th scope="col" class="text-end" style="width: 180px;">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="isLoading">
              <td colspan="7" class="text-center py-5 text-muted">
                <div class="spinner-border spinner-border-sm text-primary me-2" role="status"></div>
                <span>Loading product catalog...</span>
              </td>
            </tr>

            <tr v-else-if="filteredProducts.length === 0">
              <td colspan="7" class="text-center py-5 text-muted">
                <i class="bi bi-inbox fs-2 d-block mb-2 text-secondary"></i>
                <div class="fw-semibold">No products found</div>
                <div class="small">
                  <span v-if="searchQuery">No products match "{{ searchQuery }}".</span>
                  <span v-else>No products registered in the catalog yet. Click "New Product" above.</span>
                </div>
              </td>
            </tr>

            <tr v-for="product in filteredProducts" :key="product.productId">
              <td>
                <span class="badge bg-light text-dark border font-monospace px-2 py-1">
                  {{ product.code }}
                </span>
              </td>
              <td>
                <span class="fw-semibold text-dark">{{ product.name }}</span>
              </td>
              <td>
                <span class="text-muted small">
                  {{ product.description || '-' }}
                </span>
              </td>
              <td>
                <div class="d-flex align-items-center gap-1">
                  <i class="bi bi-person text-secondary"></i>
                  <span class="small fw-medium">{{ product.ownerName || 'Unknown Owner' }}</span>
                </div>
              </td>
              <td>
                <span
                  class="badge"
                  :class="product.status === 'ACTIVE'
                    ? 'bg-success-subtle text-success border border-success-subtle'
                    : 'bg-secondary-subtle text-secondary border border-secondary-subtle'"
                >
                  {{ product.status }}
                </span>
              </td>
              <td>
                <span class="text-muted small">{{ formatDate(product.createdAt) }}</span>
              </td>
              <td class="text-end">
                <div class="btn-group btn-group-sm" role="group">
                  <button
                    type="button"
                    class="btn btn-outline-primary"
                    @click="openEditModal(product)"
                    title="Edit Attributes"
                  >
                    <i class="bi bi-pencil"></i>
                  </button>
                  <button
                    type="button"
                    class="btn btn-outline-secondary"
                    @click="openOwnerModal(product)"
                    title="Change Owner"
                  >
                    <i class="bi bi-person-gear"></i>
                  </button>
                  <button
                    type="button"
                    class="btn"
                    :class="product.status === 'ACTIVE' ? 'btn-outline-warning' : 'btn-outline-success'"
                    @click="handleToggleStatus(product)"
                    :title="product.status === 'ACTIVE' ? 'Deactivate Product' : 'Activate Product'"
                  >
                    <i :class="product.status === 'ACTIVE' ? 'bi bi-pause-circle' : 'bi bi-play-circle'"></i>
                  </button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Create Product Modal -->
    <div
      v-if="showCreateModal"
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
              <i class="bi bi-plus-circle me-1 text-primary"></i> Create New Product
            </h5>
            <button type="button" class="btn-close" @click="showCreateModal = false" aria-label="Close"></button>
          </div>
          <form @submit.prevent="handleCreate">
            <div class="modal-body">
              <div class="mb-3">
                <label for="createCode" class="form-label small fw-semibold">
                  Product Code <span class="text-danger">*</span>
                </label>
                <input
                  id="createCode"
                  type="text"
                  class="form-control form-control-sm font-monospace text-uppercase"
                  placeholder="e.g. MYHOSPITAL, PENAEL, BTRADE3"
                  v-model="createForm.code"
                  maxlength="50"
                  required
                />
                <div class="form-text small">Unique business code identifying the product.</div>
              </div>

              <div class="mb-3">
                <label for="createName" class="form-label small fw-semibold">
                  Product Name <span class="text-danger">*</span>
                </label>
                <input
                  id="createName"
                  type="text"
                  class="form-control form-control-sm"
                  placeholder="e.g. MyHospital Integrated HIS"
                  v-model="createForm.name"
                  maxlength="150"
                  required
                />
              </div>

              <div class="mb-3">
                <label for="createDesc" class="form-label small fw-semibold">Description</label>
                <textarea
                  id="createDesc"
                  class="form-control form-control-sm"
                  rows="3"
                  placeholder="Product scope, functional characteristics, or purpose..."
                  v-model="createForm.description"
                ></textarea>
              </div>

              <div class="mb-3">
                <label for="createOwner" class="form-label small fw-semibold">
                  Product Owner (Organization) <span class="text-danger">*</span>
                </label>
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
                <div class="form-text small">
                  Active Person from Organization domain designated as authoritative Product Owner.
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
                <span>Create Product</span>
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Edit Product Modal -->
    <div
      v-if="showEditModal"
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
              <i class="bi bi-pencil me-1 text-primary"></i> Edit Product Attributes
            </h5>
            <button type="button" class="btn-close" @click="showEditModal = false" aria-label="Close"></button>
          </div>
          <form @submit.prevent="handleUpdate">
            <div class="modal-body">
              <div class="mb-3">
                <label class="form-label small fw-semibold text-muted">Product Code</label>
                <input
                  type="text"
                  class="form-control form-control-sm font-monospace bg-light"
                  :value="editForm.code"
                  disabled
                />
                <div class="form-text small">Product code cannot be changed once created.</div>
              </div>

              <div class="mb-3">
                <label for="editName" class="form-label small fw-semibold">
                  Product Name <span class="text-danger">*</span>
                </label>
                <input
                  id="editName"
                  type="text"
                  class="form-control form-control-sm"
                  v-model="editForm.name"
                  maxlength="150"
                  required
                />
              </div>

              <div class="mb-3">
                <label for="editDesc" class="form-label small fw-semibold">Description</label>
                <textarea
                  id="editDesc"
                  class="form-control form-control-sm"
                  rows="3"
                  v-model="editForm.description"
                ></textarea>
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

    <!-- Reassign Owner Modal -->
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
              <i class="bi bi-person-gear me-1 text-primary"></i> Assign Product Owner
            </h5>
            <button type="button" class="btn-close" @click="showOwnerModal = false" aria-label="Close"></button>
          </div>
          <form @submit.prevent="handleAssignOwner">
            <div class="modal-body">
              <div class="mb-3">
                <label class="form-label small fw-semibold text-muted">Product</label>
                <div class="form-control-plaintext small fw-semibold">
                  {{ ownerForm.productName }}
                  <span class="badge bg-light text-dark border font-monospace ms-1">{{ ownerForm.productCode }}</span>
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
                  New Product Owner <span class="text-danger">*</span>
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
  </div>
</template>

<style scoped>
.spin-icon {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
</style>
