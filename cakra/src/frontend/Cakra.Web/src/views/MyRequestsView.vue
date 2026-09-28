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
  <section class="container-fluid py-2" data-screen-id="SCR-REQ-004">
    <!-- Screen Header -->
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-3 mb-4">
      <div>
        <div class="d-flex align-items-center gap-2">
          <h1 class="h3 mb-0 fw-bold">My Assigned Requests</h1>
          <span class="badge text-bg-light border text-secondary">SCR-REQ-004</span>
        </div>
        <p class="text-body-secondary small mb-0 mt-1">
          Review and manage operational requests currently assigned to your personal queue.
        </p>
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
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center gap-2 mb-4"
      data-testid="my-requests-error-alert"
    >
      <i class="bi bi-exclamation-triangle-fill flex-shrink-0" aria-hidden="true"></i>
      <div>{{ errorMessage }}</div>
      <button
        type="button"
        class="btn-close"
        aria-label="Close"
        @click="errorMessage = null"
      ></button>
    </div>

    <!-- My Assigned Requests Table Card -->
    <div class="card shadow-sm border-0">
      <div class="card-header bg-body-tertiary py-3 d-flex justify-content-between align-items-center">
        <span class="fw-semibold">
          <i class="bi bi-person-workspace me-2 text-primary" aria-hidden="true"></i>
          Assigned Requests
        </span>
        <span class="badge text-bg-secondary" data-testid="my-requests-count">
          {{ myRequests.length }}
        </span>
      </div>

      <div class="card-body p-0">
        <div class="table-responsive">
          <table
            class="table table-hover align-middle mb-0"
            data-testid="my-requests-table"
          >
            <thead class="table-light">
              <tr>
                <th scope="col" class="ps-4">ID</th>
                <th scope="col">Title</th>
                <th scope="col">Customer</th>
                <th scope="col">Product</th>
                <th scope="col">Status</th>
                <th scope="col">Assignee</th>
                <th scope="col">CreatedAt</th>
                <th scope="col" class="pe-4 text-end">Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="isLoading">
                <td colspan="8" class="text-center py-5 text-body-secondary">
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
                  class="text-center py-5 text-body-secondary"
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
                <td class="ps-4">
                  <router-link
                    :to="`/requests/${req.id}`"
                    class="font-monospace small text-decoration-none"
                    data-testid="my-request-id-link"
                    @click.stop
                  >
                    {{ req.id }}
                  </router-link>
                </td>

                <td>
                  <div class="d-flex align-items-center gap-2 flex-wrap">
                    <router-link
                      :to="`/requests/${req.id}`"
                      class="fw-semibold text-decoration-none text-body"
                      data-testid="my-request-title-link"
                      @click.stop
                    >
                      {{ req.title }}
                    </router-link>
                    <span
                      v-if="req.priority"
                      class="badge"
                      :class="priorityBadgeClass(req.priority)"
                    >
                      {{ req.priority }}
                    </span>
                  </div>
                </td>

                <td>
                  {{ resolveCustomerDisplay(req) }}
                </td>

                <td>
                  {{ resolveProductDisplay(req) }}
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
                  <span>
                    <i class="bi bi-person me-1 text-secondary" aria-hidden="true"></i>
                    {{ resolveAssigneeDisplay(req) }}
                  </span>
                </td>

                <td class="text-body-secondary small">
                  {{ formatTimestamp(req.createdAt) }}
                </td>

                <td class="pe-4 text-end">
                  <router-link
                    :to="`/requests/${req.id}`"
                    class="btn btn-outline-primary btn-sm"
                    data-testid="my-request-detail-button"
                    @click.stop
                  >
                    View Detail
                  </router-link>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </section>
</template>
