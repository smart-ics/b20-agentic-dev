<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import {
  activatePerson,
  deactivatePerson,
  extractErrorMessage,
  listAllPersons,
  type PersonDto,
} from '@/api/persons'
import PersonModal from '@/components/PersonModal.vue'
import { useAuthStore } from '@/stores/auth'

/**
 * SCR-ORG-001: Person Management Screen
 * (Architecture §7, §8, §14, §19.4, §19.6 — CR-008, FEAT-ORG-001).
 *
 * - Administrative person management view restricted to Administrators.
 * - Displays screen title, summary metrics (Total, Active, Inactive), and Add Person action.
 * - Offers keyword search (filtering across first name, last name, and email) and status dropdown filtering.
 * - Renders high-density table of persons with First Name, Last Name, Email, Status badge, and Actions
 *   (Edit, Activate for inactive, Deactivate for active).
 * - Integrates with SCR-ORG-002 (PersonModal) for creating and editing persons.
 */

const authStore = useAuthStore()
const isAdmin = computed(
  () => authStore.roles.includes('Administrator') || authStore.roles.includes('Admin'),
)

// Person list state
const persons = ref<PersonDto[]>([])
const isLoading = ref(false)
const actionLoadingId = ref<string | null>(null)
const errorMessage = ref<string | null>(null)
const successMessage = ref<string | null>(null)

// Filtering state
const searchQuery = ref('')
const statusFilter = ref<'ALL' | 'ACTIVE' | 'INACTIVE'>('ALL')

// Modal state
const isModalOpen = ref(false)
const modalMode = ref<'create' | 'edit'>('create')
const selectedPerson = ref<PersonDto | null>(null)

// Computed metrics
const totalCount = computed(() => persons.value.length)
const activeCount = computed(
  () => persons.value.filter((p) => p.status?.toUpperCase() === 'ACTIVE').length,
)
const inactiveCount = computed(
  () => persons.value.filter((p) => p.status?.toUpperCase() === 'INACTIVE').length,
)

// Filtered persons list
const filteredPersons = computed(() => {
  let list = persons.value

  if (statusFilter.value !== 'ALL') {
    list = list.filter((p) => p.status?.toUpperCase() === statusFilter.value)
  }

  const query = searchQuery.value.trim().toLowerCase()
  if (query) {
    list = list.filter((p) => {
      const firstMatch = (p.firstName || '').toLowerCase().includes(query)
      const lastMatch = (p.lastName || '').toLowerCase().includes(query)
      const emailMatch = (p.email || '').toLowerCase().includes(query)
      const fullMatch = (p.fullName || '').toLowerCase().includes(query)
      return firstMatch || lastMatch || emailMatch || fullMatch
    })
  }

  return list
})

function statusBadgeClass(status: string): string {
  switch (status?.toUpperCase()) {
    case 'ACTIVE':
      return 'badge text-bg-success'
    case 'INACTIVE':
      return 'badge text-bg-secondary'
    default:
      return 'badge text-bg-secondary'
  }
}

async function loadPersons(): Promise<void> {
  isLoading.value = true
  errorMessage.value = null

  try {
    const data = await listAllPersons()
    persons.value = data ?? []
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load persons.')
  } finally {
    isLoading.value = false
  }
}

function openCreateModal(): void {
  selectedPerson.value = null
  modalMode.value = 'create'
  isModalOpen.value = true
}

function openEditModal(person: PersonDto): void {
  selectedPerson.value = person
  modalMode.value = 'edit'
  isModalOpen.value = true
}

function handleModalClose(): void {
  isModalOpen.value = false
  selectedPerson.value = null
}

async function handlePersonSaved(savedPerson: PersonDto): Promise<void> {
  const displayName = savedPerson.fullName || `${savedPerson.firstName} ${savedPerson.lastName}`.trim()
  successMessage.value = `Person '${displayName}' saved successfully.`
  await loadPersons()
}

async function handleActivate(person: PersonDto): Promise<void> {
  const targetId = person.id || person.personId
  if (!targetId) return

  actionLoadingId.value = targetId
  errorMessage.value = null

  try {
    await activatePerson(targetId)
    const displayName = person.fullName || `${person.firstName} ${person.lastName}`.trim()
    successMessage.value = `Person '${displayName}' activated successfully.`
    await loadPersons()
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to activate person.')
  } finally {
    actionLoadingId.value = null
  }
}

async function handleDeactivate(person: PersonDto): Promise<void> {
  const targetId = person.id || person.personId
  if (!targetId) return

  actionLoadingId.value = targetId
  errorMessage.value = null

  try {
    await deactivatePerson(targetId)
    const displayName = person.fullName || `${person.firstName} ${person.lastName}`.trim()
    successMessage.value = `Person '${displayName}' deactivated successfully.`
    await loadPersons()
  } catch (err: unknown) {
    errorMessage.value = extractErrorMessage(err, 'Failed to deactivate person.')
  } finally {
    actionLoadingId.value = null
  }
}

function resetFilters(): void {
  searchQuery.value = ''
  statusFilter.value = 'ALL'
}

onMounted(async () => {
  await loadPersons()
})
</script>

<template>
  <section class="person-management-view" data-screen-id="SCR-ORG-001" data-testid="person-management-view">
    <!-- Compact Screen Header -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2">
        <h1 class="op-screen-title">
          <i class="bi bi-person-lock text-primary" aria-hidden="true"></i>
          Person Management
        </h1>
        <span class="badge text-bg-light border text-secondary font-monospace" style="font-size: 11px">
          SCR-ORG-001
        </span>
        <span class="text-body-secondary small d-none d-md-inline">
          | Administrative Person Directory
        </span>
      </div>

      <div class="d-flex align-items-center gap-2">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm"
          :disabled="isLoading"
          data-testid="refresh-persons-btn"
          @click="loadPersons"
        >
          <i class="bi bi-arrow-clockwise me-1" :class="{ 'spin-icon': isLoading }" aria-hidden="true"></i>
          Refresh
        </button>
        <button
          type="button"
          class="btn btn-primary btn-sm"
          :disabled="!isAdmin"
          data-testid="add-person-btn"
          @click="openCreateModal"
        >
          <i class="bi bi-person-plus-fill me-1" aria-hidden="true"></i>
          Add Person
        </button>
      </div>
    </div>

    <!-- Metric Ribbon -->
    <div class="op-metric-ribbon">
      <div class="op-stat-item">
        <span class="op-stat-label">Total Persons:</span>
        <span class="op-stat-val text-dark" data-testid="stat-total-persons">{{ totalCount }}</span>
      </div>
      <div class="op-stat-item">
        <span class="op-stat-label">Active:</span>
        <span class="op-stat-val text-success" data-testid="stat-active-persons">{{ activeCount }}</span>
      </div>
      <div class="op-stat-item">
        <span class="op-stat-label">Inactive:</span>
        <span class="op-stat-val text-secondary" data-testid="stat-inactive-persons">{{ inactiveCount }}</span>
      </div>
    </div>

    <!-- Alert Notifications -->
    <div
      v-if="errorMessage"
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center justify-content-between py-1 px-2 mb-2 small"
      role="alert"
      data-testid="persons-error-alert"
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
          @click="loadPersons"
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
      data-testid="persons-success-alert"
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
          placeholder="Search first name, last name, email..."
          data-testid="person-search-input"
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
        <label for="personStatusFilter" class="form-label mb-0 fs-11 text-nowrap">Status:</label>
        <select
          id="personStatusFilter"
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
        Showing <strong>{{ filteredPersons.length }}</strong> of <strong>{{ totalCount }}</strong> persons
      </div>
    </div>

    <!-- Loading Skeleton / Spinner State -->
    <div v-if="isLoading" class="p-5 text-center bg-white border rounded" data-testid="persons-loading-state">
      <div class="spinner-border text-primary spinner-border-sm mb-2" role="status">
        <span class="visually-hidden">Loading...</span>
      </div>
      <div class="text-body-secondary small">Loading persons...</div>
    </div>

    <!-- Persons Table -->
    <div v-else class="table-responsive bg-white border rounded">
      <table class="table table-hover align-middle mb-0" data-testid="persons-table">
        <thead class="table-light fs-11 text-uppercase text-body-secondary">
          <tr>
            <th scope="col" style="min-width: 140px">First Name</th>
            <th scope="col" style="min-width: 140px">Last Name</th>
            <th scope="col" style="min-width: 200px">Email</th>
            <th scope="col" class="text-center" style="width: 110px">Status</th>
            <th scope="col" class="text-end" style="width: 170px">Actions</th>
          </tr>
        </thead>
        <tbody class="fs-12">
          <!-- Empty State -->
          <tr v-if="filteredPersons.length === 0">
            <td colspan="5" class="text-center py-5 text-body-secondary" data-testid="empty-persons-state">
              <i class="bi bi-people display-6 d-block mb-2 text-muted" aria-hidden="true"></i>
              <div class="fw-semibold">No persons found.</div>
              <div class="small mb-2">
                {{ searchQuery || statusFilter !== 'ALL' ? 'Try adjusting your search criteria or status filter.' : 'No persons are registered in the system.' }}
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

          <!-- Person Rows -->
          <tr
            v-for="person in filteredPersons"
            :key="person.id"
            :data-testid="'person-row-' + person.id"
          >
            <!-- First Name -->
            <td>
              <span class="fw-medium text-dark">{{ person.firstName }}</span>
            </td>

            <!-- Last Name -->
            <td>
              <span class="fw-medium text-dark">{{ person.lastName }}</span>
            </td>

            <!-- Email -->
            <td>
              <span class="text-body-secondary font-monospace" style="font-size: 11.5px">
                {{ person.email }}
              </span>
            </td>

            <!-- Status Badge -->
            <td class="text-center">
              <span :class="statusBadgeClass(person.status)">
                {{ person.status }}
              </span>
            </td>

            <!-- Actions -->
            <td class="text-end">
              <div class="d-inline-flex align-items-center gap-1 justify-content-end">
                <button
                  type="button"
                  class="btn btn-outline-primary btn-sm py-0 px-2"
                  style="font-size: 11px; height: 24px; line-height: 22px"
                  data-testid="edit-person-btn"
                  :data-testid-person="person.id"
                  title="Edit Person"
                  :disabled="!isAdmin || actionLoadingId === person.id"
                  @click="openEditModal(person)"
                >
                  <i class="bi bi-pencil-square me-1" aria-hidden="true"></i>
                  Edit
                </button>

                <button
                  v-if="person.status?.toUpperCase() === 'INACTIVE'"
                  type="button"
                  class="btn btn-outline-success btn-sm py-0 px-2"
                  style="font-size: 11px; height: 24px; line-height: 22px"
                  data-testid="activate-person-btn"
                  :data-testid-person="person.id"
                  title="Activate Person"
                  :disabled="!isAdmin || actionLoadingId === person.id"
                  @click="handleActivate(person)"
                >
                  <span
                    v-if="actionLoadingId === person.id"
                    class="spinner-border spinner-border-sm me-1"
                    role="status"
                    aria-hidden="true"
                  ></span>
                  <i v-else class="bi bi-check-circle me-1" aria-hidden="true"></i>
                  Activate
                </button>

                <button
                  v-else
                  type="button"
                  class="btn btn-outline-warning btn-sm py-0 px-2"
                  style="font-size: 11px; height: 24px; line-height: 22px"
                  data-testid="deactivate-person-btn"
                  :data-testid-person="person.id"
                  title="Deactivate Person"
                  :disabled="!isAdmin || actionLoadingId === person.id"
                  @click="handleDeactivate(person)"
                >
                  <span
                    v-if="actionLoadingId === person.id"
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

    <!-- Person Modal (SCR-ORG-002) -->
    <PersonModal
      :show="isModalOpen"
      :mode="modalMode"
      :person="selectedPerson"
      @close="handleModalClose"
      @saved="handlePersonSaved"
    />
  </section>
</template>

<style scoped>
.person-management-view {
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