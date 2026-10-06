<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import {
  getWorkInProgressOverview,
  type PersonWorkInProgressDto,
  type TaskWorkInProgressDto,
} from '@/api/requests'

/**
 * SCR-REQ-006: Work in Progress (WIP) Tracking Dashboard
 * (Architecture §4 TD-005, TD-006; GAP-004, GAP-005, GAP-006; CR-021).
 *
 * - Renders page header with title "Work in Progress" and summary stats:
 *   total active people, total in-progress tasks, total paused tasks.
 * - Renders person cards sorted with active owners first, followed by paused-only owners.
 * - For each person:
 *   - Displays Person Name and active tasks count badge.
 *   - Single IN_PROGRESS spotlight card: task title, priority, customer/product,
 *     formatted elapsed time badge (bi-clock-history Xh Ym, with decimal hours in tooltip),
 *     and router link to /requests/${id}. If idle, displays clean placeholder.
 *   - PAUSED tasks section: list of paused tasks with title, priority, customer,
 *     formatted elapsed time badge (bi-pause-circle Xh Ym), paused timestamp, and router link.
 */

interface ProblemDetailsPayload {
  title?: string
  detail?: string
  errorCode?: string
  errors?: Record<string, string[]>
}

const router = useRouter()
const persons = ref<PersonWorkInProgressDto[]>([])
const isLoading = ref(false)
const errorMessage = ref<string | null>(null)

const totalActivePeople = computed(() => persons.value.length)

const totalInProgressTasks = computed(() =>
  persons.value.reduce((acc, p) => acc + (p.inProgressTask ? 1 : 0), 0),
)

const totalPausedTasks = computed(() =>
  persons.value.reduce((acc, p) => acc + (p.pausedTasks ? p.pausedTasks.length : 0), 0),
)

// Active owners first (alphabetical by name), followed by paused-only owners (alphabetical by name)
const sortedPersons = computed(() => {
  return [...persons.value].sort((a, b) => {
    const aActive = a.inProgressTask ? 1 : 0
    const bActive = b.inProgressTask ? 1 : 0
    if (aActive !== bActive) {
      return bActive - aActive
    }
    return (a.personName || '').localeCompare(b.personName || '')
  })
})

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

async function loadWipOverview(): Promise<void> {
  isLoading.value = true
  errorMessage.value = null

  try {
    const data = await getWorkInProgressOverview()
    persons.value = data
  } catch (err) {
    errorMessage.value = extractErrorMessage(err, 'Failed to load Work in Progress overview.')
  } finally {
    isLoading.value = false
  }
}

function priorityBadgeClass(priority: string): string {
  switch ((priority ?? '').toUpperCase()) {
    case 'URGENT':
      return 'text-bg-danger'
    case 'HIGH':
      return 'text-bg-warning text-dark'
    case 'LOW':
      return 'text-bg-light border text-secondary'
    default:
      return 'text-bg-light border text-body-secondary'
  }
}

function resolveCustomerDisplay(task: TaskWorkInProgressDto): string {
  if (task.customerName && task.customerName.trim().length > 0) {
    return task.customerCode ? `${task.customerName} (${task.customerCode})` : task.customerName
  }
  if (task.customerCode && task.customerCode.trim().length > 0) {
    return task.customerCode
  }
  return task.customerId ?? '—'
}

function formatTimestamp(value: string | null | undefined): string {
  if (!value) return '—'
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return value
  return parsed.toLocaleString()
}

function navigateToDetail(requestId: string): void {
  router.push(`/requests/${requestId}`)
}

onMounted(async () => {
  await loadWipOverview()
})
</script>

<template>
  <section data-screen-id="SCR-REQ-006" data-testid="wip-screen">
    <!-- Screen Header Bar -->
    <div class="op-screen-header">
      <div class="d-flex align-items-center gap-2">
        <h1 class="op-screen-title">
          <i class="bi bi-hourglass-split text-primary" aria-hidden="true"></i>
          Work in Progress
        </h1>
        <span class="badge text-bg-light border text-secondary font-monospace">SCR-REQ-006</span>
      </div>

      <div class="d-flex align-items-center gap-2">
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm"
          :disabled="isLoading"
          data-testid="refresh-wip-button"
          @click="loadWipOverview"
        >
          <i class="bi bi-arrow-clockwise me-1" :class="{ 'spin-icon': isLoading }" aria-hidden="true"></i>
          Refresh
        </button>
      </div>
    </div>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      role="alert"
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center gap-2 py-1.5 px-3 mb-2"
      data-testid="wip-error-alert"
    >
      <i class="bi bi-exclamation-triangle-fill flex-shrink-0" aria-hidden="true"></i>
      <div>{{ errorMessage }}</div>
      <button
        type="button"
        class="btn-close py-1.5 px-2"
        aria-label="Close"
        @click="errorMessage = null"
      ></button>
    </div>

    <!-- Summary KPI Cards Ribbon -->
    <div class="row g-2 mb-3" data-testid="wip-kpis">
      <div class="col-12 col-sm-4">
        <div class="card border p-2.5 h-100 bg-white shadow-xs">
          <div class="d-flex align-items-center justify-content-between text-body-secondary mb-1">
            <span class="fs-11 fw-semibold text-uppercase">Active People</span>
            <i class="bi bi-people-fill text-primary" aria-hidden="true"></i>
          </div>
          <div class="d-flex align-items-baseline gap-1.5">
            <span class="fs-4 fw-bold text-dark" data-testid="kpi-active-people">{{ totalActivePeople }}</span>
            <span class="fs-11 text-body-secondary">with ongoing work</span>
          </div>
        </div>
      </div>
      <div class="col-12 col-sm-4">
        <div class="card border p-2.5 h-100 bg-white shadow-xs">
          <div class="d-flex align-items-center justify-content-between text-body-secondary mb-1">
            <span class="fs-11 fw-semibold text-uppercase">In-Progress Tasks</span>
            <i class="bi bi-lightning-charge-fill text-info" aria-hidden="true"></i>
          </div>
          <div class="d-flex align-items-baseline gap-1.5">
            <span class="fs-4 fw-bold text-primary" data-testid="kpi-in-progress-tasks">{{ totalInProgressTasks }}</span>
            <span class="fs-11 text-body-secondary">currently working</span>
          </div>
        </div>
      </div>
      <div class="col-12 col-sm-4">
        <div class="card border p-2.5 h-100 bg-white shadow-xs">
          <div class="d-flex align-items-center justify-content-between text-body-secondary mb-1">
            <span class="fs-11 fw-semibold text-uppercase">Paused Tasks</span>
            <i class="bi bi-pause-circle-fill text-warning" aria-hidden="true"></i>
          </div>
          <div class="d-flex align-items-baseline gap-1.5">
            <span class="fs-4 fw-bold text-warning" data-testid="kpi-paused-tasks">{{ totalPausedTasks }}</span>
            <span class="fs-11 text-body-secondary">suspended work</span>
          </div>
        </div>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="isLoading && persons.length === 0" class="text-center py-5 text-body-secondary" data-testid="wip-loading">
      <span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
      Loading Work in Progress...
    </div>

    <!-- Empty State -->
    <div
      v-else-if="!isLoading && sortedPersons.length === 0"
      class="text-center py-5 text-body-secondary border rounded bg-white shadow-xs"
      data-testid="wip-empty-state"
    >
      <i class="bi bi-check2-circle text-success fs-1 mb-2 d-block" aria-hidden="true"></i>
      <h6 class="fw-bold text-dark">No Active Work in Progress</h6>
      <p class="text-muted small mb-0">
        There are currently no active or paused tasks across operations.
      </p>
    </div>

    <!-- Persons Work in Progress Grid -->
    <div v-else class="row g-3" data-testid="wip-persons-container">
      <div
        v-for="person in sortedPersons"
        :key="person.personId"
        class="col-12 col-xl-6"
        data-testid="person-card"
        :data-person-id="person.personId"
      >
        <div class="card h-100 border shadow-xs d-flex flex-column">
          <!-- Person Header -->
          <div class="card-header bg-body-tertiary py-2 px-3 d-flex align-items-center justify-content-between">
            <div class="d-flex align-items-center gap-2 overflow-hidden">
              <div class="op-avatar-circle flex-shrink-0">
                <i class="bi bi-person-fill" aria-hidden="true"></i>
              </div>
              <div class="overflow-hidden">
                <h6 class="fw-bold mb-0 text-dark text-truncate" :title="person.personName">
                  {{ person.personName }}
                </h6>
                <span v-if="person.email" class="text-muted fs-11 text-truncate d-block" :title="person.email">
                  {{ person.email }}
                </span>
              </div>
            </div>

            <div class="d-flex align-items-center gap-1.5 flex-shrink-0 ms-2">
              <span
                v-if="person.inProgressTask"
                class="badge bg-success bg-opacity-10 text-success border border-success border-opacity-25 d-inline-flex align-items-center gap-1"
                data-testid="person-status-active"
              >
                <span class="spinner-grow spinner-grow-sm text-success" style="width: 0.35rem; height: 0.35rem;"></span>
                Working
              </span>
              <span
                v-else
                class="badge bg-secondary bg-opacity-10 text-secondary border border-secondary border-opacity-25"
                data-testid="person-status-paused"
              >
                Paused Only
              </span>

              <span class="badge text-bg-secondary" data-testid="person-tasks-count-badge">
                {{ person.totalActiveTasksCount }} active
              </span>
            </div>
          </div>

          <!-- Person Body -->
          <div class="card-body p-3 d-flex flex-column gap-3">
            <!-- Single IN_PROGRESS Spotlight Section -->
            <div class="in-progress-section">
              <div class="d-flex align-items-center justify-content-between mb-1.5">
                <span class="fs-11 fw-bold text-uppercase text-body-secondary">
                  <i class="bi bi-lightning-charge-fill text-primary me-1"></i>
                  In Progress Spotlight
                </span>
              </div>

              <!-- Active Task Spotlight Card -->
              <div
                v-if="person.inProgressTask"
                class="card border-primary border-2 bg-primary bg-opacity-10 p-2.5 shadow-xs"
                style="cursor: pointer; transition: transform 0.15s ease, box-shadow 0.15s ease;"
                data-testid="in-progress-card"
                @click="navigateToDetail(person.inProgressTask.requestId)"
              >
                <div class="d-flex align-items-center justify-content-between mb-1.5">
                  <div class="d-flex align-items-center gap-1.5">
                    <router-link
                      :to="`/requests/${person.inProgressTask.requestId}`"
                      class="font-monospace fw-bold text-primary text-decoration-none small"
                      data-testid="task-id-link"
                      @click.stop
                    >
                      {{ person.inProgressTask.requestId }}
                    </router-link>
                    <span
                      class="badge"
                      :class="priorityBadgeClass(person.inProgressTask.priority)"
                      data-testid="task-priority-badge"
                    >
                      {{ person.inProgressTask.priority }}
                    </span>
                  </div>

                  <span
                    class="badge bg-primary text-white d-inline-flex align-items-center gap-1"
                    :title="`${person.inProgressTask.totalInProgressHours} hrs`"
                    data-testid="in-progress-time-badge"
                  >
                    <i class="bi bi-clock-history" aria-hidden="true"></i>
                    {{ person.inProgressTask.totalInProgressFormatted }}
                  </span>
                </div>

                <h6 class="fw-bold mb-1 text-truncate" :title="person.inProgressTask.title">
                  <router-link
                    :to="`/requests/${person.inProgressTask.requestId}`"
                    class="text-dark text-decoration-none hover-underline"
                    data-testid="task-title-link"
                    @click.stop
                  >
                    {{ person.inProgressTask.title }}
                  </router-link>
                </h6>

                <p
                  v-if="person.inProgressTask.description"
                  class="text-body-secondary small mb-2 text-truncate"
                  style="font-size: 11.5px;"
                >
                  {{ person.inProgressTask.description }}
                </p>

                <div class="d-flex align-items-center justify-content-between flex-wrap gap-2 fs-11 text-body-secondary mt-auto pt-2 border-top border-primary border-opacity-25">
                  <div class="d-flex align-items-center gap-1 flex-wrap">
                    <span
                      class="badge text-bg-light border text-secondary"
                      :title="resolveCustomerDisplay(person.inProgressTask)"
                      data-testid="task-customer-badge"
                    >
                      <i class="bi bi-building me-1"></i>{{ resolveCustomerDisplay(person.inProgressTask) }}
                    </span>
                    <span
                      v-if="person.inProgressTask.productId"
                      class="badge text-bg-light border text-secondary"
                      :title="`Product: ${person.inProgressTask.productId}`"
                    >
                      <i class="bi bi-box me-1"></i>{{ person.inProgressTask.productId }}
                    </span>
                    <span
                      v-if="person.inProgressTask.requestType"
                      class="badge text-bg-light border text-secondary"
                    >
                      {{ person.inProgressTask.requestType }}
                    </span>
                  </div>

                  <div class="d-flex align-items-center gap-2">
                    <span
                      v-if="person.inProgressTask.lastStartedAt"
                      class="text-muted"
                      :title="formatTimestamp(person.inProgressTask.lastStartedAt)"
                    >
                      <i class="bi bi-play-circle me-1"></i>{{ formatTimestamp(person.inProgressTask.lastStartedAt) }}
                    </span>
                    <router-link
                      :to="`/requests/${person.inProgressTask.requestId}`"
                      class="btn btn-primary btn-sm py-0 px-2"
                      data-testid="task-view-link"
                      @click.stop
                    >
                      View
                    </router-link>
                  </div>
                </div>
              </div>

              <!-- Idle Placeholder -->
              <div
                v-else
                class="idle-placeholder border border-dashed rounded p-3 text-center text-muted bg-body-tertiary"
                data-testid="idle-placeholder"
              >
                <i class="bi bi-pause-circle text-secondary me-1" aria-hidden="true"></i>
                <span class="small">No task currently in progress</span>
              </div>
            </div>

            <!-- PAUSED Tasks Section -->
            <div class="paused-section">
              <div class="d-flex align-items-center justify-content-between mb-1.5">
                <span class="fs-11 fw-bold text-uppercase text-body-secondary">
                  <i class="bi bi-pause-circle-fill text-warning me-1"></i>
                  Paused Tasks ({{ person.pausedTasks ? person.pausedTasks.length : 0 }})
                </span>
              </div>

              <div
                v-if="!person.pausedTasks || person.pausedTasks.length === 0"
                class="text-muted small fst-italic ps-1"
                data-testid="empty-paused-tasks"
              >
                No paused tasks
              </div>

              <div
                v-else
                class="d-flex flex-column gap-2"
                data-testid="paused-tasks-list"
              >
                <div
                  v-for="task in person.pausedTasks"
                  :key="task.requestId"
                  class="card border p-2 bg-white shadow-xs"
                  style="cursor: pointer; transition: transform 0.15s ease, box-shadow 0.15s ease;"
                  data-testid="paused-task-card"
                  @click="navigateToDetail(task.requestId)"
                >
                  <div class="d-flex align-items-center justify-content-between mb-1">
                    <div class="d-flex align-items-center gap-1.5">
                      <router-link
                        :to="`/requests/${task.requestId}`"
                        class="font-monospace fw-bold text-primary text-decoration-none small"
                        data-testid="paused-task-id-link"
                        @click.stop
                      >
                        {{ task.requestId }}
                      </router-link>
                      <span
                        class="badge"
                        :class="priorityBadgeClass(task.priority)"
                        data-testid="paused-task-priority-badge"
                      >
                        {{ task.priority }}
                      </span>
                    </div>

                    <span
                      class="badge text-bg-warning text-dark d-inline-flex align-items-center gap-1"
                      :title="`${task.totalInProgressHours} hrs`"
                      data-testid="paused-time-badge"
                    >
                      <i class="bi bi-pause-circle" aria-hidden="true"></i>
                      {{ task.totalInProgressFormatted }}
                    </span>
                  </div>

                  <h6 class="fw-semibold small mb-1 text-truncate" :title="task.title">
                    <router-link
                      :to="`/requests/${task.requestId}`"
                      class="text-dark text-decoration-none hover-underline"
                      data-testid="paused-task-title-link"
                      @click.stop
                    >
                      {{ task.title }}
                    </router-link>
                  </h6>

                  <div class="d-flex align-items-center justify-content-between flex-wrap gap-2 fs-11 text-body-secondary mt-1 pt-1 border-top">
                    <div class="d-flex align-items-center gap-1 flex-wrap">
                      <span
                        class="badge text-bg-light border text-secondary"
                        :title="resolveCustomerDisplay(task)"
                        data-testid="paused-task-customer-badge"
                      >
                        <i class="bi bi-building me-1"></i>{{ resolveCustomerDisplay(task) }}
                      </span>
                      <span
                        v-if="task.productId"
                        class="badge text-bg-light border text-secondary"
                        :title="`Product: ${task.productId}`"
                      >
                        <i class="bi bi-box me-1"></i>{{ task.productId }}
                      </span>
                    </div>

                    <div class="d-flex align-items-center gap-2">
                      <span
                        class="text-muted"
                        :title="`Paused since ${formatTimestamp(task.updatedAt || task.createdAt)}`"
                      >
                        <i class="bi bi-clock me-1"></i>{{ formatTimestamp(task.updatedAt || task.createdAt) }}
                      </span>
                      <router-link
                        :to="`/requests/${task.requestId}`"
                        class="btn btn-outline-secondary btn-sm py-0 px-2"
                        data-testid="paused-task-view-link"
                        @click.stop
                      >
                        View
                      </router-link>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
.hover-underline:hover {
  text-decoration: underline !important;
}

.spin-icon {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  100% {
    transform: rotate(360deg);
  }
}
</style>
