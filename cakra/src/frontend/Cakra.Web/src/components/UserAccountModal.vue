<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, ref, watch } from 'vue'

import { httpClient } from '@/api/http'
import {
  createUser,
  updateUser,
  type UserAccountDetail,
  type UserAccountSummary,
} from '@/api/users'

/**
 * SCR-USR-002: Add / Edit User Account Modal Component
 * (Architecture §7, §8, §14, §19.4, §19.6 — CR-007, FEAT-USR-001).
 *
 * - In 'create' mode: fetches active persons from `GET /api/v1/organization/persons/active`
 *   to populate the Person dropdown. Captures Person, Username, Email, Password, and Status.
 * - In 'edit' mode: Username and Person Name are displayed read-only. Allows editing Email,
 *   Status (ACTIVE, LOCKED, SUSPENDED), and optional New Password for credential reset.
 * - Enforces client-side required field, email format, and password length (min 8 chars) validation.
 * - Handles duplicate username/email and person association conflict errors (409 Conflict)
 *   and validation errors inline without closing modal.
 * - Emits `saved` and `close` on successful submission.
 */

export interface ActivePersonOption {
  id?: string
  personId?: string
  firstName?: string
  lastName?: string
  fullName?: string
  email?: string
  status?: string
  isActive?: boolean
}

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const props = withDefaults(
  defineProps<{
    show?: boolean
    mode?: 'create' | 'edit'
    user?: UserAccountSummary | UserAccountDetail | null
  }>(),
  {
    show: false,
    mode: 'create',
    user: null,
  },
)

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'saved', user: UserAccountDetail): void
}>()

// Active persons for create mode dropdown
const activePersons = ref<ActivePersonOption[]>([])
const isLoadingPersons = ref(false)

// Form fields
const personId = ref('')
const username = ref('')
const email = ref('')
const password = ref('')
const newPassword = ref('')
const status = ref('ACTIVE')

// State & validation
const isSubmitting = ref(false)
const errorMessage = ref<string | null>(null)
const validationErrors = ref<{
  personId?: string
  username?: string
  email?: string
  password?: string
  newPassword?: string
}>({})

const isEditMode = computed(() => props.mode === 'edit')
const modalTitle = computed(() => (isEditMode.value ? 'Edit User Account' : 'Add User Account'))

function resolvePersonId(person: ActivePersonOption): string {
  return (person.id || person.personId || '').trim()
}

function resolvePersonName(person: ActivePersonOption): string {
  if (person.fullName && person.fullName.trim().length > 0) {
    return person.fullName.trim()
  }
  const parts = [person.firstName, person.lastName].filter(Boolean)
  return parts.length > 0 ? parts.join(' ') : 'Unnamed Person'
}

function isValidEmail(val: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(val.trim())
}

function extractErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof AxiosError) {
    const data = err.response?.data as ProblemDetailsPayload | undefined
    if (err.response?.status === 409) {
      return (
        data?.detail ||
        'Conflict: A user account with this username, email, or linked person already exists.'
      )
    }
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

async function loadActivePersons(): Promise<void> {
  isLoadingPersons.value = true
  try {
    const response = await httpClient.get<ActivePersonOption[]>('/organization/persons/active')
    activePersons.value = response.data ?? []
  } catch (err) {
    console.error('Failed to load active persons for user creation:', err)
  } finally {
    isLoadingPersons.value = false
  }
}

function resetForm(): void {
  errorMessage.value = null
  validationErrors.value = {}

  if (props.mode === 'edit' && props.user) {
    personId.value = props.user.personId || ''
    username.value = props.user.username || ''
    email.value = props.user.email || ''
    password.value = ''
    newPassword.value = ''
    status.value = props.user.status || 'ACTIVE'
  } else {
    personId.value = ''
    username.value = ''
    email.value = ''
    password.value = ''
    newPassword.value = ''
    status.value = 'ACTIVE'
  }
}

function validate(): boolean {
  const errors: typeof validationErrors.value = {}

  if (!isEditMode.value) {
    if (!personId.value.trim()) {
      errors.personId = 'Please select an associated Person.'
    }
    if (!username.value.trim()) {
      errors.username = 'Username is required.'
    }
    if (!email.value.trim()) {
      errors.email = 'Email is required.'
    } else if (!isValidEmail(email.value)) {
      errors.email = 'Please provide a valid email address.'
    }
    if (!password.value) {
      errors.password = 'Password is required.'
    } else if (password.value.length < 8) {
      errors.password = 'Password must be at least 8 characters.'
    }
  } else {
    if (!email.value.trim()) {
      errors.email = 'Email is required.'
    } else if (!isValidEmail(email.value)) {
      errors.email = 'Please provide a valid email address.'
    }
    if (newPassword.value && newPassword.value.length < 8) {
      errors.newPassword = 'New password must be at least 8 characters.'
    }
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
    let savedUser: UserAccountDetail
    if (isEditMode.value) {
      if (!props.user?.userId) {
        throw new Error('User ID is missing for update operation.')
      }
      savedUser = await updateUser(props.user.userId, {
        email: email.value.trim(),
        status: status.value,
        newPassword: newPassword.value.trim() ? newPassword.value : undefined,
      })
    } else {
      savedUser = await createUser({
        personId: personId.value.trim(),
        username: username.value.trim(),
        email: email.value.trim(),
        password: password.value,
        status: status.value,
      })
    }

    emit('saved', savedUser)
    emit('close')
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to save user account.')
  } finally {
    isSubmitting.value = false
  }
}

function handleClose(): void {
  if (!isSubmitting.value) {
    emit('close')
  }
}

watch(
  () => props.show,
  (newVal) => {
    if (newVal) {
      resetForm()
      if (!isEditMode.value && activePersons.value.length === 0) {
        loadActivePersons()
      }
    }
  },
  { immediate: true },
)

watch(
  () => props.user,
  () => {
    if (props.show) {
      resetForm()
    }
  },
)

onMounted(() => {
  if (props.show && !isEditMode.value && activePersons.value.length === 0) {
    loadActivePersons()
  }
})
</script>

<template>
  <div v-if="props.show" data-screen-id="SCR-USR-002">
    <!-- Bootstrap Modal Dialog -->
    <div
      class="modal fade show d-block"
      tabindex="-1"
      role="dialog"
      aria-labelledby="userAccountModalTitle"
      aria-modal="true"
    >
      <div class="modal-dialog modal-dialog-centered">
        <div class="modal-content shadow-2xl border border-slate-800 bg-slate-900/95 text-slate-100">
          <!-- Modal Header -->
          <div class="modal-header py-2 px-3 bg-slate-950/60 border-b border-slate-800">
            <h2 id="userAccountModalTitle" class="modal-title h6 fw-bold mb-0 d-flex align-items-center gap-2">
              <i
                v-if="isEditMode"
                class="bi bi-person-gear text-cyan-400"
                aria-hidden="true"
              ></i>
              <i
                v-else
                class="bi bi-person-plus-fill text-cyan-400"
                aria-hidden="true"
              ></i>
              <span>{{ modalTitle }}</span>
              <span class="badge bg-slate-800 border border-slate-700 text-slate-300 font-monospace" style="font-size: 10px">
                SCR-USR-002
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
                data-testid="user-modal-error-alert"
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

              <!-- Create Mode: Person Selector -->
              <div v-if="!isEditMode" class="mb-2">
                <label for="userModalPersonSelect" class="form-label mb-1 small fw-medium text-slate-300" style="font-size: 11.5px">
                  Organizational Person <span class="text-danger">*</span>
                </label>
                <div class="position-relative">
                  <select
                    id="userModalPersonSelect"
                    v-model="personId"
                    class="form-select form-select-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                    :class="{ 'is-invalid': validationErrors.personId }"
                    :disabled="isSubmitting || isLoadingPersons"
                    data-testid="select-person"
                  >
                    <option value="">
                      {{ isLoadingPersons ? 'Loading active persons...' : '-- Select an active person --' }}
                    </option>
                    <option
                      v-for="person in activePersons"
                      :key="resolvePersonId(person)"
                      :value="resolvePersonId(person)"
                    >
                      {{ resolvePersonName(person) }}
                    </option>
                  </select>
                  <div v-if="validationErrors.personId" class="invalid-feedback small" style="font-size: 11px">
                    {{ validationErrors.personId }}
                  </div>
                </div>
              </div>

              <!-- Edit Mode: Person Name (Read-Only) -->
              <div v-else class="mb-2">
                <label class="form-label mb-1 small fw-medium text-slate-400" style="font-size: 11.5px">
                  Organizational Person
                </label>
                <input
                  type="text"
                  class="form-control form-control-sm bg-slate-950/60 border-slate-700 text-slate-300"
                  :value="props.user?.personName || '—'"
                  readonly
                  disabled
                  data-testid="static-person"
                />
              </div>

              <!-- Create Mode: Username -->
              <div v-if="!isEditMode" class="mb-2">
                <label for="userModalUsername" class="form-label mb-1 small fw-medium text-slate-300" style="font-size: 11.5px">
                  Username <span class="text-danger">*</span>
                </label>
                <input
                  id="userModalUsername"
                  v-model="username"
                  type="text"
                  class="form-control form-control-sm font-monospace bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                  :class="{ 'is-invalid': validationErrors.username }"
                  placeholder="e.g. john.doe"
                  :disabled="isSubmitting"
                  data-testid="input-username"
                />
                <div v-if="validationErrors.username" class="invalid-feedback small" style="font-size: 11px">
                  {{ validationErrors.username }}
                </div>
              </div>

              <!-- Edit Mode: Username (Read-Only) -->
              <div v-else class="mb-2">
                <label class="form-label mb-1 small fw-medium text-slate-400" style="font-size: 11.5px">
                  Username
                </label>
                <input
                  type="text"
                  class="form-control form-control-sm font-monospace bg-slate-950/60 border-slate-700 text-slate-300"
                  :value="props.user?.username || '—'"
                  readonly
                  disabled
                  data-testid="static-username"
                />
              </div>

              <!-- Email Address -->
              <div class="mb-2">
                <label for="userModalEmail" class="form-label mb-1 small fw-medium text-slate-300" style="font-size: 11.5px">
                  Email Address <span class="text-danger">*</span>
                </label>
                <input
                  id="userModalEmail"
                  v-model="email"
                  type="email"
                  class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                  :class="{ 'is-invalid': validationErrors.email }"
                  placeholder="e.g. john.doe@example.com"
                  :disabled="isSubmitting"
                  data-testid="input-email"
                />
                <div v-if="validationErrors.email" class="invalid-feedback small" style="font-size: 11px">
                  {{ validationErrors.email }}
                </div>
              </div>

              <!-- Create Mode: Initial Password -->
              <div v-if="!isEditMode" class="mb-2">
                <label for="userModalPassword" class="form-label mb-1 small fw-medium text-slate-300" style="font-size: 11.5px">
                  Initial Password <span class="text-danger">*</span>
                </label>
                <input
                  id="userModalPassword"
                  v-model="password"
                  type="password"
                  class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                  :class="{ 'is-invalid': validationErrors.password }"
                  placeholder="Minimum 8 characters"
                  :disabled="isSubmitting"
                  data-testid="input-password"
                />
                <div v-if="validationErrors.password" class="invalid-feedback small" style="font-size: 11px">
                  {{ validationErrors.password }}
                </div>
              </div>

              <!-- Edit Mode: Optional New Password (Reset) -->
              <div v-else class="mb-2">
                <label for="userModalNewPassword" class="form-label mb-1 small fw-medium text-slate-300" style="font-size: 11.5px">
                  Reset Password (Optional)
                </label>
                <input
                  id="userModalNewPassword"
                  v-model="newPassword"
                  type="password"
                  class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                  :class="{ 'is-invalid': validationErrors.newPassword }"
                  placeholder="Leave blank to keep current password (min 8 chars if resetting)"
                  :disabled="isSubmitting"
                  data-testid="input-new-password"
                />
                <div v-if="validationErrors.newPassword" class="invalid-feedback small" style="font-size: 11px">
                  {{ validationErrors.newPassword }}
                </div>
                <div class="form-text small text-slate-400" style="font-size: 10.5px">
                  Only enter a new password if you wish to reset this user's credentials.
                </div>
              </div>

              <!-- Status Dropdown -->
              <div class="mb-1">
                <label for="userModalStatus" class="form-label mb-1 small fw-medium text-slate-300" style="font-size: 11.5px">
                  Account Status <span class="text-danger">*</span>
                </label>
                <select
                  id="userModalStatus"
                  v-model="status"
                  class="form-select form-select-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
                  :disabled="isSubmitting"
                  data-testid="select-status"
                >
                  <option value="ACTIVE">ACTIVE</option>
                  <option v-if="isEditMode" value="LOCKED">LOCKED</option>
                  <option value="SUSPENDED">SUSPENDED</option>
                </select>
                <div v-if="isEditMode && props.user?.status === 'LOCKED' && status === 'ACTIVE'" class="form-text text-emerald-400 small" style="font-size: 10.5px">
                  <i class="bi bi-info-circle me-1" aria-hidden="true"></i>Setting status to ACTIVE will reset failed login attempts to 0.
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
                data-testid="cancel-user-btn"
                @click="handleClose"
              >
                Cancel
              </button>
              <button
                type="submit"
                class="btn btn-primary btn-sm"
                style="font-size: 11.5px; height: 26px; line-height: 24px"
                :disabled="isSubmitting"
                data-testid="submit-user-btn"
              >
                <span
                  v-if="isSubmitting"
                  class="spinner-border spinner-border-sm me-1"
                  role="status"
                  aria-hidden="true"
                ></span>
                <i v-else class="bi bi-check-lg me-1" aria-hidden="true"></i>
                {{ isEditMode ? 'Update User' : 'Create User' }}
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
