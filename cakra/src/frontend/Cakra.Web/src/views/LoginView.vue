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
  <section class="container py-4" data-screen-id="SCR-AUTH-001">
    <div class="row justify-content-center align-items-center" style="min-height: 60vh">
      <div class="col-12 col-sm-9 col-md-6 col-lg-4">
        <div class="card shadow-sm border rounded-3 overflow-hidden">
          <div class="card-body p-3 p-md-4">
            <div class="text-center mb-3">
              <div class="mb-2 d-inline-flex p-2 rounded-2 bg-primary bg-opacity-10 text-primary">
                <i class="bi bi-shield-lock-fill fs-4" aria-hidden="true"></i>
              </div>
              <h1 class="h5 fw-bold mb-0">Sign In to CAKRA</h1>
              <p class="text-body-secondary small mb-0" style="font-size: 11.5px">
                ICS Operational System &bull; Architecture &sect;19.4
              </p>
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
                  {{ isAccountLocked ? 'Account Locked' : 'Sign In Failed' }}
                </div>
                <div style="font-size: 11px">{{ errorMessage }}</div>
              </div>
            </div>

            <form novalidate @submit.prevent="handleSubmit" data-testid="login-form">
              <div class="mb-2">
                <label for="username" class="form-label mb-0 small fw-medium" style="font-size: 11px">Username or Email</label>
                <input
                  id="username"
                  v-model="username"
                  type="text"
                  name="username"
                  class="form-control form-control-sm"
                  placeholder="Enter username or email"
                  autocomplete="username"
                  required
                  :disabled="authStore.isLoading"
                  @input="handleInput"
                />
              </div>

              <div class="mb-3">
                <label for="password" class="form-label mb-0 small fw-medium" style="font-size: 11px">Password</label>
                <input
                  id="password"
                  v-model="password"
                  type="password"
                  name="password"
                  class="form-control form-control-sm"
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
          </div>
        </div>
      </div>
    </div>
  </section>
</template>
