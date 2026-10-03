<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, ref } from 'vue'

import {
  listUsers,
  type UserAccountDetail,
  type UserAccountSummary,
} from '@/api/users'
import UserAccountModal from '@/components/UserAccountModal.vue'

/**
 * SCR-USR-001: User Management Screen
 * (Architecture §7, §8, §14, §19.4, §19.6 — CR-007, FEAT-USR-001).
 *
 * - Administrative user management view restricted strictly to Administrators.
 * - Displays screen title, summary metrics (Total, Active, Locked, Suspended), and Add User action.
 * - Offers keyword search (filtering across username, person name, and email) and status dropdown filtering.
 * - Renders high-density table of user accounts with Username, Person Name, Email, Status badge,
 *   Failed Login Attempts counter, Last Login At timestamp, and Edit action.
 * - Integrates with SCR-USR-002 (UserAccountModal) for creating and updating user accounts.
 */

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

// User account list state
const users = ref<UserAccountSummary[]>([])
const isLoading = ref(false)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)

// Filtering state
const searchQuery = ref('')
const statusFilter = ref<'ALL' | 'ACTIVE' | 'LOCKED' | 'SUSPENDED'>('ALL')

// Modal state
const isModalOpen = ref(false)
const modalMode = ref<'create' | 'edit'>('create')
const selectedUser = ref<UserAccountSummary | null>(null)

// Computed metrics
const totalCount = computed(() => users.value.length)
const activeCount = computed(
  () => users.value.filter((u) => u.status?.toUpperCase() === 'ACTIVE').length,
)
const lockedCount = computed(
  () => users.value.filter((u) => u.status?.toUpperCase() === 'LOCKED').length,
)
const suspendedCount = computed(
  () => users.value.filter((u) => u.status?.toUpperCase() === 'SUSPENDED').length,
)

// Filtered user accounts list
const filteredUsers = computed(() => {
  let list = users.value

  if (statusFilter.value !== 'ALL') {
    list = list.filter((u) => u.status?.toUpperCase() === statusFilter.value)
  }

  const query = searchQuery.value.trim().toLowerCase()
  if (query) {
    list = list.filter((u) => {
      const usernameMatch = (u.username || '').toLowerCase().includes(query)
      const personMatch = (u.personName || '').toLowerCase().includes(query)
      const emailMatch = (u.email || '').toLowerCase().includes(query)
      return usernameMatch || personMatch || emailMatch
    })
  }

  return list
})

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) {
    return 'Never'
  }
  const parsed = new Date(iso)
  if (Number.isNaN(parsed.getTime())) {
    return iso
  }
  return parsed.toLocaleString()
}

function statusBadgeClass(status: string): string {
  switch (status?.toUpperCase()) {
    case 'ACTIVE':
      return 'badge text-bg-success'
    case 'LOCKED':
      return 'badge text-bg-danger'
    case 'SUSPENDED':
      return 'badge text-bg-warning text-dark'
    default:
      return 'badge text-bg-secondary'
  }
}

function extractErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof AxiosError) {
    const data = err.response?.data as ProblemDetailsPayload | undefined
    if (data?.detail) {
      return data.detail
    }
    if (data?.title) {
      return data.title
    }
  }
  return fallback
}

async function loadUsers(): Promise<void> {
  isLoading.value = true
  errorMessage.value = null

  try {
    const data = await listUsers()
    users.value = data ?? []
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load user accounts.')
  } finally {
    isLoading.value = false
  }
}

function openCreateModal(): void {
  selectedUser.value = null
  modalMode.value = 'create'
  isModalOpen.value = true
}

function openEditModal(user: UserAccountSummary): void {
  selectedUser.value = user
  modalMode.value = 'edit'
  isModalOpen.value = true
}

function handleModalClose(): void {
  isModalOpen.value = false
  selectedUser.value = null
}

async function handleUserSaved(savedUser: UserAccountDetail): Promise<void> {
  successMessage.value = `User account '${savedUser.username}' saved successfully.`
  await loadUsers()
}

function resetFilters(): void {
  searchQuery.value = ''
  statusFilter.value = 'ALL'
}

onMounted(async () => {
  await loadUsers()
})
</script>

<template>
  <section class="user-management-view" data-screen-id="SCR-USR-001" data-testid="user-management-view">
    <!-- Compact Screen Header -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2">
        <h1 class="op-screen-title">
          <i class="bi bi-person-gear text-primary" aria-hidden="true"></i>
          User Management
        </h1>
        <span class="badge text-bg-light border text-secondary font-monospace" style="font-size: 11px">
          SCR-USR-001
        </span>
        <span class="text-body-secondary small d-none d-md-inline">
          | Administrative Identity &amp; Account Control
        </span>
      </div>

      <div class="d-flex align-items-center gap-2">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm"
          :disabled="isLoading"
          data-testid="refresh-users-btn"
          @click="loadUsers"
        >
          <i class="bi bi-arrow-clockwise me-1" :class="{ 'spin-icon': isLoading }" aria-hidden="true"></i>
          Refresh
        </button>
        <button
          type="button"
          class="btn btn-primary btn-sm"
          data-testid="add-user-btn"
          @click="openCreateModal"
        >
          <i class="bi bi-person-plus-fill me-1" aria-hidden="true"></i>
          Add User
        </button>
      </div>
    </div>

    <!-- Metric Ribbon -->
    <div class="op-metric-ribbon">
      <div class="op-stat-item">
        <span class="op-stat-label">Total Accounts:</span>
        <span class="op-stat-val text-dark" data-testid="stat-total-users">{{ totalCount }}</span>
      </div>
      <div class="op-stat-item">
        <span class="op-stat-label">Active:</span>
        <span class="op-stat-val text-success" data-testid="stat-active-users">{{ activeCount }}</span>
      </div>
      <div class="op-stat-item">
        <span class="op-stat-label">Locked:</span>
        <span class="op-stat-val text-danger" data-testid="stat-locked-users">{{ lockedCount }}</span>
      </div>
      <div class="op-stat-item">
        <span class="op-stat-label">Suspended:</span>
        <span class="op-stat-val text-warning" data-testid="stat-suspended-users">{{ suspendedCount }}</span>
      </div>
    </div>

    <!-- Alert Notifications -->
    <div
      v-if="errorMessage"
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center justify-content-between py-1 px-2 mb-2 small"
      role="alert"
      data-testid="users-error-alert"
    >
      <div class="d-flex align-items-center gap-2">
        <i class="bi bi-exclamation-triangle-fill flex-shrink-0" aria-hidden="true"></i>
        <span>{{ errorMessage }}</span>
      </div>
      <div class="d-flex align-items-center gap-2">
        <button
          type="button"
          class="btn btn-outline-danger btn-sm py-0 px-2"
          style="font-size: 11px"
          @click="loadUsers"
        >
          Retry
        </button>
        <button
          type="button"
          class="btn-close py-1 px-2"
          aria-label="Close"
          @click="errorMessage = null"
        ></button>
      </div>
    </div>

    <div
      v-if="successMessage"
      class="alert alert-success alert-dismissible fade show d-flex align-items-center gap-2 py-1 px-2 mb-2 small"
      role="alert"
      data-testid="users-success-alert"
    >
      <i class="bi bi-check-circle-fill flex-shrink-0" aria-hidden="true"></i>
      <span class="flex-grow-1">{{ successMessage }}</span>
      <button
        type="button"
        class="btn-close py-1 px-2"
        aria-label="Close"
        @click="successMessage = null"
      ></button>
    </div>

    <!-- Operational Filter Toolbar -->
    <div class="op-toolbar">
      <!-- Search Input -->
      <div class="input-group input-group-sm" style="max-width: 320px">
        <span class="input-group-text bg-light border-end-0">
          <i class="bi bi-search text-secondary" aria-hidden="true"></i>
        </span>
        <input
          v-model="searchQuery"
          type="text"
          class="form-control border-start-0"
          placeholder="Search username, person, or email..."
          data-testid="user-search-input"
        />
        <button
          v-if="searchQuery"
          class="btn btn-outline-secondary border-start-0"
          type="button"
          title="Clear search"
          @click="searchQuery = ''"
        >
          <i class="bi bi-x-lg" aria-hidden="true"></i>
        </button>
      </div>

      <!-- Status Filter -->
      <div class="d-flex align-items-center gap-1 ms-sm-2">
        <label for="userStatusFilter" class="form-label mb-0 fs-11 text-nowrap">Status:</label>
        <select
          id="userStatusFilter"
          v-model="statusFilter"
          class="form-select form-select-sm"
          style="width: 140px"
          data-testid="status-filter-select"
        >
          <option value="ALL">All Statuses</option>
          <option value="ACTIVE">ACTIVE</option>
          <option value="LOCKED">LOCKED</option>
          <option value="SUSPENDED">SUSPENDED</option>
        </select>
      </div>

      <!-- Filter Results Counter -->
      <div class="ms-auto small text-body-secondary fs-11">
        Showing <strong>{{ filteredUsers.length }}</strong> of <strong>{{ totalCount }}</strong> accounts
      </div>
    </div>

    <!-- Loading Skeleton / Spinner State -->
    <div v-if="isLoading" class="p-5 text-center bg-white border rounded" data-testid="users-loading-state">
      <div class="spinner-border text-primary spinner-border-sm mb-2" role="status">
        <span class="visually-hidden">Loading...</span>
      </div>
      <div class="text-body-secondary small">Loading user accounts...</div>
    </div>

    <!-- User Accounts Table -->
    <div v-else class="table-responsive bg-white border rounded">
      <table class="table table-hover align-middle mb-0" data-testid="users-table">
        <thead class="table-light fs-11 text-uppercase text-body-secondary">
          <tr>
            <th scope="col" style="min-width: 130px">Username</th>
            <th scope="col" style="min-width: 160px">Person Name</th>
            <th scope="col" style="min-width: 180px">Email</th>
            <th scope="col" class="text-center" style="width: 110px">Status</th>
            <th scope="col" class="text-center" style="width: 120px">Failed Logins</th>
            <th scope="col" style="min-width: 160px">Last Login</th>
            <th scope="col" class="text-end" style="width: 90px">Actions</th>
          </tr>
        </thead>
        <tbody class="fs-12">
          <!-- Empty State -->
          <tr v-if="filteredUsers.length === 0">
            <td colspan="7" class="text-center py-5 text-body-secondary" data-testid="empty-users-state">
              <i class="bi bi-people display-6 d-block mb-2 text-muted" aria-hidden="true"></i>
              <div class="fw-semibold">No user accounts found.</div>
              <div class="small mb-2">
                {{ searchQuery || statusFilter !== 'ALL' ? 'Try adjusting your search criteria or status filter.' : 'No user accounts are registered in the system.' }}
              </div>
              <button
                v-if="searchQuery || statusFilter !== 'ALL'"
                type="button"
                class="btn btn-outline-secondary btn-sm py-0 px-2"
                style="font-size: 11px"
                @click="resetFilters"
              >
                Clear Filters
              </button>
            </td>
          </tr>

          <!-- User Rows -->
          <tr
            v-for="user in filteredUsers"
            :key="user.userId"
            :data-testid="'user-row-' + user.userId"
          >
            <!-- Username -->
            <td>
              <span class="font-monospace fw-semibold text-dark">{{ user.username }}</span>
            </td>

            <!-- Person Name -->
            <td>
              <span class="fw-medium">{{ user.personName || '—' }}</span>
            </td>

            <!-- Email -->
            <td>
              <span class="text-body-secondary font-monospace" style="font-size: 11.5px">
                {{ user.email }}
              </span>
            </td>

            <!-- Status Badge -->
            <td class="text-center">
              <span :class="statusBadgeClass(user.status)">
                {{ user.status }}
              </span>
            </td>

            <!-- Failed Logins -->
            <td class="text-center">
              <span
                v-if="user.failedLoginAttempts > 0"
                class="badge text-bg-danger rounded-pill"
                data-testid="user-failed-attempts"
              >
                {{ user.failedLoginAttempts }}
              </span>
              <span v-else class="text-body-secondary font-monospace">0</span>
            </td>

            <!-- Last Login -->
            <td class="text-body-secondary small">
              {{ formatDateTime(user.lastLoginAt) }}
            </td>

            <!-- Actions -->
            <td class="text-end">
              <button
                type="button"
                class="btn btn-outline-primary btn-sm py-0 px-2"
                style="font-size: 11px; height: 24px; line-height: 22px"
                data-testid="edit-user-btn"
                :data-testid-user="user.userId"
                title="Edit User Account"
                @click="openEditModal(user)"
              >
                <i class="bi bi-pencil-square me-1" aria-hidden="true"></i>
                Edit
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- User Account Modal (SCR-USR-002) -->
    <UserAccountModal
      :show="isModalOpen"
      :mode="modalMode"
      :user="selectedUser"
      @close="handleModalClose"
      @saved="handleUserSaved"
    />
  </section>
</template>

<style scoped>
.spin-icon {
  display: inline-block;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  100% {
    transform: rotate(360deg);
  }
}
</style>
