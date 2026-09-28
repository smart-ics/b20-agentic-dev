<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'

import { useAuthStore } from '@/stores/auth'

/**
 * SCR-AUTH-001: Login Screen (Architecture §14, §19.4, §19.5, §20).
 * Accepts username/email and password, invokes `POST /api/v1/auth/login`,
 * displays distinct RFC 7807 ProblemDetails errors (invalid credentials vs. account locked),
 * and redirects to SCR-FEED-001 (`/feed`) upon successful authentication.
 */
const router = useRouter()
const authStore = useAuthStore()

const username = ref('')
const password = ref('')
const localValidationError = ref<string | null>(null)

const errorMessage = computed(() => localValidationError.value ?? authStore.error)
const isAccountLocked = computed(() => authStore.errorCode === 'ACCOUNT_LOCKED')
const isSubmitDisabled = computed(
  () => authStore.isLoading || username.value.trim().length === 0 || password.value.length === 0,
)

function handleInput(): void {
  if (localValidationError.value) {
    localValidationError.value = null
  }
  if (authStore.error) {
    authStore.clearError()
  }
}

async function handleSubmit(): Promise<void> {
  localValidationError.value = null

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
}
</script>

<template>
  <section class="container py-5" data-screen-id="SCR-AUTH-001">
    <div class="row justify-content-center align-items-center min-vh-75">
      <div class="col-12 col-sm-10 col-md-7 col-lg-5 col-xl-4">
        <div class="card shadow-sm border-0">
          <div class="card-body p-4 p-md-5">
            <div class="text-center mb-4">
              <div class="mb-2">
                <i class="bi bi-shield-lock-fill text-primary fs-1" aria-hidden="true"></i>
              </div>
              <h1 class="h4 fw-bold mb-1">Sign In to CAKRA</h1>
              <p class="text-body-secondary small mb-0">
                ICS Operational System
              </p>
            </div>

            <!-- Error Message Area -->
            <div
              v-if="errorMessage"
              role="alert"
              aria-live="assertive"
              class="alert d-flex align-items-start gap-2 mb-4"
              :class="isAccountLocked ? 'alert-warning' : 'alert-danger'"
              data-testid="login-error-alert"
            >
              <i
                class="bi flex-shrink-0 mt-1"
                :class="isAccountLocked ? 'bi-lock-fill' : 'bi-exclamation-triangle-fill'"
                aria-hidden="true"
              ></i>
              <div>
                <div class="fw-semibold">
                  {{ isAccountLocked ? 'Account Locked' : 'Sign In Failed' }}
                </div>
                <div class="small">{{ errorMessage }}</div>
              </div>
            </div>

            <form novalidate @submit.prevent="handleSubmit" data-testid="login-form">
              <div class="mb-3">
                <label for="username" class="form-label fw-medium">Username or Email</label>
                <input
                  id="username"
                  v-model="username"
                  type="text"
                  name="username"
                  class="form-control"
                  placeholder="Enter your username or email"
                  autocomplete="username"
                  required
                  :disabled="authStore.isLoading"
                  @input="handleInput"
                />
              </div>

              <div class="mb-4">
                <label for="password" class="form-label fw-medium">Password</label>
                <input
                  id="password"
                  v-model="password"
                  type="password"
                  name="password"
                  class="form-control"
                  placeholder="Enter your password"
                  autocomplete="current-password"
                  required
                  :disabled="authStore.isLoading"
                  @input="handleInput"
                />
              </div>

              <button
                type="submit"
                class="btn btn-primary w-100 py-2 fw-semibold"
                :disabled="isSubmitDisabled"
                data-testid="login-submit-button"
              >
                <span
                  v-if="authStore.isLoading"
                  class="spinner-border spinner-border-sm me-2"
                  role="status"
                  aria-hidden="true"
                ></span>
                <span>{{ authStore.isLoading ? 'Signing in...' : 'Sign In' }}</span>
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>
