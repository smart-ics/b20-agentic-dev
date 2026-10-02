<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'

import { httpClient } from '@/api/http'

/**
 * SCR-REQ-002: Create Request Screen (Architecture §7, §8 — UC-REQ-001, §9 — FEAT-REQ-001, §19.4, §19.6, §21).
 *
 * - Renders a Bootstrap 5 form with fields: Title, Description, optional RequestType & Priority,
 *   Customer select dropdown (populated from `GET /api/v1/customers/active`), and
 *   Product select dropdown (populated from `GET /api/v1/products/active`).
 * - Submits the new request via `POST /api/v1/requests` (`RequestService.RecordRequest`).
 * - Navigates to the newly created request's detail screen (`/requests/${id}`) on success.
 */

export interface ActiveCustomerOption {
  id: string
  customerId?: string
  customerCode: string
  code: string
  customerName: string
  name: string
  status: string
  hasActiveMaintenanceContract: boolean
  isActive: boolean
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
  isActive: boolean
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

const router = useRouter()

const activeCustomers = ref<ActiveCustomerOption[]>([])
const activeProducts = ref<ActiveProductOption[]>([])
const isLoadingLookups = ref(false)
const isSubmitting = ref(false)
const errorMessage = ref<string | null>(null)

const form = reactive({
  title: '',
  description: '',
  customerId: '',
  productId: '',
  requestType: 'GENERAL',
  priority: 'NORMAL',
})

const isSubmitDisabled = computed(
  () =>
    isSubmitting.value ||
    form.title.trim().length === 0 ||
    form.description.trim().length === 0,
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

function resolveCustomerLabel(customer: ActiveCustomerOption): string {
  const name = customer.customerName || customer.name
  const code = customer.customerCode || customer.code
  return code ? `${name} (${code})` : name
}

function resolveProductLabel(product: ActiveProductOption): string {
  const name = product.name || product.productName || ''
  const code = product.code || product.productCode || ''
  return code ? `${name} (${code})` : name
}

async function loadLookups(): Promise<void> {
  isLoadingLookups.value = true
  errorMessage.value = null

  try {
    const [customersResponse, productsResponse] = await Promise.all([
      httpClient.get<ActiveCustomerOption[]>('/customers/active'),
      httpClient.get<ActiveProductOption[]>('/products/active'),
    ])
    activeCustomers.value = customersResponse.data
    activeProducts.value = productsResponse.data
  } catch (err) {
    errorMessage.value = extractErrorMessage(
      err,
      'Failed to load active customers or products for selection.',
    )
  } finally {
    isLoadingLookups.value = false
  }
}

async function handleSubmit(): Promise<void> {
  errorMessage.value = null

  const trimmedTitle = form.title.trim()
  const trimmedDescription = form.description.trim()

  if (!trimmedTitle || !trimmedDescription) {
    errorMessage.value = 'Title and Description are required to record a request.'
    return
  }

  isSubmitting.value = true

  try {
    const response = await httpClient.post<CreatedRequestResponse>('/requests', {
      title: trimmedTitle,
      description: trimmedDescription,
      customerId: form.customerId.trim() || null,
      productId: form.productId.trim() || null,
      requestType: form.requestType || 'GENERAL',
      priority: form.priority || 'NORMAL',
    })

    const createdId = response.data.id ?? response.data.requestId
    await router.push(`/requests/${createdId}`)
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to create request.')
  } finally {
    isSubmitting.value = false
  }
}

async function handleCancel(): Promise<void> {
  await router.push('/requests')
}

onMounted(async () => {
  await loadLookups()
})
</script>

<template>
  <section class="create-request-view" data-screen-id="SCR-REQ-002">
    <!-- Screen Header -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2">
        <h1 class="h6 mb-0 fw-bold">Create Request</h1>
        <span class="badge text-bg-secondary font-monospace" style="font-size: 11px">SCR-REQ-002</span>
        <span class="text-body-secondary small d-none d-md-inline">| Record new demand in CAPTURED state</span>
      </div>

      <router-link
        to="/requests"
        class="btn btn-outline-secondary btn-sm py-0 px-2"
        style="font-size: 12px; height: 26px; line-height: 24px"
        data-testid="back-to-requests-button"
      >
        <i class="bi bi-arrow-left me-1" aria-hidden="true"></i>Back to Requests
      </router-link>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      role="alert"
      class="alert alert-danger alert-dismissible py-1 px-2 mb-2 small d-flex align-items-center gap-2"
      data-testid="create-request-error-alert"
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

    <!-- Create Request Form Card -->
    <div class="card shadow-none border mb-2">
      <div class="card-header py-1 px-2 bg-body-tertiary fw-semibold small">
        <i class="bi bi-file-earmark-plus me-1 text-primary" aria-hidden="true"></i>
        New Request Specifications
      </div>

      <div class="card-body p-2 p-md-3">
        <form
          novalidate
          data-testid="create-request-form"
          @submit.prevent="handleSubmit"
        >
          <div class="row g-2">
            <!-- Title -->
            <div class="col-12 col-md-8">
              <label for="requestTitle" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Title <span class="text-danger">*</span>
              </label>
              <input
                id="requestTitle"
                v-model="form.title"
                type="text"
                name="title"
                class="form-control form-control-sm"
                placeholder="Brief summary or subject of the request"
                maxlength="255"
                required
                :disabled="isSubmitting"
                data-testid="request-title-input"
              />
            </div>

            <!-- Request Type -->
            <div class="col-6 col-md-2">
              <label for="requestType" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Type
              </label>
              <select
                id="requestType"
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
              <label for="requestPriority" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Priority
              </label>
              <select
                id="requestPriority"
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
            <div class="col-12 col-md-6">
              <label for="requestCustomer" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Customer (Optional)
              </label>
              <select
                id="requestCustomer"
                v-model="form.customerId"
                name="customerId"
                class="form-select form-select-sm"
                :disabled="isSubmitting || isLoadingLookups"
                data-testid="request-customer-select"
              >
                <option value="">Select customer...</option>
                <option
                  v-for="customer in activeCustomers"
                  :key="customer.id"
                  :value="customer.id"
                >
                  {{ resolveCustomerLabel(customer) }}
                </option>
              </select>
            </div>

            <!-- Product Select -->
            <div class="col-12 col-md-6">
              <label for="requestProduct" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Product (Optional)
              </label>
              <select
                id="requestProduct"
                v-model="form.productId"
                name="productId"
                class="form-select form-select-sm"
                :disabled="isSubmitting || isLoadingLookups"
                data-testid="request-product-select"
              >
                <option value="">Select product...</option>
                <option
                  v-for="product in activeProducts"
                  :key="product.id"
                  :value="product.id"
                >
                  {{ resolveProductLabel(product) }}
                </option>
              </select>
            </div>

            <!-- Description -->
            <div class="col-12">
              <label for="requestDescription" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Description <span class="text-danger">*</span>
              </label>
              <textarea
                id="requestDescription"
                v-model="form.description"
                name="description"
                class="form-control form-control-sm"
                rows="4"
                placeholder="Detailed description and operational context of the request"
                required
                :disabled="isSubmitting"
                data-testid="request-description-input"
              ></textarea>
            </div>
          </div>

          <!-- Form Actions -->
          <div class="d-flex justify-content-end gap-1 mt-2 pt-2 border-top">
            <button
              type="button"
              class="btn btn-outline-secondary btn-sm"
              :disabled="isSubmitting"
              data-testid="cancel-request-button"
              @click="handleCancel"
            >
              Cancel
            </button>
            <button
              type="submit"
              class="btn btn-primary btn-sm"
              :disabled="isSubmitDisabled"
              data-testid="submit-request-button"
            >
              <span
                v-if="isSubmitting"
                class="spinner-border spinner-border-sm me-1"
                role="status"
                aria-hidden="true"
              ></span>
              <i v-else class="bi bi-check-lg me-1" aria-hidden="true"></i>
              {{ isSubmitting ? 'Submitting...' : 'Submit Request' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </section>
</template>
