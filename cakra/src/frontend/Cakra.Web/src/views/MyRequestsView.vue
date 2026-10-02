<script setup lang="ts">
import { AxiosError } from 'axios'
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { httpClient } from '@/api/http'

/**
 * SCR-REQ-004: My Requests Screen
 * (Architecture §7, §8 — UC-COL-004, §9 — FEAT-COL-001..004, §19.4, §19.6, §20, §21).
 *
 * - Renders a Bootstrap 5 table of operational requests assigned to the current authenticated user.
 * - Calls `GET /api/v1/requests/my` (`RequestQueryService.ListMyAssignedRequests`).
 * - Provides navigation links to `/requests/${id}` (`SCR-REQ-003`) for each request.
 */

export interface AssignedRequestItem {
  id: string
  requestId?: string
  title: string
  description: string
  requestType: string
  status:
    | 'CAPTURED'
    | 'EVALUATING'
    | 'ACCEPTED'
    | 'REJECTED'
    | 'IN_PROGRESS'
    | 'ESCALATED'
    | 'COMPLETED'
    | string
  priority: 'LOW' | 'NORMAL' | 'HIGH' | 'URGENT' | string
  ownerPersonId: string | null
  assigneePersonId?: string | null
  ownerName?: string | null
  assigneeName?: string | null
  customerId: string | null
  customerName?: string | null
  customerCode?: string | null
  productId: string | null
  productName?: string | null
  productCode?: string | null
  workPackageId?: string | null
  createdAt: string
  updatedAt?: string | null
}

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const router = useRouter()

const myRequests = ref<AssignedRequestItem[]>([])
const isLoading = ref(false)
const errorMessage = ref<string | null>(null)

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

function statusBadgeClass(status: string): string {
  switch ((status ?? '').toUpperCase()) {
    case 'CAPTURED':
      return 'text-bg-secondary'
    case 'EVALUATING':
      return 'text-bg-info'
    case 'ACCEPTED':
      return 'text-bg-primary'
    case 'IN_PROGRESS':
      return 'text-bg-primary'
    case 'ESCALATED':
      return 'text-bg-warning'
    case 'COMPLETED':
      return 'text-bg-success'
    case 'REJECTED':
      return 'text-bg-danger'
    default:
      return 'text-bg-secondary'
  }
}

function priorityBadgeClass(priority: string): string {
  switch ((priority ?? '').toUpperCase()) {
    case 'URGENT':
      return 'text-bg-danger'
    case 'HIGH':
      return 'text-bg-warning'
    case 'LOW':
      return 'text-bg-light border text-secondary'
    default:
      return 'text-bg-light border text-body-secondary'
  }
}

function resolveCustomerDisplay(item: AssignedRequestItem): string {
  if (item.customerName && item.customerName.trim().length > 0) {
    return item.customerCode ? `${item.customerName} (${item.customerCode})` : item.customerName
  }
  if (item.customerCode && item.customerCode.trim().length > 0) {
    return item.customerCode
  }
  return item.customerId ?? '—'
}

function resolveProductDisplay(item: AssignedRequestItem): string {
  if (item.productName && item.productName.trim().length > 0) {
    return item.productCode ? `${item.productName} (${item.productCode})` : item.productName
  }
  if (item.productCode && item.productCode.trim().length > 0) {
    return item.productCode
  }
  return item.productId ?? '—'
}

function resolveAssigneeDisplay(item: AssignedRequestItem): string {
  if (item.assigneeName && item.assigneeName.trim().length > 0) {
    return item.assigneeName
  }
  if (item.ownerName && item.ownerName.trim().length > 0) {
    return item.ownerName
  }
  return item.assigneePersonId ?? item.ownerPersonId ?? 'Assigned to Me'
}

function formatTimestamp(value: string | null | undefined): string {
  if (!value) {
    return '—'
  }
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }
  return parsed.toLocaleString()
}

async function loadMyRequests(): Promise<void> {
  isLoading.value = true
  errorMessage.value = null

  try {
    const response = await httpClient.get<AssignedRequestItem[] | { items?: AssignedRequestItem[] }>(
      '/requests/my',
    )
    if (Array.isArray(response.data)) {
      myRequests.value = response.data
    } else {
      myRequests.value = response.data.items ?? []
    }
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load assigned requests.')
  } finally {
    isLoading.value = false
  }
}

async function navigateToDetail(id: string): Promise<void> {
  await router.push(`/requests/${id}`)
}

onMounted(async () => {
  await loadMyRequests()
})
</script>

<template>
  <section data-screen-id="SCR-REQ-004">
    <!-- Compact Screen Header -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2">
        <h1 class="op-screen-title">
          <i class="bi bi-person-workspace text-primary" aria-hidden="true"></i>
          My Assigned Requests
        </h1>
        <span class="badge text-bg-light border text-secondary font-monospace">SCR-REQ-004</span>
        <span class="badge text-bg-secondary ms-1" data-testid="my-requests-count">
          {{ myRequests.length }}
        </span>
      </div>

      <div class="d-flex align-items-center gap-2">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm"
          :disabled="isLoading"
          data-testid="refresh-my-requests-button"
          @click="loadMyRequests"
        >
          <i class="bi bi-arrow-clockwise me-1" aria-hidden="true"></i>
          Refresh
        </button>

        <router-link
          to="/requests"
          class="btn btn-outline-secondary btn-sm"
          data-testid="all-requests-link"
        >
          <i class="bi bi-card-checklist me-1" aria-hidden="true"></i>
          All Requests
        </router-link>

        <router-link
          to="/requests/create"
          class="btn btn-primary btn-sm"
          data-testid="create-request-link"
        >
          <i class="bi bi-plus-lg me-1" aria-hidden="true"></i>
          Create Request
        </router-link>
      </div>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      role="alert"
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center gap-2 py-1 px-2 mb-2"
      data-testid="my-requests-error-alert"
    >
      <i class="bi bi-exclamation-triangle-fill flex-shrink-0" aria-hidden="true"></i>
      <div>{{ errorMessage }}</div>
      <button
        type="button"
        class="btn-close py-1 px-2"
        aria-label="Close"
        @click="errorMessage = null"
      ></button>
    </div>

    <!-- High-Density My Assigned Requests Table Card -->
    <div class="card border">
      <div class="table-responsive">
        <table
          class="table table-hover align-middle mb-0"
          data-testid="my-requests-table"
        >
          <thead>
            <tr>
              <th scope="col" style="width: 105px;">ID</th>
              <th scope="col">Title</th>
              <th scope="col" style="width: 150px;">Customer</th>
              <th scope="col" style="width: 140px;">Product</th>
              <th scope="col" style="width: 105px;">Status</th>
              <th scope="col" style="width: 140px;">Assignee</th>
              <th scope="col" style="width: 125px;">CreatedAt</th>
              <th scope="col" style="width: 90px;" class="text-end">Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="isLoading">
              <td colspan="8" class="text-center py-4 text-body-secondary">
                <span
                  class="spinner-border spinner-border-sm me-2"
                  role="status"
                  aria-hidden="true"
                ></span>
                Loading assigned requests...
              </td>
            </tr>

            <tr v-else-if="myRequests.length === 0">
              <td
                colspan="8"
                class="text-center py-4 text-body-secondary"
                data-testid="empty-my-requests-row"
              >
                You currently have no assigned operational requests.
              </td>
            </tr>

            <tr
              v-for="req in myRequests"
              v-else
              :key="req.id"
              :data-request-id="req.id"
              style="cursor: pointer"
              data-testid="my-request-row"
              @click="navigateToDetail(req.id)"
            >
              <td>
                <router-link
                  :to="`/requests/${req.id}`"
                  class="font-monospace text-decoration-none fw-semibold"
                  style="font-size: 11.5px;"
                  data-testid="my-request-id-link"
                  @click.stop
                >
                  {{ req.id }}
                </router-link>
              </td>

              <td>
                <div class="d-flex align-items-center gap-1.5 flex-nowrap">
                  <router-link
                    :to="`/requests/${req.id}`"
                    class="fw-semibold text-decoration-none text-dark text-truncate"
                    style="max-width: 360px;"
                    data-testid="my-request-title-link"
                    :title="req.title"
                    @click.stop
                  >
                    {{ req.title }}
                  </router-link>
                  <span
                    v-if="req.priority"
                    class="badge flex-shrink-0"
                    :class="priorityBadgeClass(req.priority)"
                  >
                    {{ req.priority }}
                  </span>
                </div>
              </td>

              <td>
                <span class="text-truncate d-inline-block" style="max-width: 145px;" :title="resolveCustomerDisplay(req)">
                  {{ resolveCustomerDisplay(req) }}
                </span>
              </td>

              <td>
                <span class="text-truncate d-inline-block" style="max-width: 135px;" :title="resolveProductDisplay(req)">
                  {{ resolveProductDisplay(req) }}
                </span>
              </td>

              <td>
                <span
                  class="badge"
                  :class="statusBadgeClass(req.status)"
                  data-testid="my-request-status-badge"
                >
                  {{ req.status }}
                </span>
              </td>

              <td>
                <span class="text-truncate d-inline-block" style="max-width: 135px;">
                  <i class="bi bi-person me-0.5 text-secondary" aria-hidden="true"></i>
                  {{ resolveAssigneeDisplay(req) }}
                </span>
              </td>

              <td class="text-body-secondary fs-11">
                {{ formatTimestamp(req.createdAt) }}
              </td>

              <td class="text-end" @click.stop>
                <router-link
                  :to="`/requests/${req.id}`"
                  class="btn btn-outline-primary btn-sm py-0 px-1.5 fs-11"
                  data-testid="my-request-detail-button"
                  @click.stop
                >
                  View
                </router-link>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>
