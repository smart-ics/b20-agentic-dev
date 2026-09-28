import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import HomeView from '@/views/HomeView.vue'
import LoginView from '@/views/LoginView.vue'
import FeedView from '@/views/FeedView.vue'
import WorkPackageView from '@/views/WorkPackageView.vue'
import { useAuthStore } from '@/stores/auth'

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'home',
    component: HomeView,
    meta: { title: 'Dashboard' }
  },
  {
    path: '/requests',
    name: 'requests',
    component: () => import('@/views/RequestListView.vue'),
    meta: { screenId: 'SCR-REQ-001', title: 'Request List', requiresAuth: true }
  },
  {
    path: '/requests/new',
    name: 'request-create',
    component: () => import('@/views/RequestCreateView.vue'),
    meta: { screenId: 'SCR-REQ-002', title: 'Record Request', requiresAuth: true }
  },
  {
    path: '/requests/:id',
    name: 'request-detail',
    component: () => import('@/views/RequestDetailView.vue'),
    meta: { screenId: 'SCR-REQ-003', title: 'Request Detail', requiresAuth: true }
  },
  {
    path: '/requests/my-assigned',
    alias: '/my-requests',
    name: 'my-requests',
    component: () => import('@/views/MyRequestsView.vue'),
    meta: { screenId: 'SCR-REQ-004', title: 'My Assigned Requests', requiresAuth: true }
  },
  {
    path: '/requests/search',
    name: 'request-search',
    component: () => import('@/views/RequestSearchView.vue'),
    meta: { screenId: 'SCR-REQ-005', title: 'Search & History', requiresAuth: true }
  },
  {
    path: '/products',
    name: 'products',
    component: () => import('@/views/ProductCatalogView.vue'),
    meta: { screenId: 'SCR-PRD-001', title: 'Product Catalog', requiresAuth: true }
  },
  {
    path: '/feed',
    name: 'feed',
    component: FeedView,
    meta: { screenId: 'SCR-FEED-001', title: 'Operational Feed', requiresAuth: true }
  },
  {
    path: '/work-packages',
    name: 'work-packages',
    component: WorkPackageView,
    meta: { screenId: 'SCR-WP-001', title: 'Work Package Management', requiresAuth: true }
  },
  {
    path: '/login',
    name: 'login',
    component: LoginView,
    meta: { screenId: 'SCR-AUTH-001', title: 'Sign In', guestOnly: true }
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('@/views/NotFoundView.vue'),
    meta: { title: 'Not Found' }
  }
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes
})

router.beforeEach(async (to, _from, next) => {
  if (to.meta.title) {
    document.title = `${to.meta.title} - ICS Operational System`
  } else {
    document.title = 'ICS Operational System'
  }

  const authStore = useAuthStore()

  if (to.meta.requiresAuth && !authStore.isAuthenticated) {
    const user = await authStore.checkAuth()
    if (!user) {
      return next({ path: '/login', query: { redirect: to.fullPath } })
    }
  }

  if (to.meta.guestOnly && authStore.isAuthenticated) {
    return next({ path: '/feed' })
  }

  next()
})

export default router
