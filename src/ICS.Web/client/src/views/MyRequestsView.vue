<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import {
  requestService,
  type MyAssignedRequestsResponseDto
} from '@/services/requestService'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()

// State
const data = ref<MyAssignedRequestsResponseDto | null>(null)
const includeClosed = ref(false)
const isLoading = ref(true)
const errorMessage = ref<string | null>(null)

const myRequests = computed(() => data.value?.items ?? [])

async function loadMyRequests() {
  isLoading.value = true
  errorMessage.value = null
  try {
    data.value = await requestService.getMyAssignedRequests(includeClosed.value)
  } catch (err: any) {
    errorMessage.value =
      err.response?.data?.detail ||
      'Failed to load your assigned requests queue. Ensure your user is linked to a staff Person.'
  } finally {
    isLoading.value = false
  }
}

function toggleIncludeClosed() {
  includeClosed.value = !includeClosed.value
  loadMyRequests()
}

function getStatusBadgeClass(status: string): string {
  switch (status) {
    case 'CAPTURED':
      return 'bg-secondary'
    case 'EVALUATING':
      return 'bg-info text-dark'
    case 'ACCEPTED':
      return 'bg-primary'
    case 'IN_PROGRESS':
      return 'bg-warning text-dark'
    case 'ESCALATED':
      return 'bg-danger'
    case 'COMPLETED':
      return 'bg-success'
    case 'REJECTED':
      return 'bg-dark'
    default:
      return 'bg-secondary'
  }
}

function getPriorityBadgeClass(priority: string): string {
  switch (priority?.toUpperCase()) {
    case 'CRITICAL':
      return 'badge bg-danger-subtle text-danger border border-danger-subtle'
    case 'HIGH':
      return 'badge bg-warning-subtle text-warning-emphasis border border-warning-subtle'
    case 'MEDIUM':
      return 'badge bg-info-subtle text-info-emphasis border border-info-subtle'
    case 'LOW':
      return 'badge bg-light text-secondary border'
    default:
      return 'badge bg-light text-secondary border'
  }
}

function formatDate(iso?: string | null): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  })
}

onMounted(() => {
  loadMyRequests()
})
</script>

<template>
  <div class="container py-4">
    <!-- Header -->
    <div class="d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3 mb-4">
      <div>
        <div class="d-flex align-items-center gap-2 mb-1">
          <h1 class="h3 fw-bold mb-0">My Assigned Requests</h1>
          <span class="badge bg-primary-subtle text-primary border border-primary-subtle font-monospace">
            SCR-REQ-004
          </span>
        </div>
        <p class="text-secondary small mb-0">
          Personal operational queue for <strong class="text-dark">{{ authStore.user?.username || 'Current User' }}</strong>.
        </p>
      </div>

      <div class="d-flex align-items-center gap-2">
        <div class="form-check form-switch mb-0">
          <input
            id="closedToggle"
            class="form-check-input"
            type="checkbox"
            :checked="includeClosed"
            @change="toggleIncludeClosed"
          />
          <label class="form-check-label small text-secondary" for="closedToggle">Include Closed</label>
        </div>
        <button
          class="btn btn-outline-secondary btn-sm"
          @click="loadMyRequests"
          :disabled="isLoading"
          title="Refresh"
        >
          <i class="bi bi-arrow-repeat" :class="{ 'spin-icon': isLoading }"></i>
        </button>
      </div>
    </div>

    <!-- Error Alert -->
    <div v-if="errorMessage" class="alert alert-danger alert-dismissible fade show" role="alert">
      <i class="bi bi-exclamation-triangle-fill me-2"></i>
      {{ errorMessage }}
      <button type="button" class="btn-close" @click="errorMessage = null" aria-label="Close"></button>
    </div>

    <!-- Metrics Cards Row -->
    <div v-if="data" class="row g-3 mb-4">
      <div class="col-md-4">
        <div class="card border-0 shadow-sm border-start border-primary border-4">
          <div class="card-body">
            <div class="text-secondary small text-uppercase fw-semibold mb-1">Active Workload</div>
            <div class="d-flex align-items-baseline gap-2">
              <span class="h2 fw-bold text-dark mb-0">{{ data.activeCount }}</span>
              <span class="text-secondary small">assigned</span>
            </div>
          </div>
        </div>
      </div>

      <div class="col-md-4">
        <div class="card border-0 shadow-sm border-start border-warning border-4">
          <div class="card-body">
            <div class="text-secondary small text-uppercase fw-semibold mb-1">Awaiting Management Decision</div>
            <div class="d-flex align-items-baseline gap-2">
              <span class="h2 fw-bold text-warning-emphasis mb-0">{{ data.awaitingDecisionCount }}</span>
              <span class="text-secondary small">dockets pending</span>
            </div>
          </div>
        </div>
      </div>

      <div class="col-md-4">
        <div class="card border-0 shadow-sm border-start border-danger border-4">
          <div class="card-body">
            <div class="text-secondary small text-uppercase fw-semibold mb-1">Escalated Attention</div>
            <div class="d-flex align-items-baseline gap-2">
              <span class="h2 fw-bold text-danger mb-0">{{ data.escalatedCount }}</span>
              <span class="text-secondary small">escalations</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Queue Table Card -->
    <div class="card border-0 shadow-sm">
      <div class="card-header bg-white py-3 d-flex justify-content-between align-items-center">
        <h2 class="h6 fw-bold mb-0">Assigned Requests Queue</h2>
        <span v-if="data" class="badge bg-secondary-subtle text-secondary">
          {{ data.items.length }} items
        </span>
      </div>

      <div class="table-responsive">
        <table class="table table-hover align-middle mb-0">
          <thead class="table-light">
            <tr>
              <th scope="col" style="min-width: 250px;">Request</th>
              <th scope="col">Customer / Product</th>
              <th scope="col">Status</th>
              <th scope="col">Priority</th>
              <th scope="col">Created</th>
              <th scope="col" class="text-end">Action</th>
            </tr>
          </thead>
          <tbody>
            <!-- Loading -->
            <tr v-if="isLoading">
              <td colspan="6" class="text-center py-5 text-secondary">
                <div class="spinner-border spinner-border-sm text-primary me-2" role="status"></div>
                Loading assigned queue...
              </td>
            </tr>

            <!-- Empty -->
            <tr v-else-if="!data || data.items.length === 0">
              <td colspan="6" class="text-center py-5 text-secondary">
                <i class="bi bi-check2-circle fs-2 d-block mb-2 text-success"></i>
                <div class="fw-semibold">No assigned requests in your queue</div>
                <div class="small text-muted mb-3">All caught up! Check the general request list for incoming triage items.</div>
                <RouterLink to="/requests" class="btn btn-outline-primary btn-sm">
                  View All Requests
                </RouterLink>
              </td>
            </tr>

            <!-- Rows -->
            <tr v-for="req in myRequests" :key="req.requestId">
              <td>
                <div class="fw-bold text-dark mb-1">
                  <RouterLink :to="`/requests/${req.requestId}`" class="text-decoration-none text-dark hover-primary">
                    {{ req.title }}
                  </RouterLink>
                </div>
                <div class="d-flex align-items-center gap-2">
                  <span class="badge bg-light text-dark border font-monospace" style="font-size: 0.7rem;">
                    {{ req.type }}
                  </span>
                  <span v-if="req.isAwaitingManagementDecision" class="badge bg-warning text-dark" style="font-size: 0.65rem;">
                    <i class="bi bi-hourglass-split me-1"></i>Decision
                  </span>
                  <span v-if="req.escalatedAt" class="badge bg-danger" style="font-size: 0.65rem;">
                    <i class="bi bi-exclamation-triangle-fill me-1"></i>Escalated
                  </span>
                </div>
              </td>
              <td>
                <div class="small fw-semibold text-dark">{{ req.customerName || '—' }}</div>
                <div class="text-secondary small">{{ req.productName || '—' }}</div>
              </td>
              <td>
                <span class="badge" :class="getStatusBadgeClass(req.status)">
                  {{ req.status }}
                </span>
              </td>
              <td>
                <span :class="getPriorityBadgeClass(req.priority)">
                  {{ req.priority || 'MEDIUM' }}
                </span>
              </td>
              <td class="small text-secondary">
                {{ formatDate(req.createdAt) }}
              </td>
              <td class="text-end">
                <RouterLink :to="`/requests/${req.requestId}`" class="btn btn-outline-primary btn-sm">
                  Interact <i class="bi bi-arrow-right ms-1"></i>
                </RouterLink>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>
</template>

<style scoped>
.hover-primary:hover {
  color: #0d6efd !important;
}
.spin-icon {
  animation: spin 1s linear infinite;
}
@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}
</style>
