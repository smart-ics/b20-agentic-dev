<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import {
  requestService,
  type RequestSummaryDto,
  type CustomerLookupDto,
  type ProductLookupDto
} from '@/services/requestService'

// State
const requests = ref<RequestSummaryDto[]>([])
const customers = ref<CustomerLookupDto[]>([])
const products = ref<ProductLookupDto[]>([])
const totalCount = ref(0)
const isLoading = ref(false)
const errorMessage = ref<string | null>(null)

// Filters
const searchTerm = ref('')
const selectedCustomerId = ref('')
const selectedProductId = ref('')
const selectedStatus = ref('')
const selectedPriority = ref('')

// Pagination
const pageSize = ref(15)
const currentPage = ref(1)

async function loadLookups() {
  try {
    const [custData, prodData] = await Promise.all([
      requestService.getActiveCustomers(),
      requestService.getActiveProducts()
    ])
    customers.value = custData
    products.value = prodData
  } catch (err: any) {
    console.error('Failed to load lookup dropdowns', err)
  }
}

async function loadRequests() {
  isLoading.value = true
  errorMessage.value = null
  try {
    const skip = (currentPage.value - 1) * pageSize.value
    const data = await requestService.getRequests({
      searchTerm: searchTerm.value.trim() || undefined,
      customerId: selectedCustomerId.value || undefined,
      productId: selectedProductId.value || undefined,
      statuses: selectedStatus.value || undefined,
      priority: selectedPriority.value || undefined,
      skip,
      take: pageSize.value
    })
    requests.value = data.items
    totalCount.value = data.totalCount
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || 'Failed to load requests list.'
  } finally {
    isLoading.value = false
  }
}

function handleSearch() {
  currentPage.value = 1
  loadRequests()
}

function handleReset() {
  searchTerm.value = ''
  selectedCustomerId.value = ''
  selectedProductId.value = ''
  selectedStatus.value = ''
  selectedPriority.value = ''
  currentPage.value = 1
  loadRequests()
}

function goToPage(page: number) {
  if (page < 1 || (page - 1) * pageSize.value >= totalCount.value) return
  currentPage.value = page
  loadRequests()
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

function formatDate(iso: string): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  })
}

onMounted(async () => {
  await Promise.all([loadLookups(), loadRequests()])
})
</script>

<template>
  <div class="container py-4">
    <!-- Header -->
    <div class="d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3 mb-4">
      <div>
        <div class="d-flex align-items-center gap-2 mb-1">
          <h1 class="h3 fw-bold mb-0">Request List</h1>
          <span class="badge bg-primary-subtle text-primary border border-primary-subtle font-monospace">
            SCR-REQ-001
          </span>
        </div>
        <p class="text-secondary small mb-0">
          Browse, filter, and monitor operational requests across all stages.
        </p>
      </div>

      <div class="d-flex flex-wrap gap-2">
        <RouterLink to="/requests/my-assigned" class="btn btn-outline-secondary btn-sm d-flex align-items-center gap-1">
          <i class="bi bi-person-check"></i>
          <span>My Assigned</span>
        </RouterLink>
        <RouterLink to="/requests/search" class="btn btn-outline-secondary btn-sm d-flex align-items-center gap-1">
          <i class="bi bi-clock-history"></i>
          <span>Search & History</span>
        </RouterLink>
        <RouterLink to="/requests/new" class="btn btn-primary btn-sm d-flex align-items-center gap-1">
          <i class="bi bi-plus-lg"></i>
          <span>New Request</span>
        </RouterLink>
      </div>
    </div>

    <!-- Error Alert -->
    <div v-if="errorMessage" class="alert alert-danger alert-dismissible fade show" role="alert">
      <i class="bi bi-exclamation-triangle-fill me-2"></i>
      {{ errorMessage }}
      <button type="button" class="btn-close" @click="errorMessage = null" aria-label="Close"></button>
    </div>

    <!-- Filters Card -->
    <div class="card border-0 shadow-sm mb-4">
      <div class="card-body">
        <form @submit.prevent="handleSearch" class="row g-2 align-items-end">
          <div class="col-12 col-md-3">
            <label class="form-label small text-secondary fw-semibold mb-1">Search Keywords</label>
            <div class="input-group input-group-sm">
              <span class="input-group-text"><i class="bi bi-search"></i></span>
              <input
                v-model="searchTerm"
                type="text"
                class="form-control"
                placeholder="Title or description..."
              />
            </div>
          </div>

          <div class="col-6 col-md-2">
            <label class="form-label small text-secondary fw-semibold mb-1">Customer</label>
            <select v-model="selectedCustomerId" class="form-select form-select-sm">
              <option value="">All Customers</option>
              <option v-for="c in customers" :key="c.customerId" :value="c.customerId">
                {{ c.name }} ({{ c.code }})
              </option>
            </select>
          </div>

          <div class="col-6 col-md-2">
            <label class="form-label small text-secondary fw-semibold mb-1">Product</label>
            <select v-model="selectedProductId" class="form-select form-select-sm">
              <option value="">All Products</option>
              <option v-for="p in products" :key="p.productId" :value="p.productId">
                {{ p.name }} ({{ p.code }})
              </option>
            </select>
          </div>

          <div class="col-6 col-md-2">
            <label class="form-label small text-secondary fw-semibold mb-1">Status</label>
            <select v-model="selectedStatus" class="form-select form-select-sm">
              <option value="">All Statuses</option>
              <option value="CAPTURED">CAPTURED</option>
              <option value="EVALUATING">EVALUATING</option>
              <option value="ACCEPTED">ACCEPTED</option>
              <option value="IN_PROGRESS">IN_PROGRESS</option>
              <option value="ESCALATED">ESCALATED</option>
              <option value="COMPLETED">COMPLETED</option>
              <option value="REJECTED">REJECTED</option>
            </select>
          </div>

          <div class="col-6 col-md-1">
            <label class="form-label small text-secondary fw-semibold mb-1">Priority</label>
            <select v-model="selectedPriority" class="form-select form-select-sm">
              <option value="">All</option>
              <option value="LOW">LOW</option>
              <option value="MEDIUM">MEDIUM</option>
              <option value="HIGH">HIGH</option>
              <option value="CRITICAL">CRITICAL</option>
            </select>
          </div>

          <div class="col-12 col-md-2 d-flex gap-2">
            <button type="submit" class="btn btn-primary btn-sm flex-grow-1" :disabled="isLoading">
              <i class="bi bi-funnel me-1"></i> Filter
            </button>
            <button type="button" @click="handleReset" class="btn btn-outline-secondary btn-sm" title="Reset Filters">
              <i class="bi bi-arrow-counterclockwise"></i>
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Requests Table -->
    <div class="card border-0 shadow-sm">
      <div class="card-header bg-white py-3 d-flex justify-content-between align-items-center">
        <h2 class="h6 fw-bold mb-0">
          Requests
          <span class="badge bg-secondary-subtle text-secondary ms-2">{{ totalCount }}</span>
        </h2>
        <button @click="loadRequests" class="btn btn-outline-secondary btn-sm" :disabled="isLoading" title="Refresh">
          <i class="bi bi-arrow-repeat" :class="{ 'spin-icon': isLoading }"></i>
        </button>
      </div>

      <div class="table-responsive">
        <table class="table table-hover align-middle mb-0">
          <thead class="table-light">
            <tr>
              <th scope="col" style="min-width: 250px;">Request</th>
              <th scope="col">Customer / Product</th>
              <th scope="col">Status</th>
              <th scope="col">Priority</th>
              <th scope="col">Assignee</th>
              <th scope="col">Created</th>
              <th scope="col" class="text-end">Action</th>
            </tr>
          </thead>
          <tbody>
            <!-- Loading Row -->
            <tr v-if="isLoading">
              <td colspan="7" class="text-center py-5 text-secondary">
                <div class="spinner-border spinner-border-sm text-primary me-2" role="status"></div>
                Loading requests...
              </td>
            </tr>

            <!-- Empty Row -->
            <tr v-else-if="requests.length === 0">
              <td colspan="7" class="text-center py-5 text-secondary">
                <i class="bi bi-inbox fs-2 d-block mb-2 text-muted"></i>
                <div class="fw-semibold">No requests found</div>
                <div class="small text-muted mb-3">Adjust your search filters or record a new customer request.</div>
                <RouterLink to="/requests/new" class="btn btn-primary btn-sm">
                  <i class="bi bi-plus-lg me-1"></i> Record New Request
                </RouterLink>
              </td>
            </tr>

            <!-- Data Rows -->
            <tr v-for="req in requests" :key="req.requestId">
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
              <td>
                <div class="small fw-semibold text-dark">{{ req.ownerName || 'Unassigned' }}</div>
                <div class="text-secondary" style="font-size: 0.75rem;">
                  Req: {{ req.requesterName || 'System' }}
                </div>
              </td>
              <td class="small text-secondary">
                {{ formatDate(req.createdAt) }}
              </td>
              <td class="text-end">
                <RouterLink :to="`/requests/${req.requestId}`" class="btn btn-outline-primary btn-sm">
                  View <i class="bi bi-chevron-right ms-1"></i>
                </RouterLink>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Pagination Footer -->
      <div v-if="totalCount > pageSize" class="card-footer bg-white d-flex justify-content-between align-items-center py-2">
        <div class="small text-secondary">
          Showing {{ (currentPage - 1) * pageSize + 1 }} to
          {{ Math.min(currentPage * pageSize, totalCount) }} of {{ totalCount }} requests
        </div>
        <div class="btn-group btn-group-sm">
          <button
            class="btn btn-outline-secondary"
            :disabled="currentPage === 1 || isLoading"
            @click="goToPage(currentPage - 1)"
          >
            <i class="bi bi-chevron-left"></i> Previous
          </button>
          <button
            class="btn btn-outline-secondary"
            :disabled="currentPage * pageSize >= totalCount || isLoading"
            @click="goToPage(currentPage + 1)"
          >
            Next <i class="bi bi-chevron-right"></i>
          </button>
        </div>
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
