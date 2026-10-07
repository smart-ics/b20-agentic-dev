<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'

import {
  createCustomerContact,
  getCustomerContacts,
  updateCustomerContact,
  type CustomerContactDto,
  type CustomerDto,
} from '@/api/customers'

/**
 * SCR-CUST-003: Customer Contact Persons Management Modal Component
 * (CR-010, FEAT-CUST-001).
 *
 * - Allows viewing, adding, editing, and toggling status of contact persons for a customer.
 * - Enforces client-side validation (non-empty name, valid email format if provided).
 * - Refreshes contacts list and displays inline success/error feedback.
 */

const props = withDefaults(
  defineProps<{
    isOpen?: boolean
    show?: boolean
    customer?: CustomerDto | null
  }>(),
  {
    isOpen: false,
    show: false,
    customer: null,
  },
)

const emit = defineEmits<{
  (e: 'close'): void
}>()

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const isVisible = computed(() => props.isOpen || props.show)
const customerId = computed(() => {
  if (!props.customer) return ''
  return (props.customer.id || props.customer.customerId || '').trim()
})
const customerDisplayName = computed(() => {
  if (!props.customer) return ''
  const name = props.customer.customerName || props.customer.name || ''
  const code = props.customer.customerCode || props.customer.code || ''
  return code ? `${name} (${code})` : name
})

const contacts = ref<CustomerContactDto[]>([])
const isLoading = ref(false)
const isSubmitting = ref(false)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)

// Form Mode: null (closed), 'add', 'edit'
const formMode = ref<'add' | 'edit' | null>(null)
const editingContactId = ref<string | null>(null)

// Form fields
const formName = ref('')
const formPosition = ref('')
const formEmail = ref('')
const formPhone = ref('')
const formStatus = ref<'ACTIVE' | 'INACTIVE'>('ACTIVE')

const validationErrors = ref<{
  name?: string
  email?: string
}>({})

function extractErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof AxiosError) {
    if (err.response?.status === 409) {
      const data = err.response.data as ProblemDetailsPayload | undefined
      return data?.detail || 'A conflict occurred while saving the contact person.'
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

function clearMessages(): void {
  errorMessage.value = null
  successMessage.value = null
  validationErrors.value = {}
}

function isValidEmail(val: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(val.trim())
}

function validateForm(): boolean {
  const errors: { name?: string; email?: string } = {}

  if (!formName.value.trim()) {
    errors.name = 'Contact Name is required.'
  }

  if (formEmail.value.trim() && !isValidEmail(formEmail.value)) {
    errors.email = 'Please provide a valid email address format.'
  }

  validationErrors.value = errors
  return Object.keys(errors).length === 0
}

async function fetchContacts(): Promise<void> {
  if (!customerId.value) {
    contacts.value = []
    return
  }

  isLoading.value = true
  clearMessages()

  try {
    const list = await getCustomerContacts(customerId.value)
    contacts.value = Array.isArray(list) ? list : []
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load customer contact persons.')
  } finally {
    isLoading.value = false
  }
}

function openAddForm(): void {
  formMode.value = 'add'
  editingContactId.value = null
  formName.value = ''
  formPosition.value = ''
  formEmail.value = ''
  formPhone.value = ''
  formStatus.value = 'ACTIVE'
  clearMessages()
}

function openEditForm(contact: CustomerContactDto): void {
  formMode.value = 'edit'
  editingContactId.value = contact.id || contact.contactId || null
  formName.value = contact.name || ''
  formPosition.value = contact.position || ''
  formEmail.value = contact.email || ''
  formPhone.value = contact.phoneNumber || ''
  formStatus.value = (contact.status === 'INACTIVE' || !contact.isActive) ? 'INACTIVE' : 'ACTIVE'
  clearMessages()
}

function closeForm(): void {
  formMode.value = null
  editingContactId.value = null
  validationErrors.value = {}
}

async function handleSaveContact(): Promise<void> {
  if (!customerId.value || !validateForm()) {
    return
  }

  isSubmitting.value = true
  errorMessage.value = null
  successMessage.value = null

  try {
    if (formMode.value === 'add') {
      const created = await createCustomerContact(customerId.value, {
        name: formName.value.trim(),
        position: formPosition.value.trim() || null,
        phoneNumber: formPhone.value.trim() || null,
        email: formEmail.value.trim() || null,
      })
      contacts.value = [...contacts.value, created]
      successMessage.value = `Contact "${created.name}" added successfully.`
      closeForm()
    } else if (formMode.value === 'edit' && editingContactId.value) {
      const updated = await updateCustomerContact(customerId.value, editingContactId.value, {
        name: formName.value.trim(),
        position: formPosition.value.trim() || null,
        phoneNumber: formPhone.value.trim() || null,
        email: formEmail.value.trim() || null,
        status: formStatus.value,
      })
      contacts.value = contacts.value.map((c) =>
        (c.id === editingContactId.value || c.contactId === editingContactId.value) ? updated : c,
      )
      successMessage.value = `Contact "${updated.name}" updated successfully.`
      closeForm()
    }
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to save customer contact.')
  } finally {
    isSubmitting.value = false
  }
}

async function handleToggleStatus(contact: CustomerContactDto): Promise<void> {
  const cId = contact.id || contact.contactId
  if (!customerId.value || !cId) return

  isSubmitting.value = true
  errorMessage.value = null
  successMessage.value = null

  const nextStatus = (contact.status === 'ACTIVE' || contact.isActive) ? 'INACTIVE' : 'ACTIVE'

  try {
    const updated = await updateCustomerContact(customerId.value, cId, {
      name: contact.name,
      position: contact.position,
      phoneNumber: contact.phoneNumber,
      email: contact.email,
      status: nextStatus,
    })
    contacts.value = contacts.value.map((c) =>
      (c.id === cId || c.contactId === cId) ? updated : c,
    )
    successMessage.value = `Contact "${updated.name}" status changed to ${nextStatus}.`
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to change contact status.')
  } finally {
    isSubmitting.value = false
  }
}

function handleClose(): void {
  clearMessages()
  closeForm()
  emit('close')
}

function handleKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape' && isVisible.value) {
    handleClose()
  }
}

watch(
  () => [isVisible.value, customerId.value] as const,
  ([visible, id]) => {
    if (visible && id) {
      closeForm()
      void fetchContacts()
    } else {
      contacts.value = []
      closeForm()
      clearMessages()
    }
  },
  { immediate: true },
)

onMounted(() => {
  window.addEventListener('keydown', handleKeydown)
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <div v-if="isVisible" data-screen-id="SCR-CUST-003" data-testid="customer-contact-modal">
    <!-- Bootstrap Modal Dialog -->
    <div
      class="modal fade show d-block"
      tabindex="-1"
      role="dialog"
      aria-labelledby="customerContactModalTitle"
      aria-modal="true"
    >
      <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
        <div class="modal-content shadow-2xl border border-slate-800 bg-slate-900/95 text-slate-100">
          <!-- Modal Header -->
          <div class="modal-header py-2 px-3 bg-slate-950/60 border-b border-slate-800">
            <div class="d-flex align-items-center gap-2">
              <i class="bi bi-people-fill text-cyan-400" aria-hidden="true"></i>
              <h2 id="customerContactModalTitle" class="modal-title h6 fw-bold mb-0">
                Customer Contacts
              </h2>
              <span class="badge bg-slate-800 border border-slate-700 text-slate-300 font-monospace" style="font-size: 10px">
                SCR-CUST-003
              </span>
            </div>
            <button
              type="button"
              class="btn-close btn-close-white py-1 px-2"
              aria-label="Close"
              :disabled="isSubmitting"
              @click="handleClose"
            ></button>
          </div>

          <!-- Modal Body -->
          <div class="modal-body p-3">
            <!-- Customer Context Subtitle -->
            <div
              v-if="customerDisplayName"
              class="d-flex align-items-center justify-content-between p-2 mb-3 rounded bg-light border"
            >
              <div class="d-flex align-items-center gap-2 small">
                <i class="bi bi-building text-secondary" aria-hidden="true"></i>
                <span class="text-secondary">Customer:</span>
                <strong class="text-dark">{{ customerDisplayName }}</strong>
              </div>
              <button
                v-if="!formMode"
                type="button"
                class="btn btn-primary btn-sm d-flex align-items-center gap-1"
                style="font-size: 11.5px; height: 26px; line-height: 24px"
                :disabled="isLoading || isSubmitting"
                data-testid="add-contact-btn"
                @click="openAddForm"
              >
                <i class="bi bi-person-plus-fill" aria-hidden="true"></i>
                <span>Add Contact</span>
              </button>
            </div>

            <!-- Success Alert -->
            <div
              v-if="successMessage"
              class="alert alert-success alert-dismissible fade show py-1 px-2 mb-3 small d-flex align-items-center gap-2"
              role="alert"
              data-testid="contact-modal-success-alert"
            >
              <i class="bi bi-check-circle-fill flex-shrink-0" aria-hidden="true"></i>
              <div class="flex-grow-1">{{ successMessage }}</div>
              <button
                type="button"
                class="btn-close py-1 px-2"
                aria-label="Close"
                @click="successMessage = null"
              ></button>
            </div>

            <!-- Error Alert -->
            <div
              v-if="errorMessage"
              class="alert alert-danger alert-dismissible fade show py-1 px-2 mb-3 small d-flex align-items-center gap-2"
              role="alert"
              data-testid="contact-modal-error-alert"
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

            <!-- Add / Edit Contact Form Section -->
            <div
              v-if="formMode"
              class="card border border-primary mb-3 shadow-sm"
              data-testid="contact-form-card"
            >
              <div class="card-header py-1 px-3 bg-primary bg-opacity-10 d-flex justify-content-between align-items-center">
                <span class="small fw-bold text-primary">
                  <i
                    class="bi me-1"
                    :class="formMode === 'edit' ? 'bi-pencil-square' : 'bi-person-plus'"
                    aria-hidden="true"
                  ></i>
                  {{ formMode === 'edit' ? 'Edit Contact Person' : 'Add Contact Person' }}
                </span>
                <button
                  type="button"
                  class="btn btn-sm btn-link text-secondary p-0 text-decoration-none"
                  aria-label="Cancel form"
                  :disabled="isSubmitting"
                  @click="closeForm"
                >
                  <i class="bi bi-x-lg" aria-hidden="true"></i>
                </button>
              </div>
              <div class="card-body p-3">
                <form @submit.prevent="handleSaveContact">
                  <div class="row g-2">
                    <!-- Contact Name -->
                    <div class="col-12 col-md-6">
                      <label for="contactFormName" class="form-label mb-1 small fw-medium" style="font-size: 11.5px">
                        Name <span class="text-danger">*</span>
                      </label>
                      <input
                        id="contactFormName"
                        v-model="formName"
                        type="text"
                        maxlength="200"
                        class="form-control form-control-sm"
                        :class="{ 'is-invalid': validationErrors.name }"
                        placeholder="e.g. Jane Doe"
                        :disabled="isSubmitting"
                        data-testid="contact-name-input"
                      />
                      <div v-if="validationErrors.name" class="invalid-feedback small" style="font-size: 11px">
                        {{ validationErrors.name }}
                      </div>
                    </div>

                    <!-- Position -->
                    <div class="col-12 col-md-6">
                      <label for="contactFormPosition" class="form-label mb-1 small fw-medium" style="font-size: 11.5px">
                        Position
                      </label>
                      <input
                        id="contactFormPosition"
                        v-model="formPosition"
                        type="text"
                        maxlength="100"
                        class="form-control form-control-sm"
                        placeholder="e.g. Operations Manager"
                        :disabled="isSubmitting"
                        data-testid="contact-position-input"
                      />
                    </div>

                    <!-- Email -->
                    <div class="col-12 col-md-6">
                      <label for="contactFormEmail" class="form-label mb-1 small fw-medium" style="font-size: 11.5px">
                        Email
                      </label>
                      <input
                        id="contactFormEmail"
                        v-model="formEmail"
                        type="email"
                        maxlength="255"
                        class="form-control form-control-sm"
                        :class="{ 'is-invalid': validationErrors.email }"
                        placeholder="e.g. jane.doe@customer.com"
                        :disabled="isSubmitting"
                        data-testid="contact-email-input"
                      />
                      <div v-if="validationErrors.email" class="invalid-feedback small" style="font-size: 11px">
                        {{ validationErrors.email }}
                      </div>
                    </div>

                    <!-- Phone Number -->
                    <div class="col-12 col-md-6">
                      <label for="contactFormPhone" class="form-label mb-1 small fw-medium" style="font-size: 11.5px">
                        Phone Number
                      </label>
                      <input
                        id="contactFormPhone"
                        v-model="formPhone"
                        type="tel"
                        maxlength="50"
                        class="form-control form-control-sm"
                        placeholder="e.g. +62 812 3456 7890"
                        :disabled="isSubmitting"
                        data-testid="contact-phone-input"
                      />
                    </div>

                    <!-- Status (only for edit mode) -->
                    <div v-if="formMode === 'edit'" class="col-12 col-md-6">
                      <label for="contactFormStatus" class="form-label mb-1 small fw-medium" style="font-size: 11.5px">
                        Status
                      </label>
                      <select
                        id="contactFormStatus"
                        v-model="formStatus"
                        class="form-select form-select-sm"
                        :disabled="isSubmitting"
                        data-testid="contact-status-select"
                      >
                        <option value="ACTIVE">ACTIVE</option>
                        <option value="INACTIVE">INACTIVE</option>
                      </select>
                    </div>
                  </div>

                  <!-- Form Action Buttons -->
                  <div class="d-flex justify-content-end gap-2 mt-3 pt-2 border-top">
                    <button
                      type="button"
                      class="btn btn-outline-secondary btn-sm"
                      style="font-size: 11.5px; height: 26px; line-height: 24px"
                      :disabled="isSubmitting"
                      data-testid="cancel-contact-form-btn"
                      @click="closeForm"
                    >
                      Cancel
                    </button>
                    <button
                      type="submit"
                      class="btn btn-primary btn-sm"
                      style="font-size: 11.5px; height: 26px; line-height: 24px"
                      :disabled="isSubmitting"
                      data-testid="save-contact-btn"
                    >
                      <span
                        v-if="isSubmitting"
                        class="spinner-border spinner-border-sm me-1"
                        role="status"
                        aria-hidden="true"
                      ></span>
                      <i v-else class="bi bi-check-lg me-1" aria-hidden="true"></i>
                      {{ formMode === 'edit' ? 'Update Contact' : 'Save Contact' }}
                    </button>
                  </div>
                </form>
              </div>
            </div>

            <!-- Loading Spinner -->
            <div v-if="isLoading" class="text-center py-4 text-secondary">
              <div class="spinner-border spinner-border-sm text-primary me-2" role="status">
                <span class="visually-hidden">Loading...</span>
              </div>
              <span class="small">Loading contact persons...</span>
            </div>

            <!-- Contacts Table -->
            <div v-else class="table-responsive border rounded">
              <table class="table table-hover table-sm align-middle mb-0" data-testid="contacts-table">
                <thead class="table-light">
                  <tr style="font-size: 11px">
                    <th scope="col" class="ps-3 py-2 text-secondary text-uppercase fw-semibold">Name</th>
                    <th scope="col" class="py-2 text-secondary text-uppercase fw-semibold">Position</th>
                    <th scope="col" class="py-2 text-secondary text-uppercase fw-semibold">Email</th>
                    <th scope="col" class="py-2 text-secondary text-uppercase fw-semibold">Phone</th>
                    <th scope="col" class="py-2 text-secondary text-uppercase fw-semibold text-center">Status</th>
                    <th scope="col" class="pe-3 py-2 text-secondary text-uppercase fw-semibold text-end">Actions</th>
                  </tr>
                </thead>
                <tbody style="font-size: 12px">
                  <tr v-if="contacts.length === 0">
                    <td colspan="6" class="text-center py-4 text-secondary">
                      <div class="d-flex flex-column align-items-center gap-1">
                        <i class="bi bi-people text-secondary opacity-50" style="font-size: 24px"></i>
                        <span>No contact persons recorded for this customer.</span>
                        <button
                          v-if="!formMode"
                          type="button"
                          class="btn btn-outline-primary btn-sm mt-1"
                          style="font-size: 11px"
                          @click="openAddForm"
                        >
                          <i class="bi bi-plus-lg me-1"></i> Add the first contact
                        </button>
                      </div>
                    </td>
                  </tr>
                  <tr
                    v-for="contact in contacts"
                    :key="contact.id || contact.contactId || contact.name"
                    :class="{ 'table-active': editingContactId === (contact.id || contact.contactId) }"
                    data-testid="contact-row"
                  >
                    <!-- Name -->
                    <td class="ps-3 py-2 fw-semibold text-dark">
                      {{ contact.name }}
                    </td>

                    <!-- Position -->
                    <td class="py-2 text-secondary">
                      {{ contact.position || '—' }}
                    </td>

                    <!-- Email -->
                    <td class="py-2">
                      <a
                        v-if="contact.email"
                        :href="`mailto:${contact.email}`"
                        class="text-decoration-none font-monospace small"
                      >
                        {{ contact.email }}
                      </a>
                      <span v-else class="text-secondary">—</span>
                    </td>

                    <!-- Phone -->
                    <td class="py-2">
                      <a
                        v-if="contact.phoneNumber"
                        :href="`tel:${contact.phoneNumber}`"
                        class="text-decoration-none small text-dark"
                      >
                        {{ contact.phoneNumber }}
                      </a>
                      <span v-else class="text-secondary">—</span>
                    </td>

                    <!-- Status Badge -->
                    <td class="py-2 text-center">
                      <span
                        v-if="contact.status === 'ACTIVE' || contact.isActive"
                        class="badge bg-success-subtle text-success border border-success-subtle px-2 py-1"
                        style="font-size: 10.5px"
                      >
                        ACTIVE
                      </span>
                      <span
                        v-else
                        class="badge bg-secondary-subtle text-secondary border border-secondary-subtle px-2 py-1"
                        style="font-size: 10.5px"
                      >
                        INACTIVE
                      </span>
                    </td>

                    <!-- Actions -->
                    <td class="pe-3 py-2 text-end">
                      <div class="btn-group btn-group-sm" role="group" aria-label="Contact actions">
                        <button
                          type="button"
                          class="btn btn-outline-secondary btn-sm py-0 px-2"
                          style="font-size: 11px; height: 24px; line-height: 22px"
                          title="Edit Contact"
                          :disabled="isSubmitting"
                          data-testid="edit-contact-btn"
                          @click="openEditForm(contact)"
                        >
                          <i class="bi bi-pencil" aria-hidden="true"></i>
                        </button>
                        <button
                          type="button"
                          class="btn btn-sm py-0 px-2"
                          :class="(contact.status === 'ACTIVE' || contact.isActive) ? 'btn-outline-warning' : 'btn-outline-success'"
                          style="font-size: 11px; height: 24px; line-height: 22px"
                          :title="(contact.status === 'ACTIVE' || contact.isActive) ? 'Deactivate Contact' : 'Activate Contact'"
                          :disabled="isSubmitting"
                          data-testid="toggle-contact-status-btn"
                          @click="handleToggleStatus(contact)"
                        >
                          <i
                            class="bi"
                            :class="(contact.status === 'ACTIVE' || contact.isActive) ? 'bi-slash-circle' : 'bi-check-circle'"
                            aria-hidden="true"
                          ></i>
                        </button>
                      </div>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <!-- Modal Footer -->
          <div class="modal-footer py-2 px-3 bg-slate-950/60 border-t border-slate-800">
            <button
              type="button"
              class="btn btn-secondary btn-sm"
              style="font-size: 11.5px; height: 26px; line-height: 24px"
              :disabled="isSubmitting"
              data-testid="close-contact-modal-btn"
              @click="handleClose"
            >
              Close
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Backdrop -->
    <div class="modal-backdrop fade show" style="background-color: rgba(2, 6, 23, 0.8); backdrop-filter: blur(4px);" @click="handleClose"></div>
  </div>
</template>
