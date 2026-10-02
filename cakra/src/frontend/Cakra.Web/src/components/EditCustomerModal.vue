<script setup lang="ts">
import { AxiosError } from 'axios'
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'

import {
  activateCustomer,
  createCustomerContact,
  deactivateCustomer,
  getCustomerById,
  getCustomerContacts,
  updateCustomer,
  updateCustomerContact,
  type CustomerContactDto,
  type CustomerDto,
} from '@/api/customers'

/**
 * SCR-CUST-002: Edit Customer Modal Component
 * (Architecture §7, §8, §19.4, §19.6 — CR-002, SCR-CUST-002).
 *
 * - Allows updating Customer attributes (Code, Name, Maintenance Contract toggle).
 * - Enables Customer status toggling (Activate / Deactivate).
 * - Provides Customer Contact management (List, Add Contact, Edit/Toggle Contact status).
 * - Emits `saved` event on any customer update to refresh portfolio.
 */

const props = withDefaults(
  defineProps<{
    show?: boolean
    customerId?: string | null
  }>(),
  {
    show: false,
    customerId: null,
  },
)

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'saved', customer?: CustomerDto): void
}>()

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const activeTab = ref<'attributes' | 'contacts'>('attributes')

const customer = ref<CustomerDto | null>(null)
const contacts = ref<CustomerContactDto[]>([])

// Form state for customer attributes
const customerCode = ref('')
const customerName = ref('')
const hasActiveMaintenanceContract = ref(false)

// Form state for new contact
const newContactName = ref('')
const newContactPosition = ref('')
const newContactPhone = ref('')
const newContactEmail = ref('')
const isAddingContact = ref(false)

// Form state for editing contact
const editingContactId = ref<string | null>(null)
const editContactName = ref('')
const editContactPosition = ref('')
const editContactPhone = ref('')
const editContactEmail = ref('')
const editContactStatus = ref('ACTIVE')

const isLoading = ref(false)
const isSubmitting = ref(false)
const isTogglingStatus = ref(false)
const isSubmittingContact = ref(false)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)
const validationErrors = ref<{ customerCode?: string; customerName?: string; contactName?: string }>({})

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

function clearMessages(): void {
  errorMessage.value = null
  successMessage.value = null
  validationErrors.value = {}
}

async function loadData(): Promise<void> {
  const id = props.customerId
  if (!id) {
    return
  }

  isLoading.value = true
  clearMessages()

  try {
    const [fetchedCustomer, fetchedContacts] = await Promise.all([
      getCustomerById(id),
      getCustomerContacts(id),
    ])

    customer.value = fetchedCustomer
    contacts.value = fetchedContacts

    customerCode.value = fetchedCustomer.customerCode || fetchedCustomer.code || ''
    customerName.value = fetchedCustomer.customerName || fetchedCustomer.name || ''
    hasActiveMaintenanceContract.value = fetchedCustomer.hasActiveMaintenanceContract
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load customer details.')
  } finally {
    isLoading.value = false
  }
}

function validateAttributes(): boolean {
  const errors: { customerCode?: string; customerName?: string } = {}
  if (!customerCode.value.trim()) {
    errors.customerCode = 'Customer Code is required.'
  }
  if (!customerName.value.trim()) {
    errors.customerName = 'Customer Name is required.'
  }
  validationErrors.value = { ...validationErrors.value, ...errors }
  return Object.keys(errors).length === 0
}

async function handleSaveAttributes(): Promise<void> {
  const id = props.customerId
  if (!id || !validateAttributes()) {
    return
  }

  isSubmitting.value = true
  clearMessages()

  try {
    const updated = await updateCustomer(id, {
      customerCode: customerCode.value.trim(),
      customerName: customerName.value.trim(),
      hasActiveMaintenanceContract: hasActiveMaintenanceContract.value,
    })

    customer.value = updated
    successMessage.value = 'Customer attributes updated successfully.'
    emit('saved', updated)
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to update customer attributes.')
  } finally {
    isSubmitting.value = false
  }
}

async function handleToggleStatus(): Promise<void> {
  const id = props.customerId
  if (!id || !customer.value) {
    return
  }

  isTogglingStatus.value = true
  clearMessages()

  try {
    const updated = customer.value.isActive
      ? await deactivateCustomer(id)
      : await activateCustomer(id)

    customer.value = updated
    successMessage.value = `Customer ${updated.isActive ? 'activated' : 'deactivated'} successfully.`
    emit('saved', updated)
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to change customer status.')
  } finally {
    isTogglingStatus.value = false
  }
}

async function handleAddContact(): Promise<void> {
  const id = props.customerId
  if (!id) {
    return
  }

  if (!newContactName.value.trim()) {
    validationErrors.value = { ...validationErrors.value, contactName: 'Contact Name is required.' }
    return
  }

  isSubmittingContact.value = true
  clearMessages()

  try {
    const createdContact = await createCustomerContact(id, {
      name: newContactName.value.trim(),
      position: newContactPosition.value.trim() || null,
      phoneNumber: newContactPhone.value.trim() || null,
      email: newContactEmail.value.trim() || null,
    })

    contacts.value = [...contacts.value, createdContact]
    newContactName.value = ''
    newContactPosition.value = ''
    newContactPhone.value = ''
    newContactEmail.value = ''
    isAddingContact.value = false
    successMessage.value = 'Customer contact added successfully.'
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to add customer contact.')
  } finally {
    isSubmittingContact.value = false
  }
}

function startEditContact(contact: CustomerContactDto): void {
  editingContactId.value = contact.id || contact.contactId || null
  editContactName.value = contact.name
  editContactPosition.value = contact.position || ''
  editContactPhone.value = contact.phoneNumber || ''
  editContactEmail.value = contact.email || ''
  editContactStatus.value = contact.status || 'ACTIVE'
  clearMessages()
}

function cancelEditContact(): void {
  editingContactId.value = null
  clearMessages()
}

async function handleSaveContact(): Promise<void> {
  const id = props.customerId
  const contactId = editingContactId.value
  if (!id || !contactId) {
    return
  }

  if (!editContactName.value.trim()) {
    validationErrors.value = { ...validationErrors.value, contactName: 'Contact Name is required.' }
    return
  }

  isSubmittingContact.value = true
  clearMessages()

  try {
    const updatedContact = await updateCustomerContact(id, contactId, {
      name: editContactName.value.trim(),
      position: editContactPosition.value.trim() || null,
      phoneNumber: editContactPhone.value.trim() || null,
      email: editContactEmail.value.trim() || null,
      status: editContactStatus.value,
    })

    contacts.value = contacts.value.map((c) =>
      (c.id === contactId || c.contactId === contactId) ? updatedContact : c,
    )
    editingContactId.value = null
    successMessage.value = 'Contact details updated successfully.'
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to update contact.')
  } finally {
    isSubmittingContact.value = false
  }
}

async function handleToggleContactStatus(contact: CustomerContactDto): Promise<void> {
  const id = props.customerId
  const contactId = contact.id || contact.contactId
  if (!id || !contactId) {
    return
  }

  isSubmittingContact.value = true
  clearMessages()

  const nextStatus = contact.isActive ? 'INACTIVE' : 'ACTIVE'

  try {
    const updatedContact = await updateCustomerContact(id, contactId, {
      name: contact.name,
      position: contact.position,
      phoneNumber: contact.phoneNumber,
      email: contact.email,
      status: nextStatus,
    })

    contacts.value = contacts.value.map((c) =>
      (c.id === contactId || c.contactId === contactId) ? updatedContact : c,
    )
    successMessage.value = `Contact ${updatedContact.isActive ? 'activated' : 'deactivated'}.`
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to update contact status.')
  } finally {
    isSubmittingContact.value = false
  }
}

function handleClose(): void {
  clearMessages()
  isAddingContact.value = false
  editingContactId.value = null
  emit('close')
}

function handleKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape' && props.show) {
    handleClose()
  }
}

watch(
  () => [props.show, props.customerId] as const,
  ([visible, id]) => {
    if (visible && id) {
      activeTab.value = 'attributes'
      void loadData()
    } else {
      customer.value = null
      contacts.value = []
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
  <div v-if="props.show" data-screen-id="SCR-CUST-002">
    <!-- Bootstrap Modal Dialog -->
    <div
      class="modal fade show d-block"
      tabindex="-1"
      role="dialog"
      aria-labelledby="editCustomerModalTitle"
      aria-modal="true"
    >
      <div class="modal-dialog modal-lg modal-dialog-centered modal-dialog-scrollable">
        <div class="modal-content shadow border">
          <div class="modal-header py-1 px-3 bg-body-tertiary">
            <div class="d-flex align-items-center gap-2">
              <h2 id="editCustomerModalTitle" class="modal-title h6 fw-bold mb-0">
                <i class="bi bi-pencil-square me-1 text-primary" aria-hidden="true"></i>
                Edit Customer
              </h2>
              <span v-if="customer" class="text-body-secondary small font-monospace" style="font-size: 11px">
                {{ customer.customerName }} ({{ customer.customerCode }})
              </span>
            </div>
            <button
              type="button"
              class="btn-close py-1 px-2"
              aria-label="Close"
              :disabled="isSubmitting || isTogglingStatus || isSubmittingContact"
              @click="handleClose"
            ></button>
          </div>

          <!-- Modal Body -->
          <div class="modal-body p-2 p-md-3">
            <!-- Loading Spinner -->
            <div v-if="isLoading" class="text-center py-3">
              <div class="spinner-border spinner-border-sm text-primary" role="status">
                <span class="visually-hidden">Loading customer details...</span>
              </div>
            </div>

            <template v-else-if="customer">
              <!-- Error Alert -->
              <div
                v-if="errorMessage"
                class="alert alert-danger alert-dismissible fade show py-1 px-2 mb-2 small"
                role="alert"
                data-testid="edit-customer-error-alert"
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

              <!-- Success Feedback Alert -->
              <div
                v-if="successMessage"
                class="alert alert-success alert-dismissible fade show py-1 px-2 mb-2 small"
                role="alert"
                data-testid="edit-customer-success-alert"
              >
                <i class="bi bi-check-circle-fill me-1" aria-hidden="true"></i>
                {{ successMessage }}
                <button
                  type="button"
                  class="btn-close py-1 px-2"
                  aria-label="Close"
                  @click="successMessage = null"
                ></button>
              </div>

              <!-- Nav Tabs -->
              <ul class="nav nav-tabs mb-2" role="tablist" style="font-size: 12px">
                <li class="nav-item" role="presentation">
                  <button
                    class="nav-link py-1 px-2.5"
                    :class="{ active: activeTab === 'attributes' }"
                    type="button"
                    role="tab"
                    @click="activeTab = 'attributes'"
                  >
                    <i class="bi bi-info-circle me-1" aria-hidden="true"></i>
                    Attributes
                  </button>
                </li>
                <li class="nav-item" role="presentation">
                  <button
                    class="nav-link py-1 px-2.5"
                    :class="{ active: activeTab === 'contacts' }"
                    type="button"
                    role="tab"
                    @click="activeTab = 'contacts'"
                  >
                    <i class="bi bi-person-lines-fill me-1" aria-hidden="true"></i>
                    Contacts ({{ contacts.length }})
                  </button>
                </li>
              </ul>

              <!-- TAB 1: Customer Attributes -->
              <div v-if="activeTab === 'attributes'">
                <!-- Status & Lifecycle Summary -->
                <div class="card bg-body-tertiary border mb-2 shadow-none">
                  <div class="card-body p-2 d-flex align-items-center justify-content-between">
                    <div class="d-flex align-items-center gap-2">
                      <span class="fw-semibold small text-body-secondary" style="font-size: 11px">Status:</span>
                      <span
                        class="badge"
                        style="font-size: 10px; padding: 2px 6px"
                        :class="customer.isActive ? 'text-bg-success' : 'text-bg-secondary'"
                        data-testid="customer-status-badge"
                      >
                        {{ customer.status }}
                      </span>
                    </div>

                    <div>
                      <button
                        v-if="customer.isActive"
                        type="button"
                        class="btn btn-outline-danger btn-xs py-0 px-2"
                        style="font-size: 11px; height: 24px; line-height: 22px"
                        :disabled="isTogglingStatus || isSubmitting"
                        data-testid="deactivate-customer-button"
                        @click="handleToggleStatus"
                      >
                        <span
                          v-if="isTogglingStatus"
                          class="spinner-border spinner-border-sm me-1"
                          role="status"
                        ></span>
                        <i v-else class="bi bi-dash-circle me-1" aria-hidden="true"></i>
                        Deactivate Customer
                      </button>
                      <button
                        v-else
                        type="button"
                        class="btn btn-outline-success btn-xs py-0 px-2"
                        style="font-size: 11px; height: 24px; line-height: 22px"
                        :disabled="isTogglingStatus || isSubmitting"
                        data-testid="activate-customer-button"
                        @click="handleToggleStatus"
                      >
                        <span
                          v-if="isTogglingStatus"
                          class="spinner-border spinner-border-sm me-1"
                          role="status"
                        ></span>
                        <i v-else class="bi bi-check-circle me-1" aria-hidden="true"></i>
                        Activate Customer
                      </button>
                    </div>
                  </div>
                </div>

                <!-- Attributes Form -->
                <form @submit.prevent="handleSaveAttributes">
                  <div class="row g-2">
                    <div class="col-12 col-md-4">
                      <label for="editCustomerCode" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                        Customer Code <span class="text-danger">*</span>
                      </label>
                      <input
                        id="editCustomerCode"
                        v-model="customerCode"
                        type="text"
                        class="form-control form-control-sm font-monospace"
                        :class="{ 'is-invalid': validationErrors.customerCode }"
                        :disabled="isSubmitting"
                        data-testid="input-edit-customer-code"
                      />
                      <div v-if="validationErrors.customerCode" class="invalid-feedback small" style="font-size: 11px">
                        {{ validationErrors.customerCode }}
                      </div>
                    </div>

                    <div class="col-12 col-md-8">
                      <label for="editCustomerName" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                        Customer Name <span class="text-danger">*</span>
                      </label>
                      <input
                        id="editCustomerName"
                        v-model="customerName"
                        type="text"
                        class="form-control form-control-sm"
                        :class="{ 'is-invalid': validationErrors.customerName }"
                        :disabled="isSubmitting"
                        data-testid="input-edit-customer-name"
                      />
                      <div v-if="validationErrors.customerName" class="invalid-feedback small" style="font-size: 11px">
                        {{ validationErrors.customerName }}
                      </div>
                    </div>

                    <div class="col-12">
                      <div class="form-check form-switch mb-1">
                        <input
                          id="editHasMaintenanceContract"
                          v-model="hasActiveMaintenanceContract"
                          class="form-check-input"
                          type="checkbox"
                          role="switch"
                          :disabled="isSubmitting"
                          data-testid="toggle-edit-maintenance-contract"
                        />
                        <label
                          class="form-check-label small fw-semibold"
                          for="editHasMaintenanceContract"
                          style="font-size: 11.5px"
                        >
                          Active Maintenance Contract (SLA)
                        </label>
                      </div>
                    </div>
                  </div>

                  <div class="d-flex justify-content-end mt-2">
                    <button
                      type="submit"
                      class="btn btn-primary btn-sm py-0 px-2"
                      style="font-size: 11px; height: 24px; line-height: 22px"
                      :disabled="isSubmitting"
                      data-testid="submit-edit-customer"
                    >
                      <span
                        v-if="isSubmitting"
                        class="spinner-border spinner-border-sm me-1"
                        role="status"
                        aria-hidden="true"
                      ></span>
                      <i v-else class="bi bi-save me-1" aria-hidden="true"></i>
                      Save Customer Changes
                    </button>
                  </div>
                </form>
              </div>

              <!-- TAB 2: Customer Contacts -->
              <div v-else-if="activeTab === 'contacts'">
                <div class="d-flex justify-content-between align-items-center mb-1">
                  <span class="small fw-semibold text-body-secondary" style="font-size: 11px">Contact Directory</span>
                  <button
                    v-if="!isAddingContact && !editingContactId"
                    type="button"
                    class="btn btn-outline-primary btn-xs py-0 px-2"
                    style="font-size: 11px; height: 22px; line-height: 20px"
                    data-testid="show-add-contact-form"
                    @click="isAddingContact = true; clearMessages()"
                  >
                    <i class="bi bi-person-plus me-1" aria-hidden="true"></i>
                    Add Contact
                  </button>
                </div>

                <!-- Add Contact Form -->
                <div v-if="isAddingContact" class="card bg-body-tertiary border rounded p-2 mb-2 shadow-none">
                  <div class="small fw-bold mb-1" style="font-size: 11.5px">New Contact</div>
                  <div class="row g-1">
                    <div class="col-12 col-md-6">
                      <input
                        v-model="newContactName"
                        type="text"
                        class="form-control form-control-sm"
                        :class="{ 'is-invalid': validationErrors.contactName }"
                        placeholder="Contact Name *"
                        data-testid="input-new-contact-name"
                      />
                    </div>
                    <div class="col-12 col-md-6">
                      <input
                        v-model="newContactPosition"
                        type="text"
                        class="form-control form-control-sm"
                        placeholder="Position / Title"
                        data-testid="input-new-contact-position"
                      />
                    </div>
                    <div class="col-12 col-md-6">
                      <input
                        v-model="newContactPhone"
                        type="text"
                        class="form-control form-control-sm"
                        placeholder="Phone Number"
                        data-testid="input-new-contact-phone"
                      />
                    </div>
                    <div class="col-12 col-md-6">
                      <input
                        v-model="newContactEmail"
                        type="email"
                        class="form-control form-control-sm"
                        placeholder="Email Address"
                        data-testid="input-new-contact-email"
                      />
                    </div>
                  </div>
                  <div class="d-flex justify-content-end gap-1 mt-1">
                    <button
                      type="button"
                      class="btn btn-outline-secondary btn-xs py-0 px-2"
                      style="font-size: 11px; height: 22px; line-height: 20px"
                      :disabled="isSubmittingContact"
                      @click="isAddingContact = false"
                    >
                      Cancel
                    </button>
                    <button
                      type="button"
                      class="btn btn-primary btn-xs py-0 px-2"
                      style="font-size: 11px; height: 22px; line-height: 20px"
                      :disabled="isSubmittingContact"
                      data-testid="add-contact-button"
                      @click="handleAddContact"
                    >
                      <span
                        v-if="isSubmittingContact"
                        class="spinner-border spinner-border-sm me-1"
                        role="status"
                      ></span>
                      Add Contact
                    </button>
                  </div>
                </div>

                <!-- Edit Contact Form -->
                <div v-if="editingContactId" class="card bg-body-tertiary border rounded p-2 mb-2 shadow-none">
                  <div class="small fw-bold mb-1" style="font-size: 11.5px">Edit Contact</div>
                  <div class="row g-1">
                    <div class="col-12 col-md-6">
                      <input
                        v-model="editContactName"
                        type="text"
                        class="form-control form-control-sm"
                        :class="{ 'is-invalid': validationErrors.contactName }"
                        placeholder="Contact Name *"
                        data-testid="input-edit-contact-name"
                      />
                    </div>
                    <div class="col-12 col-md-6">
                      <input
                        v-model="editContactPosition"
                        type="text"
                        class="form-control form-control-sm"
                        placeholder="Position"
                        data-testid="input-edit-contact-position"
                      />
                    </div>
                    <div class="col-12 col-md-6">
                      <input
                        v-model="editContactPhone"
                        type="text"
                        class="form-control form-control-sm"
                        placeholder="Phone"
                        data-testid="input-edit-contact-phone"
                      />
                    </div>
                    <div class="col-12 col-md-6">
                      <input
                        v-model="editContactEmail"
                        type="email"
                        class="form-control form-control-sm"
                        placeholder="Email"
                        data-testid="input-edit-contact-email"
                      />
                    </div>
                  </div>
                  <div class="d-flex justify-content-end gap-1 mt-1">
                    <button
                      type="button"
                      class="btn btn-outline-secondary btn-xs py-0 px-2"
                      style="font-size: 11px; height: 22px; line-height: 20px"
                      :disabled="isSubmittingContact"
                      @click="cancelEditContact"
                    >
                      Cancel
                    </button>
                    <button
                      type="button"
                      class="btn btn-primary btn-xs py-0 px-2"
                      style="font-size: 11px; height: 22px; line-height: 20px"
                      :disabled="isSubmittingContact"
                      data-testid="save-edit-contact-button"
                      @click="handleSaveContact"
                    >
                      <span
                        v-if="isSubmittingContact"
                        class="spinner-border spinner-border-sm me-1"
                        role="status"
                      ></span>
                      Save Contact
                    </button>
                  </div>
                </div>

                <!-- Contacts Table -->
                <div v-if="contacts.length === 0" class="text-center py-3 text-body-secondary small">
                  No contacts found for this customer organization.
                </div>
                <div v-else class="table-responsive">
                  <table class="table table-hover align-middle mb-0 text-nowrap" data-testid="customer-contacts-table">
                    <thead class="table-light">
                      <tr>
                        <th scope="col">Name</th>
                        <th scope="col">Position</th>
                        <th scope="col">Phone</th>
                        <th scope="col">Email</th>
                        <th scope="col" class="text-center">Status</th>
                        <th scope="col" class="text-end">Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="contact in contacts" :key="contact.id || contact.contactId">
                        <td class="fw-semibold small">{{ contact.name }}</td>
                        <td class="small">{{ contact.position || '—' }}</td>
                        <td class="small font-monospace" style="font-size: 11px">{{ contact.phoneNumber || '—' }}</td>
                        <td class="small font-monospace" style="font-size: 11px">{{ contact.email || '—' }}</td>
                        <td class="text-center">
                          <span
                            class="badge"
                            style="font-size: 10px; padding: 2px 5px"
                            :class="contact.isActive ? 'text-bg-success' : 'text-bg-secondary'"
                          >
                            {{ contact.status }}
                          </span>
                        </td>
                        <td class="text-end">
                          <div class="d-inline-flex align-items-center gap-1">
                            <button
                              type="button"
                              class="btn btn-outline-secondary btn-xs py-0 px-1"
                              style="font-size: 10px; height: 22px; line-height: 20px"
                              title="Edit Contact"
                              data-testid="edit-contact-action"
                              @click="startEditContact(contact)"
                            >
                              <i class="bi bi-pencil" aria-hidden="true"></i>
                            </button>
                            <button
                              type="button"
                              class="btn btn-xs py-0 px-1"
                              style="font-size: 10px; height: 22px; line-height: 20px"
                              :class="contact.isActive ? 'btn-outline-warning' : 'btn-outline-success'"
                              :title="contact.isActive ? 'Deactivate Contact' : 'Activate Contact'"
                              data-testid="toggle-contact-status-action"
                              @click="handleToggleContactStatus(contact)"
                            >
                              <i
                                class="bi"
                                :class="contact.isActive ? 'bi-person-dash' : 'bi-person-check'"
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
            </template>
          </div>

          <div class="modal-footer py-1 px-3 bg-body-tertiary">
            <button
              type="button"
              class="btn btn-secondary btn-sm py-0 px-2"
              style="font-size: 11px; height: 24px; line-height: 22px"
              :disabled="isSubmitting || isTogglingStatus || isSubmittingContact"
              data-testid="close-edit-customer"
              @click="handleClose"
            >
              Close
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Backdrop -->
    <div class="modal-backdrop fade show" @click="handleClose"></div>
  </div>
</template>
