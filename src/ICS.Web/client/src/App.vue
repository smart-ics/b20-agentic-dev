<script setup lang="ts">
import { onMounted } from 'vue'
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useSystemStore } from '@/stores/system'
import { useAuthStore } from '@/stores/auth'

const systemStore = useSystemStore()
const authStore = useAuthStore()
const router = useRouter()

onMounted(async () => {
  await authStore.checkAuth()
})

async function handleLogout() {
  await authStore.logout()
  await router.push('/login')
}
</script>

<template>
  <div class="d-flex flex-column min-vh-100 bg-light-subtle">
    <!-- Main Top Navigation -->
    <header>
      <nav class="navbar navbar-expand-lg navbar-dark bg-dark shadow-sm">
        <div class="container">
          <RouterLink class="navbar-brand d-flex align-items-center fw-bold" to="/">
            <i class="bi bi-diagram-3-fill text-primary me-2 fs-4"></i>
            <span>{{ systemStore.appTitle }}</span>
          </RouterLink>

          <button
            class="navbar-toggler"
            type="button"
            data-bs-toggle="collapse"
            data-bs-target="#navbarMain"
            aria-controls="navbarMain"
            aria-expanded="false"
            aria-label="Toggle navigation"
          >
            <span class="navbar-toggler-icon"></span>
          </button>

          <div class="collapse navbar-collapse" id="navbarMain">
            <ul class="navbar-nav me-auto mb-2 mb-lg-0">
              <li class="nav-item">
                <RouterLink class="nav-link" active-class="active" to="/">
                  <i class="bi bi-speedometer2 me-1"></i> Dashboard
                </RouterLink>
              </li>
              <li class="nav-item dropdown">
                <a
                  class="nav-link dropdown-toggle"
                  href="#"
                  role="button"
                  data-bs-toggle="dropdown"
                  aria-expanded="false"
                >
                  <i class="bi bi-inboxes me-1"></i> Requests
                  <span class="badge bg-primary-subtle text-primary border border-primary-subtle ms-1 font-monospace" style="font-size: 0.65rem;">
                    SCR-REQ
                  </span>
                </a>
                <ul class="dropdown-menu shadow">
                  <li>
                    <RouterLink class="dropdown-item d-flex justify-content-between align-items-center py-2" to="/requests">
                      <span><i class="bi bi-list-ul me-2"></i>All Requests</span>
                      <span class="badge bg-light text-secondary border font-monospace" style="font-size: 0.65rem;">SCR-REQ-001</span>
                    </RouterLink>
                  </li>
                  <li>
                    <RouterLink class="dropdown-item d-flex justify-content-between align-items-center py-2" to="/requests/new">
                      <span><i class="bi bi-plus-circle me-2"></i>Record Request</span>
                      <span class="badge bg-light text-secondary border font-monospace" style="font-size: 0.65rem;">SCR-REQ-002</span>
                    </RouterLink>
                  </li>
                  <li><hr class="dropdown-divider" /></li>
                  <li>
                    <RouterLink class="dropdown-item d-flex justify-content-between align-items-center py-2" to="/requests/my-assigned">
                      <span><i class="bi bi-person-check me-2"></i>My Assigned Queue</span>
                      <span class="badge bg-light text-secondary border font-monospace" style="font-size: 0.65rem;">SCR-REQ-004</span>
                    </RouterLink>
                  </li>
                  <li>
                    <RouterLink class="dropdown-item d-flex justify-content-between align-items-center py-2" to="/requests/search">
                      <span><i class="bi bi-clock-history me-2"></i>Search & Audit Trail</span>
                      <span class="badge bg-light text-secondary border font-monospace" style="font-size: 0.65rem;">SCR-REQ-005</span>
                    </RouterLink>
                  </li>
                </ul>
              </li>
              <li class="nav-item">
                <RouterLink class="nav-link" active-class="active" to="/products">
                  <i class="bi bi-box-seam me-1"></i> Products
                  <span class="badge bg-secondary-subtle text-light-emphasis border ms-1 font-monospace" style="font-size: 0.65rem;">
                    SCR-PRD-001
                  </span>
                </RouterLink>
              </li>
              <li class="nav-item dropdown">
                <a
                  class="nav-link dropdown-toggle"
                  href="#"
                  role="button"
                  data-bs-toggle="dropdown"
                  aria-expanded="false"
                >
                  <i class="bi bi-archive me-1"></i> Work Packages
                  <span class="badge bg-primary-subtle text-primary border border-primary-subtle ms-1 font-monospace" style="font-size: 0.65rem;">
                    SCR-WP
                  </span>
                </a>
                <ul class="dropdown-menu shadow">
                  <li>
                    <RouterLink class="dropdown-item d-flex justify-content-between align-items-center py-2" to="/work-packages">
                      <span><i class="bi bi-list-ul me-2"></i>Work Package Management</span>
                      <span class="badge bg-light text-secondary border font-monospace" style="font-size: 0.65rem;">SCR-WP-001</span>
                    </RouterLink>
                  </li>
                </ul>
              </li>
              <li class="nav-item">
                <RouterLink class="nav-link" active-class="active" to="/feed">
                  <i class="bi bi-activity me-1"></i> Operational Feed
                  <span class="badge bg-primary-subtle text-primary border border-primary-subtle ms-1 font-monospace" style="font-size: 0.65rem;">
                    SCR-FEED-001
                  </span>
                </RouterLink>
              </li>
            </ul>

            <div class="d-flex align-items-center gap-3">
              <span class="badge bg-success-subtle text-success border border-success-subtle px-2 py-1 d-none d-sm-inline-block">
                <i class="bi bi-circle-fill me-1" style="font-size: 0.5rem; vertical-align: middle;"></i> Host Online
              </span>

              <!-- Authenticated User Menu -->
              <div v-if="authStore.isAuthenticated" class="d-flex align-items-center gap-2">
                <div class="text-light small text-end d-none d-md-block">
                  <div class="fw-semibold">{{ authStore.user?.username }}</div>
                  <div class="text-white-50" style="font-size: 0.75rem;">
                    <span v-if="authStore.userRoles.length > 0">{{ authStore.userRoles.join(', ') }}</span>
                    <span v-else>Active</span>
                  </div>
                </div>

                <button
                  type="button"
                  class="btn btn-outline-light btn-sm d-flex align-items-center gap-1"
                  @click="handleLogout"
                  title="Sign Out"
                >
                  <i class="bi bi-box-arrow-right"></i>
                  <span class="d-none d-sm-inline">Sign Out</span>
                </button>
              </div>

              <!-- Unauthenticated State -->
              <div v-else class="d-flex align-items-center">
                <RouterLink to="/login" class="btn btn-primary btn-sm d-flex align-items-center gap-1">
                  <i class="bi bi-box-arrow-in-right"></i>
                  <span>Sign In</span>
                </RouterLink>
              </div>
            </div>
          </div>
        </div>
      </nav>
    </header>

    <!-- Main Content Area -->
    <main class="flex-grow-1">
      <RouterView />
    </main>

    <!-- Footer -->
    <footer class="footer mt-auto py-3 bg-white border-top text-muted">
      <div class="container d-flex flex-wrap justify-content-between align-items-center">
        <div class="col-md-6 d-flex align-items-center">
          <span class="text-body-secondary small">
            &copy; 2026 ICS Operational System &bull; Phase 2 Identity &amp; Access (SCR-AUTH-001)
          </span>
        </div>
        <ul class="nav col-md-6 justify-content-end list-unstyled d-flex small gap-3">
          <li><span class="text-body-secondary"><i class="bi bi-shield-lock me-1"></i>Cookie Auth (§19.5)</span></li>
          <li><span class="text-body-secondary"><i class="bi bi-layers me-1"></i>Bootstrap 5.3</span></li>
          <li><span class="text-body-secondary"><i class="bi bi-code-slash me-1"></i>Vite + Vue 3</span></li>
        </ul>
      </div>
    </footer>
  </div>
</template>

<style scoped>
</style>
