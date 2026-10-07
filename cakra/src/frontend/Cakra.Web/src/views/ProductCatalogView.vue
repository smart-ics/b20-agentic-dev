<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, reactive, ref } from 'vue'

import { httpClient } from '@/api/http'

/**
 * SCR-PRD-001: Product Catalog Screen (Architecture §9, §10, §19.4, §19.6, §21).
 *
 * - Lists all products (or active products) in a Bootstrap 5 table with columns: Code, Name, Owner, Status.
 * - Populates Product Owner dropdown selectors via OrganizationQueryService.ListActivePersons
 *   (`GET /api/v1/organization/persons/active`).
 * - Supports creating new products (`POST /api/v1/products`), viewing/editing product details
 *   (`GET /api/v1/products/{id}`, `PUT /api/v1/products/{id}`), reassigning product owners
 *   (`PUT /api/v1/products/{id}/owner`), and toggling product lifecycle status
 *   (`POST /api/v1/products/{id}/activate` and `POST /api/v1/products/{id}/deactivate`).
 */

export interface ProductItem {
  id: string
  productId?: string
  code: string
  productCode?: string
  name: string
  productName?: string
  description: string | null
  ownerPersonId: string
  ownerName?: string | null
  status: 'ACTIVE' | 'INACTIVE' | string
  isActive: boolean
  createdAt: string
  updatedAt?: string | null
}

export interface ActivePersonItem {
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

const products = ref<ProductItem[]>([])
const activePersons = ref<ActivePersonItem[]>([])
const isLoading = ref(false)
const isSubmitting = ref(false)
const togglingProductId = ref<string | null>(null)
const assigningOwnerProductId = ref<string | null>(null)
const showActiveOnly = ref(false)

const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)

// Create Product modal/inline form state
const showCreateForm = ref(false)
const createForm = reactive({
  code: '',
  name: '',
  description: '',
  ownerPersonId: '',
})

// Edit Product / Assign Owner modal state
const editingProduct = ref<ProductItem | null>(null)
const editForm = reactive({
  id: '',
  code: '',
  name: '',
  description: '',
  ownerPersonId: '',
})

// Inline owner reassignment state per row
const reassigningProductId = ref<string | null>(null)
const selectedNewOwnerId = ref<string>('')

const ownerNameByPersonId = computed<Record<string, string>>(() => {
  const map: Record<string, string> = {}
  for (const person of activePersons.value) {
    const label = person.fullName || `${person.firstName} ${person.lastName}`.trim()
    map[person.id] = label
  }
  return map
})

const isCreateDisabled = computed(
  () =>
    isSubmitting.value ||
    createForm.code.trim().length === 0 ||
    createForm.name.trim().length === 0 ||
    createForm.ownerPersonId.trim().length === 0,
)

const isEditDisabled = computed(
  () =>
    isSubmitting.value ||
    editForm.name.trim().length === 0 ||
    editForm.ownerPersonId.trim().length === 0,
)

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

function resolveOwnerDisplay(product: ProductItem): string {
  if (product.ownerName && product.ownerName.trim().length > 0) {
    return product.ownerName
  }
  return ownerNameByPersonId.value[product.ownerPersonId] ?? product.ownerPersonId
}

async function loadActivePersons(): Promise<void> {
  try {
    const response = await httpClient.get<ActivePersonItem[]>('/organization/persons/active')
    activePersons.value = response.data
  } catch {
    const fallback = await httpClient.get<ActivePersonItem[]>('/products/owners')
    activePersons.value = fallback.data
  }
}

async function loadProducts(): Promise<void> {
  isLoading.value = true
  errorMessage.value = null

  try {
    const endpoint = showActiveOnly.value ? '/products/active' : '/products'
    const response = await httpClient.get<ProductItem[]>(endpoint)
    products.value = response.data
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load product catalog.')
  } finally {
    isLoading.value = false
  }
}

async function handleFilterToggle(): Promise<void> {
  await loadProducts()
}

function openCreateForm(): void {
  errorMessage.value = null
  successMessage.value = null
  createForm.code = ''
  createForm.name = ''
  createForm.description = ''
  createForm.ownerPersonId = activePersons.value[0]?.id ?? ''
  showCreateForm.value = true
}

function closeCreateForm(): void {
  showCreateForm.value = false
}

async function handleCreateProduct(): Promise<void> {
  if (isCreateDisabled.value) {
    return
  }

  isSubmitting.value = true
  errorMessage.value = null
  successMessage.value = null

  try {
    const response = await httpClient.post<ProductItem>('/products', {
      code: createForm.code.trim(),
      name: createForm.name.trim(),
      description: createForm.description.trim() || null,
      ownerPersonId: createForm.ownerPersonId,
    })

    showCreateForm.value = false
    successMessage.value = `Product "${response.data.name}" (${response.data.code}) created successfully.`
    await loadProducts()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to create product.')
  } finally {
    isSubmitting.value = false
  }
}

async function openEditProduct(product: ProductItem): Promise<void> {
  errorMessage.value = null
  successMessage.value = null

  try {
    const detailResponse = await httpClient.get<ProductItem>(`/products/${product.id}`)
    const detail = detailResponse.data
    editingProduct.value = detail
    editForm.id = detail.id
    editForm.code = detail.code
    editForm.name = detail.name
    editForm.description = detail.description ?? ''
    editForm.ownerPersonId = detail.ownerPersonId
  } catch {
    editingProduct.value = product
    editForm.id = product.id
    editForm.code = product.code
    editForm.name = product.name
    editForm.description = product.description ?? ''
    editForm.ownerPersonId = product.ownerPersonId
  }
}

function closeEditProduct(): void {
  editingProduct.value = null
}

async function handleUpdateProduct(): Promise<void> {
  if (!editingProduct.value || isEditDisabled.value) {
    return
  }

  isSubmitting.value = true
  errorMessage.value = null
  successMessage.value = null

  try {
    const productId = editForm.id
    await httpClient.put<ProductItem>(`/products/${productId}`, {
      name: editForm.name.trim(),
      description: editForm.description.trim() || null,
    })

    if (editForm.ownerPersonId !== editingProduct.value.ownerPersonId) {
      await httpClient.put<ProductItem>(`/products/${productId}/owner`, {
        newOwnerPersonId: editForm.ownerPersonId,
      })
    }

    editingProduct.value = null
    successMessage.value = `Product "${editForm.name.trim()}" updated successfully.`
    await loadProducts()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to update product.')
  } finally {
    isSubmitting.value = false
  }
}

function startInlineOwnerAssign(product: ProductItem): void {
  reassigningProductId.value = product.id
  selectedNewOwnerId.value = product.ownerPersonId
}

function cancelInlineOwnerAssign(): void {
  reassigningProductId.value = null
  selectedNewOwnerId.value = ''
}

async function handleAssignOwner(product: ProductItem): Promise<void> {
  if (!selectedNewOwnerId.value || selectedNewOwnerId.value === product.ownerPersonId) {
    cancelInlineOwnerAssign()
    return
  }

  assigningOwnerProductId.value = product.id
  errorMessage.value = null
  successMessage.value = null

  try {
    await httpClient.put<ProductItem>(`/products/${product.id}/owner`, {
      newOwnerPersonId: selectedNewOwnerId.value,
    })

    reassigningProductId.value = null
    successMessage.value = `Updated owner for "${product.name}".`
    await loadProducts()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to assign product owner.')
  } finally {
    assigningOwnerProductId.value = null
  }
}

async function handleToggleStatus(product: ProductItem): Promise<void> {
  togglingProductId.value = product.id
  errorMessage.value = null
  successMessage.value = null

  const isCurrentlyActive = product.status.toUpperCase() === 'ACTIVE'
  const actionPath = isCurrentlyActive
    ? `/products/${product.id}/deactivate`
    : `/products/${product.id}/activate`

  try {
    const response = await httpClient.post<ProductItem>(actionPath)
    successMessage.value = `Product "${response.data.name}" is now ${response.data.status}.`
    await loadProducts()
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to update product status.')
  } finally {
    togglingProductId.value = null
  }
}

onMounted(async () => {
  await Promise.all([loadActivePersons(), loadProducts()])
})
</script>

<template>
  <section class="product-catalog-view" data-screen-id="SCR-PRD-001">
    <!-- Screen Header -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2">
        <h1 class="h6 mb-0 fw-bold">Product Catalog</h1>
        <span class="badge text-bg-secondary font-monospace" style="font-size: 11px">SCR-PRD-001</span>
        <span class="text-body-secondary small d-none d-md-inline">| Product master data &amp; lifecycle ownership</span>
      </div>

      <div class="d-flex align-items-center gap-2">
        <div class="form-check form-switch mb-0 d-flex align-items-center gap-1">
          <input
            id="activeOnlySwitch"
            v-model="showActiveOnly"
            class="form-check-input my-0"
            type="checkbox"
            role="switch"
            data-testid="active-only-switch"
            @change="handleFilterToggle"
          />
          <label class="form-check-label small fw-medium mb-0" for="activeOnlySwitch" style="font-size: 11.5px">
            Active Only
          </label>
        </div>

        <button
          type="button"
          class="btn btn-outline-secondary btn-sm py-0 px-2"
          style="font-size: 12px; height: 26px; line-height: 24px"
          :disabled="isLoading"
          data-testid="refresh-catalog-button"
          @click="loadProducts"
        >
          <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>Refresh
        </button>

        <button
          type="button"
          class="btn btn-primary btn-sm py-0 px-2"
          style="font-size: 12px; height: 26px; line-height: 24px"
          data-testid="create-product-button"
          @click="openCreateForm"
        >
          <i class="bi bi-plus-lg me-1" aria-hidden="true"></i>New Product
        </button>
      </div>
    </div>

    <!-- Feedback Alerts -->
    <div
      v-if="errorMessage"
      role="alert"
      class="alert alert-danger alert-dismissible py-1 px-2 mb-2 small d-flex align-items-center gap-2"
      data-testid="product-error-alert"
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
      v-if="successMessage"
      role="status"
      class="alert alert-success alert-dismissible py-1 px-2 mb-2 small d-flex align-items-center gap-2"
      data-testid="product-success-alert"
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

    <!-- Create Product Inline Form Card -->
    <div
      v-if="showCreateForm"
      class="card shadow-none bg-slate-900/90 border border-slate-800 text-slate-100 mb-2"
      data-testid="create-product-modal"
    >
      <div class="card-header py-1 px-2 bg-slate-950/60 border-b border-slate-800 text-white d-flex justify-content-between align-items-center">
        <span class="fw-semibold small">
          <i class="bi bi-box-seam me-1" aria-hidden="true"></i>Create New Product
        </span>
        <button
          type="button"
          class="btn-close btn-close-white py-1 px-2"
          aria-label="Close"
          @click="closeCreateForm"
        ></button>
      </div>
      <div class="card-body p-2">
        <form novalidate data-testid="create-product-form" @submit.prevent="handleCreateProduct">
          <div class="row g-2">
            <div class="col-12 col-md-3">
              <label for="createProductCode" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Product Code <span class="text-danger">*</span>
              </label>
              <input
                id="createProductCode"
                v-model="createForm.code"
                type="text"
                class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                placeholder="e.g. MYHOSPITAL"
                maxlength="50"
                required
                :disabled="isSubmitting"
                data-testid="create-product-code-input"
              />
            </div>

            <div class="col-12 col-md-4">
              <label for="createProductName" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Product Name <span class="text-danger">*</span>
              </label>
              <input
                id="createProductName"
                v-model="createForm.name"
                type="text"
                class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                placeholder="e.g. MyHospital"
                maxlength="200"
                required
                :disabled="isSubmitting"
                data-testid="create-product-name-input"
              />
            </div>

            <div class="col-12 col-md-5">
              <label for="createProductOwner" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Product Owner <span class="text-danger">*</span>
              </label>
              <select
                id="createProductOwner"
                v-model="createForm.ownerPersonId"
                class="form-select form-select-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                required
                :disabled="isSubmitting"
                data-testid="create-product-owner-select"
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

            <div class="col-12">
              <label for="createProductDescription" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Description (Optional)
              </label>
              <input
                id="createProductDescription"
                v-model="createForm.description"
                type="text"
                class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                maxlength="1000"
                placeholder="Brief summary of product's business scope"
                :disabled="isSubmitting"
                data-testid="create-product-description-input"
              />
            </div>
          </div>

          <div class="d-flex justify-content-end gap-1 mt-2 pt-2 border-top">
            <button
              type="button"
              class="btn btn-outline-secondary btn-sm"
              :disabled="isSubmitting"
              @click="closeCreateForm"
            >
              Cancel
            </button>
            <button
              type="submit"
              class="btn btn-primary btn-sm"
              :disabled="isCreateDisabled"
              data-testid="create-product-submit-button"
            >
              <span
                v-if="isSubmitting"
                class="spinner-border spinner-border-sm me-1"
                role="status"
                aria-hidden="true"
              ></span>
              Save Product
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Edit Product Inline Form Card -->
    <div
      v-if="editingProduct"
      class="card shadow-none bg-slate-900/90 border border-slate-800 text-slate-100 mb-2"
      data-testid="edit-product-modal"
    >
      <div class="card-header py-1 px-2 bg-slate-950/60 border-b border-slate-800 d-flex justify-content-between align-items-center">
        <span class="fw-semibold small">
          <i class="bi bi-pencil-square me-1" aria-hidden="true"></i>
          Edit Product: <code class="text-cyan-400">{{ editForm.code }}</code>
        </span>
        <button
          type="button"
          class="btn-close py-1 px-2"
          aria-label="Close"
          @click="closeEditProduct"
        ></button>
      </div>
      <div class="card-body p-2">
        <form novalidate data-testid="edit-product-form" @submit.prevent="handleUpdateProduct">
          <div class="row g-2">
            <div class="col-12 col-md-3">
              <label class="form-label mb-0 small fw-medium" style="font-size: 11px">Product Code</label>
              <input
                type="text"
                class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100"
                :value="editForm.code"
                disabled
                readonly
              />
            </div>

            <div class="col-12 col-md-4">
              <label for="editProductName" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Product Name <span class="text-danger">*</span>
              </label>
              <input
                id="editProductName"
                v-model="editForm.name"
                type="text"
                class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                maxlength="200"
                required
                :disabled="isSubmitting"
                data-testid="edit-product-name-input"
              />
            </div>

            <div class="col-12 col-md-5">
              <label for="editProductOwner" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Product Owner <span class="text-danger">*</span>
              </label>
              <select
                id="editProductOwner"
                v-model="editForm.ownerPersonId"
                class="form-select form-select-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                required
                :disabled="isSubmitting"
                data-testid="edit-product-owner-select"
              >
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
              <label for="editProductDescription" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Description
              </label>
              <input
                id="editProductDescription"
                v-model="editForm.description"
                type="text"
                class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                maxlength="1000"
                :disabled="isSubmitting"
                data-testid="edit-product-description-input"
              />
            </div>
          </div>

          <div class="d-flex justify-content-end gap-1 mt-2 pt-2 border-t border-slate-800">
            <button
              type="button"
              class="btn btn-outline-secondary btn-sm"
              :disabled="isSubmitting"
              @click="closeEditProduct"
            >
              Cancel
            </button>
            <button
              type="submit"
              class="btn btn-primary btn-sm"
              :disabled="isEditDisabled"
              data-testid="edit-product-submit-button"
            >
              <span
                v-if="isSubmitting"
                class="spinner-border spinner-border-sm me-1"
                role="status"
                aria-hidden="true"
              ></span>
              Update Product
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Product Catalog Table -->
    <div class="card card-table shadow-none bg-slate-900/90 border border-slate-800 text-slate-100 mb-2">
      <div class="card-body p-0">
        <div class="table-responsive">
          <table
            class="table table-hover align-middle mb-0 text-nowrap"
            data-testid="products-table"
          >
            <thead class="table-light">
              <tr>
                <th scope="col" style="width: 140px">Code</th>
                <th scope="col" style="min-width: 200px">Name</th>
                <th scope="col">Owner</th>
                <th scope="col" class="text-center" style="width: 90px">Status</th>
                <th scope="col" class="text-end" style="width: 150px">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="isLoading">
                <td colspan="5" class="text-center py-4 text-body-secondary small">
                  <span
                    class="spinner-border spinner-border-sm me-2"
                    role="status"
                    aria-hidden="true"
                  ></span>
                  Loading product catalog...
                </td>
              </tr>

              <tr v-else-if="products.length === 0">
                <td colspan="5" class="text-center py-4 text-body-secondary small" data-testid="empty-catalog-row">
                  No products found. Click <strong>New Product</strong> to create one.
                </td>
              </tr>

              <tr
                v-for="product in products"
                v-else
                :key="product.id"
                :data-product-id="product.id"
                data-testid="product-row"
              >
                <!-- Code Column -->
                <td class="fw-semibold">
                  <code class="text-primary font-monospace" style="font-size: 11.5px">{{ product.code }}</code>
                </td>

                <!-- Name Column -->
                <td>
                  <span class="fw-medium">{{ product.name }}</span>
                  <span v-if="product.description" class="text-body-secondary small ms-2 text-truncate d-inline-block" style="max-width: 260px">
                    {{ product.description }}
                  </span>
                </td>

                <!-- Owner Column -->
                <td>
                  <div
                    v-if="reassigningProductId === product.id"
                    class="d-flex align-items-center gap-1"
                  >
                    <select
                      v-model="selectedNewOwnerId"
                      class="form-select form-select-sm py-0"
                      style="max-width: 180px; font-size: 11.5px; height: 24px"
                      :disabled="assigningOwnerProductId === product.id"
                      data-testid="inline-owner-select"
                    >
                      <option
                        v-for="person in activePersons"
                        :key="person.id"
                        :value="person.id"
                      >
                        {{ person.fullName }}
                      </option>
                    </select>
                    <button
                      type="button"
                      class="btn btn-xs btn-primary py-0 px-1"
                      style="font-size: 11px; height: 24px; line-height: 22px"
                      :disabled="assigningOwnerProductId === product.id"
                      data-testid="confirm-owner-button"
                      @click="handleAssignOwner(product)"
                    >
                      Save
                    </button>
                    <button
                      type="button"
                      class="btn btn-xs btn-outline-secondary py-0 px-1"
                      style="font-size: 11px; height: 24px; line-height: 22px"
                      :disabled="assigningOwnerProductId === product.id"
                      @click="cancelInlineOwnerAssign"
                    >
                      Cancel
                    </button>
                  </div>

                  <div v-else class="d-flex align-items-center gap-1">
                    <span class="small">{{ resolveOwnerDisplay(product) }}</span>
                    <button
                      type="button"
                      class="btn btn-link btn-sm p-0 text-decoration-none text-body-secondary"
                      title="Assign Product Owner"
                      data-testid="assign-owner-button"
                      @click="startInlineOwnerAssign(product)"
                    >
                      <i class="bi bi-pencil-fill" style="font-size: 10px" aria-hidden="true"></i>
                    </button>
                  </div>
                </td>

                <!-- Status Column -->
                <td class="text-center">
                  <span
                    class="badge"
                    style="font-size: 10px; padding: 2px 6px"
                    :class="
                      product.status.toUpperCase() === 'ACTIVE'
                        ? 'text-bg-success'
                        : 'text-bg-secondary'
                    "
                    data-testid="product-status-badge"
                  >
                    {{ product.status }}
                  </span>
                </td>

                <!-- Actions Column (Edit + Status Toggle Button per row) -->
                <td class="text-end">
                  <div class="d-inline-flex align-items-center gap-1">
                    <button
                      type="button"
                      class="btn btn-outline-secondary btn-sm py-0 px-2"
                      style="font-size: 11px; height: 22px; line-height: 20px"
                      data-testid="edit-product-button"
                      @click="openEditProduct(product)"
                    >
                      <i class="bi bi-pencil me-1" aria-hidden="true"></i>Edit
                    </button>

                    <button
                      type="button"
                      class="btn btn-sm py-0 px-2"
                      style="font-size: 11px; height: 22px; line-height: 20px"
                      :class="
                        product.status.toUpperCase() === 'ACTIVE'
                          ? 'btn-outline-warning'
                          : 'btn-outline-success'
                      "
                      :disabled="togglingProductId === product.id"
                      data-testid="toggle-status-button"
                      @click="handleToggleStatus(product)"
                    >
                      <span
                        v-if="togglingProductId === product.id"
                        class="spinner-border spinner-border-sm me-1"
                        role="status"
                        aria-hidden="true"
                      ></span>
                      {{ product.status.toUpperCase() === 'ACTIVE' ? 'Deactivate' : 'Activate' }}
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </section>
</template>
