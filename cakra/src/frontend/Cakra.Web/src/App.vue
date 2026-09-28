<script setup lang="ts">
import { useRouter } from 'vue-router'

import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

async function handleLogout(): Promise<void> {
  await authStore.logout()
  await router.push('/login')
}
</script>

<template>
  <div class="d-flex flex-column min-vh-100">
    <header class="cakra-header border-bottom bg-body-tertiary">
      <nav class="navbar navbar-expand-lg container-fluid px-4">
        <span class="navbar-brand mb-0 h1 d-flex align-items-center gap-2">
          <i class="bi bi-diagram-3-fill text-primary" aria-hidden="true"></i>
          <span>CAKRA - ICS Operational System</span>
        </span>

        <ul v-if="authStore.isAuthenticated" class="navbar-nav me-auto ms-4 mb-2 mb-lg-0">
          <li class="nav-item">
            <router-link class="nav-link" to="/feed" active-class="active fw-semibold">
              <i class="bi bi-activity me-1" aria-hidden="true"></i>
              Operational Feed
            </router-link>
          </li>
          <li class="nav-item">
            <router-link
              class="nav-link"
              to="/requests"
              active-class="active fw-semibold"
              data-testid="nav-requests-link"
            >
              <i class="bi bi-card-checklist me-1" aria-hidden="true"></i>
              Requests
            </router-link>
          </li>
          <li class="nav-item">
            <router-link
              class="nav-link"
              to="/requests/my"
              active-class="active fw-semibold"
              data-testid="nav-my-requests-link"
            >
              <i class="bi bi-person-workspace me-1" aria-hidden="true"></i>
              My Requests
            </router-link>
          </li>
          <li class="nav-item">
            <router-link
              class="nav-link"
              to="/requests/search"
              active-class="active fw-semibold"
              data-testid="nav-request-search-link"
            >
              <i class="bi bi-search me-1" aria-hidden="true"></i>
              Request Search
            </router-link>
          </li>
          <li class="nav-item">
            <router-link
              class="nav-link"
              to="/work-packages"
              active-class="active fw-semibold"
              data-testid="nav-work-packages-link"
            >
              <i class="bi bi-kanban me-1" aria-hidden="true"></i>
              Work Packages
            </router-link>
          </li>
          <li class="nav-item">
            <router-link
              class="nav-link"
              to="/products"
              active-class="active fw-semibold"
              data-testid="nav-products-link"
            >
              <i class="bi bi-box-seam me-1" aria-hidden="true"></i>
              Product Catalog
            </router-link>
          </li>
          <li class="nav-item">
            <router-link
              class="nav-link"
              to="/analytics/customer-portfolio"
              active-class="active fw-semibold"
              data-testid="nav-customer-portfolio-link"
            >
              <i class="bi bi-building me-1" aria-hidden="true"></i>
              Customer Portfolio
            </router-link>
          </li>
          <li class="nav-item">
            <router-link
              class="nav-link"
              to="/analytics/programmer-performance"
              active-class="active fw-semibold"
              data-testid="nav-programmer-performance-link"
            >
              <i class="bi bi-graph-up me-1" aria-hidden="true"></i>
              Programmer Performance
            </router-link>
          </li>
          <li class="nav-item">
            <router-link
              class="nav-link"
              to="/analytics/programmer-workload"
              active-class="active fw-semibold"
              data-testid="nav-programmer-workload-link"
            >
              <i class="bi bi-people me-1" aria-hidden="true"></i>
              Programmer Workload
            </router-link>
          </li>
        </ul>

        <div v-if="authStore.isAuthenticated" class="ms-auto d-flex align-items-center gap-3">
          <span v-if="authStore.roles.length > 0" class="badge text-bg-secondary">
            {{ authStore.roles.join(', ') }}
          </span>
          <button
            type="button"
            class="btn btn-outline-secondary btn-sm"
            data-testid="logout-button"
            @click="handleLogout"
          >
            <i class="bi bi-box-arrow-right me-1" aria-hidden="true"></i>
            Sign Out
          </button>
        </div>
      </nav>
    </header>

    <main class="flex-grow-1 py-4">
      <div class="container-fluid px-4">
        <router-view />
      </div>
    </main>

    <footer class="cakra-footer border-top py-3 text-body-secondary">
      <div class="container-fluid px-4 small">
        CAKRA - ICS Operational System
      </div>
    </footer>
  </div>
</template>
