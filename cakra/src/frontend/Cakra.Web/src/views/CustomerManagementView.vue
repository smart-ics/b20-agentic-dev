<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, ref } from 'vue'

import {
  activateCustomer,
  deactivateCustomer,
  listCustomers,
  type CustomerDto,
} from '@/api/customers'
import CustomerContactModal from '@/components/CustomerContactModal.vue'
import CustomerModal from '@/components/CustomerModal.vue'
import { useAuthStore } from '@/stores/auth'

/**
 * SCR-CUST-001: Customer Management Screen
 * (Architecture §7, §8, §14, §19.4, §19.6 — CR-010, FEAT-CUST-001).
 *
 * - Administrative customer master management view restricted to Administrators.
 * - Displays screen title, summary KPI metrics (Total, Active, Inactive, Active Contracts), and Add Customer action.
 * - Offers keyword search (filtering across customer code and customer name) and status dropdown filtering.
 * - Renders high-density table of customers with Customer Code, Customer Name, Maintenance Contract badge,
 *   Status badge, and Actions (Edit, Contacts, Activate / Deactivate).
 * - Integrates with SCR-CUST-002 (CustomerModal) and SCR-CUST-003 (CustomerContactModal).
 */

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const authStore = useAuthStore()
const isAdmin = computed(
  () => authStore.roles.includes('Administrator') || authStore.roles.includes('Admin'),
)

// Customer list state
const customers = ref<CustomerDto[]>([])
const isLoading = ref(false)
const actionLoadingId = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)

// Filtering state
const searchQuery = ref('')
const statusFilter = ref<'ALL' | 'ACTIVE' | 'INACTIVE'>('ALL')

// Modal state
const isCustomerModalOpen = ref(false)
const customerModalMode = ref<'create' | 'edit'>('create')
const isContactModalOpen = ref(false)
const selectedCustomer = ref<CustomerDto | null>(null)

// Computed KPI metrics
const totalCount = computed(() => customers.value.length)
const activeCount = computed(
  () =>
    customers.value.filter((c) => {
      const status = (c.status || (c.isActive ? 'ACTIVE' : 'INACTIVE')).toUpperCase()
      return status === 'ACTIVE'
    }).length,
)
const inactiveCount = computed(
  () =>
    customers.value.filter((c) => {
      const status = (c.status || (c.isActive ? 'ACTIVE' : 'INACTIVE')).toUpperCase()
      return status === 'INACTIVE'
    }).length,
)
const activeContractsCount = computed(
  () => customers.value.filter((c) => Boolean(c.hasActiveMaintenanceContract)).length,
)

// Filtered customers list
const filteredCustomers = computed(() => {
  let list = customers.value

  if (statusFilter.value !== 'ALL') {
    list = list.filter((c) => {
      const status = (c.status || (c.isActive ? 'ACTIVE' : 'INACTIVE')).toUpperCase()
      return status === statusFilter.value
    })
  }

  const query = searchQuery.value.trim().toLowerCase()
  if (query) {
    list = list.filter((c) => {
      const codeMatch = (c.customerCode || c.code || '').toLowerCase().includes(query)
      const nameMatch = (c.customerName || c.name || '').toLowerCase().includes(query)
      return codeMatch || nameMatch
    })
  }

  return list
})

function statusBadgeClass(status?: string): string {
  switch (status?.toUpperCase()) {
    case 'ACTIVE':
      return 'badge text-bg-success'
    case 'INACTIVE':
      return 'badge text-bg-secondary'
    default:
      return 'badge text-bg-secondary'
  }
}

function extractErrorMessage(err: unknown, fallback: string): string {
  if (err instanceof AxiosError) {
    const data = err.response?.data as ProblemDetailsPayload | undefined
    if (data?.errors) {
      const messages = Object.values(data.errors).flat()
      if (messages.length > 0) {
        return messages.join(' ')
      }
    }
    if (data?.detail) {
      return data.detail
    }
    if (data?.title) {
      return data.title
    }
  }
  return fallback
}

function resolveCustomerId(customer: CustomerDto): string {
  return (customer.id || customer.customerId || '').trim()
}

function resolveCustomerDisplayName(customer: CustomerDto): string {
  return (customer.customerName || customer.name || customer.customerCode || customer.code || '').trim()
}

async function loadCustomers(): Promise<void> {
  isLoading.value = true
  errorMessage.value = null

  try {
    const data = await listCustomers()
    customers.value = data ?? []
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load customers.')
  } finally {
    isLoading.value = false
  }
}

function openCreateModal(): void {
  selectedCustomer.value = null
  customerModalMode.value = 'create'
  isCustomerModalOpen.value = true
}

function openEditModal(customer: CustomerDto): void {
  selectedCustomer.value = customer
  customerModalMode.value = 'edit'
  isCustomerModalOpen.value = true
}

function openContactModal(customer: CustomerDto): void {
  selectedCustomer.value = customer
  isContactModalOpen.value = true
}

function handleCustomerModalClose(): void {
  isCustomerModalOpen.value = false
  selectedCustomer.value = null
}

function handleContactModalClose(): void {
  isContactModalOpen.value = false
  selectedCustomer.value = null
}

async function handleCustomerSaved(savedCustomer: CustomerDto): Promise<void> {
  const displayName = resolveCustomerDisplayName(savedCustomer)
  successMessage.value = `Customer '${displayName}' saved successfully.`
  await loadCustomers()
}

async function handleActivate(customer: CustomerDto): Promise<void> {
  const targetId = resolveCustomerId(customer)
  if (!targetId) return

  const displayName = resolveCustomerDisplayName(customer)
  const confirmed = window.confirm(`Are you sure you want to activate customer '${displayName}'?`)
  if (!confirmed) return

  actionLoadingId.value = targetId
  errorMessage.value = null

  try {
    await activateCustomer(targetId)
    successMessage.value = `Customer '${displayName}' activated successfully.`
    await loadCustomers()
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to activate customer.')
  } finally {
    actionLoadingId.value = null
  }
}

async function handleDeactivate(customer: CustomerDto): Promise<void> {
  const targetId = resolveCustomerId(customer)
  if (!targetId) return

  const displayName = resolveCustomerDisplayName(customer)
  const confirmed = window.confirm(`Are you sure you want to deactivate customer '${displayName}'?`)
  if (!confirmed) return

  actionLoadingId.value = targetId
  errorMessage.value = null

  try {
    await deactivateCustomer(targetId)
    successMessage.value = `Customer '${displayName}' deactivated successfully.`
    await loadCustomers()
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to deactivate customer.')
  } finally {
    actionLoadingId.value = null
  }
}

function resetFilters(): void {
  searchQuery.value = ''
  statusFilter.value = 'ALL'
}

onMounted(async () => {
  await loadCustomers()
})
</script>

<template>
  <section
    class="customer-management-view"
    data-screen-id="SCR-CUST-001"
    data-testid="customer-management-view"
  >
    <!-- Compact Screen Header -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2">
        <h1 class="op-screen-title">
          <i class="bi bi-building-gear text-primary" aria-hidden="true"></i>
          Customer Management
        </h1>
        <span class="badge text-bg-light border text-secondary font-monospace" style="font-size: 11px">
          SCR-CUST-001
        </span>
        <span class="text-body-secondary small d-none d-md-inline">
          | Administrative Customer Master Directory
        </span>
      </div>

      <div class="d-flex align-items-center gap-2">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm"
          :disabled="isLoading"
          data-testid="refresh-customers-btn"
          @click="loadCustomers"
        >
          <i class="bi bi-arrow-clockwise me-1" :class="{ 'spin-icon': isLoading }" aria-hidden="true"></i>
          Refresh
        </button>
        <button
          type="button"
          class="btn btn-primary btn-sm"
          :disabled="!isAdmin"
          data-testid="add-customer-btn"
          @click="openCreateModal"
        >
          <i class="bi bi-building-add me-1" aria-hidden="true"></i>
          Add Customer
        </button>
      </div>
    </div>

    <!-- Metric Ribbon -->
    <div class="op-metric-ribbon">
      <div class="op-stat-item">
        <span class="op-stat-label">Total Customers:</span>
        <span class="op-stat-val text-dark" data-testid="stat-total-customers">{{ totalCount }}</span>
      </div>
      <div class="op-stat-item">
        <span class="op-stat-label">Active:</span>
        <span class="op-stat-val text-success" data-testid="stat-active-customers">{{ activeCount }}</span>
      </div>
      <div class="op-stat-item">
        <span class="op-stat-label">Inactive:</span>
        <span class="op-stat-val text-secondary" data-testid="stat-inactive-customers">{{ inactiveCount }}</span>
      </div>
      <div class="op-stat-item">
        <span class="op-stat-label">Active Contracts:</span>
        <span class="op-stat-val text-info-emphasis" data-testid="stat-active-contracts">{{ activeContractsCount }}</span>
      </div>
    </div>

    <!-- Alert Notifications -->
    <div
      v-if="errorMessage"
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center justify-content-between py-1 px-2 mb-2 small"
      role="alert"
      data-testid="customers-error-alert"
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
          @click="loadCustomers"
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
      data-testid="customers-success-alert"
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
          placeholder="Search customer code or name..."
          data-testid="customer-search-input"
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
        <label for="customerStatusFilter" class="form-label mb-0 fs-11 text-nowrap">Status:</label>
        <select
          id="customerStatusFilter"
          v-model="statusFilter"
          class="form-select form-select-sm"
          style="width: 140px"
          data-testid="status-filter-select"
        >
          <option value="ALL">All Statuses</option>
          <option value="ACTIVE">Active</option>
          <option value="INACTIVE">Inactive</option>
        </select>
      </div>

      <!-- Filter Results Counter -->
      <div class="ms-auto small text-body-secondary fs-11">
        Showing <strong>{{ filteredCustomers.length }}</strong> of <strong>{{ totalCount }}</strong> customers
      </div>
    </div>

    <!-- Loading Skeleton / Spinner State -->
    <div v-if="isLoading" class="p-5 text-center bg-slate-900/90 border border-slate-800 rounded text-slate-400" data-testid="customers-loading-state">
      <div class="spinner-border text-cyan-400 spinner-border-sm mb-2" role="status">
        <span class="visually-hidden">Loading...</span>
      </div>
      <div class="text-slate-400 small">Loading customers...</div>
    </div>

    <!-- Customers Table -->
    <div v-else class="table-responsive bg-slate-900/90 border border-slate-800 rounded text-slate-100 shadow-md">
      <table class="table table-hover align-middle mb-0" data-testid="customers-table">
        <thead class="table-dark fs-11 text-uppercase text-slate-400">
          <tr>
            <th scope="col" style="min-width: 140px">Customer Code</th>
            <th scope="col" style="min-width: 220px">Customer Name</th>
            <th scope="col" class="text-center" style="width: 180px">Maintenance Contract</th>
            <th scope="col" class="text-center" style="width: 110px">Status</th>
            <th scope="col" class="text-end" style="width: 240px">Actions</th>
          </tr>
        </thead>
        <tbody class="fs-12">
          <!-- Empty State -->
          <tr v-if="filteredCustomers.length === 0">
            <td colspan="5" class="text-center py-5 text-body-secondary" data-testid="empty-customers-state">
              <i class="bi bi-building display-6 d-block mb-2 text-muted" aria-hidden="true"></i>
              <div class="fw-semibold">No customers found.</div>
              <div class="small mb-2">
                {{ searchQuery || statusFilter !== 'ALL' ? 'Try adjusting your search criteria or status filter.' : 'No customer records are registered in the system.' }}
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

          <!-- Customer Rows -->
          <tr
            v-for="customer in filteredCustomers"
            :key="resolveCustomerId(customer)"
            :data-testid="'customer-row-' + resolveCustomerId(customer)"
            data-testid-row="customer-row"
          >
            <!-- Customer Code -->
            <td>
              <span class="font-monospace fw-semibold text-dark">
                {{ customer.customerCode || customer.code }}
              </span>
            </td>

            <!-- Customer Name -->
            <td>
              <span class="fw-medium text-dark">
                {{ customer.customerName || customer.name }}
              </span>
            </td>

            <!-- Maintenance Contract Badge -->
            <td class="text-center">
              <span
                v-if="customer.hasActiveMaintenanceContract"
                class="badge bg-info-subtle text-info-emphasis border border-info-subtle px-2 py-1"
                data-testid="customer-contract-badge"
              >
                <i class="bi bi-shield-check me-1" aria-hidden="true"></i>Active Contract
              </span>
              <span
                v-else
                class="badge bg-light text-secondary border px-2 py-1"
                data-testid="customer-contract-badge"
              >
                <i class="bi bi-shield-x me-1 text-muted" aria-hidden="true"></i>No Contract
              </span>
            </td>

            <!-- Status Badge -->
            <td class="text-center">
              <span
                :class="statusBadgeClass(customer.status || (customer.isActive ? 'ACTIVE' : 'INACTIVE'))"
                data-testid="customer-status-badge"
              >
                {{ (customer.status || (customer.isActive ? 'ACTIVE' : 'INACTIVE')).toUpperCase() }}
              </span>
            </td>

            <!-- Actions -->
            <td class="text-end">
              <div class="d-inline-flex align-items-center gap-1 justify-content-end">
                <!-- Edit Action -->
                <button
                  type="button"
                  class="btn btn-outline-primary btn-sm py-0 px-2"
                  style="font-size: 11px; height: 24px; line-height: 22px"
                  data-testid="edit-customer-btn"
                  :data-testid-customer="resolveCustomerId(customer)"
                  title="Edit Customer"
                  :disabled="!isAdmin || actionLoadingId === resolveCustomerId(customer)"
                  @click="openEditModal(customer)"
                >
                  <i class="bi bi-pencil-square me-1" aria-hidden="true"></i>
                  Edit
                </button>

                <!-- Contacts Action -->
                <button
                  type="button"
                  class="btn btn-outline-secondary btn-sm py-0 px-2"
                  style="font-size: 11px; height: 24px; line-height: 22px"
                  data-testid="contacts-customer-btn"
                  :data-testid-customer="resolveCustomerId(customer)"
                  title="Manage Contacts"
                  :disabled="actionLoadingId === resolveCustomerId(customer)"
                  @click="openContactModal(customer)"
                >
                  <i class="bi bi-people-fill me-1" aria-hidden="true"></i>
                  Contacts
                </button>

                <!-- Activate Action -->
                <button
                  v-if="(customer.status?.toUpperCase() === 'INACTIVE' || (!customer.isActive && customer.status !== 'ACTIVE'))"
                  type="button"
                  class="btn btn-outline-success btn-sm py-0 px-2"
                  style="font-size: 11px; height: 24px; line-height: 22px"
                  data-testid="activate-customer-btn"
                  :data-testid-customer="resolveCustomerId(customer)"
                  title="Activate Customer"
                  :disabled="!isAdmin || actionLoadingId === resolveCustomerId(customer)"
                  @click="handleActivate(customer)"
                >
                  <span
                    v-if="actionLoadingId === resolveCustomerId(customer)"
                    class="spinner-border spinner-border-sm me-1"
                    role="status"
                    aria-hidden="true"
                  ></span>
                  <i v-else class="bi bi-check-circle me-1" aria-hidden="true"></i>
                  Activate
                </button>

                <!-- Deactivate Action -->
                <button
                  v-else
                  type="button"
                  class="btn btn-outline-warning btn-sm py-0 px-2"
                  style="font-size: 11px; height: 24px; line-height: 22px"
                  data-testid="deactivate-customer-btn"
                  :data-testid-customer="resolveCustomerId(customer)"
                  title="Deactivate Customer"
                  :disabled="!isAdmin || actionLoadingId === resolveCustomerId(customer)"
                  @click="handleDeactivate(customer)"
                >
                  <span
                    v-if="actionLoadingId === resolveCustomerId(customer)"
                    class="spinner-border spinner-border-sm me-1"
                    role="status"
                    aria-hidden="true"
                  ></span>
                  <i v-else class="bi bi-slash-circle me-1" aria-hidden="true"></i>
                  Deactivate
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Customer Master Modal (SCR-CUST-002) -->
    <CustomerModal
      :is-open="isCustomerModalOpen"
      :mode="customerModalMode"
      :customer="selectedCustomer"
      @close="handleCustomerModalClose"
      @saved="handleCustomerSaved"
    />

    <!-- Customer Contact Modal (SCR-CUST-003) -->
    <CustomerContactModal
      :is-open="isContactModalOpen"
      :customer="selectedCustomer"
      @close="handleContactModalClose"
    />
  </section>
</template>

<style scoped>
.customer-management-view {
  padding: 0;
}

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
