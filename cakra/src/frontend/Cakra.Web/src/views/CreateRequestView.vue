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
  deadline?: string | null
  createdAt?: string
}

export interface ActivePersonOption {
  id: string
  personId?: string
  firstName?: string
  lastName?: string
  fullName: string
  email?: string
  status?: string
}

export interface InitialSubTaskItem {
  id: string
  title: string
  assigneePersonId: string | null
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
const activePersons = ref<ActivePersonOption[]>([])
const initialSubTasks = ref<InitialSubTaskItem[]>([])
const newSubTaskTitle = ref('')
const newSubTaskAssignee = ref('')

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
  complexity: 1,
  deadline: null as string | null,
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

function resolvePersonName(personId: string): string {
  const person = activePersons.value.find((p) => p.id === personId || p.personId === personId)
  if (person) {
    return person.fullName || `${person.firstName ?? ''} ${person.lastName ?? ''}`.trim()
  }
  return personId
}

function addInitialSubTask(): void {
  const title = newSubTaskTitle.value.trim()
  if (!title) return

  initialSubTasks.value.push({
    id: `temp-${Date.now()}-${Math.random().toString(36).slice(2, 9)}`,
    title,
    assigneePersonId: newSubTaskAssignee.value.trim() || null,
  })

  newSubTaskTitle.value = ''
  newSubTaskAssignee.value = ''
}

function removeInitialSubTask(index: number): void {
  initialSubTasks.value.splice(index, 1)
}

async function loadLookups(): Promise<void> {
  isLoadingLookups.value = true
  errorMessage.value = null

  try {
    const [customersResponse, productsResponse, personsResponse] = await Promise.all([
      httpClient.get<ActiveCustomerOption[]>('/customers/active'),
      httpClient.get<ActiveProductOption[]>('/products/active'),
      httpClient.get<ActivePersonOption[]>('/organization/persons/active'),
    ])
    activeCustomers.value = customersResponse.data
    activeProducts.value = productsResponse.data
    activePersons.value = personsResponse.data
  } catch (err) {
    errorMessage.value = extractErrorMessage(
      err,
      'Failed to load active customers, products, or team members for selection.',
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
    const initialSubTasksPayload = initialSubTasks.value.map((st, idx) => ({
      title: st.title,
      assigneePersonId: st.assigneePersonId || null,
      sortOrder: idx + 1,
    }))

    const response = await httpClient.post<CreatedRequestResponse>('/requests', {
      title: trimmedTitle,
      description: trimmedDescription,
      customerId: form.customerId.trim() || null,
      productId: form.productId.trim() || null,
      requestType: form.requestType || 'GENERAL',
      priority: form.priority || 'NORMAL',
      complexity: Number(form.complexity) || 1,
      deadline: form.deadline ? form.deadline : null,
      initialSubTasks: initialSubTasksPayload.length > 0 ? initialSubTasksPayload : undefined,
    })

    const createdId = response.data.id ?? response.data.requestId
    await router.push(`/requests/${createdId}`)
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to create request.')
  } finally {
    isSubmitting.value = false
  }
}

async function handleBack(): Promise<void> {
  if (window.history.length > 1) {
    router.back()
  } else {
    await router.push('/feed')
  }
}

async function handleCancel(): Promise<void> {
  await handleBack()
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

      <button
        type="button"
        class="btn btn-outline-secondary btn-sm py-0 px-2"
        style="font-size: 12px; height: 26px; line-height: 24px"
        data-testid="back-button"
        @click="handleBack"
      >
        <i class="bi bi-arrow-left me-1" aria-hidden="true"></i>Back
      </button>
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
    <div class="card shadow-none bg-slate-900/90 border border-slate-800 text-slate-100 mb-2">
      <div class="card-header py-1 px-2 bg-slate-950/60 border-b border-slate-800 fw-semibold small text-white">
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
                class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
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
                class="form-select form-select-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
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
                class="form-select form-select-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                :disabled="isSubmitting"
                data-testid="request-priority-select"
              >
                <option v-for="priority in PRIORITIES" :key="priority" :value="priority">
                  {{ priority }}
                </option>
              </select>
            </div>

            <!-- Customer Select -->
            <div class="col-12 col-md-3">
              <label for="requestCustomer" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Customer (Optional)
              </label>
              <select
                id="requestCustomer"
                v-model="form.customerId"
                name="customerId"
                class="form-select form-select-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
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
            <div class="col-12 col-md-3">
              <label for="requestProduct" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Product (Optional)
              </label>
              <select
                id="requestProduct"
                v-model="form.productId"
                name="productId"
                class="form-select form-select-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
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

            <!-- Complexity Select -->
            <div class="col-6 col-md-3">
              <label for="requestComplexity" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Complexity
              </label>
              <select
                id="requestComplexity"
                v-model.number="form.complexity"
                name="complexity"
                class="form-select form-select-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
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

            <!-- Target Deadline -->
            <div class="col-6 col-md-3">
              <label for="createDeadlineInput" class="form-label mb-0 small fw-medium" style="font-size: 11px">
                Deadline (Optional)
              </label>
              <input
                id="createDeadlineInput"
                v-model="form.deadline"
                type="date"
                name="deadline"
                class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                :disabled="isSubmitting"
                data-testid="create-deadline-input"
              />
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
                class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                rows="4"
                placeholder="Detailed description and operational context of the request"
                required
                :disabled="isSubmitting"
                data-testid="request-description-input"
              ></textarea>
            </div>

            <!-- Initial Sub-Tasks Checklist Section (CR-006, Architecture §4 TD-010) -->
            <div class="col-12 mt-2" data-testid="initial-subtasks-section">
              <label class="form-label mb-1 small fw-medium d-flex justify-content-between align-items-center" style="font-size: 11px">
                <span>
                  <i class="bi bi-list-check me-1 text-primary" aria-hidden="true"></i>
                  Initial Sub-Tasks Checklist (Optional)
                </span>
                <span class="text-body-secondary" style="font-size: 10.5px">
                  {{ initialSubTasks.length }} item(s) defined
                </span>
              </label>

              <!-- Added Sub-Tasks List -->
              <div
                v-if="initialSubTasks.length > 0"
                class="border border-slate-800 rounded p-2 mb-2 bg-slate-950/60"
                data-testid="initial-subtasks-list"
              >
                <div
                  v-for="(subTask, index) in initialSubTasks"
                  :key="subTask.id"
                  class="d-flex align-items-center justify-content-between gap-2 p-1.5 mb-1 bg-slate-900 border border-slate-800 rounded small text-slate-100"
                  data-testid="initial-subtask-item"
                >
                  <div class="d-flex align-items-center gap-2 flex-grow-1 min-w-0">
                    <span class="badge text-bg-light border text-secondary font-monospace" style="font-size: 10px;">
                      #{{ index + 1 }}
                    </span>
                    <span class="text-truncate fw-medium" :title="subTask.title" style="font-size: 12.5px;">
                      {{ subTask.title }}
                    </span>
                    <span
                      v-if="subTask.assigneePersonId"
                      class="badge text-bg-light border text-body-secondary small"
                    >
                      <i class="bi bi-person me-1"></i>{{ resolvePersonName(subTask.assigneePersonId) }}
                    </span>
                  </div>
                  <button
                    type="button"
                    class="btn btn-outline-danger btn-sm py-0 px-1"
                    style="font-size: 11px; height: 22px; line-height: 20px;"
                    title="Remove sub-task"
                    :disabled="isSubmitting"
                    data-testid="remove-initial-subtask-button"
                    @click="removeInitialSubTask(index)"
                  >
                    <i class="bi bi-trash" aria-hidden="true"></i>
                  </button>
                </div>
              </div>

              <!-- Add Sub-Task Inline Inputs -->
              <div class="input-group input-group-sm">
                <input
                  v-model="newSubTaskTitle"
                  type="text"
                  class="form-control"
                  placeholder="Sub-task title (e.g. Prepare design docs, write tests)..."
                  maxlength="255"
                  :disabled="isSubmitting"
                  data-testid="initial-subtask-title-input"
                  @keydown.enter.prevent="addInitialSubTask"
                />
                <select
                  v-model="newSubTaskAssignee"
                  class="form-select"
                  style="max-width: 200px;"
                  :disabled="isSubmitting || isLoadingLookups"
                  data-testid="initial-subtask-assignee-select"
                >
                  <option value="">(Assignee: Optional)</option>
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
                  class="btn btn-outline-primary"
                  :disabled="isSubmitting || !newSubTaskTitle.trim()"
                  data-testid="add-initial-subtask-button"
                  @click="addInitialSubTask"
                >
                  <i class="bi bi-plus-lg me-1" aria-hidden="true"></i>Add Sub-Task
                </button>
              </div>
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
