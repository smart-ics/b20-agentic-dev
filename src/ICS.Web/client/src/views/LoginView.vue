<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const username = ref('')
const password = ref('')
const showPassword = ref(false)
const isLoading = ref(false)
const errorMessage = ref<string | null>(null)
const errorCode = ref<string | null>(null)

function togglePasswordVisibility() {
  showPassword.value = !showPassword.value
}

async function handleLogin() {
  errorMessage.value = null
  errorCode.value = null

  const trimmedUsername = username.value.trim()
  if (!trimmedUsername || !password.value) {
    errorMessage.value = 'Please enter both username/email and password.'
    errorCode.value = 'VALIDATION_ERROR'
    return
  }

  isLoading.value = true

  try {
    await authStore.login(trimmedUsername, password.value)

    // Successful login: redirect to SCR-FEED-001 (/feed) per Architecture §14 and P2-S11
    const redirectPath = (route.query.redirect as string) || '/feed'
    await router.push(redirectPath)
  } catch (error: any) {
    if (error.response) {
      const status = error.response.status
      const data = error.response.data

      const extractedCode = data?.errorCode || ''
      errorCode.value = extractedCode

      if (status === 423 || extractedCode === 'ACCOUNT_LOCKED') {
        // Distinct RFC 7807 Account Locked messaging (Architecture §14, §19.6)
        errorMessage.value = data?.detail || 'Account is locked. Please contact your system administrator.'
      } else if (status === 401 || extractedCode === 'INVALID_CREDENTIALS') {
        // Distinct RFC 7807 Invalid Credentials messaging
        errorMessage.value = data?.detail || 'Invalid username or password. Please verify your credentials and try again.'
      } else if (status === 400) {
        errorMessage.value = data?.detail || 'Invalid request. Please check your credentials format.'
      } else {
        errorMessage.value = data?.detail || 'An unexpected error occurred during authentication. Please try again.'
      }
    } else if (error.request) {
      errorMessage.value = 'Unable to connect to the authentication service. Please verify your network connection.'
    } else {
      errorMessage.value = error.message || 'An error occurred during login.'
    }
  } finally {
    isLoading.value = false
  }
}
</script>

<template>
  <div class="container py-5">
    <div class="row justify-content-center">
      <div class="col-11 col-sm-9 col-md-6 col-lg-5 col-xl-4">
        <!-- Brand & System Header -->
        <div class="text-center mb-4">
          <div class="d-inline-flex align-items-center justify-content-center bg-primary text-white rounded-3 p-3 shadow-sm mb-3">
            <i class="bi bi-diagram-3-fill fs-2"></i>
          </div>
          <h1 class="h3 fw-bold mb-1">ICS Operational System</h1>
          <div class="d-flex align-items-center justify-content-center gap-2">
            <span class="badge bg-primary-subtle text-primary border border-primary-subtle px-2 py-1 font-monospace">
              SCR-AUTH-001
            </span>
            <span class="text-muted small">Sign In</span>
          </div>
        </div>

        <!-- Login Card Form -->
        <div class="card shadow-sm border-0 rounded-3">
          <div class="card-body p-4 p-sm-5">
            <h2 class="h5 fw-bold mb-3 text-secondary text-center">User Authentication</h2>

            <!-- Distinct Error Alert -->
            <div
              v-if="errorMessage"
              class="alert d-flex align-items-start gap-2 mb-4"
              :class="errorCode === 'ACCOUNT_LOCKED' ? 'alert-danger border-danger' : 'alert-warning border-warning'"
              role="alert"
            >
              <i
                class="bi fs-5 flex-shrink-0"
                :class="errorCode === 'ACCOUNT_LOCKED' ? 'bi-shield-slash-fill text-danger' : 'bi-exclamation-triangle-fill text-warning'"
              ></i>
              <div class="small">
                <strong v-if="errorCode === 'ACCOUNT_LOCKED'" class="d-block mb-1">Account Locked</strong>
                <strong v-else-if="errorCode === 'INVALID_CREDENTIALS'" class="d-block mb-1">Authentication Failed</strong>
                <span>{{ errorMessage }}</span>
              </div>
            </div>

            <form @submit.prevent="handleLogin" novalidate>
              <!-- Username or Email Field -->
              <div class="mb-3">
                <label for="usernameOrEmail" class="form-label small fw-semibold text-secondary">
                  Username or Email
                </label>
                <div class="input-group">
                  <span class="input-group-text bg-light text-muted">
                    <i class="bi bi-person-fill"></i>
                  </span>
                  <input
                    id="usernameOrEmail"
                    v-model="username"
                    type="text"
                    class="form-control"
                    placeholder="e.g. admin or username"
                    autocomplete="username"
                    autofocus
                    required
                    :disabled="isLoading"
                  />
                </div>
              </div>

              <!-- Password Field with Visibility Toggle -->
              <div class="mb-4">
                <div class="d-flex justify-content-between align-items-center mb-1">
                  <label for="password" class="form-label small fw-semibold text-secondary mb-0">
                    Password
                  </label>
                </div>
                <div class="input-group">
                  <span class="input-group-text bg-light text-muted">
                    <i class="bi bi-key-fill"></i>
                  </span>
                  <input
                    id="password"
                    v-model="password"
                    :type="showPassword ? 'text' : 'password'"
                    class="form-control"
                    placeholder="Enter your password"
                    autocomplete="current-password"
                    required
                    :disabled="isLoading"
                  />
                  <button
                    class="btn btn-outline-secondary"
                    type="button"
                    @click="togglePasswordVisibility"
                    :title="showPassword ? 'Hide password' : 'Show password'"
                    tabindex="-1"
                  >
                    <i class="bi" :class="showPassword ? 'bi-eye-slash-fill' : 'bi-eye-fill'"></i>
                  </button>
                </div>
              </div>

              <!-- Submit Button -->
              <div class="d-grid mb-3">
                <button
                  type="submit"
                  class="btn btn-primary btn-lg fw-semibold py-2 d-flex align-items-center justify-content-center gap-2"
                  :disabled="isLoading || !username || !password"
                >
                  <span v-if="isLoading" class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
                  <i v-else class="bi bi-box-arrow-in-right"></i>
                  <span>{{ isLoading ? 'Signing In...' : 'Sign In' }}</span>
                </button>
              </div>
            </form>

            <!-- Security Footnote -->
            <div class="text-center pt-3 border-top mt-3">
              <span class="text-muted small d-inline-flex align-items-center gap-1">
                <i class="bi bi-shield-check text-success"></i>
                <span>Secure HttpOnly Session Cookie (Architecture §19.5)</span>
              </span>
            </div>
          </div>
        </div>

        <!-- Help Info -->
        <div class="text-center mt-3">
          <small class="text-muted">
            Internal Operations Portal &bull; ICS Operational System
          </small>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.form-control:focus {
  border-color: #0d6efd;
  box-shadow: 0 0 0 0.25rem rgba(13, 110, 253, 0.15);
}
</style>
