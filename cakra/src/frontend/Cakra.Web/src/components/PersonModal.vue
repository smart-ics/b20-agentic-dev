<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'

import {
  createPerson,
  listRoles,
  updatePerson,
  type PersonDto,
  type ProblemDetailsPayload,
  type RoleDto,
} from '@/api/persons'
import { useAuthStore } from '@/stores/auth'

/**
 * SCR-ORG-002: Add / Edit Organizational Person Modal Component
 * (Architecture §7, §8, §14, §19.4, §19.6 — CR-008, FEAT-ORG-001; CR-027, FEAT-ORG-002).
 *
 * - In 'create' mode: captures First Name, Last Name, Email, and mandatory Roles to create a new Person.
 * - In 'edit' mode: populates and allows updating First Name, Last Name, Email, and Roles.
 * - Enforces client-side required field, max length, email format, and at least one role assigned.
 * - Self-Demotion Guard: Prevents logged-in Administrator from unchecking Administrator on their own record.
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

const authStore = useAuthStore()

// Form fields
const firstName = ref('')
const lastName = ref('')
const email = ref('')
const selectedRoleIds = ref<string[]>([])

// Role master catalogue state
const availableRoles = ref<RoleDto[]>([])
const isLoadingRoles = ref(false)
const rolesLoadError = ref<string | null>(null)

// State & validation
const isSubmitting = ref(false)
const errorMessage = ref<string | null>(null)
const validationErrors = ref<{
  firstName?: string
  lastName?: string
  email?: string
  roles?: string
}>({})

const isEditMode = computed(() => props.mode === 'edit')
const modalTitle = computed(() => (isEditMode.value ? 'Edit Person' : 'Add Person'))

function resolvePersonId(): string | null {
  if (!props.person) return null
  return (props.person.id || props.person.personId || '').trim() || null
}

const isEditingSelf = computed<boolean>(() => {
  if (!isEditMode.value) return false
  const myPersonId = (authStore.user?.personId || authStore.currentUser?.personId || '')
    .trim()
    .toLowerCase()
  const targetPersonId = (resolvePersonId() || '').trim().toLowerCase()
  return Boolean(myPersonId && targetPersonId && myPersonId === targetPersonId)
})

function isAdministratorRole(role: RoleDto): boolean {
  return (role.name || '').trim().toLowerCase() === 'administrator'
}

function isRoleDisabled(role: RoleDto): boolean {
  return isEditingSelf.value && isAdministratorRole(role)
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

function syncSelectedRolesFromPerson(): void {
  if (props.mode === 'edit' && props.person) {
    const activeRoleNames = (props.person.roles || []).map((r) => r.trim().toLowerCase())
    const matchedIds = availableRoles.value
      .filter((role) => activeRoleNames.includes(role.name.trim().toLowerCase()))
      .map((role) => role.id)

    // Ensure administrator role ID is retained if editing self
    if (isEditingSelf.value) {
      const adminRole = availableRoles.value.find((r) => isAdministratorRole(r))
      if (adminRole && !matchedIds.includes(adminRole.id)) {
        matchedIds.push(adminRole.id)
      }
    }

    selectedRoleIds.value = matchedIds
  } else {
    selectedRoleIds.value = []
  }
}

async function fetchRoles(): Promise<void> {
  isLoadingRoles.value = true
  rolesLoadError.value = null
  try {
    const roles = await listRoles()
    availableRoles.value = roles
    if (props.show && props.mode === 'edit' && props.person) {
      syncSelectedRolesFromPerson()
    }
  } catch {
    rolesLoadError.value = 'Failed to load master roles.'
  } finally {
    isLoadingRoles.value = false
  }
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

  syncSelectedRolesFromPerson()
}

function validate(): boolean {
  const errors: {
    firstName?: string
    lastName?: string
    email?: string
    roles?: string
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

  if (selectedRoleIds.value.length === 0) {
    errors.roles = 'At least one role must be assigned.'
  }

  validationErrors.value = errors
  return Object.keys(errors).length === 0
}

watch(
  () => selectedRoleIds.value.length,
  (count) => {
    if (count > 0 && validationErrors.value.roles) {
      validationErrors.value.roles = undefined
    }
  },
)

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
        roleIds: [...selectedRoleIds.value],
      })
    } else {
      savedPerson = await createPerson({
        firstName: firstName.value.trim(),
        lastName: lastName.value.trim(),
        email: email.value.trim(),
        roleIds: [...selectedRoleIds.value],
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
      fetchRoles()
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
  fetchRoles()
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
        <div class="modal-content shadow-2xl border border-slate-800 bg-slate-900/95 text-slate-100">
          <!-- Modal Header -->
          <div class="modal-header py-2 px-3 bg-slate-950/60 border-b border-slate-800">
            <h2 id="personModalTitle" class="modal-title h6 fw-bold mb-0 d-flex align-items-center gap-2">
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
                SCR-ORG-002
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
                <label for="personFirstName" class="form-label mb-1 small fw-medium text-slate-300" style="font-size: 11.5px">
                  First Name <span class="text-danger">*</span>
                </label>
                <input
                  id="personFirstName"
                  v-model="firstName"
                  type="text"
                  maxlength="100"
                  class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
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
                <label for="personLastName" class="form-label mb-1 small fw-medium text-slate-300" style="font-size: 11.5px">
                  Last Name <span class="text-danger">*</span>
                </label>
                <input
                  id="personLastName"
                  v-model="lastName"
                  type="text"
                  maxlength="100"
                  class="form-control form-control-sm bg-slate-900 border-slate-700 text-slate-100 focus:border-cyan-400"
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
                <label for="personEmail" class="form-label mb-1 small fw-medium text-slate-300" style="font-size: 11.5px">
                  Email Address <span class="text-danger">*</span>
                </label>
                <input
                  id="personEmail"
                  v-model="email"
                  type="email"
                  maxlength="255"
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

              <!-- Roles Fieldset -->
              <div class="mb-2">
                <label class="form-label mb-1 small fw-medium text-slate-300" style="font-size: 11.5px">
                  Roles <span class="text-danger">*</span>
                </label>
                <fieldset
                  class="border rounded p-2 bg-slate-950/40"
                  :class="validationErrors.roles ? 'border-danger' : 'border-slate-800'"
                  data-testid="roles-fieldset"
                >
                  <legend class="visually-hidden">Roles</legend>

                  <!-- Loading state -->
                  <div
                    v-if="isLoadingRoles && availableRoles.length === 0"
                    class="text-slate-400 small py-2 d-flex align-items-center gap-2"
                  >
                    <span
                      class="spinner-border spinner-border-sm text-cyan-400"
                      role="status"
                      aria-hidden="true"
                    ></span>
                    <span>Loading available roles...</span>
                  </div>

                  <!-- Error state -->
                  <div
                    v-else-if="rolesLoadError && availableRoles.length === 0"
                    class="text-danger small py-1 d-flex align-items-center justify-content-between"
                  >
                    <span>{{ rolesLoadError }}</span>
                    <button
                      type="button"
                      class="btn btn-outline-secondary btn-sm py-0 px-2 text-xs"
                      @click="fetchRoles"
                    >
                      Retry
                    </button>
                  </div>

                  <!-- Role Checkboxes List -->
                  <div
                    v-else
                    class="d-flex flex-column gap-2"
                    style="max-height: 200px; overflow-y: auto;"
                    data-testid="roles-list"
                  >
                    <div
                      v-for="role in availableRoles"
                      :key="role.id"
                      class="form-check p-2 rounded border transition-colors mb-0"
                      :class="[
                        selectedRoleIds.includes(role.id)
                          ? 'bg-slate-800/70 border-cyan-500/40'
                          : 'bg-slate-900/50 border-slate-800'
                      ]"
                    >
                      <input
                        :id="`role-${role.id}`"
                        v-model="selectedRoleIds"
                        type="checkbox"
                        :value="role.id"
                        class="form-check-input bg-slate-900 border-slate-700 text-cyan-500"
                        :disabled="isSubmitting || isRoleDisabled(role)"
                        :data-testid="`role-checkbox-${role.name.toLowerCase().replace(/\s+/g, '-')}`"
                      />
                      <label
                        :for="`role-${role.id}`"
                        class="form-check-label w-100 cursor-pointer ms-1"
                      >
                        <div class="d-flex align-items-center flex-wrap gap-2">
                          <span class="fw-semibold text-slate-200 small" style="font-size: 12px">
                            {{ role.name }}
                          </span>
                          <span
                            v-if="isRoleDisabled(role)"
                            class="badge bg-amber-500/20 text-amber-300 border border-amber-500/30 font-normal"
                            style="font-size: 10px"
                            data-testid="self-demotion-guard-badge"
                            title="Protected: Cannot remove Administrator from your own profile"
                          >
                            <i class="bi bi-shield-lock-fill me-1" aria-hidden="true"></i>
                            Protected: Cannot remove Administrator from your own profile
                          </span>
                        </div>
                        <div
                          v-if="role.description"
                          class="text-slate-400 small mt-0.5"
                          style="font-size: 11px"
                        >
                          {{ role.description }}
                        </div>
                      </label>
                    </div>
                  </div>
                </fieldset>
                <div
                  v-if="validationErrors.roles"
                  class="invalid-feedback d-block small mt-1"
                  style="font-size: 11px"
                  data-testid="roles-validation-error"
                >
                  {{ validationErrors.roles }}
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
    <div class="modal-backdrop fade show" style="background-color: rgba(2, 6, 23, 0.8); backdrop-filter: blur(4px);" @click="handleClose"></div>
  </div>
</template>
