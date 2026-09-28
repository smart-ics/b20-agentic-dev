<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import {
  requestService,
  type RequestSummaryDto,
  type RequestStateHistoryDto,
  type CustomerLookupDto,
  type ProductLookupDto,
  type PersonLookupDto
} from '@/services/requestService'

// State
const requests = ref<RequestSummaryDto[]>([])
const customers = ref<CustomerLookupDto[]>([])
const products = ref<ProductLookupDto[]>([])
const persons = ref<PersonLookupDto[]>([])

const totalCount = ref(0)
const isLoading = ref(false)
const errorMessage = ref<string | null>(null)

// Search Filters
const filter = ref({
  searchTerm: '',
  customerId: '',
  productId: '',
  ownerPersonId: '',
  statuses: '',
  priority: '',
  fromDate: '',
  toDate: '',
  skip: 0,
  take: 25
})

// Audit Trail Modal State
const selectedRequest = ref<RequestSummaryDto | null>(null)
const auditHistory = ref<RequestStateHistoryDto[]>([])
const isLoadingHistory = ref(false)
const showHistoryModal = ref(false)

async function loadDropdowns() {
  try {
    const [c, p, pers] = await Promise.all([
      requestService.getActiveCustomers(),
      requestService.getActiveProducts(),
      requestService.getActivePersons()
    ])
    customers.value = c
    products.value = p
    persons.value = pers
  } catch (err: any) {
    console.error('Failed to load search dropdowns', err)
  }
}

async function executeSearch() {
  isLoading.value = true
  errorMessage.value = null
  try {
    const result = await requestService.getRequests({
      searchTerm: filter.value.searchTerm.trim() || undefined,
      customerId: filter.value.customerId || undefined,
      productId: filter.value.productId || undefined,
      ownerPersonId: filter.value.ownerPersonId || undefined,
      statuses: filter.value.statuses || undefined,
      priority: filter.value.priority || undefined,
      fromDate: filter.value.fromDate || undefined,
      toDate: filter.value.toDate || undefined,
      skip: filter.value.skip,
      take: filter.value.take
    })
    requests.value = result.items
    totalCount.value = result.totalCount
  } catch (err: any) {
    errorMessage.value = err.response?.data?.detail || 'Failed to search request records.'
  } finally {
    isLoading.value = false
  }
}

function handleReset() {
  filter.value = {
    searchTerm: '',
    customerId: '',
    productId: '',
    ownerPersonId: '',
    statuses: '',
    priority: '',
    fromDate: '',
    toDate: '',
    skip: 0,
    take: 25
  }
  executeSearch()
}

async function openAuditTrail(req: RequestSummaryDto) {
  selectedRequest.value = req
  showHistoryModal.value = true
  isLoadingHistory.value = true
  try {
    auditHistory.value = await requestService.getRequestHistory(req.requestId)
  } catch (err: any) {
    console.error('Failed to fetch request history', err)
  } finally {
    isLoadingHistory.value = false
  }
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

function formatDate(iso?: string | null): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric'
  })
}

function formatDateTime(iso?: string | null): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

onMounted(async () => {
  await Promise.all([loadDropdowns(), executeSearch()])
})
</script>

<template>
  <div class="container py-4">
    <!-- Header -->
    <div class="d-flex flex-column flex-md-row justify-content-between align-items-md-center gap-3 mb-4">
      <div>
        <div class="d-flex align-items-center gap-2 mb-1">
          <h1 class="h3 fw-bold mb-0">Search & History</h1>
          <span class="badge bg-primary-subtle text-primary border border-primary-subtle font-monospace">
            SCR-REQ-005
          </span>
        </div>
        <p class="text-secondary small mb-0">
          Query historical request records across customer, product, date, and audit trail events.
        </p>
      </div>

      <div class="d-flex gap-2">
        <RouterLink to="/requests" class="btn btn-outline-secondary btn-sm">
          <i class="bi bi-list-ul me-1"></i> Request List
        </RouterLink>
      </div>
    </div>

    <!-- Error Alert -->
    <div v-if="errorMessage" class="alert alert-danger alert-dismissible fade show" role="alert">
      <i class="bi bi-exclamation-triangle-fill me-2"></i>
      {{ errorMessage }}
      <button type="button" class="btn-close" @click="errorMessage = null" aria-label="Close"></button>
    </div>

    <!-- Advanced Filter Accordion/Card -->
    <div class="card border-0 shadow-sm mb-4">
      <div class="card-header bg-white py-3">
        <h2 class="h6 fw-bold mb-0 text-dark">
          <i class="bi bi-sliders me-2 text-primary"></i>Historical Search Criteria
        </h2>
      </div>
      <div class="card-body">
        <form @submit.prevent="executeSearch">
          <div class="row g-3">
            <div class="col-md-4">
              <label class="form-label small fw-semibold text-secondary">Keywords</label>
              <input
                v-model="filter.searchTerm"
                type="text"
                class="form-control form-control-sm"
                placeholder="Title or description content..."
              />
            </div>

            <div class="col-md-4">
              <label class="form-label small fw-semibold text-secondary">Customer</label>
              <select v-model="filter.customerId" class="form-select form-select-sm">
                <option value="">All Customers</option>
                <option v-for="c in customers" :key="c.customerId" :value="c.customerId">
                  {{ c.name }} ({{ c.code }})
                </option>
              </select>
            </div>

            <div class="col-md-4">
              <label class="form-label small fw-semibold text-secondary">Product</label>
              <select v-model="filter.productId" class="form-select form-select-sm">
                <option value="">All Products</option>
                <option v-for="p in products" :key="p.productId" :value="p.productId">
                  {{ p.name }} ({{ p.code }})
                </option>
              </select>
            </div>

            <div class="col-md-3">
              <label class="form-label small fw-semibold text-secondary">Assigned Owner</label>
              <select v-model="filter.ownerPersonId" class="form-select form-select-sm">
                <option value="">All Owners</option>
                <option v-for="person in persons" :key="person.personId" :value="person.personId">
                  {{ person.name }}
                </option>
              </select>
            </div>

            <div class="col-md-3">
              <label class="form-label small fw-semibold text-secondary">Status</label>
              <select v-model="filter.statuses" class="form-select form-select-sm">
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

            <div class="col-md-3">
              <label class="form-label small fw-semibold text-secondary">From Date</label>
              <input v-model="filter.fromDate" type="date" class="form-control form-control-sm" />
            </div>

            <div class="col-md-3">
              <label class="form-label small fw-semibold text-secondary">To Date</label>
              <input v-model="filter.toDate" type="date" class="form-control form-control-sm" />
            </div>
          </div>

          <div class="d-flex justify-content-end gap-2 mt-3 pt-3 border-top">
            <button type="button" @click="handleReset" class="btn btn-outline-secondary btn-sm">
              <i class="bi bi-x-circle me-1"></i> Reset
            </button>
            <button type="submit" class="btn btn-primary btn-sm" :disabled="isLoading">
              <i class="bi bi-search me-1"></i> Search Records
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Results Table Card -->
    <div class="card border-0 shadow-sm">
      <div class="card-header bg-white py-3 d-flex justify-content-between align-items-center">
        <h2 class="h6 fw-bold mb-0">
          Query Results
          <span class="badge bg-secondary-subtle text-secondary ms-2">{{ totalCount }}</span>
        </h2>
      </div>

      <div class="table-responsive">
        <table class="table table-hover align-middle mb-0">
          <thead class="table-light">
            <tr>
              <th scope="col" style="min-width: 250px;">Request Record</th>
              <th scope="col">Customer / Product</th>
              <th scope="col">Status</th>
              <th scope="col">Owner</th>
              <th scope="col">Resolution</th>
              <th scope="col">Created</th>
              <th scope="col" class="text-end">History / Action</th>
            </tr>
          </thead>
          <tbody>
            <!-- Loading -->
            <tr v-if="isLoading">
              <td colspan="7" class="text-center py-5 text-secondary">
                <div class="spinner-border spinner-border-sm text-primary me-2" role="status"></div>
                Searching request history...
              </td>
            </tr>

            <!-- Empty -->
            <tr v-else-if="requests.length === 0">
              <td colspan="7" class="text-center py-5 text-secondary">
                <i class="bi bi-search fs-2 d-block mb-2 text-muted"></i>
                <div class="fw-semibold">No records match your query</div>
                <div class="small text-muted">Try relaxing your customer, product, or date range filters.</div>
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
                <div class="small text-muted font-monospace" style="font-size: 0.75rem;">
                  {{ req.requestId }}
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
              <td class="small">
                {{ req.ownerName || 'Unassigned' }}
              </td>
              <td>
                <span v-if="req.resolutionOutcome" class="badge bg-success-subtle text-success border border-success-subtle">
                  {{ req.resolutionOutcome }}
                </span>
                <span v-else class="text-muted small">—</span>
              </td>
              <td class="small text-secondary">
                {{ formatDate(req.createdAt) }}
              </td>
              <td class="text-end">
                <div class="btn-group btn-group-sm">
                  <button
                    type="button"
                    class="btn btn-outline-secondary"
                    @click="openAuditTrail(req)"
                    title="Inspect State Audit Trail"
                  >
                    <i class="bi bi-clock-history"></i>
                  </button>
                  <RouterLink :to="`/requests/${req.requestId}`" class="btn btn-outline-primary" title="Open Detail">
                    <i class="bi bi-box-arrow-up-right"></i>
                  </RouterLink>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- State Audit Trail Modal -->
    <div v-if="showHistoryModal" class="modal d-block bg-dark bg-opacity-50" tabindex="-1">
      <div class="modal-dialog modal-dialog-centered modal-lg">
        <div class="modal-content">
          <div class="modal-header">
            <div>
              <h5 class="modal-title fw-bold mb-0">State Audit Trail</h5>
              <div class="small text-muted">{{ selectedRequest?.title }}</div>
            </div>
            <button type="button" class="btn-close" @click="showHistoryModal = false"></button>
          </div>
          <div class="modal-body p-0">
            <div v-if="isLoadingHistory" class="text-center py-5 text-secondary">
              <div class="spinner-border spinner-border-sm text-primary me-2"></div>
              Loading audit timeline...
            </div>
            <div v-else-if="auditHistory.length === 0" class="text-center py-4 text-secondary">
              No audit records recorded for this request.
            </div>
            <div v-else class="table-responsive">
              <table class="table table-hover align-middle mb-0">
                <thead class="table-light">
                  <tr>
                    <th>Transition</th>
                    <th>Actor</th>
                    <th>Context / Reason</th>
                    <th>Timestamp</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="h in auditHistory" :key="h.stateHistoryId">
                    <td>
                      <span v-if="h.fromStatus" class="badge bg-light text-secondary border me-1">
                        {{ h.fromStatus }}
                      </span>
                      <i class="bi bi-arrow-right mx-1 text-secondary"></i>
                      <span class="badge" :class="getStatusBadgeClass(h.toStatus)">
                        {{ h.toStatus }}
                      </span>
                    </td>
                    <td class="fw-semibold small text-dark">{{ h.actorName || h.actorPersonId }}</td>
                    <td class="small">{{ h.reason || '—' }}</td>
                    <td class="small text-secondary">{{ formatDateTime(h.changedAt) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
          <div class="modal-footer">
            <button type="button" class="btn btn-secondary btn-sm" @click="showHistoryModal = false">Close</button>
            <RouterLink
              v-if="selectedRequest"
              :to="`/requests/${selectedRequest.requestId}`"
              class="btn btn-primary btn-sm"
            >
              Open Request Detail
            </RouterLink>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.hover-primary:hover {
  color: #0d6efd !important;
}
</style>
