<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import {
  requestService,
  type CustomerLookupDto,
  type ProductLookupDto,
  type PersonLookupDto
} from '@/services/requestService'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

// State
const customers = ref<CustomerLookupDto[]>([])
const products = ref<ProductLookupDto[]>([])
const persons = ref<PersonLookupDto[]>([])
const isLoadingLookups = ref(false)
const isSubmitting = ref(false)
const errorMessage = ref<string | null>(null)

// Form
const form = ref({
  title: '',
  description: '',
  type: 'FEATURE',
  priority: 'MEDIUM',
  customerId: '',
  productId: '',
  initialOwnerPersonId: '',
  requesterName: ''
})

async function loadDropdowns() {
  isLoadingLookups.value = true
  errorMessage.value = null
  try {
    const [custData, prodData, personData] = await Promise.all([
      requestService.getActiveCustomers(),
      requestService.getActiveProducts(),
      requestService.getActivePersons()
    ])
    customers.value = custData
    products.value = prodData
    persons.value = personData

    if (authStore.user?.username && !form.value.requesterName) {
      form.value.requesterName = authStore.user.username
    }
  } catch (err: any) {
    errorMessage.value = 'Failed to load master data dropdowns. Please refresh.'
  } finally {
    isLoadingLookups.value = false
  }
}

async function handleSubmit() {
  if (!form.value.title.trim()) {
    errorMessage.value = 'Please provide a Request Title.'
    return
  }
  if (!form.value.description.trim()) {
    errorMessage.value = 'Please provide a Request Description.'
    return
  }

  isSubmitting.value = true
  errorMessage.value = null

  try {
    const created = await requestService.createRequest({
      title: form.value.title.trim(),
      description: form.value.description.trim(),
      type: form.value.type,
      priority: form.value.priority,
      customerId: form.value.customerId || undefined,
      productId: form.value.productId || undefined,
      initialOwnerPersonId: form.value.initialOwnerPersonId || undefined,
      requesterName: form.value.requesterName.trim() || undefined
    })

    await router.push(`/requests/${created.requestId}`)
  } catch (err: any) {
    errorMessage.value =
      err.response?.data?.detail ||
      err.response?.data?.title ||
      'Failed to record request. Please check required fields.'
  } finally {
    isSubmitting.value = false
  }
}

onMounted(() => {
  loadDropdowns()
})
</script>

<template>
  <div class="container py-4" style="max-width: 860px;">
    <!-- Navigation Back Link -->
    <div class="mb-3">
      <RouterLink to="/requests" class="text-decoration-none text-secondary small d-inline-flex align-items-center gap-1">
        <i class="bi bi-arrow-left"></i>
        <span>Back to Request List</span>
      </RouterLink>
    </div>

    <!-- Header -->
    <div class="d-flex align-items-center gap-2 mb-1">
      <h1 class="h3 fw-bold mb-0">Record New Request</h1>
      <span class="badge bg-primary-subtle text-primary border border-primary-subtle font-monospace">
        SCR-REQ-002
      </span>
    </div>
    <p class="text-secondary small mb-4">
      Capture a customer operational request and route to evaluation.
    </p>

    <!-- Error Alert -->
    <div v-if="errorMessage" class="alert alert-danger alert-dismissible fade show" role="alert">
      <i class="bi bi-exclamation-triangle-fill me-2"></i>
      {{ errorMessage }}
      <button type="button" class="btn-close" @click="errorMessage = null" aria-label="Close"></button>
    </div>

    <!-- Form Card -->
    <div class="card border-0 shadow-sm">
      <div class="card-body p-4">
        <form @submit.prevent="handleSubmit">
          <!-- Title -->
          <div class="mb-3">
            <label class="form-label fw-semibold text-dark">
              Request Title <span class="text-danger">*</span>
            </label>
            <input
              v-model="form.title"
              type="text"
              class="form-control"
              placeholder="e.g. Add monthly patient billing reconciliation export"
              required
              maxlength="200"
            />
            <div class="form-text">Brief, clear summary of what is requested.</div>
          </div>

          <!-- Type & Priority Row -->
          <div class="row g-3 mb-3">
            <div class="col-md-6">
              <label class="form-label fw-semibold text-dark">
                Request Type <span class="text-danger">*</span>
              </label>
              <select v-model="form.type" class="form-select" required>
                <option value="FEATURE">Feature Request</option>
                <option value="BUG">Defect / Bug Report</option>
                <option value="ENHANCEMENT">Enhancement</option>
                <option value="SUPPORT">Operational Support</option>
                <option value="CHANGE_REQUEST">Change Request</option>
              </select>
            </div>

            <div class="col-md-6">
              <label class="form-label fw-semibold text-dark">Priority</label>
              <select v-model="form.priority" class="form-select">
                <option value="LOW">Low</option>
                <option value="MEDIUM">Medium</option>
                <option value="HIGH">High</option>
                <option value="CRITICAL">Critical</option>
              </select>
            </div>
          </div>

          <!-- Customer & Product Row -->
          <div class="row g-3 mb-3">
            <div class="col-md-6">
              <label class="form-label fw-semibold text-dark">Customer</label>
              <select v-model="form.customerId" class="form-select" :disabled="isLoadingLookups">
                <option value="">-- Optional / Internal --</option>
                <option v-for="c in customers" :key="c.customerId" :value="c.customerId">
                  {{ c.name }} ({{ c.code }})
                </option>
              </select>
            </div>

            <div class="col-md-6">
              <label class="form-label fw-semibold text-dark">Target Product</label>
              <select v-model="form.productId" class="form-select" :disabled="isLoadingLookups">
                <option value="">-- Optional / General --</option>
                <option v-for="p in products" :key="p.productId" :value="p.productId">
                  {{ p.name }} ({{ p.code }})
                </option>
              </select>
            </div>
          </div>

          <!-- Assignee & Requester Row -->
          <div class="row g-3 mb-3">
            <div class="col-md-6">
              <label class="form-label fw-semibold text-dark">Initial Assignee</label>
              <select v-model="form.initialOwnerPersonId" class="form-select" :disabled="isLoadingLookups">
                <option value="">-- Assign Later in Triage --</option>
                <option v-for="person in persons" :key="person.personId" :value="person.personId">
                  {{ person.name }} ({{ person.primaryRole || 'Staff' }})
                </option>
              </select>
              <div class="form-text">If left empty, request can be assigned during triage.</div>
            </div>

            <div class="col-md-6">
              <label class="form-label fw-semibold text-dark">Requester Reference</label>
              <input
                v-model="form.requesterName"
                type="text"
                class="form-control"
                placeholder="Name or stakeholder contact"
              />
            </div>
          </div>

          <!-- Description -->
          <div class="mb-4">
            <label class="form-label fw-semibold text-dark">
              Detailed Description <span class="text-danger">*</span>
            </label>
            <textarea
              v-model="form.description"
              rows="6"
              class="form-control"
              placeholder="Provide background, steps to reproduce, user story, or acceptance criteria..."
              required
            ></textarea>
          </div>

          <!-- Actions -->
          <div class="d-flex justify-content-end gap-2 pt-3 border-top">
            <RouterLink to="/requests" class="btn btn-outline-secondary">
              Cancel
            </RouterLink>
            <button
              type="submit"
              class="btn btn-primary d-flex align-items-center gap-2"
              :disabled="isSubmitting"
            >
              <span v-if="isSubmitting" class="spinner-border spinner-border-sm" role="status"></span>
              <i v-else class="bi bi-check2"></i>
              <span>Record Request</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>
