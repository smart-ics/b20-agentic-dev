<script setup lang="ts">
import { AxiosError } from 'axios'
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'

import { createCustomer, type CustomerDto } from '@/api/customers'

/**
 * SCR-CUST-001: Create Customer Modal Component
 * (Architecture §7, §8 — CR-002, SCR-CUST-001).
 *
 * - Captures Customer Code, Customer Name, and Maintenance Contract toggle.
 * - Enforces client-side required field validation before calling API.
 * - Handles duplicate Customer Code conflict errors (409 Conflict) and generic API errors.
 * - Emits `saved` event on successful customer creation to refresh portfolio.
 */

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
  (e: 'saved', customer: CustomerDto): void
}>()

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const customerCode = ref('')
const customerName = ref('')
const hasActiveMaintenanceContract = ref(false)

const isSubmitting = ref(false)
const errorMessage = ref<string | null>(null)
const validationErrors = ref<{ customerCode?: string; customerName?: string }>({})

function extractErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof AxiosError) {
    if (err.response?.status === 409) {
      const data = err.response.data as ProblemDetailsPayload | undefined
      return data?.detail || 'Customer Code already exists. Please specify a unique code.'
    }
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

function resetForm(): void {
  customerCode.value = ''
  customerName.value = ''
  hasActiveMaintenanceContract.value = false
  errorMessage.value = null
  validationErrors.value = {}
}

function validate(): boolean {
  const errors: { customerCode?: string; customerName?: string } = {}
  if (!customerCode.value.trim()) {
    errors.customerCode = 'Customer Code is required.'
  }
  if (!customerName.value.trim()) {
    errors.customerName = 'Customer Name is required.'
  }
  validationErrors.value = errors
  return Object.keys(errors).length === 0
}

async function handleSubmit(): Promise<void> {
  errorMessage.value = null
  if (!validate()) {
    return
  }

  isSubmitting.value = true

  try {
    const newCustomer = await createCustomer({
      customerCode: customerCode.value.trim(),
      customerName: customerName.value.trim(),
      hasActiveMaintenanceContract: hasActiveMaintenanceContract.value,
    })

    emit('saved', newCustomer)
    resetForm()
    emit('close')
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to create customer record.')
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
  (visible) => {
    if (visible) {
      resetForm()
    }
  },
)

onMounted(() => {
  window.addEventListener('keydown', handleKeydown)
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <div v-if="props.show" data-screen-id="SCR-CUST-001">
    <!-- Bootstrap Modal Dialog -->
    <div
      class="modal fade show d-block"
      tabindex="-1"
      role="dialog"
      aria-labelledby="createCustomerModalTitle"
      aria-modal="true"
    >
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content shadow">
          <div class="modal-header">
            <h5 id="createCustomerModalTitle" class="modal-title">
              <i class="bi bi-building-add me-2 text-primary" aria-hidden="true"></i>
              Add New Customer
            </h5>
            <button
              type="button"
              class="btn-close"
              aria-label="Close"
              :disabled="isSubmitting"
              @click="handleClose"
            ></button>
          </div>

          <form @submit.prevent="handleSubmit">
            <div class="modal-body">
              <!-- Error Alert -->
              <div
                v-if="errorMessage"
                class="alert alert-danger alert-dismissible fade show"
                role="alert"
                data-testid="create-customer-error-alert"
              >
                <i class="bi bi-exclamation-triangle-fill me-2" aria-hidden="true"></i>
                {{ errorMessage }}
                <button
                  type="button"
                  class="btn-close"
                  aria-label="Close"
                  @click="errorMessage = null"
                ></button>
              </div>

              <!-- Customer Code -->
              <div class="mb-3">
                <label for="createCustomerCode" class="form-label fw-semibold">
                  Customer Code <span class="text-danger">*</span>
                </label>
                <input
                  id="createCustomerCode"
                  v-model="customerCode"
                  type="text"
                  class="form-select-sm form-control"
                  :class="{ 'is-invalid': validationErrors.customerCode }"
                  placeholder="e.g. CUST-001"
                  :disabled="isSubmitting"
                  data-testid="input-customer-code"
                />
                <div v-if="validationErrors.customerCode" class="invalid-feedback">
                  {{ validationErrors.customerCode }}
                </div>
              </div>

              <!-- Customer Name -->
              <div class="mb-3">
                <label for="createCustomerName" class="form-label fw-semibold">
                  Customer Name <span class="text-danger">*</span>
                </label>
                <input
                  id="createCustomerName"
                  v-model="customerName"
                  type="text"
                  class="form-control"
                  :class="{ 'is-invalid': validationErrors.customerName }"
                  placeholder="e.g. PT Acme Corporation"
                  :disabled="isSubmitting"
                  data-testid="input-customer-name"
                />
                <div v-if="validationErrors.customerName" class="invalid-feedback">
                  {{ validationErrors.customerName }}
                </div>
              </div>

              <!-- Maintenance Contract Toggle -->
              <div class="form-check form-switch mb-3">
                <input
                  id="createHasMaintenanceContract"
                  v-model="hasActiveMaintenanceContract"
                  class="form-check-input"
                  type="checkbox"
                  role="switch"
                  :disabled="isSubmitting"
                  data-testid="toggle-maintenance-contract"
                />
                <label
                  class="form-check-label fw-semibold"
                  for="createHasMaintenanceContract"
                >
                  Active Maintenance Contract
                </label>
                <div class="form-text">
                  Indicates whether this customer has an active SLA maintenance agreement.
                </div>
              </div>
            </div>

            <div class="modal-footer">
              <button
                type="button"
                class="btn btn-secondary"
                :disabled="isSubmitting"
                data-testid="cancel-create-customer"
                @click="handleClose"
              >
                Cancel
              </button>
              <button
                type="submit"
                class="btn btn-primary"
                :disabled="isSubmitting"
                data-testid="submit-create-customer"
              >
                <span
                  v-if="isSubmitting"
                  class="spinner-border spinner-border-sm me-1"
                  role="status"
                  aria-hidden="true"
                ></span>
                <i v-else class="bi bi-check-lg me-1" aria-hidden="true"></i>
                Save Customer
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
