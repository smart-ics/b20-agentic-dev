<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'

import { useAuthStore } from '@/stores/auth'

/**
 * SCR-AUTH-001: Login Screen (Architecture §14, §19.4, §19.5, §20, CR-009, FEAT-USR-002).
 * Dual-mode component accepting Sign In (username/email and password, POST /api/v1/auth/login)
 * or Sign Up (username, email, password, confirm password, POST /api/v1/auth/register).
 * Displays distinct RFC 7807 ProblemDetails errors, client-side validation errors,
 * and a post-registration green success alert banner.
 */
const router = useRouter()
const authStore = useAuthStore()

type AuthMode = 'signin' | 'signup'
const mode = ref<AuthMode>('signin')

const username = ref('')
const email = ref('')
const password = ref('')
const confirmPassword = ref('')
const localValidationError = ref<string | null>(null)
const successMessage = ref<string | null>(null)

const errorMessage = computed(() => localValidationError.value ?? authStore.error)
const successBannerMessage = computed(
  () => successMessage.value ?? authStore.registrationMessage,
)
const isAccountLocked = computed(() => authStore.errorCode === 'ACCOUNT_LOCKED')

const isSubmitDisabled = computed(() => {
  if (authStore.isLoading) return true

  if (mode.value === 'signin') {
    return username.value.trim().length === 0 || password.value.length === 0
  }

  return (
    username.value.trim().length === 0 ||
    email.value.trim().length === 0 ||
    password.value.length === 0 ||
    confirmPassword.value.length === 0
  )
})

function handleInput(): void {
  if (localValidationError.value) {
    localValidationError.value = null
  }
  if (authStore.error) {
    authStore.clearError()
  }
}

function toggleMode(): void {
  mode.value = mode.value === 'signin' ? 'signup' : 'signin'
  localValidationError.value = null
  authStore.clearError()
  authStore.clearRegistrationState()
  successMessage.value = null
  username.value = ''
  email.value = ''
  password.value = ''
  confirmPassword.value = ''
}

async function handleSubmit(): Promise<void> {
  localValidationError.value = null
  successMessage.value = null
  authStore.clearRegistrationState()

  if (mode.value === 'signin') {
    const trimmedUsername = username.value.trim()
    if (!trimmedUsername || !password.value) {
      localValidationError.value = 'Username and password are required.'
      return
    }

    try {
      await authStore.login(trimmedUsername, password.value)
      await router.push('/feed')
    } catch {
      // Error details are populated in authStore.error and authStore.errorCode
    }
  } else {
    const trimmedUsername = username.value.trim()
    const trimmedEmail = email.value.trim()

    if (!trimmedUsername || !trimmedEmail || !password.value || !confirmPassword.value) {
      localValidationError.value = 'All fields are required.'
      return
    }

    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
    if (!emailRegex.test(trimmedEmail)) {
      localValidationError.value = 'Please enter a valid email address.'
      return
    }

    if (password.value.length < 8) {
      localValidationError.value = 'Password must be at least 8 characters long.'
      return
    }

    if (password.value !== confirmPassword.value) {
      localValidationError.value = 'Passwords do not match.'
      return
    }

    try {
      await authStore.register(trimmedUsername, trimmedEmail, password.value)
      mode.value = 'signin'
      username.value = ''
      email.value = ''
      password.value = ''
      confirmPassword.value = ''
      successMessage.value = 'Registration submitted! Pending admin approval.'
    } catch {
      // Error details are populated in authStore.error and authStore.errorCode
    }
  }
}
</script>

<template>
  <section class="container py-4" data-screen-id="SCR-AUTH-001">
    <div class="row justify-content-center align-items-center" style="min-height: 60vh">
      <div class="col-12 col-sm-9 col-md-6 col-lg-4">
        <div class="card shadow-2xl border border-slate-800 rounded-3 overflow-hidden bg-slate-900/90 text-slate-100">
          <div class="card-body p-3 p-md-4">
            <div class="text-center mb-3">
              <div class="mb-2 d-inline-flex p-2 rounded-2 bg-cyan-950/60 border border-cyan-800/50 text-cyan-400">
                <i
                  class="bi fs-4"
                  :class="mode === 'signin' ? 'bi-shield-lock-fill' : 'bi-person-plus-fill'"
                  aria-hidden="true"
                ></i>
              </div>
              <h1 class="h5 fw-bold mb-0 text-slate-100">
                {{ mode === 'signin' ? 'Sign In to CAKRA' : 'Sign Up for CAKRA' }}
              </h1>
              <p class="text-slate-400 small mb-0" style="font-size: 11.5px">
                ICS Operational System &bull; Architecture &sect;19.4
              </p>
            </div>

            <!-- Success Message Area -->
            <div
              v-if="successBannerMessage && mode === 'signin'"
              role="alert"
              aria-live="polite"
              class="alert alert-success py-1 px-2 d-flex align-items-start gap-1 mb-2 small"
              data-testid="registration-success-alert"
            >
              <i class="bi bi-check-circle-fill flex-shrink-0 mt-0.5" aria-hidden="true"></i>
              <div>
                <div class="fw-semibold" style="font-size: 11.5px">Registration Submitted</div>
                <div style="font-size: 11px">{{ successBannerMessage }}</div>
              </div>
            </div>

            <!-- Error Message Area -->
            <div
              v-if="errorMessage"
              role="alert"
              aria-live="assertive"
              class="alert py-1 px-2 d-flex align-items-start gap-1 mb-2 small"
              :class="isAccountLocked ? 'alert-warning' : 'alert-danger'"
              data-testid="login-error-alert"
            >
              <i
                class="bi flex-shrink-0 mt-0.5"
                :class="isAccountLocked ? 'bi-lock-fill' : 'bi-exclamation-triangle-fill'"
                aria-hidden="true"
              ></i>
              <div>
                <div class="fw-semibold" style="font-size: 11.5px">
                  {{ isAccountLocked ? 'Account Locked' : (mode === 'signup' ? 'Sign Up Failed' : 'Sign In Failed') }}
                </div>
                <div style="font-size: 11px">{{ errorMessage }}</div>
              </div>
            </div>

            <!-- Sign In Form -->
            <form
              v-if="mode === 'signin'"
              novalidate
              @submit.prevent="handleSubmit"
              data-testid="login-form"
            >
              <div class="mb-2">
                <label for="username" class="form-label mb-0 small fw-medium text-slate-300" style="font-size: 11px">
                  Username or Email
                </label>
                <input
                  id="username"
                  v-model="username"
                  type="text"
                  name="username"
                  class="form-control form-control-sm bg-slate-950/60 border-slate-700 text-slate-100 focus:border-cyan-400"
                  placeholder="Enter username or email"
                  autocomplete="username"
                  required
                  :disabled="authStore.isLoading"
                  @input="handleInput"
                />
              </div>

              <div class="mb-3">
                <label for="password" class="form-label mb-0 small fw-medium text-slate-300" style="font-size: 11px">
                  Password
                </label>
                <input
                  id="password"
                  v-model="password"
                  type="password"
                  name="password"
                  class="form-control form-control-sm bg-slate-950/60 border-slate-700 text-slate-100 focus:border-cyan-400"
                  placeholder="Enter password"
                  autocomplete="current-password"
                  required
                  :disabled="authStore.isLoading"
                  @input="handleInput"
                />
              </div>

              <button
                type="submit"
                class="btn btn-primary w-100 py-1 fw-semibold btn-sm"
                style="height: 32px"
                :disabled="isSubmitDisabled"
                data-testid="login-submit-button"
              >
                <span
                  v-if="authStore.isLoading"
                  class="spinner-border spinner-border-sm me-1"
                  role="status"
                  aria-hidden="true"
                ></span>
                <span>{{ authStore.isLoading ? 'Signing in...' : 'Sign In' }}</span>
              </button>
            </form>

            <!-- Sign Up Form -->
            <form
              v-else
              novalidate
              @submit.prevent="handleSubmit"
              data-testid="signup-form"
            >
              <div class="mb-2">
                <label for="signup-username" class="form-label mb-0 small fw-medium text-slate-300" style="font-size: 11px">
                  Username
                </label>
                <input
                  id="signup-username"
                  v-model="username"
                  type="text"
                  name="username"
                  class="form-control form-control-sm bg-slate-950/60 border-slate-700 text-slate-100 focus:border-cyan-400"
                  placeholder="Enter username"
                  autocomplete="username"
                  required
                  :disabled="authStore.isLoading"
                  @input="handleInput"
                />
              </div>

              <div class="mb-2">
                <label for="signup-email" class="form-label mb-0 small fw-medium text-slate-300" style="font-size: 11px">
                  Email
                </label>
                <input
                  id="signup-email"
                  v-model="email"
                  type="email"
                  name="email"
                  class="form-control form-control-sm bg-slate-950/60 border-slate-700 text-slate-100 focus:border-cyan-400"
                  placeholder="Enter email address"
                  autocomplete="email"
                  required
                  :disabled="authStore.isLoading"
                  @input="handleInput"
                />
              </div>

              <div class="mb-2">
                <label for="signup-password" class="form-label mb-0 small fw-medium text-slate-300" style="font-size: 11px">
                  Password
                </label>
                <input
                  id="signup-password"
                  v-model="password"
                  type="password"
                  name="password"
                  class="form-control form-control-sm bg-slate-950/60 border-slate-700 text-slate-100 focus:border-cyan-400"
                  placeholder="Enter password (min. 8 characters)"
                  autocomplete="new-password"
                  required
                  :disabled="authStore.isLoading"
                  @input="handleInput"
                />
              </div>

              <div class="mb-3">
                <label for="signup-confirm-password" class="form-label mb-0 small fw-medium text-slate-300" style="font-size: 11px">
                  Confirm Password
                </label>
                <input
                  id="signup-confirm-password"
                  v-model="confirmPassword"
                  type="password"
                  name="confirmPassword"
                  class="form-control form-control-sm bg-slate-950/60 border-slate-700 text-slate-100 focus:border-cyan-400"
                  placeholder="Confirm password"
                  autocomplete="new-password"
                  required
                  :disabled="authStore.isLoading"
                  @input="handleInput"
                />
              </div>

              <button
                type="submit"
                class="btn btn-primary w-100 py-1 fw-semibold btn-sm"
                style="height: 32px"
                :disabled="isSubmitDisabled"
                data-testid="signup-submit-button"
              >
                <span
                  v-if="authStore.isLoading"
                  class="spinner-border spinner-border-sm me-1"
                  role="status"
                  aria-hidden="true"
                ></span>
                <span>{{ authStore.isLoading ? 'Signing up...' : 'Sign Up' }}</span>
              </button>
            </form>

            <!-- Mode Toggle Link -->
            <div class="text-center mt-3 pt-2 border-t border-slate-800">
              <span class="text-slate-400 small me-1" style="font-size: 11.5px">
                {{ mode === 'signin' ? "Don't have an account?" : 'Already have an account?' }}
              </span>
              <button
                type="button"
                class="btn btn-link p-0 small fw-semibold text-decoration-none text-cyan-400"
                style="font-size: 11.5px"
                data-testid="toggle-auth-mode"
                @click="toggleMode"
              >
                {{ mode === 'signin' ? 'Sign Up' : 'Sign In' }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>
