<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

// Responsive and collapsible sidebar state
const isCollapsed = ref(false)
const isMobileOpen = ref(false)

function toggleSidebar(): void {
  isCollapsed.value = !isCollapsed.value
}

function toggleMobile(): void {
  isMobileOpen.value = !isMobileOpen.value
}

function closeMobile(): void {
  isMobileOpen.value = false
}

// Auto-close mobile drawer when route changes
watch(
  () => route.fullPath,
  () => {
    isMobileOpen.value = false
  },
)

async function handleLogout(): Promise<void> {
  await authStore.logout()
  await router.push('/login')
}

// Derive clean contextual titles for the topbar
const currentScreenTitle = computed(() => {
  const path = route.path
  if (path.startsWith('/feed')) return 'Operational Feed'
  if (path.startsWith('/requests/create')) return 'Create Operational Request'
  if (path.startsWith('/requests/my')) return 'My Assigned Requests'
  if (path.startsWith('/requests/search')) return 'Request Search'
  if (path.startsWith('/requests/')) return 'Request Details'
  if (path.startsWith('/requests')) return 'Operational Requests'
  if (path.startsWith('/work-packages')) return 'Work Packages'
  if (path.startsWith('/products')) return 'Product Catalog'
  if (path.startsWith('/analytics/customer-portfolio')) return 'Customer Portfolio'
  if (path.startsWith('/analytics/programmer-performance')) return 'Programmer Performance'
  if (path.startsWith('/analytics/programmer-workload')) return 'Programmer Workload'
  return 'Operational Overview'
})
</script>

<template>
  <!-- Authenticated Shell: Left Side Menu Navigation + Topbar + Content Area -->
  <div v-if="authStore.isAuthenticated" class="cakra-layout">
    <!-- Backdrop for mobile drawer -->
    <div
      v-if="isMobileOpen"
      class="cakra-sidebar-backdrop"
      @click="closeMobile"
      aria-hidden="true"
    ></div>

    <!-- Left Side Menu (Sidebar) -->
    <aside
      class="cakra-sidebar"
      :class="{
        'is-collapsed': isCollapsed,
        'is-mobile-open': isMobileOpen,
      }"
      aria-label="Main Navigation"
    >
      <!-- Sidebar Brand Header -->
      <div class="sidebar-brand">
        <div class="brand-icon-wrapper">
          <i class="bi bi-diagram-3-fill" aria-hidden="true"></i>
        </div>
        <div v-show="!isCollapsed" class="brand-text-wrapper">
          <span class="brand-title">CAKRA</span>
          <span class="brand-subtitle">ICS Operational</span>
        </div>
        <!-- Mobile close button -->
        <button
          type="button"
          class="btn-close-mobile d-lg-none ms-auto"
          aria-label="Close navigation"
          @click="closeMobile"
        >
          <i class="bi bi-x-lg" aria-hidden="true"></i>
        </button>
      </div>

      <!-- Navigation Links -->
      <nav class="sidebar-nav">
        <!-- Section: Operations -->
        <div class="nav-section-title" v-show="!isCollapsed">Operations</div>

        <router-link
          class="sidebar-nav-item"
          to="/feed"
          active-class="active"
          :title="isCollapsed ? 'Operational Feed' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-activity nav-icon" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Operational Feed</span>
        </router-link>

        <router-link
          class="sidebar-nav-item"
          to="/requests"
          active-class="active"
          data-testid="nav-requests-link"
          :title="isCollapsed ? 'Requests' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-card-checklist nav-icon" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Requests</span>
        </router-link>

        <router-link
          class="sidebar-nav-item"
          to="/requests/my"
          active-class="active"
          data-testid="nav-my-requests-link"
          :title="isCollapsed ? 'My Requests' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-person-workspace nav-icon" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">My Requests</span>
        </router-link>

        <router-link
          class="sidebar-nav-item"
          to="/requests/search"
          active-class="active"
          data-testid="nav-request-search-link"
          :title="isCollapsed ? 'Request Search' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-search nav-icon" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Request Search</span>
        </router-link>

        <router-link
          class="sidebar-nav-item"
          to="/work-packages"
          active-class="active"
          data-testid="nav-work-packages-link"
          :title="isCollapsed ? 'Work Packages' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-kanban nav-icon" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Work Packages</span>
        </router-link>

        <!-- Section: Catalog -->
        <div class="nav-section-title" v-show="!isCollapsed">Catalog</div>

        <router-link
          class="sidebar-nav-item"
          to="/products"
          active-class="active"
          data-testid="nav-products-link"
          :title="isCollapsed ? 'Product Catalog' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-box-seam nav-icon" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Product Catalog</span>
        </router-link>

        <!-- Section: Management & Analytics -->
        <div class="nav-section-title" v-show="!isCollapsed">Management</div>

        <router-link
          class="sidebar-nav-item"
          to="/analytics/customer-portfolio"
          active-class="active"
          data-testid="nav-customer-portfolio-link"
          :title="isCollapsed ? 'Customer Portfolio' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-building nav-icon" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Customer Portfolio</span>
        </router-link>

        <router-link
          class="sidebar-nav-item"
          to="/analytics/programmer-performance"
          active-class="active"
          data-testid="nav-programmer-performance-link"
          :title="isCollapsed ? 'Programmer Performance' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-graph-up nav-icon" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Programmer Performance</span>
        </router-link>

        <router-link
          class="sidebar-nav-item"
          to="/analytics/programmer-workload"
          active-class="active"
          data-testid="nav-programmer-workload-link"
          :title="isCollapsed ? 'Programmer Workload' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-people nav-icon" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Programmer Workload</span>
        </router-link>
      </nav>

      <!-- Sidebar Footer (User info & collapse toggle) -->
      <div class="sidebar-footer">
        <div v-show="!isCollapsed" class="sidebar-user-info">
          <div class="user-avatar">
            <i class="bi bi-person-fill" aria-hidden="true"></i>
          </div>
          <div class="user-meta">
            <span class="user-role-badge" v-if="authStore.roles.length > 0">
              {{ authStore.roles.join(', ') }}
            </span>
            <span class="user-role-badge" v-else>Operational User</span>
          </div>
        </div>

        <div class="sidebar-actions" :class="{ 'flex-column': isCollapsed }">
          <button
            type="button"
            class="btn-sidebar-collapse d-none d-lg-flex"
            :title="isCollapsed ? 'Expand Sidebar' : 'Collapse Sidebar'"
            @click="toggleSidebar"
          >
            <i
              class="bi"
              :class="isCollapsed ? 'bi-chevron-double-right' : 'bi-chevron-double-left'"
              aria-hidden="true"
            ></i>
          </button>

          <button
            type="button"
            class="btn-sidebar-logout"
            data-testid="logout-button"
            :title="isCollapsed ? 'Sign Out' : undefined"
            @click="handleLogout"
          >
            <i class="bi bi-box-arrow-right me-lg-1" aria-hidden="true"></i>
            <span v-show="!isCollapsed">Sign Out</span>
          </button>
        </div>
      </div>
    </aside>

    <!-- Main Workspace Container -->
    <div class="cakra-main-container" :class="{ 'sidebar-collapsed': isCollapsed }">
      <!-- Top Bar -->
      <header class="cakra-topbar">
        <div class="d-flex align-items-center gap-2">
          <!-- Mobile toggle button -->
          <button
            type="button"
            class="btn btn-outline-secondary btn-sm d-lg-none"
            aria-label="Toggle navigation menu"
            @click="toggleMobile"
          >
            <i class="bi bi-list" aria-hidden="true"></i>
          </button>

          <!-- Breadcrumb / Active Screen Title -->
          <div class="topbar-title-group">
            <span class="topbar-eyebrow">CAKRA</span>
            <span class="text-muted small">&rsaquo;</span>
            <h1 class="topbar-screen-title mb-0">{{ currentScreenTitle }}</h1>
          </div>
        </div>

        <!-- Topbar Right Utilities -->
        <div class="d-flex align-items-center gap-2">
          <!-- Live Telemetry Status -->
          <span class="badge bg-success bg-opacity-10 text-success border border-success border-opacity-25 d-none d-md-inline-flex align-items-center gap-1 px-2 py-0.5" style="font-size: 11px;">
            <span class="spinner-grow spinner-grow-sm text-success" style="width: 0.35rem; height: 0.35rem;"></span>
            Live
          </span>

          <span v-if="authStore.roles.length > 0" class="badge cakra-role-badge d-none d-sm-inline-flex">
            <i class="bi bi-shield-check me-1" aria-hidden="true"></i>
            {{ authStore.roles.join(', ') }}
          </span>

          <button
            type="button"
            class="btn btn-outline-secondary btn-sm d-lg-none"
            @click="handleLogout"
            title="Sign Out"
          >
            <i class="bi bi-box-arrow-right" aria-hidden="true"></i>
          </button>
        </div>
      </header>

      <!-- Page Content View -->
      <main class="cakra-page-content">
        <div class="container-fluid px-2 px-md-3 py-2">
          <router-view />
        </div>
      </main>

      <!-- App Footer -->
      <footer class="cakra-app-footer">
        <div class="container-fluid px-2 px-md-3 d-flex justify-content-between align-items-center">
          <span>CAKRA &bull; ICS Operational Workspace</span>
          <span class="text-muted">Enterprise Operational Control</span>
        </div>
      </footer>
    </div>
  </div>

  <!-- Unauthenticated Shell (e.g. Login Screen) -->
  <div v-else class="cakra-unauth-layout">
    <header class="cakra-unauth-header">
      <div class="container d-flex align-items-center justify-content-center py-3">
        <div class="d-flex align-items-center gap-2">
          <div class="brand-icon-wrapper-unauth">
            <i class="bi bi-diagram-3-fill text-white fs-4" aria-hidden="true"></i>
          </div>
          <div>
            <div class="fw-bold text-white fs-5 lh-1">CAKRA</div>
            <div class="text-cakra-ice fs-12 lh-1">ICS Operational System</div>
          </div>
        </div>
      </div>
    </header>

    <main class="cakra-unauth-content flex-grow-1">
      <router-view />
    </main>

    <footer class="cakra-unauth-footer py-3 text-center text-muted small">
      CAKRA &bull; ICS Operational System
    </footer>
  </div>
</template>
