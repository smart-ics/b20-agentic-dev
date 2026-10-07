import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

import { useAuthStore } from '@/stores/auth'
import CreateRequestView from '@/views/CreateRequestView.vue'
import CustomerPortfolioView from '@/views/CustomerPortfolioView.vue'
import FeedView from '@/views/FeedView.vue'
import LoginView from '@/views/LoginView.vue'
import MyRequestsView from '@/views/MyRequestsView.vue'
import ProductCatalogView from '@/views/ProductCatalogView.vue'
import ProgrammerPerformanceView from '@/views/ProgrammerPerformanceView.vue'
import ProgrammerWorkloadView from '@/views/ProgrammerWorkloadView.vue'
import RequestDetailView from '@/views/RequestDetailView.vue'
import RequestSearchView from '@/views/RequestSearchView.vue'
import WorkPackageView from '@/views/WorkPackageView.vue'

declare module 'vue-router' {
  interface RouteMeta {
    requiresAuth?: boolean
    guestOnly?: boolean
    screenId?: string
    containerWidth?: 'narrow' | 'wide'
    requiresRole?: string
  }
}

// Vue Router 4 configuration (Architecture §9, §10, §11, §12, §13, §14, §19.4, §19.5; CR-017).
// Routes `/login` to SCR-AUTH-001 (`LoginView.vue`), `/feed` to SCR-FEED-001 (`FeedView.vue`),
// `/products` to SCR-PRD-001 (`ProductCatalogView.vue`), `/requests` redirects to `/feed` (CR-017),
// `/requests/create` to SCR-REQ-002 (`CreateRequestView.vue`),
// `/requests/my` to SCR-REQ-004 (`MyRequestsView.vue`), `/requests/search` to SCR-REQ-005
// (`RequestSearchView.vue`), `/requests/:id` to SCR-REQ-003 (`RequestDetailView.vue`),
// `/operations/wip` to SCR-REQ-006 (`WorkInProgressView.vue`; CR-021),
// `/work-packages` & `/work-packages/:id` to SCR-WP-001 (`WorkPackageView.vue`),
// `/analytics/customer-portfolio` to SCR-MGT-001 (`CustomerPortfolioView.vue`),
// `/analytics/programmer-performance` to SCR-MGT-002 (`ProgrammerPerformanceView.vue`),
// and `/analytics/programmer-workload` to SCR-MGT-003 (`ProgrammerWorkloadView.vue`).
const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: LoginView,
    meta: {
      requiresAuth: false,
      guestOnly: true,
      screenId: 'SCR-AUTH-001',
    },
  },
  {
    path: '/feed',
    name: 'feed',
    component: FeedView,
    meta: {
      requiresAuth: true,
      screenId: 'SCR-FEED-001',
    },
  },
  {
    path: '/requests',
    name: 'requests-redirect',
    redirect: '/feed',
  },
  {
    path: '/requests/create',
    name: 'create-request',
    component: CreateRequestView,
    meta: {
      requiresAuth: true,
      screenId: 'SCR-REQ-002',
    },
  },
  {
    path: '/requests/my',
    name: 'my-requests',
    component: MyRequestsView,
    meta: {
      requiresAuth: true,
      screenId: 'SCR-REQ-004',
    },
  },
  {
    path: '/requests/search',
    name: 'request-search',
    component: RequestSearchView,
    meta: {
      requiresAuth: true,
      screenId: 'SCR-REQ-005',
    },
  },
  {
    path: '/requests/:id',
    name: 'request-detail',
    component: RequestDetailView,
    meta: {
      requiresAuth: true,
      screenId: 'SCR-REQ-003',
    },
  },
  {
    path: '/operations/wip',
    name: 'work-in-progress',
    component: () => import('@/views/WorkInProgressView.vue'),
    meta: {
      requiresAuth: true,
      screenId: 'SCR-REQ-006',
      containerWidth: 'narrow',
    },
  },
  {
    path: '/work-packages',
    name: 'work-packages',
    component: WorkPackageView,
    meta: {
      requiresAuth: true,
      screenId: 'SCR-WP-001',
    },
  },
  {
    path: '/work-packages/:id',
    name: 'work-package-detail',
    component: WorkPackageView,
    meta: {
      requiresAuth: true,
      screenId: 'SCR-WP-001',
    },
  },
  {
    path: '/products',
    name: 'products',
    component: ProductCatalogView,
    meta: {
      requiresAuth: true,
      screenId: 'SCR-PRD-001',
    },
  },
  {
    path: '/analytics/customer-portfolio',
    name: 'analytics-customer-portfolio',
    component: CustomerPortfolioView,
    meta: {
      requiresAuth: true,
      screenId: 'SCR-MGT-001',
    },
  },
  {
    path: '/analytics/programmer-performance',
    name: 'analytics-programmer-performance',
    component: ProgrammerPerformanceView,
    meta: {
      requiresAuth: true,
      screenId: 'SCR-MGT-002',
    },
  },
  {
    path: '/analytics/programmer-workload',
    name: 'analytics-programmer-workload',
    component: ProgrammerWorkloadView,
    meta: {
      requiresAuth: true,
      screenId: 'SCR-MGT-003',
    },
  },
  {
    path: '/admin/users',
    name: 'user-management',
    component: () => import('@/views/UserManagementView.vue'),
    meta: {
      requiresAuth: true,
      requiresRole: 'Administrator',
      screenId: 'SCR-USR-001',
    },
  },
  {
    path: '/admin/persons',
    name: 'person-management',
    component: () => import('@/views/PersonManagementView.vue'),
    meta: {
      requiresAuth: true,
      requiresRole: 'Administrator',
      screenId: 'SCR-ORG-001',
    },
  },
  {
    path: '/admin/customers',
    name: 'customer-management',
    component: () => import('@/views/CustomerManagementView.vue'),
    meta: {
      requiresAuth: true,
      requiresRole: 'Administrator',
      screenId: 'SCR-CUST-001',
    },
  },
  {
    path: '/',
    name: 'home',
    redirect: '/feed',
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    redirect: '/feed',
  },
]

export const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

router.beforeEach(async (to) => {
  const authStore = useAuthStore()

  if (!authStore.isInitialized && !authStore.isAuthenticated) {
    await authStore.fetchCurrentUser()
  }

  const requiresAuth = to.meta.requiresAuth !== false
  if (requiresAuth && !authStore.isAuthenticated) {
    return { name: 'login' }
  }

  if (to.meta.guestOnly && authStore.isAuthenticated) {
    return { path: '/feed' }
  }

  if (to.meta.requiresRole) {
    const hasRequiredRole =
      authStore.roles.includes('Administrator') || authStore.roles.includes('Admin')
    if (!hasRequiredRole) {
      return { path: '/feed' }
    }
  }

  return true
})

export default router
