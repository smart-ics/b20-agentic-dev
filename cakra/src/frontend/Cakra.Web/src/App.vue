<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { useAuthStore } from '@/stores/auth'
import { useThemeStore } from '@/stores/theme'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()
const themeStore = useThemeStore()

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

// Role-based visibility for administrative sections (Architecture §14; CR-007)
const isAdmin = computed(
  () => authStore.roles.includes('Administrator') || authStore.roles.includes('Admin'),
)

// Derive clean contextual titles for the topbar
const currentScreenTitle = computed(() => {
  const path = route.path
  if (path.startsWith('/admin/customers')) return 'Customer Management'
  if (path.startsWith('/admin/persons')) return 'Person Management'
  if (path.startsWith('/admin/users')) return 'User Management'
  if (path.startsWith('/feed')) return 'Operational Feed'
  if (path.startsWith('/requests/create')) return 'Create Operational Request'
  if (path.startsWith('/requests/my')) return 'My Assigned Requests'
  if (path.startsWith('/requests/search')) return 'Request Search'
  if (path.startsWith('/requests/')) return 'Request Details'
  if (path.startsWith('/operations/wip')) return 'Work in Progress'
  if (path.startsWith('/operations/cockpit')) return 'Operations Cockpit'
  if (path.startsWith('/work-packages')) return 'Work Packages'
  if (path.startsWith('/products')) return 'Product Catalog'
  if (path.startsWith('/analytics/customer-portfolio')) return 'Customer Portfolio'
  if (path.startsWith('/analytics/programmer-performance')) return 'Programmer Performance'
  if (path.startsWith('/analytics/programmer-workload')) return 'Programmer Workload'
  return 'Operational Overview'
})

// Dynamic container width: 'narrow' (1024px) for focused operational feeds, 'wide' (1440px) default
const containerClass = computed(() => {
  const width = route.meta.containerWidth
  if (width === 'narrow') return 'cakra-shell-narrow'
  return 'cakra-shell-wide'
})
</script>

<template>
  <!-- Authenticated Shell: Left Side Menu Navigation + Topbar + Content Area -->
  <div v-if="authStore.isAuthenticated" class="cakra-layout bg-slate-950 text-slate-100 min-h-screen">
    <!-- Backdrop for mobile drawer -->
    <div
      v-if="isMobileOpen"
      class="cakra-sidebar-backdrop"
      @click="closeMobile"
      aria-hidden="true"
    ></div>

    <!-- Left Side Menu (Sidebar) -->
    <aside
      class="cakra-sidebar border-r border-slate-800"
      :class="{
        'is-collapsed': isCollapsed,
        'is-mobile-open': isMobileOpen,
      }"
      style="background: linear-gradient(180deg, #020617 0%, #0f172a 50%, #1e1b4b 100%);"
      aria-label="Main Navigation"
    >
      <!-- Sidebar Brand Header -->
      <div class="sidebar-brand border-b border-slate-800 bg-slate-950/80 backdrop-blur">
        <div class="brand-icon-wrapper shadow-lg shadow-cyan-500/20">
          <i class="bi bi-diagram-3-fill" aria-hidden="true"></i>
        </div>
        <div v-show="!isCollapsed" class="brand-text-wrapper">
          <span class="brand-title text-white tracking-wider font-extrabold">CAKRA</span>
          <span class="brand-subtitle text-cyan-400 font-mono tracking-widest text-[10px]">ICS Operational</span>
        </div>
        <!-- Mobile close button -->
        <button
          type="button"
          class="btn-close-mobile d-lg-none ms-auto text-slate-400 hover:text-white"
          aria-label="Close navigation"
          @click="closeMobile"
        >
          <i class="bi bi-x-lg" aria-hidden="true"></i>
        </button>
      </div>

      <!-- Navigation Links -->
      <nav class="sidebar-nav">
        <!-- Section: Operations -->
        <div class="nav-section-title text-cyan-400/80 text-[10px] font-semibold tracking-wider uppercase" v-show="!isCollapsed">Operations</div>

        <router-link
          class="sidebar-nav-item"
          to="/feed"
          active-class="active bg-cyan-500/10 text-cyan-300 border-l-2 border-cyan-400"
          :title="isCollapsed ? 'Operational Feed' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-activity nav-icon text-cyan-400" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Operational Feed</span>
        </router-link>

        <router-link
          class="sidebar-nav-item"
          to="/requests/my"
          active-class="active bg-cyan-500/10 text-cyan-300 border-l-2 border-cyan-400"
          data-testid="nav-my-requests-link"
          :title="isCollapsed ? 'My Requests' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-person-workspace nav-icon text-cyan-400" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">My Requests</span>
        </router-link>

        <router-link
          class="sidebar-nav-item"
          to="/operations/wip"
          active-class="active bg-cyan-500/10 text-cyan-300 border-l-2 border-cyan-400"
          data-testid="nav-wip-link"
          :title="isCollapsed ? 'Work in Progress' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-hourglass-split nav-icon text-cyan-400" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Work in Progress</span>
        </router-link>

        <router-link
          class="sidebar-nav-item"
          to="/operations/cockpit"
          active-class="active bg-cyan-500/10 text-cyan-300 border-l-2 border-cyan-400"
          data-testid="nav-operations-cockpit-link"
          :title="isCollapsed ? 'Operations Cockpit' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-speedometer2 nav-icon text-cyan-400" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Operations Cockpit</span>
        </router-link>

        <router-link
          class="sidebar-nav-item"
          to="/work-packages"
          active-class="active bg-cyan-500/10 text-cyan-300 border-l-2 border-cyan-400"
          data-testid="nav-work-packages-link"
          :title="isCollapsed ? 'Work Packages' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-kanban nav-icon text-cyan-400" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Work Packages</span>
        </router-link>

        <!-- Section: Catalog -->
        <div class="nav-section-title text-cyan-400/80 text-[10px] font-semibold tracking-wider uppercase" v-show="!isCollapsed">Catalog</div>

        <router-link
          class="sidebar-nav-item"
          to="/products"
          active-class="active bg-cyan-500/10 text-cyan-300 border-l-2 border-cyan-400"
          data-testid="nav-products-link"
          :title="isCollapsed ? 'Product Catalog' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-box-seam nav-icon text-cyan-400" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Product Catalog</span>
        </router-link>

        <!-- Section: Management & Analytics -->
        <div class="nav-section-title text-cyan-400/80 text-[10px] font-semibold tracking-wider uppercase" v-show="!isCollapsed">Management</div>

        <router-link
          class="sidebar-nav-item"
          to="/analytics/customer-portfolio"
          active-class="active bg-cyan-500/10 text-cyan-300 border-l-2 border-cyan-400"
          data-testid="nav-customer-portfolio-link"
          :title="isCollapsed ? 'Customer Portfolio' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-building nav-icon text-cyan-400" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Customer Portfolio</span>
        </router-link>

        <router-link
          class="sidebar-nav-item"
          to="/analytics/programmer-performance"
          active-class="active bg-cyan-500/10 text-cyan-300 border-l-2 border-cyan-400"
          data-testid="nav-programmer-performance-link"
          :title="isCollapsed ? 'Programmer Performance' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-graph-up nav-icon text-cyan-400" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Programmer Performance</span>
        </router-link>

        <router-link
          class="sidebar-nav-item"
          to="/analytics/programmer-workload"
          active-class="active bg-cyan-500/10 text-cyan-300 border-l-2 border-cyan-400"
          data-testid="nav-programmer-workload-link"
          :title="isCollapsed ? 'Programmer Workload' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-people nav-icon text-cyan-400" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Programmer Workload</span>
        </router-link>

        <!-- Section: Administration -->
        <div class="nav-section-title text-cyan-400/80 text-[10px] font-semibold tracking-wider uppercase" v-if="isAdmin && !isCollapsed">Administration</div>

        <router-link
          v-if="isAdmin"
          class="sidebar-nav-item"
          to="/admin/users"
          active-class="active bg-cyan-500/10 text-cyan-300 border-l-2 border-cyan-400"
          data-testid="nav-user-management-link"
          :title="isCollapsed ? 'User Management' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-person-gear nav-icon text-cyan-400" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">User Management</span>
        </router-link>

        <router-link
          v-if="isAdmin"
          class="sidebar-nav-item"
          to="/admin/persons"
          active-class="active bg-cyan-500/10 text-cyan-300 border-l-2 border-cyan-400"
          data-testid="nav-person-management-link"
          :title="isCollapsed ? 'Person Management' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-person-lock nav-icon text-cyan-400" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Person Management</span>
        </router-link>

        <router-link
          v-if="isAdmin"
          class="sidebar-nav-item"
          to="/admin/customers"
          active-class="active bg-cyan-500/10 text-cyan-300 border-l-2 border-cyan-400"
          data-testid="nav-customer-management-link"
          :title="isCollapsed ? 'Customer Management' : undefined"
          @click="closeMobile"
        >
          <i class="bi bi-building-gear nav-icon text-cyan-400" aria-hidden="true"></i>
          <span v-show="!isCollapsed" class="nav-label">Customer Management</span>
        </router-link>
      </nav>

      <!-- Sidebar Footer (User info & collapse toggle) -->
      <div class="sidebar-footer border-t border-slate-800 bg-slate-950/80 backdrop-blur">
        <div v-show="!isCollapsed" class="sidebar-user-info">
          <div class="user-avatar bg-gradient-to-br from-cyan-500 to-indigo-600 border border-slate-700">
            <i class="bi bi-person-fill" aria-hidden="true"></i>
          </div>
          <div class="user-meta">
            <span class="user-role-badge bg-cyan-500/10 text-cyan-300 border border-cyan-500/20" v-if="authStore.roles.length > 0">
              {{ authStore.roles.join(', ') }}
            </span>
            <span class="user-role-badge bg-slate-800 text-slate-300 border border-slate-700" v-else>Operational User</span>
          </div>
        </div>

        <div class="sidebar-actions" :class="{ 'flex-column': isCollapsed }">
          <button
            type="button"
            class="btn-sidebar-collapse d-none d-lg-flex border border-slate-800 bg-slate-900/60 text-slate-400 hover:text-white hover:bg-slate-800"
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
            class="btn-sidebar-logout border border-slate-800 bg-slate-900/60 text-slate-400 hover:text-rose-400 hover:border-rose-500/30 hover:bg-rose-500/10"
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
    <div class="cakra-main-container bg-slate-950 text-slate-100 min-h-screen" :class="{ 'sidebar-collapsed': isCollapsed }">
      <!-- Top Bar -->
      <header class="cakra-topbar bg-slate-900/90 backdrop-blur border-b border-slate-800 text-slate-100">
        <div class="d-flex align-items-center gap-2">
          <!-- Mobile toggle button -->
          <button
            type="button"
            class="btn btn-outline-secondary btn-sm d-lg-none border-slate-700 text-slate-300 hover:bg-slate-800"
            aria-label="Toggle navigation menu"
            @click="toggleMobile"
          >
            <i class="bi bi-list" aria-hidden="true"></i>
          </button>

          <!-- Breadcrumb / Active Screen Title -->
          <div class="topbar-title-group">
            <span class="topbar-eyebrow text-cyan-400 font-bold tracking-wider text-xs">CAKRA</span>
            <span class="text-slate-500 small">&rsaquo;</span>
            <h1 class="topbar-screen-title mb-0 text-white font-bold text-sm tracking-tight">{{ currentScreenTitle }}</h1>
          </div>
        </div>

        <!-- Topbar Right Utilities -->
        <div class="d-flex align-items-center gap-2">
          <!-- Live Telemetry Status -->
          <span class="badge bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 d-none d-md-inline-flex align-items-center gap-1 px-2 py-0.5" style="font-size: 11px;">
            <span class="spinner-grow spinner-grow-sm text-emerald-400" style="width: 0.35rem; height: 0.35rem;"></span>
            Live
          </span>

          <span v-if="authStore.roles.length > 0" class="badge cakra-role-badge d-none d-sm-inline-flex bg-slate-800 text-cyan-300 border border-slate-700">
            <i class="bi bi-shield-check me-1" aria-hidden="true"></i>
            {{ authStore.roles.join(', ') }}
          </span>

          <!-- Theme Toggle Button -->
          <button
            type="button"
            class="p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition border-0 bg-transparent flex items-center justify-center cursor-pointer"
            :title="themeStore.isDark ? 'Switch to Light Mode' : 'Switch to Dark Mode'"
            aria-label="Toggle theme"
            @click="themeStore.toggleTheme()"
          >
            <i v-if="themeStore.isDark" class="bi bi-sun-fill text-amber-400" aria-hidden="true"></i>
            <i v-else class="bi bi-moon-stars-fill text-indigo-400" aria-hidden="true"></i>
          </button>

          <button
            type="button"
            class="btn btn-outline-secondary btn-sm d-lg-none border-slate-700 text-slate-300 hover:bg-slate-800"
            @click="handleLogout"
            title="Sign Out"
          >
            <i class="bi bi-box-arrow-right" aria-hidden="true"></i>
          </button>
        </div>
      </header>

      <!-- Page Content View -->
      <main class="cakra-page-content bg-slate-950 text-slate-100 flex-1">
        <div class="cakra-shell-container py-2" :class="containerClass">
          <router-view />
        </div>
      </main>

      <!-- App Footer -->
      <footer class="cakra-app-footer bg-slate-950/80 backdrop-blur border-t border-slate-800 text-slate-400 py-2">
        <div class="container-fluid px-2 px-md-3 d-flex justify-content-between align-items-center">
          <span class="text-xs text-slate-400">CAKRA &bull; ICS Operational Workspace</span>
          <span class="text-xs text-slate-500">Enterprise Operational Control</span>
        </div>
      </footer>
    </div>
  </div>

  <!-- Unauthenticated Shell (e.g. Login Screen) -->
  <div v-else class="cakra-unauth-layout bg-slate-950 text-slate-100 min-h-screen flex flex-col">
    <header class="cakra-unauth-header bg-slate-900/80 backdrop-blur border-b border-slate-800">
      <div class="container d-flex align-items-center justify-content-center py-3">
        <div class="d-flex align-items-center gap-2">
          <div class="brand-icon-wrapper-unauth bg-gradient-to-br from-cyan-500 to-indigo-600 shadow-lg shadow-cyan-500/20 border border-slate-700">
            <i class="bi bi-diagram-3-fill text-white fs-4" aria-hidden="true"></i>
          </div>
          <div>
            <div class="fw-bold text-white fs-5 lh-1 tracking-tight">CAKRA</div>
            <div class="text-cyan-400 fs-12 lh-1 font-mono tracking-wider">ICS Operational System</div>
          </div>
        </div>
      </div>
    </header>

    <main class="cakra-unauth-content flex-grow-1">
      <router-view />
    </main>

    <footer class="cakra-unauth-footer py-3 text-center text-slate-500 text-xs border-t border-slate-800">
      CAKRA &bull; ICS Operational System
    </footer>
  </div>
</template>
