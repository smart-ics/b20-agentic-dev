<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import {
  getWorkInProgressOverview,
  type PersonWorkInProgressDto,
  type TaskWorkInProgressDto,
} from '@/api/requests'
import BaseAvatar from '@/components/base/BaseAvatar.vue'
import BaseBadge from '@/components/base/BaseBadge.vue'
import BaseCard from '@/components/base/BaseCard.vue'
import PageHeader from '@/components/base/PageHeader.vue'
import SlideOverDrawer from '@/components/base/SlideOverDrawer.vue'
import StatusStrip, { type StatusStripItem } from '@/components/base/StatusStrip.vue'

/**
 * SCR-REQ-006: Work in Progress (WIP) Live Operational Awareness Board
 * (CR-021, CR-022; TD-005, TD-006; GAP-004, GAP-005, GAP-006).
 *
 * Modernized using Cakra UI Foundation base primitives:
 * - PageHeader with live telemetry and StatusStrip ribbon.
 * - BaseCard containers (bordered continuous feed rows, flat interactive task spotlights).
 * - BaseAvatar with presence beacons (online for active, paused for idle).
 * - BaseBadge semantic badges with subtle styling and animated pulsing dots.
 * - SlideOverDrawer for deep contextual task inspection with ESC key dismissal.
 * - Strict preservation of 100% automated test selectors.
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

// Filtering & Search state matching prototype
const activeFilter = ref<'all' | 'working' | 'paused'>('all')
const searchQuery = ref('')

// Slide-over drawer inspection state
const drawerOpen = ref(false)
const selectedTask = ref<TaskWorkInProgressDto | null>(null)
const selectedPerson = ref<PersonWorkInProgressDto | null>(null)
const selectedTaskType = ref<'in-progress' | 'paused'>('in-progress')

const totalActivePeople = computed(() => persons.value.length)

const totalInProgressTasks = computed(() =>
  persons.value.reduce((acc, p) => acc + (p.inProgressTask ? 1 : 0), 0),
)

const totalPausedTasks = computed(() =>
  persons.value.reduce((acc, p) => acc + (p.pausedTasks ? p.pausedTasks.length : 0), 0),
)

const countHasPaused = computed(() =>
  persons.value.filter((p) => p.pausedTasks && p.pausedTasks.length > 0).length,
)

// StatusStrip KPI items configuration
const kpiItems = computed<StatusStripItem[]>(() => [
  {
    label: 'working',
    value: totalActivePeople.value,
    color: 'success',
    dot: true,
    dataTestId: 'kpi-active-people',
    testId: 'kpi-active-people',
  },
  {
    label: 'tasks active',
    value: totalInProgressTasks.value,
    color: 'primary',
    dataTestId: 'kpi-in-progress-tasks',
    testId: 'kpi-in-progress-tasks',
  },
  {
    label: 'paused',
    value: totalPausedTasks.value,
    color: 'warning',
    dataTestId: 'kpi-paused-tasks',
    testId: 'kpi-paused-tasks',
  },
])

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

// Filtered persons based on quick pills and search input
const filteredPersons = computed(() => {
  return sortedPersons.value.filter((p) => {
    if (activeFilter.value === 'working' && !p.inProgressTask) {
      return false
    }
    if (activeFilter.value === 'paused' && (!p.pausedTasks || p.pausedTasks.length === 0)) {
      return false
    }
    if (searchQuery.value.trim()) {
      const q = searchQuery.value.trim().toLowerCase()
      const matchPerson =
        p.personName.toLowerCase().includes(q) ||
        (p.email && p.email.toLowerCase().includes(q))
      const matchActive =
        p.inProgressTask &&
        (p.inProgressTask.title.toLowerCase().includes(q) ||
          (p.inProgressTask.customerName && p.inProgressTask.customerName.toLowerCase().includes(q)) ||
          (p.inProgressTask.customerCode && p.inProgressTask.customerCode.toLowerCase().includes(q)) ||
          p.inProgressTask.requestId.toLowerCase().includes(q))
      const matchPaused =
        p.pausedTasks &&
        p.pausedTasks.some(
          (t) =>
            t.title.toLowerCase().includes(q) ||
            (t.customerName && t.customerName.toLowerCase().includes(q)) ||
            (t.customerCode && t.customerCode.toLowerCase().includes(q)) ||
            t.requestId.toLowerCase().includes(q),
        )
      return matchPerson || matchActive || matchPaused
    }
    return true
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

function priorityBadgeVariant(priority: string): 'neutral' | 'success' | 'warning' | 'danger' | 'brand' | 'info' {
  switch ((priority ?? '').toUpperCase()) {
    case 'URGENT':
      return 'danger'
    case 'HIGH':
      return 'warning'
    case 'LOW':
      return 'neutral'
    default:
      return 'neutral'
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

function formatShortTime(value: string | null | undefined): string {
  if (!value) return '—'
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return value
  const month = parsed.toLocaleString('en-US', { month: 'short' })
  const day = parsed.getDate()
  const hours = parsed.getHours().toString().padStart(2, '0')
  const minutes = parsed.getMinutes().toString().padStart(2, '0')
  return `${month} ${day} · ${hours}:${minutes}`
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

function openTaskDrawer(
  person: PersonWorkInProgressDto,
  task: TaskWorkInProgressDto,
  type: 'in-progress' | 'paused' = 'in-progress',
): void {
  selectedPerson.value = person
  selectedTask.value = task
  selectedTaskType.value = type
  drawerOpen.value = true
}

onMounted(async () => {
  await loadWipOverview()
})
</script>

<template>
  <section class="wip-board-container pb-4" data-screen-id="SCR-REQ-006" data-testid="wip-screen">
    <!-- Screen Header Bar using PageHeader and StatusStrip primitives -->
    <PageHeader
      title="Work in Progress"
      subtitle="Live operational awareness · See what everyone is actively working on across operations"
      screen-id="SCR-REQ-006"
      :live="true"
      class="mb-3"
    >
      <!-- Status Strip Slot: Compact awareness ribbon -->
      <template #stats>
        <StatusStrip :items="kpiItems" data-testid="wip-kpis" />
      </template>

      <!-- Action Slot: Refresh button -->
      <template #actions>
        <button
          type="button"
          class="btn btn-outline-secondary btn-sm d-flex align-items-center gap-1.5"
          :disabled="isLoading"
          data-testid="refresh-wip-button"
          @click="loadWipOverview"
        >
          <i class="bi bi-arrow-clockwise" :class="{ 'spin-icon': isLoading }" aria-hidden="true"></i>
          <span>Refresh</span>
        </button>
      </template>
    </PageHeader>

    <!-- Error Alert -->
    <div
      v-if="errorMessage"
      role="alert"
      class="alert alert-danger alert-dismissible fade show d-flex align-items-center gap-2 py-2 px-3 mb-3"
      data-testid="wip-error-alert"
    >
      <i class="bi bi-exclamation-triangle-fill flex-shrink-0" aria-hidden="true"></i>
      <div>{{ errorMessage }}</div>
      <button
        type="button"
        class="btn-close py-2 px-2"
        aria-label="Close"
        @click="errorMessage = null"
      ></button>
    </div>

    <!-- Filter & Search Toolbar (Prototype Visual Parity) -->
    <div class="d-flex flex-column flex-sm-row align-items-sm-center justify-content-between gap-2.5 mb-3 text-xs">
      <!-- Quick Filter Pills -->
      <div class="d-inline-flex align-items-center gap-1 p-1 rounded-xl bg-white dark:bg-slate-900/90 border border-slate-200 dark:border-slate-800 shadow-xs">
        <button
          type="button"
          class="btn btn-sm py-1 px-2.5 rounded-lg font-medium transition"
          :class="activeFilter === 'all' ? 'bg-slate-900 text-white dark:bg-cyan-500/20 dark:text-cyan-300 dark:border dark:border-cyan-500/30 shadow-xs' : 'btn-light dark:bg-transparent text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white border-0'"
          @click="activeFilter === 'all'"
        >
          All People ({{ totalActivePeople }})
        </button>
        <button
          type="button"
          class="btn btn-sm py-1 px-2.5 rounded-lg font-medium transition"
          :class="activeFilter === 'working' ? 'bg-slate-900 text-white dark:bg-cyan-500/20 dark:text-cyan-300 dark:border dark:border-cyan-500/30 shadow-xs' : 'btn-light dark:bg-transparent text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white border-0'"
          @click="activeFilter === 'working'"
        >
          Working Now ({{ totalInProgressTasks }})
        </button>
        <button
          type="button"
          class="btn btn-sm py-1 px-2.5 rounded-lg font-medium transition"
          :class="activeFilter === 'paused' ? 'bg-slate-900 text-white dark:bg-cyan-500/20 dark:text-cyan-300 dark:border dark:border-cyan-500/30 shadow-xs' : 'btn-light dark:bg-transparent text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white border-0'"
          @click="activeFilter === 'paused'"
        >
          Has Paused ({{ countHasPaused }})
        </button>
      </div>

      <!-- Search Input -->
      <div class="position-relative search-input-container">
        <input
          v-model="searchQuery"
          type="text"
          placeholder="Filter person, task, customer..."
          class="form-control form-control-sm ps-4 rounded-lg bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-slate-900 dark:text-slate-100 placeholder:text-slate-500 shadow-none"
        />
        <i class="bi bi-search position-absolute top-50 start-0 translate-middle-y ms-2.5 text-slate-400 dark:text-slate-500 small" aria-hidden="true"></i>
      </div>
    </div>

    <!-- Loading State -->
    <div v-if="isLoading && persons.length === 0" class="text-center py-5 text-slate-500 dark:text-slate-400" data-testid="wip-loading">
      <span class="spinner-border spinner-border-sm me-2 text-cyan-500" role="status" aria-hidden="true"></span>
      Loading live operational board...
    </div>

    <!-- Empty State -->
    <div
      v-else-if="!isLoading && sortedPersons.length === 0"
      class="text-center py-5 bg-white dark:bg-slate-900/90 rounded-xl border border-slate-200 dark:border-slate-800 shadow-sm p-4"
      data-testid="wip-empty-state"
    >
      <i class="bi bi-check2-circle text-emerald-500 dark:text-emerald-400 fs-2 mb-2 d-block" aria-hidden="true"></i>
      <h6 class="fw-bold text-slate-900 dark:text-white mb-1">No Active Work in Progress</h6>
      <p class="text-slate-500 dark:text-slate-400 small mb-0">
        There are currently no active or paused tasks across operations.
      </p>
    </div>

    <!-- Filtered Empty State -->
    <div
      v-else-if="!isLoading && persons.length > 0 && filteredPersons.length === 0"
      class="text-center py-5 bg-white dark:bg-slate-900/90 rounded-xl border border-slate-200 dark:border-slate-800 shadow-sm p-4 text-slate-500 dark:text-slate-400"
    >
      <p class="small mb-0">No people match the selected filter.</p>
    </div>

    <!-- Continuous Operational Awareness Feed -->
    <!-- BaseCard primitive used for continuous feed rows (variant="bordered", rounded="xl", padding="lg") -->
    <div v-else class="d-flex flex-column gap-3" data-testid="wip-persons-container">
      <BaseCard
        v-for="person in filteredPersons"
        :key="person.personId"
        variant="bordered"
        rounded="xl"
        padding="lg"
        :hover-effect="true"
        class="person-feed-item"
        data-testid="person-card"
        :data-person-id="person.personId"
      >
        <!-- 1. Person Presence & Identity Row -->
        <header class="d-flex align-items-center justify-content-between pb-2 mb-3 border-bottom border-slate-200 dark:border-slate-800/80">
          <div class="d-flex align-items-center gap-3 overflow-hidden">
            <!-- BaseAvatar with integrated presence beacon -->
            <BaseAvatar
              :name="person.personName"
              size="md"
              :status="person.inProgressTask ? 'online' : 'paused'"
            />

            <div class="overflow-hidden">
              <div class="d-flex align-items-center gap-2">
                <h2 class="fs-6 fw-bold mb-0 text-slate-900 dark:text-white text-truncate" :title="person.personName">
                  {{ person.personName }}
                </h2>
                <!-- Working status badge using BaseBadge -->
                <BaseBadge
                  v-if="person.inProgressTask"
                  variant="success"
                  size="sm"
                  data-testid="person-status-active"
                >
                  Working
                </BaseBadge>
                <BaseBadge
                  v-else
                  variant="neutral"
                  size="sm"
                  data-testid="person-status-paused"
                >
                  Paused Only
                </BaseBadge>
              </div>
              <span v-if="person.email" class="text-slate-500 dark:text-slate-400 small text-truncate d-block mt-0.5">
                {{ person.email }}
              </span>
            </div>
          </div>

          <div class="text-end flex-shrink-0 ms-2">
            <BaseBadge
              variant="neutral"
              size="sm"
              data-testid="person-tasks-count-badge"
            >
              {{ person.totalActiveTasksCount }} active
            </BaseBadge>
          </div>
        </header>

        <!-- 2. Current Activity (Visually dominant active work) -->
        <div class="current-activity-section mb-3">
          <!-- Active Task Spotlight using BaseCard flat -->
          <BaseCard
            v-if="person.inProgressTask"
            variant="flat"
            rounded="md"
            padding="md"
            :hover-effect="true"
            class="active-task-card cursor-pointer"
            data-testid="in-progress-card"
            @click="openTaskDrawer(person, person.inProgressTask, 'in-progress')"
          >
            <div class="d-flex flex-column flex-sm-row justify-content-between align-items-sm-baseline gap-2">
              <!-- Task details -->
              <div class="flex-grow-1 overflow-hidden pe-sm-3">
                <div class="d-flex align-items-center gap-2 mb-1 flex-wrap">
                  <span class="text-amber-500 fw-bold small d-inline-flex align-items-center gap-1 font-mono">
                    ⚡ In progress
                  </span>
                  <span class="text-slate-400 small">·</span>
                  <BaseBadge
                    :variant="priorityBadgeVariant(person.inProgressTask.priority)"
                    size="sm"
                    data-testid="task-priority-badge"
                  >
                    {{ person.inProgressTask.priority }}
                  </BaseBadge>
                  <span
                    class="text-slate-500 dark:text-slate-400 small text-truncate"
                    data-testid="task-customer-badge"
                  >
                    {{ resolveCustomerDisplay(person.inProgressTask) }}
                    <template v-if="person.inProgressTask.requestType"> · {{ person.inProgressTask.requestType }}</template>
                  </span>
                </div>

                <h3 class="fs-6 fw-bold text-slate-900 dark:text-white mb-1 text-truncate hover-primary" :title="person.inProgressTask.title">
                  <router-link
                    :to="`/requests/${person.inProgressTask.requestId}`"
                    class="text-slate-900 dark:text-white dark:hover:text-cyan-400 text-decoration-none"
                    data-testid="task-title-link"
                    @click.stop
                  >
                    {{ person.inProgressTask.title }}
                  </router-link>
                </h3>

                <p
                  v-if="person.inProgressTask.description"
                  class="text-slate-600 dark:text-slate-300 small mb-1 text-truncate"
                >
                  {{ person.inProgressTask.description }}
                </p>

                <!-- Clean metadata footnote -->
                <div class="d-flex align-items-center justify-content-between text-slate-500 dark:text-slate-400 fs-11 mt-2 pt-2 border-top border-slate-200 dark:border-slate-800/80">
                  <span
                    v-if="person.inProgressTask.lastStartedAt"
                    :title="`Started at ${formatTimestamp(person.inProgressTask.lastStartedAt)}`"
                  >
                    Started {{ formatShortTime(person.inProgressTask.lastStartedAt) }}
                  </span>
                  <router-link
                    :to="`/requests/${person.inProgressTask.requestId}`"
                    class="text-slate-400 dark:text-slate-500 text-decoration-none font-mono fs-11 hover-underline dark:hover:text-cyan-400"
                    data-testid="task-id-link"
                    :title="person.inProgressTask.requestId"
                    @click.stop
                  >
                    {{ person.inProgressTask.requestId }}
                  </router-link>
                </div>
              </div>

              <!-- Prominent Elapsed Time & Inspect Action -->
              <div class="d-flex flex-sm-column align-items-center align-items-sm-end justify-content-between flex-shrink-0 pt-2 pt-sm-0 border-top border-slate-200 dark:border-slate-800 border-sm-0">
                <div class="text-sm-end">
                  <div class="d-flex align-items-baseline gap-1">
                    <span
                      class="elapsed-time-counter fw-bold text-slate-900 dark:text-white font-mono d-block"
                      :title="`${person.inProgressTask.totalInProgressHours} hrs`"
                      data-testid="in-progress-time-badge"
                    >
                      {{ person.inProgressTask.totalInProgressFormatted }}
                    </span>
                    <span class="text-slate-500 dark:text-slate-400 fs-11">elapsed</span>
                  </div>
                </div>

                <div class="d-flex align-items-center gap-2 mt-sm-1">
                  <span class="text-cyan-600 dark:text-cyan-400 small fw-medium d-inline-flex align-items-center gap-1 hover-primary">
                    <span>Inspect</span>
                    <i class="bi bi-arrow-right" aria-hidden="true"></i>
                  </span>
                  <router-link
                    :to="`/requests/${person.inProgressTask.requestId}`"
                    class="btn btn-sm btn-link text-slate-500 dark:text-slate-400 text-decoration-none p-0 d-inline-flex align-items-center gap-1 hover-primary"
                    data-testid="task-view-link"
                    @click.stop
                  >
                    <span class="small">Open</span>
                  </router-link>
                </div>
              </div>
            </div>
          </BaseCard>

          <!-- Clean idle placeholder -->
          <div
            v-else
            class="p-3 rounded-lg bg-slate-50 dark:bg-slate-950/40 border border-slate-200 dark:border-slate-800/60 text-slate-500 dark:text-slate-400 small fst-italic"
            data-testid="idle-placeholder"
          >
            <i class="bi bi-moon-stars me-1.5 text-slate-400 dark:text-slate-500" aria-hidden="true"></i>
            No task currently in progress
          </div>
        </div>

        <!-- 3. Paused Work (Secondary, subtle list using BaseCard flat) -->
        <footer class="paused-work-section pt-2 border-top border-slate-200 dark:border-slate-800/80">
          <div class="d-flex align-items-center justify-content-between mb-2">
            <div class="d-flex align-items-center gap-2">
              <span class="fs-11 fw-bold text-uppercase text-slate-500 dark:text-slate-400 letter-spacing-1">
                Paused
              </span>
              <BaseBadge variant="neutral" size="sm">
                {{ person.pausedTasks ? person.pausedTasks.length : 0 }}
              </BaseBadge>
            </div>
          </div>

          <!-- Empty paused state (quiet and non-alarmist) -->
          <div
            v-if="!person.pausedTasks || person.pausedTasks.length === 0"
            class="text-slate-400 dark:text-slate-500 small fst-italic"
            data-testid="empty-paused-tasks"
          >
            No paused work
          </div>

          <!-- Paused tasks list -->
          <div
            v-else
            class="d-flex flex-column gap-2"
            data-testid="paused-tasks-list"
          >
            <BaseCard
              v-for="task in person.pausedTasks"
              :key="task.requestId"
              variant="flat"
              rounded="md"
              padding="sm"
              :hover-effect="true"
              class="paused-task-row cursor-pointer"
              data-testid="paused-task-card"
              @click="openTaskDrawer(person, task, 'paused')"
            >
              <div class="d-flex align-items-center justify-content-between w-100">
                <div class="d-flex align-items-center gap-2 overflow-hidden pe-2">
                  <span class="text-slate-400 dark:text-slate-500 fs-11" aria-hidden="true">⏸</span>
                  <router-link
                    :to="`/requests/${task.requestId}`"
                    class="text-slate-800 dark:text-slate-200 dark:hover:text-cyan-400 text-decoration-none fw-medium small text-truncate hover-primary"
                    data-testid="paused-task-title-link"
                    @click.stop
                  >
                    {{ task.title }}
                  </router-link>
                  <BaseBadge
                    :variant="priorityBadgeVariant(task.priority)"
                    size="sm"
                    data-testid="paused-task-priority-badge"
                  >
                    {{ task.priority }}
                  </BaseBadge>
                  <span
                    class="text-slate-500 dark:text-slate-400 fs-11 text-truncate d-none d-md-inline"
                    data-testid="paused-task-customer-badge"
                  >
                    · {{ resolveCustomerDisplay(task) }}
                  </span>
                  <!-- Subtle testable ID link -->
                  <router-link
                    :to="`/requests/${task.requestId}`"
                    class="d-none"
                    data-testid="paused-task-id-link"
                    @click.stop
                  >
                    {{ task.requestId }}
                  </router-link>
                </div>

                <div class="d-flex align-items-center gap-2 flex-shrink-0">
                  <span
                    class="fw-semibold small text-slate-500 dark:text-slate-400 font-mono"
                    :title="`${task.totalInProgressHours} hrs`"
                    data-testid="paused-time-badge"
                  >
                    {{ task.totalInProgressFormatted }}
                  </span>
                  <span class="text-slate-400 dark:text-slate-500 small">→</span>
                  <router-link
                    :to="`/requests/${task.requestId}`"
                    class="d-none"
                    data-testid="paused-task-view-link"
                    @click.stop
                  >
                    Open
                  </router-link>
                </div>
              </div>
            </BaseCard>
          </div>
        </footer>
      </BaseCard>
    </div>

    <!-- Contextual Inspection Drawer (SlideOverDrawer primitive) -->
    <SlideOverDrawer
      v-model="drawerOpen"
      :title="selectedTask?.title || 'Task Details'"
      :subtitle="selectedTask ? (selectedTaskType === 'in-progress' ? 'Active Work Session' : 'Paused Work Session') : undefined"
      width="md"
    >
      <template v-if="selectedTask">
        <!-- Status & Request ID Row -->
        <div class="d-flex align-items-center justify-content-between pb-3 mb-3 border-bottom dark:border-slate-800">
          <div class="d-flex align-items-center gap-2">
            <BaseBadge
              :variant="selectedTaskType === 'in-progress' ? 'success' : 'warning'"
              :pulse="selectedTaskType === 'in-progress'"
              size="md"
            >
              {{ selectedTaskType === 'in-progress' ? '● In Progress' : '⏸ Paused' }}
            </BaseBadge>
            <BaseBadge variant="neutral" size="sm">
              {{ selectedTask.requestId }}
            </BaseBadge>
          </div>
          <BaseBadge :variant="priorityBadgeVariant(selectedTask.priority)" size="sm">
            {{ selectedTask.priority }}
          </BaseBadge>
        </div>

        <!-- Description -->
        <div class="mb-4">
          <h6 class="text-uppercase text-slate-500 dark:text-slate-400 fs-11 fw-bold letter-spacing-1 mb-1">Description</h6>
          <p class="text-slate-700 dark:text-slate-300 small mb-0 lh-base">
            {{ selectedTask.description || 'No detailed description provided.' }}
          </p>
        </div>

        <!-- Quick Metrics Grid -->
        <div class="row g-3 py-3 border-top border-bottom dark:border-slate-800 mb-4 text-xs">
          <div class="col-6">
            <span class="text-uppercase text-slate-500 dark:text-slate-400 fs-11 fw-semibold d-block letter-spacing-1">Assigned Owner</span>
            <span class="fw-semibold text-slate-900 dark:text-white mt-1 d-block">
              {{ selectedPerson?.personName || '—' }}
            </span>
          </div>
          <div class="col-6">
            <span class="text-uppercase text-slate-500 dark:text-slate-400 fs-11 fw-semibold d-block letter-spacing-1">Elapsed Duration</span>
            <span class="fw-bold text-cyan-600 dark:text-cyan-400 font-mono mt-1 d-block">
              {{ selectedTask.totalInProgressFormatted }}
              <span class="text-slate-500 dark:text-slate-400 fw-normal fs-11">({{ selectedTask.totalInProgressHours }} hrs)</span>
            </span>
          </div>
          <div class="col-6">
            <span class="text-uppercase text-slate-500 dark:text-slate-400 fs-11 fw-semibold d-block letter-spacing-1">Customer</span>
            <span class="fw-semibold text-slate-900 dark:text-white mt-1 d-block text-truncate">
              {{ resolveCustomerDisplay(selectedTask) }}
            </span>
          </div>
          <div class="col-6">
            <span class="text-uppercase text-slate-500 dark:text-slate-400 fs-11 fw-semibold d-block letter-spacing-1">Priority & Type</span>
            <span class="fw-semibold text-slate-900 dark:text-white mt-1 d-block">
              {{ selectedTask.priority }} · {{ selectedTask.requestType || 'General' }}
            </span>
          </div>
        </div>

        <!-- Operational Timing -->
        <div class="mb-4">
          <span class="text-uppercase text-slate-500 dark:text-slate-400 fs-11 fw-semibold d-block letter-spacing-1 mb-2">Operational Timing</span>
          <div class="p-3 rounded-lg bg-slate-50 dark:bg-slate-950/60 border border-slate-200 dark:border-slate-800 small space-y-2">
            <div class="d-flex justify-content-between mb-1">
              <span class="text-slate-500 dark:text-slate-400">Work session started:</span>
              <span class="fw-medium text-slate-900 dark:text-white font-mono">{{ formatTimestamp(selectedTask.lastStartedAt) }}</span>
            </div>
            <div class="d-flex justify-content-between">
              <span class="text-slate-500 dark:text-slate-400">Created:</span>
              <span class="fw-medium text-slate-900 dark:text-white font-mono">{{ formatTimestamp(selectedTask.createdAt) }}</span>
            </div>
          </div>
        </div>
      </template>

      <!-- Action Footer -->
      <template #footer="{ close }">
        <div class="d-flex align-items-center justify-content-between w-100 gap-2">
          <span class="text-slate-500 dark:text-slate-400 small">Press ESC to dismiss</span>
          <div class="d-flex gap-2">
            <button type="button" class="btn btn-outline-secondary btn-sm dark:bg-slate-800 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-700" @click="close">
              Close
            </button>
            <button
              v-if="selectedTask"
              type="button"
              class="btn btn-primary btn-sm d-flex align-items-center gap-1.5"
              @click="navigateToDetail(selectedTask.requestId)"
            >
              <span>Open Request Detail</span>
              <i class="bi bi-arrow-right" aria-hidden="true"></i>
            </button>
          </div>
        </div>
      </template>
    </SlideOverDrawer>
  </section>
</template>

<style scoped>
.search-input-container {
  min-width: 240px;
  max-width: 320px;
}

@media (max-width: 576px) {
  .search-input-container {
    max-width: 100%;
  }
}

.letter-spacing-1 {
  letter-spacing: 0.05em;
}

.hover-primary:hover {
  color: var(--cakra-primary, #364f6b) !important;
}

:global(.dark) .hover-primary:hover {
  color: #22d3ee !important;
}

.hover-underline:hover {
  text-decoration: underline !important;
}

.cursor-pointer {
  cursor: pointer;
}

.text-teal {
  color: var(--cakra-secondary, #3fc1c9);
}

.text-amber-500 {
  color: var(--cakra-amber, #f59e0b);
}

.active-task-card {
  border-left: 3px solid var(--cakra-primary, #364f6b) !important;
}

:global(.dark) .active-task-card {
  border-left: 3px solid #22d3ee !important;
}

.elapsed-time-counter {
  font-size: 1.15rem;
  letter-spacing: -0.02em;
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
