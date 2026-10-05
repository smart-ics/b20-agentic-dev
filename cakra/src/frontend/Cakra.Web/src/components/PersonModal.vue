<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'

import {
  createPerson,
  updatePerson,
  type PersonDto,
  type ProblemDetailsPayload,
} from '@/api/persons'

/**
 * SCR-ORG-002: Add / Edit Organizational Person Modal Component
 * (Architecture §7, §8, §14, §19.4, §19.6 — CR-008, FEAT-ORG-001).
 *
 * - In 'create' mode: captures First Name, Last Name, and Email to create a new Person.
 * - In 'edit' mode: populates and allows updating First Name, Last Name, and Email.
 * - Enforces client-side required field, max length, and email format validation.
 * - Handles duplicate email conflict errors (409 Conflict) and generic RFC 7807 problem details inline.
 * - Emits `saved` and `close` on successful submission.
 */

const props = withDefaults(
  defineProps<{
    show?: boolean
    mode?: 'create' | 'edit'
    person?: PersonDto | null
  }>(),
  {
    show: false,
    mode: 'create',
    person: null,
  },
)

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'saved', person: PersonDto): void
}>()

// Form fields
const firstName = ref('')
const lastName = ref('')
const email = ref('')

// State & validation
const isSubmitting = ref(false)
const errorMessage = ref<string | null>(null)
const validationErrors = ref<{
  firstName?: string
  lastName?: string
  email?: string
}>({})

const isEditMode = computed(() => props.mode === 'edit')
const modalTitle = computed(() => (isEditMode.value ? 'Edit Person' : 'Add Person'))

function resolvePersonId(): string | null {
  if (!props.person) return null
  return (props.person.id || props.person.personId || '').trim() || null
}

function isValidEmail(val: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(val.trim())
}

function extractErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof AxiosError) {
    if (err.response?.status === 409) {
      const data = err.response.data as ProblemDetailsPayload | undefined
      return (
        data?.detail ||
        'A person with this email address already exists. Please specify a unique email.'
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

  if (props.mode === 'edit' && props.person) {
    firstName.value = props.person.firstName || ''
    lastName.value = props.person.lastName || ''
    email.value = props.person.email || ''
  } else {
    firstName.value = ''
    lastName.value = ''
    email.value = ''
  }
}

function validate(): boolean {
  const errors: {
    firstName?: string
    lastName?: string
    email?: string
  } = {}

  const trimmedFirst = firstName.value.trim()
  if (!trimmedFirst) {
    errors.firstName = 'First name is required.'
  } else if (trimmedFirst.length > 100) {
    errors.firstName = 'First name cannot exceed 100 characters.'
  }

  const trimmedLast = lastName.value.trim()
  if (!trimmedLast) {
    errors.lastName = 'Last name is required.'
  } else if (trimmedLast.length > 100) {
    errors.lastName = 'Last name cannot exceed 100 characters.'
  }

  const trimmedEmail = email.value.trim()
  if (!trimmedEmail) {
    errors.email = 'Email is required.'
  } else if (trimmedEmail.length > 255) {
    errors.email = 'Email cannot exceed 255 characters.'
  } else if (!isValidEmail(trimmedEmail)) {
    errors.email = 'Please provide a valid email address.'
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
    let savedPerson: PersonDto
    if (isEditMode.value) {
      const personId = resolvePersonId()
      if (!personId) {
        throw new Error('Person ID is missing for update operation.')
      }
      savedPerson = await updatePerson(personId, {
        firstName: firstName.value.trim(),
        lastName: lastName.value.trim(),
        email: email.value.trim(),
      })
    } else {
      savedPerson = await createPerson({
        firstName: firstName.value.trim(),
        lastName: lastName.value.trim(),
        email: email.value.trim(),
      })
    }

    emit('saved', savedPerson)
    resetForm()
    emit('close')
  } catch (err: unknown) {
    const defaultMsg = isEditMode.value
      ? 'Failed to update person record.'
      : 'Failed to create person record.'
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
  if (event.key === 'Escape' && props.show && !isSubmitting.value) {
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
  { immediate: true },
)

watch(
  () => props.person,
  () => {
    if (props.show) {
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
  <div v-if="props.show" data-screen-id="SCR-ORG-002" data-testid="person-modal">
    <!-- Bootstrap Modal Dialog -->
    <div
      class="modal fade show d-block"
      tabindex="-1"
      role="dialog"
      aria-labelledby="personModalTitle"
      aria-modal="true"
    >
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content shadow border">
          <!-- Modal Header -->
          <div class="modal-header py-2 px-3 bg-body-tertiary">
            <h2 id="personModalTitle" class="modal-title h6 fw-bold mb-0 d-flex align-items-center gap-2">
              <i
                v-if="isEditMode"
                class="bi bi-person-gear text-primary"
                aria-hidden="true"
              ></i>
              <i
                v-else
                class="bi bi-person-plus-fill text-primary"
                aria-hidden="true"
              ></i>
              <span>{{ modalTitle }}</span>
              <span class="badge text-bg-light border text-secondary font-monospace" style="font-size: 10px">
                SCR-ORG-002
              </span>
            </h2>
            <button
              type="button"
              class="btn-close py-1 px-2"
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
                data-testid="person-modal-error-alert"
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

              <!-- First Name -->
              <div class="mb-2">
                <label for="personFirstName" class="form-label mb-1 small fw-medium" style="font-size: 11.5px">
                  First Name <span class="text-danger">*</span>
                </label>
                <input
                  id="personFirstName"
                  v-model="firstName"
                  type="text"
                  maxlength="100"
                  class="form-control form-control-sm"
                  :class="{ 'is-invalid': validationErrors.firstName }"
                  placeholder="e.g. John"
                  :disabled="isSubmitting"
                  data-testid="input-first-name"
                />
                <div v-if="validationErrors.firstName" class="invalid-feedback small" style="font-size: 11px">
                  {{ validationErrors.firstName }}
                </div>
              </div>

              <!-- Last Name -->
              <div class="mb-2">
                <label for="personLastName" class="form-label mb-1 small fw-medium" style="font-size: 11.5px">
                  Last Name <span class="text-danger">*</span>
                </label>
                <input
                  id="personLastName"
                  v-model="lastName"
                  type="text"
                  maxlength="100"
                  class="form-control form-control-sm"
                  :class="{ 'is-invalid': validationErrors.lastName }"
                  placeholder="e.g. Doe"
                  :disabled="isSubmitting"
                  data-testid="input-last-name"
                />
                <div v-if="validationErrors.lastName" class="invalid-feedback small" style="font-size: 11px">
                  {{ validationErrors.lastName }}
                </div>
              </div>

              <!-- Email Address -->
              <div class="mb-2">
                <label for="personEmail" class="form-label mb-1 small fw-medium" style="font-size: 11.5px">
                  Email Address <span class="text-danger">*</span>
                </label>
                <input
                  id="personEmail"
                  v-model="email"
                  type="email"
                  maxlength="255"
                  class="form-control form-control-sm"
                  :class="{ 'is-invalid': validationErrors.email }"
                  placeholder="e.g. john.doe@example.com"
                  :disabled="isSubmitting"
                  data-testid="input-email"
                />
                <div v-if="validationErrors.email" class="invalid-feedback small" style="font-size: 11px">
                  {{ validationErrors.email }}
                </div>
              </div>
            </div>

            <!-- Modal Footer -->
            <div class="modal-footer py-2 px-3 bg-body-tertiary">
              <button
                type="button"
                class="btn btn-outline-secondary btn-sm"
                style="font-size: 11.5px; height: 26px; line-height: 24px"
                :disabled="isSubmitting"
                data-testid="cancel-person-btn"
                @click="handleClose"
              >
                Cancel
              </button>
              <button
                type="submit"
                class="btn btn-primary btn-sm"
                style="font-size: 11.5px; height: 26px; line-height: 24px"
                :disabled="isSubmitting"
                data-testid="submit-person-btn"
              >
                <span
                  v-if="isSubmitting"
                  class="spinner-border spinner-border-sm me-1"
                  role="status"
                  aria-hidden="true"
                ></span>
                <i v-else class="bi bi-check-lg me-1" aria-hidden="true"></i>
                {{ isEditMode ? 'Update Person' : 'Create Person' }}
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
