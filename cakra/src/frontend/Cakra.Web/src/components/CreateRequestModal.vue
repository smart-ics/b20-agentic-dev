<script setup lang="ts">
import { AxiosError } from 'axios'
import { onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'

import { httpClient } from '@/api/http'

/**
 * SCR-FEED-001 / SCR-REQ-002: Create Request Modal Component
 * (Architecture §4, §5, §6 — CR-003, FEAT-REQ-001, FEAT-AWR-001).
 *
 * - Renders a Bootstrap 5 modal dialog capturing new Request specifications.
 * - Form inputs: Title (required), Description (required), Customer dropdown,
 *   Product dropdown, Request Type dropdown (default GENERAL), Priority dropdown (default NORMAL).
 * - Enforces client-side validation for required Title and Description.
 * - Lazily fetches active customers (`GET /api/v1/customers/active`) and active products
 *   (`GET /api/v1/products/active`) on modal display.
 * - Submits payload to `POST /api/v1/requests` via `httpClient`.
 * - Handles RFC 7807 ProblemDetails error responses without discarding entered inputs.
 * - Emits `close` and `saved(createdRequest)` events.
 */

export interface ActiveCustomerOption {
  id: string
  customerId?: string
  customerCode?: string
  code?: string
  customerName?: string
  name?: string
  status?: string
  hasActiveMaintenanceContract?: boolean
  isActive?: boolean
}

export interface ActiveProductOption {
  id: string
  productId?: string
  code?: string
  productCode?: string
  name?: string
  productName?: string
  description?: string | null
  ownerPersonId?: string
  ownerName?: string | null
  status?: string
  isActive?: boolean
}

export interface CreatedRequestResponse {
  id: string
  requestId?: string
  title: string
  description: string
  status: string
  customerId?: string | null
  productId?: string | null
  requestType?: string
  priority?: string
  createdAt?: string
}

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const REQUEST_TYPES = ['GENERAL', 'BUG', 'FEATURE', 'SUPPORT', 'CHANGE_REQUEST', 'INCIDENT'] as const
const PRIORITIES = ['LOW', 'NORMAL', 'HIGH', 'URGENT'] as const

const props = withDefaults(
  defineProps<{
    show?: boolean
  }>(),
  {
    show: false,
  },
)

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'saved', request: CreatedRequestResponse): void
}>()

const activeCustomers = ref<ActiveCustomerOption[]>([])
const activeProducts = ref<ActiveProductOption[]>([])
const isLoadingLookups = ref(false)
const isSubmitting = ref(false)
const errorMessage = ref<string | null>(null)
const validationErrors = ref<{ title?: string; description?: string }>({})

const form = reactive({
  title: '',
  description: '',
  customerId: '',
  productId: '',
  requestType: 'GENERAL',
  priority: 'NORMAL',
  complexity: 1,
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
  if (err instanceof Error && err.message) {
    return err.message
  }
  return fallback
}

function resolveCustomerLabel(customer: ActiveCustomerOption): string {
  const name = customer.customerName || customer.name || ''
  const code = customer.customerCode || customer.code || ''
  return code ? `${name} (${code})` : name
}

function resolveProductLabel(product: ActiveProductOption): string {
  const name = product.name || product.productName || ''
  const code = product.code || product.productCode || ''
  return code ? `${name} (${code})` : name
}

async function loadLookups(): Promise<void> {
  if (activeCustomers.value.length > 0 && activeProducts.value.length > 0) {
    return
  }

  isLoadingLookups.value = true
  try {
    const [customersResponse, productsResponse] = await Promise.all([
      httpClient.get<ActiveCustomerOption[]>('/customers/active'),
      httpClient.get<ActiveProductOption[]>('/products/active'),
    ])
    activeCustomers.value = customersResponse.data ?? []
    activeProducts.value = productsResponse.data ?? []
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(
      err,
      'Failed to load active customers or products for selection.',
    )
  } finally {
    isLoadingLookups.value = false
  }
}

function resetForm(): void {
  form.title = ''
  form.description = ''
  form.customerId = ''
  form.productId = ''
  form.requestType = 'GENERAL'
  form.priority = 'NORMAL'
  form.complexity = 1
  errorMessage.value = null
  validationErrors.value = {}
}

function validate(): boolean {
  const errors: { title?: string; description?: string } = {}
  if (!form.title.trim()) {
    errors.title = 'Title is required.'
  }
  if (!form.description.trim()) {
    errors.description = 'Description is required.'
  }
  validationErrors.value = errors
  if (Object.keys(errors).length > 0) {
    errorMessage.value = 'Title and Description are required to record a request.'
    return false
  }
  return true
}

async function handleSubmit(): Promise<void> {
  errorMessage.value = null
  if (!validate()) {
    return
  }

  isSubmitting.value = true

  try {
    const response = await httpClient.post<CreatedRequestResponse>('/requests', {
      title: form.title.trim(),
      description: form.description.trim(),
      customerId: form.customerId.trim() || null,
      productId: form.productId.trim() || null,
      requestType: form.requestType || 'GENERAL',
      priority: form.priority || 'NORMAL',
      complexity: Number(form.complexity) || 1,
    })

    emit('saved', response.data)
    resetForm()
    emit('close')
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to create request.')
  } finally {
    isSubmitting.value = false
  }
}

function handleClose(): void {
  resetForm()
  emit('close')
}

function handleKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape' && props.show) {
    handleClose()
  }
}

watch(
  () => props.show,
  async (visible) => {
    if (visible) {
      resetForm()
      await loadLookups()
    }
  },
)

onMounted(async () => {
  window.addEventListener('keydown', handleKeydown)
  if (props.show) {
    await loadLookups()
  }
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <div v-if="props.show" data-screen-id="SCR-FEED-001" data-testid="create-request-modal">
    <!-- Bootstrap Modal Dialog -->
    <div
      class="modal fade show d-block"
      tabindex="-1"
      role="dialog"
      aria-labelledby="createRequestModalTitle"
      aria-modal="true"
    >
      <div class="modal-dialog modal-dialog-centered modal-lg">
        <div class="modal-content shadow border">
          <div class="modal-header py-1 px-3 bg-body-tertiary">
            <h2 id="createRequestModalTitle" class="modal-title h6 fw-bold mb-0">
              <i class="bi bi-file-earmark-plus me-1 text-primary" aria-hidden="true"></i>
              Create New Request
            </h2>
            <button
              type="button"
              class="btn-close py-1 px-2"
              aria-label="Close"
              :disabled="isSubmitting"
              @click="handleClose"
            ></button>
          </div>

          <form novalidate data-testid="create-request-form" @submit.prevent="handleSubmit">
            <div class="modal-body p-2 p-md-3">
              <!-- Error Alert -->
              <div
                v-if="errorMessage"
                class="alert alert-danger alert-dismissible fade show py-1 px-2 mb-2 small"
                role="alert"
                data-testid="create-request-error-alert"
              >
                <i class="bi bi-exclamation-triangle-fill me-1" aria-hidden="true"></i>
                {{ errorMessage }}
                <button
                  type="button"
                  class="btn-close py-1 px-2"
                  aria-label="Close"
                  @click="errorMessage = null"
                ></button>
              </div>

              <div class="row g-2">
                <!-- Title -->
                <div class="col-12 col-md-8">
                  <label for="createRequestTitle" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                    Title <span class="text-danger">*</span>
                  </label>
                  <input
                    id="createRequestTitle"
                    v-model="form.title"
                    type="text"
                    name="title"
                    class="form-control form-control-sm"
                    :class="{ 'is-invalid': validationErrors.title }"
                    placeholder="Brief summary or subject of the request"
                    maxlength="255"
                    required
                    :disabled="isSubmitting"
                    data-testid="request-title-input"
                  />
                  <div v-if="validationErrors.title" class="invalid-feedback small" style="font-size: 11px">
                    {{ validationErrors.title }}
                  </div>
                </div>

                <!-- Request Type -->
                <div class="col-6 col-md-2">
                  <label for="createRequestType" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                    Type
                  </label>
                  <select
                    id="createRequestType"
                    v-model="form.requestType"
                    name="requestType"
                    class="form-select form-select-sm"
                    :disabled="isSubmitting"
                    data-testid="request-type-select"
                  >
                    <option v-for="type in REQUEST_TYPES" :key="type" :value="type">
                      {{ type }}
                    </option>
                  </select>
                </div>

                <!-- Priority -->
                <div class="col-6 col-md-2">
                  <label for="createRequestPriority" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                    Priority
                  </label>
                  <select
                    id="createRequestPriority"
                    v-model="form.priority"
                    name="priority"
                    class="form-select form-select-sm"
                    :disabled="isSubmitting"
                    data-testid="request-priority-select"
                  >
                    <option v-for="priority in PRIORITIES" :key="priority" :value="priority">
                      {{ priority }}
                    </option>
                  </select>
                </div>

                <!-- Customer Select -->
                <div class="col-12 col-md-4">
                  <label for="createRequestCustomer" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                    Customer (Optional)
                  </label>
                  <select
                    id="createRequestCustomer"
                    v-model="form.customerId"
                    name="customerId"
                    class="form-select form-select-sm"
                    :disabled="isSubmitting || isLoadingLookups"
                    data-testid="request-customer-select"
                  >
                    <option value="">Select customer...</option>
                    <option
                      v-for="customer in activeCustomers"
                      :key="customer.id || customer.customerId"
                      :value="customer.id || customer.customerId"
                    >
                      {{ resolveCustomerLabel(customer) }}
                    </option>
                  </select>
                </div>

                <!-- Product Select -->
                <div class="col-12 col-md-4">
                  <label for="createRequestProduct" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                    Product (Optional)
                  </label>
                  <select
                    id="createRequestProduct"
                    v-model="form.productId"
                    name="productId"
                    class="form-select form-select-sm"
                    :disabled="isSubmitting || isLoadingLookups"
                    data-testid="request-product-select"
                  >
                    <option value="">Select product...</option>
                    <option
                      v-for="product in activeProducts"
                      :key="product.id || product.productId"
                      :value="product.id || product.productId"
                    >
                      {{ resolveProductLabel(product) }}
                    </option>
                  </select>
                </div>

                <!-- Complexity Select -->
                <div class="col-12 col-md-4">
                  <label for="createRequestComplexity" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                    Complexity
                  </label>
                  <select
                    id="createRequestComplexity"
                    v-model.number="form.complexity"
                    name="complexity"
                    class="form-select form-select-sm"
                    :disabled="isSubmitting"
                    data-testid="request-complexity-select"
                  >
                    <option :value="1">1 (Very Low)</option>
                    <option :value="2">2 (Low)</option>
                    <option :value="3">3 (Medium)</option>
                    <option :value="4">4 (High)</option>
                    <option :value="5">5 (Very High)</option>
                  </select>
                </div>

                <!-- Description -->
                <div class="col-12">
                  <label for="createRequestDescription" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                    Description <span class="text-danger">*</span>
                  </label>
                  <textarea
                    id="createRequestDescription"
                    v-model="form.description"
                    name="description"
                    class="form-control form-control-sm"
                    :class="{ 'is-invalid': validationErrors.description }"
                    rows="4"
                    placeholder="Detailed description and operational context of the request"
                    required
                    :disabled="isSubmitting"
                    data-testid="request-description-input"
                  ></textarea>
                  <div v-if="validationErrors.description" class="invalid-feedback small" style="font-size: 11px">
                    {{ validationErrors.description }}
                  </div>
                </div>
              </div>
            </div>

            <div class="modal-footer py-1 px-3 bg-body-tertiary">
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm py-0 px-2"
                style="font-size: 11px; height: 24px; line-height: 22px"
                :disabled="isSubmitting"
                data-testid="cancel-create-request"
                @click="handleClose"
              >
                Cancel
              </button>
              <button
                type="submit"
                class="btn btn-primary btn-sm py-0 px-2"
                style="font-size: 11px; height: 24px; line-height: 22px"
                :disabled="isSubmitting"
                data-testid="submit-create-request"
              >
                <span
                  v-if="isSubmitting"
                  class="spinner-border spinner-border-sm me-1"
                  role="status"
                  aria-hidden="true"
                ></span>
                <i v-else class="bi bi-check-lg me-1" aria-hidden="true"></i>
                Submit Request
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Backdrop -->
    <div class="modal-backdrop fade show" @click="handleClose"></div>
  </div>
</template>
