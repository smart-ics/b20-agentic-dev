<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'

import {
  createCustomer,
  updateCustomer,
  type CustomerDto,
} from '@/api/customers'

/**
 * SCR-CUST-002: Customer Master Create & Edit Modal Component
 * (Architecture §7, §8, §19.4, §19.6 — CR-010, FEAT-CUST-001).
 *
 * - In 'create' mode: captures Customer Code (uppercase), Customer Name, and Active Maintenance Contract flag.
 * - In 'edit' mode: displays Customer Code as read-only, allows updating Customer Name and Maintenance Contract flag.
 * - Enforces client-side required field validation before API invocation.
 * - Handles duplicate Customer Code conflict errors (409 Conflict) and RFC 7807 problem details inline.
 * - Emits `saved` and `close` on successful operation.
 */

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const props = withDefaults(
  defineProps<{
    isOpen?: boolean
    show?: boolean
    mode?: 'create' | 'edit'
    customer?: CustomerDto | null
  }>(),
  {
    isOpen: false,
    show: false,
    mode: 'create',
    customer: null,
  },
)

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'saved', customer: CustomerDto): void
}>()

// Form state
const customerCode = ref('')
const customerName = ref('')
const hasActiveMaintenanceContract = ref(false)

// UI state & validation
const isSubmitting = ref(false)
const errorMessage = ref<string | null>(null)
const validationErrors = ref<{
  customerCode?: string
  customerName?: string
}>({})

const isVisible = computed(() => props.isOpen || props.show)
const isEditMode = computed(() => props.mode === 'edit')
const modalTitle = computed(() => (isEditMode.value ? 'Edit Customer' : 'Add New Customer'))

function resolveCustomerId(): string | null {
  if (!props.customer) return null
  return (props.customer.id || props.customer.customerId || '').trim() || null
}

function handleCodeInput(event: Event): void {
  const target = event.target as HTMLInputElement
  if (target) {
    customerCode.value = target.value.toUpperCase()
  }
}

function extractErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof AxiosError) {
    if (err.response?.status === 409) {
      const data = err.response.data as ProblemDetailsPayload | undefined
      return (
        data?.detail ||
        `A customer with code '${customerCode.value.trim()}' already exists. Please specify a unique code.`
      )
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
  errorMessage.value = null
  validationErrors.value = {}

  if (props.mode === 'edit' && props.customer) {
    customerCode.value = (props.customer.customerCode || props.customer.code || '').trim().toUpperCase()
    customerName.value = (props.customer.customerName || props.customer.name || '').trim()
    hasActiveMaintenanceContract.value = Boolean(props.customer.hasActiveMaintenanceContract)
  } else {
    customerCode.value = ''
    customerName.value = ''
    hasActiveMaintenanceContract.value = false
  }
}

function validate(): boolean {
  const errors: {
    customerCode?: string
    customerName?: string
  } = {}

  const trimmedCode = customerCode.value.trim()
  if (!trimmedCode) {
    errors.customerCode = 'Customer Code is required.'
  } else if (trimmedCode.length > 50) {
    errors.customerCode = 'Customer Code cannot exceed 50 characters.'
  }

  const trimmedName = customerName.value.trim()
  if (!trimmedName) {
    errors.customerName = 'Customer Name is required.'
  } else if (trimmedName.length > 200) {
    errors.customerName = 'Customer Name cannot exceed 200 characters.'
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
    let savedCustomer: CustomerDto
    const formattedCode = customerCode.value.trim().toUpperCase()
    const formattedName = customerName.value.trim()

    if (isEditMode.value) {
      const customerId = resolveCustomerId()
      if (!customerId) {
        throw new Error('Customer ID is missing for update operation.')
      }

      savedCustomer = await updateCustomer(customerId, {
        customerCode: formattedCode,
        customerName: formattedName,
        hasActiveMaintenanceContract: hasActiveMaintenanceContract.value,
      })
    } else {
      savedCustomer = await createCustomer({
        customerCode: formattedCode,
        customerName: formattedName,
        hasActiveMaintenanceContract: hasActiveMaintenanceContract.value,
      })
    }

    emit('saved', savedCustomer)
    resetForm()
    emit('close')
  } catch (err: unknown) {
    const defaultMsg = isEditMode.value
      ? 'Failed to update customer record.'
      : 'Failed to create customer record.'
    errorMessage.value = extractErrorMessage(err, defaultMsg)
  } finally {
    isSubmitting.value = false
  }
}

function handleClose(): void {
  if (!isSubmitting.value) {
    resetForm()
    emit('close')
  }
}

function handleKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape' && isVisible.value && !isSubmitting.value) {
    handleClose()
  }
}

watch(
  () => [props.isOpen, props.show],
  ([openVal, showVal]) => {
    if (openVal || showVal) {
      resetForm()
    }
  },
  { immediate: true },
)

watch(
  () => props.customer,
  () => {
    if (isVisible.value) {
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
  <div v-if="isVisible" data-screen-id="SCR-CUST-002" data-testid="customer-modal">
    <!-- Bootstrap Modal Dialog -->
    <div
      class="modal fade show d-block"
      tabindex="-1"
      role="dialog"
      aria-labelledby="customerModalTitle"
      aria-modal="true"
    >
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content shadow-2xl border border-slate-800 bg-slate-900/95 text-slate-100">
          <!-- Modal Header -->
          <div class="modal-header py-2 px-3 bg-slate-950/60 border-b border-slate-800">
            <h2 id="customerModalTitle" class="modal-title h6 fw-bold mb-0 d-flex align-items-center gap-2">
              <i
                v-if="isEditMode"
                class="bi bi-building-gear text-cyan-400"
                aria-hidden="true"
              ></i>
              <i
                v-else
                class="bi bi-building-add text-cyan-400"
                aria-hidden="true"
              ></i>
              <span>{{ modalTitle }}</span>
              <span class="badge bg-slate-800 border border-slate-700 text-slate-300 font-monospace" style="font-size: 10px">
                SCR-CUST-002
              </span>
            </h2>
            <button
              type="button"
              class="btn-close btn-close-white py-1 px-2"
              aria-label="Close"
              :disabled="isSubmitting"
              @click="handleClose"
            ></button>
          </div>

          <!-- Form Body -->
          <form @submit.prevent="handleSubmit">
            <div class="modal-body p-3">
              <!-- Error Alert -->
              <div
                v-if="errorMessage"
                class="alert alert-danger alert-dismissible fade show py-1 px-2 mb-3 small d-flex align-items-center gap-2"
                role="alert"
                data-testid="customer-modal-error-alert"
              >
                <i class="bi bi-exclamation-triangle-fill flex-shrink-0" aria-hidden="true"></i>
                <div class="flex-grow-1">{{ errorMessage }}</div>
                <button
                  type="button"
                  class="btn-close py-1 px-2"
                  aria-label="Close"
                  @click="errorMessage = null"
                ></button>
              </div>

              <!-- Customer Code -->
              <div class="mb-2">
                <label for="customerCodeInput" class="form-label mb-1 small fw-medium text-slate-300" style="font-size: 11.5px">
                  Customer Code <span class="text-danger">*</span>
                </label>
                <input
                  id="customerCodeInput"
                  v-model="customerCode"
                  type="text"
                  maxlength="50"
                  class="form-control form-control-sm font-monospace text-uppercase bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                  :class="{ 'is-invalid': validationErrors.customerCode }"
                  placeholder="e.g. CUST-001"
                  :disabled="isSubmitting || isEditMode"
                  data-testid="input-customer-code"
                  @input="handleCodeInput"
                />
                <div v-if="validationErrors.customerCode" class="invalid-feedback small" style="font-size: 11px">
                  {{ validationErrors.customerCode }}
                </div>
                <div v-if="isEditMode" class="form-text text-slate-400" style="font-size: 10.5px">
                  Customer Code is immutable and cannot be modified.
                </div>
              </div>

              <!-- Customer Name -->
              <div class="mb-3">
                <label for="customerNameInput" class="form-label mb-1 small fw-medium text-slate-300" style="font-size: 11.5px">
                  Customer Name <span class="text-danger">*</span>
                </label>
                <input
                  id="customerNameInput"
                  v-model="customerName"
                  type="text"
                  maxlength="200"
                  class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                  :class="{ 'is-invalid': validationErrors.customerName }"
                  placeholder="e.g. Acme Corporation"
                  :disabled="isSubmitting"
                  data-testid="input-customer-name"
                />
                <div v-if="validationErrors.customerName" class="invalid-feedback small" style="font-size: 11px">
                  {{ validationErrors.customerName }}
                </div>
              </div>

              <!-- Maintenance Contract Switch -->
              <div class="mb-2">
                <div class="form-check form-switch d-flex align-items-center gap-2">
                  <input
                    id="hasActiveMaintenanceContractSwitch"
                    v-model="hasActiveMaintenanceContract"
                    class="form-check-input bg-slate-900 border-slate-700"
                    type="checkbox"
                    role="switch"
                    :disabled="isSubmitting"
                    data-testid="switch-maintenance-contract"
                  />
                  <label class="form-check-label small fw-medium text-slate-300" for="hasActiveMaintenanceContractSwitch" style="font-size: 11.5px">
                    Has Active Maintenance Contract
                  </label>
                </div>
                <div class="form-text text-slate-400 ms-4 ps-2" style="font-size: 10.5px">
                  Indicates whether this customer is under an active maintenance contract.
                </div>
              </div>
            </div>

            <!-- Modal Footer -->
            <div class="modal-footer py-2 px-3 bg-slate-950/60 border-t border-slate-800">
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm"
                style="font-size: 11.5px; height: 26px; line-height: 24px"
                :disabled="isSubmitting"
                data-testid="cancel-customer-btn"
                @click="handleClose"
              >
                Cancel
              </button>
              <button
                type="submit"
                class="btn btn-primary btn-sm"
                style="font-size: 11.5px; height: 26px; line-height: 24px"
                :disabled="isSubmitting"
                data-testid="submit-customer-btn"
              >
                <span
                  v-if="isSubmitting"
                  class="spinner-border spinner-border-sm me-1"
                  role="status"
                  aria-hidden="true"
                ></span>
                <i v-else class="bi bi-check-lg me-1" aria-hidden="true"></i>
                {{ isEditMode ? 'Update Customer' : 'Create Customer' }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>

    <!-- Backdrop -->
    <div class="modal-backdrop fade show" style="background-color: rgba(2, 6, 23, 0.8); backdrop-filter: blur(4px);" @click="handleClose"></div>
  </div>
</template>
