<script lang="ts">
import type { PersonWorkInProgressDto, TaskWorkInProgressDto } from '@/api/requests'

// Duration Heat Tiers (Signal 1: Absolute hours)
export function elapsedDurationTier(hours: number): 'ok' | 'warn' | 'crit' {
  if (hours > 8.0) return 'crit'
  if (hours > 4.0) return 'warn'
  return 'ok'
}

export function elapsedColorClass(tier: 'ok' | 'warn' | 'crit'): string {
  if (tier === 'crit') return 'eh-crit'
  if (tier === 'warn') return 'eh-warn'
  return 'eh-ok'
}

export function elapsedGlowClass(tier: 'ok' | 'warn' | 'crit'): string {
  if (tier === 'crit') return 'glow-red'
  if (tier === 'warn') return 'glow-warn'
  return ''
}

// Budget Burn Tiers (Signal 2: % of allocated budget)
export function burnHeatTier(burnPercentage: number): 'ok' | 'warn' | 'crit' {
  if (burnPercentage >= 100) return 'crit'
  if (burnPercentage >= 75) return 'warn'
  return 'ok'
}

export function burnBarClass(tier: 'ok' | 'warn' | 'crit'): string {
  if (tier === 'crit') return 'bh-crit'
  if (tier === 'warn') return 'bh-warn'
  return 'bh-ok'
}

export function burnTextClass(tier: 'ok' | 'warn' | 'crit'): string {
  if (tier === 'crit') return 'bh-text-crit'
  if (tier === 'warn') return 'bh-text-warn'
  return 'bh-text-ok'
}

// Over budget predicate per person
export function isPersonOverBudget(person: PersonWorkInProgressDto): boolean {
  if (person.inProgressTask?.isOverBudget || (person.inProgressTask && person.inProgressTask.burnPercentage >= 100)) {
    return true
  }
  return person.pausedTasks?.some((t) => t.isOverBudget || t.burnPercentage >= 100) ?? false
}

// Workload Overload Tier (Person row left-border color)
export function personWorkloadTier(person: PersonWorkInProgressDto): 'idle' | 'normal' | 'busy' | 'overloaded' {
  const paused = person.pausedTasks?.length || 0
  if (!person.inProgressTask && paused === 0) return 'idle'
  if (paused >= 4) return 'overloaded'
  if (paused >= 2) return 'busy'
  return 'normal'
}

export function personBorderClass(person: PersonWorkInProgressDto): string {
  const tier = personWorkloadTier(person)
  if (tier === 'idle') return 'idle-row'
  if (tier === 'overloaded') return 'crit-row'
  if (tier === 'busy') return 'busy-row'
  return 'ok-row'
}

// Task row background subtle heat tinting
export function taskRowBgClass(task: TaskWorkInProgressDto): string {
  if (task.isOverBudget || task.burnPercentage >= 100) return 'over-bg'
  const eh = elapsedDurationTier(task.totalInProgressHours)
  if (eh === 'warn') return 'warn-bg'
  return ''
}

export function priorityBadgeVariant(priority: string): 'neutral' | 'success' | 'warning' | 'danger' | 'brand' | 'info' {
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

export function resolveCustomerDisplay(task: TaskWorkInProgressDto): string {
  if (task.customerName && task.customerName.trim().length > 0) {
    return task.customerCode ? `${task.customerName} (${task.customerCode})` : task.customerName
  }
  if (task.customerCode && task.customerCode.trim().length > 0) {
    return task.customerCode
  }
  return task.customerId ?? '—'
}

export function formatShortTime(value: string | null | undefined): string {
  if (!value) return '—'
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return value
  const month = parsed.toLocaleString('en-US', { month: 'short' })
  const day = parsed.getDate()
  const hours = parsed.getHours().toString().padStart(2, '0')
  const minutes = parsed.getMinutes().toString().padStart(2, '0')
  return `${month} ${day} · ${hours}:${minutes}`
}

export function formatTimestamp(value: string | null | undefined): string {
  if (!value) return '—'
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return value
  return parsed.toLocaleString()
}
</script>

<script setup lang="ts">
import { AxiosError } from 'axios'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { getWorkInProgressOverview } from '@/api/requests'
import BaseAvatar from '@/components/base/BaseAvatar.vue'
import BaseBadge from '@/components/base/BaseBadge.vue'
import PageHeader from '@/components/base/PageHeader.vue'
import SlideOverDrawer from '@/components/base/SlideOverDrawer.vue'
import StatusStrip, { type StatusStripItem } from '@/components/base/StatusStrip.vue'

/**
 * SCR-REQ-006: Work in Progress (WIP) Live Operational Awareness Board
 * (CR-021, CR-022, CR-034; TD-004, TD-005, TD-006, TD-007).
 *
 * Dense, high-signal continuous feed featuring:
 * - Dual independent time signals:
 *   - Signal 1: Prominent monospaced elapsed counter colored by duration tiers (≤4h green, >4h amber, >8h red glow).
 *   - Signal 2: Horizontal 4px burn bar (<75% green, 75-99% amber, ≥100% red with overflow pip), elapsed vs budget fraction, C{n} badge, OVER badge.
 * - Workload triage left-border styling on person rows:
 *   - Green: 0-1 paused tasks.
 *   - Amber: 2-3 paused tasks (Busy).
 *   - Red: 4+ paused tasks (Overloaded) with count badges.
 * - Top StatusStrip ribbon with 'over budget' KPI metric.
 * - Filter toolbar with 'Over Budget' quick pill.
 * - SlideOverDrawer with dual time metrics panel and strictly labeled `Complexity: {n} ({h}h)`.
 * - 100% automated test selector preservation.
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
const activeFilter = ref<'all' | 'working' | 'paused' | 'over'>('all')
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

const countOverBudgetPeople = computed(() =>
  persons.value.filter((p) => isPersonOverBudget(p)).length,
)

// StatusStrip KPI items configuration (CR-034 includes 'over budget' count)
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
  {
    label: 'over budget',
    value: countOverBudgetPeople.value,
    color: 'danger',
    dot: true,
    dataTestId: 'kpi-over-budget-people',
    testId: 'kpi-over-budget-people',
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
    if (activeFilter.value === 'over' && !isPersonOverBudget(p)) {
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
      subtitle="Live operational awareness · Compact continuous feed with dual urgency & effort signals"
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

    <!-- Filter & Search Toolbar (CR-034: Includes Over Budget Pill) -->
    <div class="d-flex flex-column flex-sm-row align-items-sm-center justify-content-between gap-2.5 mb-3 text-xs">
      <!-- Quick Filter Pills -->
      <div class="d-inline-flex align-items-center gap-1 p-1 rounded-xl bg-white dark:bg-slate-900/90 border border-slate-200 dark:border-slate-800 shadow-xs flex-wrap">
        <button
          type="button"
          class="btn btn-sm py-1 px-2.5 rounded-lg font-medium transition"
          :class="activeFilter === 'all' ? 'bg-slate-900 text-white dark:bg-cyan-500/20 dark:text-cyan-300 dark:border dark:border-cyan-500/30 shadow-xs' : 'btn-light dark:bg-transparent text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white border-0'"
          @click="activeFilter = 'all'"
        >
          All ({{ totalActivePeople }})
        </button>
        <button
          type="button"
          class="btn btn-sm py-1 px-2.5 rounded-lg font-medium transition"
          :class="activeFilter === 'working' ? 'bg-slate-900 text-white dark:bg-cyan-500/20 dark:text-cyan-300 dark:border dark:border-cyan-500/30 shadow-xs' : 'btn-light dark:bg-transparent text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white border-0'"
          @click="activeFilter = 'working'"
        >
          Working ({{ totalInProgressTasks }})
        </button>
        <button
          type="button"
          class="btn btn-sm py-1 px-2.5 rounded-lg font-medium transition"
          :class="activeFilter === 'paused' ? 'bg-slate-900 text-white dark:bg-cyan-500/20 dark:text-cyan-300 dark:border dark:border-cyan-500/30 shadow-xs' : 'btn-light dark:bg-transparent text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white border-0'"
          @click="activeFilter = 'paused'"
        >
          Busy ({{ countHasPaused }})
        </button>
        <button
          type="button"
          class="btn btn-sm py-1 px-2.5 rounded-lg font-medium transition"
          :class="activeFilter === 'over' ? 'bg-rose-600 text-white dark:bg-rose-500/20 dark:text-rose-300 dark:border dark:border-rose-500/30 shadow-xs' : 'btn-light dark:bg-transparent text-slate-600 dark:text-slate-400 hover:text-rose-600 dark:hover:text-rose-400 border-0'"
          @click="activeFilter = 'over'"
        >
          Over Budget ({{ countOverBudgetPeople }})
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

    <!-- Compact Continuous Operational Awareness Board Frame -->
    <div v-else class="bg-white dark:bg-slate-900/90 rounded-xl border border-slate-200 dark:border-slate-800 shadow-sm overflow-hidden mb-3">
      <!-- Dual Signal Legend Ribbon (Prototype Alignment) -->
      <div class="grid grid-cols-1 md:grid-cols-2 divide-y md:divide-y-0 md:divide-x divide-slate-200 dark:divide-slate-800 border-b border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-950/40 px-3 py-1.5 text-[11px]">
        <!-- Signal 1 Legend -->
        <div class="flex items-center gap-3 py-1 md:py-0">
          <span class="text-[10px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex-shrink-0">
            ① elapsed
          </span>
          <div class="flex items-center gap-2.5 text-[10px]">
            <span class="flex items-center gap-1"><span class="eh-ok font-bold">●</span> ≤4h ok</span>
            <span class="flex items-center gap-1"><span class="eh-warn font-bold">●</span> >4h watch</span>
            <span class="flex items-center gap-1"><span class="eh-crit font-bold">●</span> >8h escalate</span>
          </div>
        </div>
        <!-- Signal 2 Legend -->
        <div class="flex items-center gap-3 py-1 md:py-0 md:ps-3">
          <span class="text-[10px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 flex-shrink-0">
            ② budget burn
          </span>
          <div class="flex items-center gap-2 text-[10px]">
            <span class="flex items-center gap-1">
              <span class="inline-block w-6 h-1 rounded-full bh-ok"></span> &lt;75% on track
            </span>
            <span class="flex items-center gap-1">
              <span class="inline-block w-6 h-1 rounded-full bh-warn"></span> 75–99% near limit
            </span>
            <span class="flex items-center gap-1">
              <span class="inline-block w-6 h-1 rounded-full bh-crit"></span> ≥100% <b>OVER</b>
            </span>
          </div>
          <div class="ms-auto text-[10px] text-slate-400 dark:text-slate-500 hidden sm:block flex-shrink-0">
            C1=2h · C2=4h · C3=8h · C4=16h · C5=24h
          </div>
        </div>
      </div>

      <!-- Continuous Operational Awareness Feed -->
      <div class="board-scroll divide-y divide-slate-200 dark:divide-slate-800" data-testid="wip-persons-container">
        <div
          v-for="person in filteredPersons"
          :key="person.personId"
          class="person-row py-3 px-3 transition-colors"
          :class="personBorderClass(person)"
          data-testid="person-card"
          :data-person-id="person.personId"
          style="border-left-width: 3px; border-left-style: solid; padding-left: 12px"
        >
          <!-- 1. Person Presence & Identity Row -->
          <div class="flex items-center gap-2 px-1 mb-2">
            <!-- BaseAvatar with presence beacon -->
            <BaseAvatar
              :name="person.personName"
              size="sm"
              :status="person.inProgressTask ? 'online' : 'paused'"
            />

            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-1.5 flex-wrap">
                <span class="text-sm font-semibold text-slate-900 dark:text-white truncate" :title="person.personName">
                  {{ person.personName }}
                </span>
                <span
                  v-if="person.inProgressTask"
                  class="badge-custom badge-green"
                  data-testid="person-status-active"
                >
                  Working
                </span>
                <span
                  v-else
                  class="badge-custom badge-slate"
                  data-testid="person-status-paused"
                >
                  Paused Only
                </span>
                <span
                  v-if="isPersonOverBudget(person)"
                  class="badge-custom badge-red text-[10px]"
                >
                  ⚠ over budget
                </span>
              </div>
              <span v-if="person.email" class="text-[10px] text-slate-500 dark:text-slate-400 truncate block">
                {{ person.email }}
              </span>
            </div>

            <span class="badge-custom badge-slate flex-shrink-0" data-testid="person-tasks-count-badge">
              {{ person.totalActiveTasksCount }} active
            </span>
          </div>

          <!-- 2. Current Activity (Visually dominant active work) -->
          <div class="px-1 mb-1">
            <!-- Active Task Row Spotlight -->
            <div
              v-if="person.inProgressTask"
              class="task-row flex items-start gap-2 px-3 py-2 mb-1.5 rounded-md border border-slate-200 dark:border-slate-800/80 cursor-pointer transition-colors hover:bg-slate-50 dark:hover:bg-slate-800/50"
              :class="taskRowBgClass(person.inProgressTask)"
              data-testid="in-progress-card"
              @click="openTaskDrawer(person, person.inProgressTask, 'in-progress')"
            >
              <span class="text-amber-500 text-xs flex-shrink-0 mt-0.5" aria-hidden="true">⚡</span>
              <div class="flex-1 min-w-0">
                <div class="flex items-center gap-1.5 flex-wrap mb-0.5">
                  <router-link
                    :to="`/requests/${person.inProgressTask.requestId}`"
                    class="text-xs font-semibold text-slate-900 dark:text-white hover:underline truncate max-w-xs md:max-w-md"
                    data-testid="task-title-link"
                    :title="person.inProgressTask.title"
                    @click.stop
                  >
                    {{ person.inProgressTask.title }}
                  </router-link>
                  <BaseBadge
                    :variant="priorityBadgeVariant(person.inProgressTask.priority)"
                    size="sm"
                    data-testid="task-priority-badge"
                  >
                    {{ person.inProgressTask.priority }}
                  </BaseBadge>
                  <span
                    class="badge-custom badge-slate text-[10px]"
                    data-testid="task-customer-badge"
                  >
                    {{ resolveCustomerDisplay(person.inProgressTask) }}
                  </span>
                  <span
                    v-if="person.inProgressTask.requestType"
                    class="badge-custom badge-blue text-[10px]"
                  >
                    {{ person.inProgressTask.requestType }}
                  </span>
                </div>
                <div class="text-[10px] text-slate-500 dark:text-slate-400 font-mono flex items-center gap-1.5 flex-wrap">
                  <router-link
                    :to="`/requests/${person.inProgressTask.requestId}`"
                    class="hover:underline text-slate-500 dark:text-slate-400 font-mono"
                    data-testid="task-id-link"
                    @click.stop
                  >
                    {{ person.inProgressTask.requestId }}
                  </router-link>
                  <span>·</span>
                  <span v-if="person.inProgressTask.lastStartedAt">
                    Started {{ formatShortTime(person.inProgressTask.lastStartedAt) }}
                  </span>
                  <!-- Hidden open link for selector preservation -->
                  <router-link
                    :to="`/requests/${person.inProgressTask.requestId}`"
                    class="hidden"
                    data-testid="task-view-link"
                    @click.stop
                  >
                    Open
                  </router-link>
                </div>
              </div>

              <!-- DUAL TIME SIGNALS: Active Task -->
              <div class="flex flex-col items-end gap-1 flex-shrink-0 min-w-[90px] ms-auto">
                <!-- ① Elapsed Counter -->
                <div class="flex items-baseline gap-1">
                  <span
                    class="elapsed-lg font-mono font-bold"
                    :class="[
                      elapsedColorClass(elapsedDurationTier(person.inProgressTask.totalInProgressHours)),
                      elapsedGlowClass(elapsedDurationTier(person.inProgressTask.totalInProgressHours))
                    ]"
                    data-testid="in-progress-time-badge"
                    :title="`${person.inProgressTask.totalInProgressHours}h elapsed`"
                  >
                    {{ person.inProgressTask.totalInProgressFormatted }}
                  </span>
                </div>

                <!-- ② Budget Burn Bar -->
                <div class="burn-track w-[88px]">
                  <div
                    class="burn-fill"
                    :class="burnBarClass(burnHeatTier(person.inProgressTask.burnPercentage))"
                    :style="{ width: `${Math.min(person.inProgressTask.burnPercentage, 100)}%` }"
                  />
                  <div
                    v-if="person.inProgressTask.isOverBudget || person.inProgressTask.burnPercentage >= 100"
                    class="burn-over-pip"
                  />
                </div>

                <!-- ② Budget Burn Subtext & Badges -->
                <div class="flex items-center gap-1 flex-wrap justify-end max-w-[110px]">
                  <span
                    class="text-[10px] font-mono"
                    :class="burnTextClass(burnHeatTier(person.inProgressTask.burnPercentage))"
                    :title="`${person.inProgressTask.burnPercentage}% of effort budget used`"
                  >
                    {{ person.inProgressTask.totalInProgressFormatted }}/{{ person.inProgressTask.budgetHours }}h
                  </span>
                  <span class="effort-pip">C{{ person.inProgressTask.complexity }}</span>
                  <span
                    v-if="person.inProgressTask.isOverBudget || person.inProgressTask.burnPercentage >= 100"
                    class="badge-custom badge-red text-[9px] px-1 py-0 font-bold"
                  >
                    OVER
                  </span>
                </div>
                <div class="text-[9px] text-slate-400 dark:text-slate-500">① elapsed · ② burn</div>
              </div>
            </div>

            <!-- Clean Idle Placeholder -->
            <div
              v-else
              class="flex items-center gap-2 px-3 py-1.5 mb-1.5 rounded-md text-slate-400 dark:text-slate-500 text-xs italic bg-slate-50 dark:bg-slate-950/40 border border-slate-200 dark:border-slate-800/60"
              data-testid="idle-placeholder"
            >
              <span aria-hidden="true">🌙</span>
              <span>No task in progress</span>
            </div>
          </div>

          <!-- 3. Paused Work (Secondary, subtle list) -->
          <div
            v-if="person.pausedTasks && person.pausedTasks.length > 0"
            class="px-1 border-t border-slate-200 dark:border-slate-800/80 mt-2 pt-2"
            data-testid="paused-tasks-list"
          >
            <div class="flex items-center gap-1.5 px-1 mb-1">
              <span class="text-[10px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                Paused
              </span>
              <span
                v-if="person.pausedTasks.length >= 4"
                class="badge-custom badge-red ml-1"
              >
                {{ person.pausedTasks.length }} paused ⚠
              </span>
              <span
                v-else-if="person.pausedTasks.length >= 2"
                class="badge-custom badge-amber ml-1"
              >
                {{ person.pausedTasks.length }} paused
              </span>
              <span
                v-else
                class="badge-custom badge-slate ml-1"
              >
                {{ person.pausedTasks.length }} paused
              </span>
            </div>

            <div class="flex flex-col gap-1">
              <div
                v-for="task in person.pausedTasks"
                :key="task.requestId"
                class="task-row flex items-center justify-between gap-2 px-2.5 py-1.5 rounded-md hover:bg-slate-50 dark:hover:bg-slate-800/50 cursor-pointer transition-colors"
                :class="taskRowBgClass(task)"
                data-testid="paused-task-card"
                @click="openTaskDrawer(person, task, 'paused')"
              >
                <div class="flex-1 min-w-0 flex items-center gap-1.5 flex-wrap">
                  <span class="text-slate-400 dark:text-slate-500 text-xs flex-shrink-0" aria-hidden="true">⏸</span>
                  <router-link
                    :to="`/requests/${task.requestId}`"
                    class="text-xs font-medium text-slate-800 dark:text-slate-200 hover:underline truncate max-w-xs"
                    data-testid="paused-task-title-link"
                    :title="task.title"
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
                    class="text-[10px] text-slate-500 dark:text-slate-400 truncate"
                    data-testid="paused-task-customer-badge"
                  >
                    · {{ resolveCustomerDisplay(task) }}
                  </span>
                  <!-- Hidden links for selector preservation -->
                  <router-link
                    :to="`/requests/${task.requestId}`"
                    class="hidden"
                    data-testid="paused-task-id-link"
                    @click.stop
                  >
                    {{ task.requestId }}
                  </router-link>
                  <router-link
                    :to="`/requests/${task.requestId}`"
                    class="hidden"
                    data-testid="paused-task-view-link"
                    @click.stop
                  >
                    Open
                  </router-link>
                </div>

                <!-- COMPACT DUAL TIME SIGNALS: Paused Task -->
                <div class="flex flex-col items-end gap-0.5 flex-shrink-0 min-w-[80px]">
                  <div class="flex items-center gap-1">
                    <span
                      class="text-xs font-semibold font-mono"
                      :class="elapsedColorClass(elapsedDurationTier(task.totalInProgressHours))"
                      data-testid="paused-time-badge"
                      :title="`① elapsed ${task.totalInProgressFormatted}`"
                    >
                      {{ task.totalInProgressFormatted }}
                    </span>
                    <span class="effort-pip">C{{ task.complexity }}</span>
                    <span
                      v-if="task.isOverBudget || task.burnPercentage >= 100"
                      class="badge-custom badge-red text-[9px] px-1 py-0 font-bold"
                    >
                      OVER
                    </span>
                  </div>
                  <div class="burn-track w-[76px]">
                    <div
                      class="burn-fill"
                      :class="burnBarClass(burnHeatTier(task.burnPercentage))"
                      :style="{ width: `${Math.min(task.burnPercentage, 100)}%` }"
                    />
                    <div
                      v-if="task.isOverBudget || task.burnPercentage >= 100"
                      class="burn-over-pip"
                    />
                  </div>
                  <span
                    class="text-[9px] font-mono"
                    :class="burnTextClass(burnHeatTier(task.burnPercentage))"
                  >
                    {{ task.totalInProgressFormatted }}/{{ task.budgetHours }}h ({{ task.burnPercentage }}%)
                  </span>
                </div>
              </div>
            </div>
          </div>
          <div
            v-else
            class="text-slate-400 dark:text-slate-500 text-[11px] italic px-2 pt-1"
            data-testid="empty-paused-tasks"
          >
            No paused work
          </div>
        </div>
      </div>

      <!-- Feed Footer -->
      <div class="px-4 py-2 border-t border-slate-200 dark:border-slate-800 text-[10px] text-slate-400 dark:text-slate-500 flex items-center justify-between">
        <span>Click any task row to inspect details</span>
        <span>① = elapsed heat &nbsp;·&nbsp; ② = budget burn — they can disagree</span>
      </div>
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

        <!-- ── Dual Signal Telemetry Panel (Prototype Alignment) ── -->
        <div class="p-3 rounded-lg border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-950/60 mb-4">
          <div class="row g-3">
            <!-- Signal 1: Pure Elapsed -->
            <div class="col-6 d-flex flex-column gap-1">
              <span class="text-[10px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                ① pure elapsed
              </span>
              <div
                class="elapsed-lg font-mono font-bold"
                :class="[
                  elapsedColorClass(elapsedDurationTier(selectedTask.totalInProgressHours)),
                  elapsedGlowClass(elapsedDurationTier(selectedTask.totalInProgressHours))
                ]"
              >
                {{ selectedTask.totalInProgressFormatted }}
              </div>
              <div class="text-[10px] text-slate-500 dark:text-slate-400">
                <template v-if="elapsedDurationTier(selectedTask.totalInProgressHours) === 'crit'">
                  ⚠ Escalate — over 8h
                </template>
                <template v-else-if="elapsedDurationTier(selectedTask.totalInProgressHours) === 'warn'">
                  Watch — over 4h
                </template>
                <template v-else>
                  On track — ≤4h
                </template>
              </div>
            </div>

            <!-- Signal 2: Budget Burn -->
            <div class="col-6 d-flex flex-column gap-1">
              <span class="text-[10px] font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400">
                ② budget burn
              </span>
              <div class="d-flex align-items-baseline gap-1.5 flex-wrap">
                <span
                  class="font-mono font-bold fs-6"
                  :class="burnTextClass(burnHeatTier(selectedTask.burnPercentage))"
                >
                  {{ selectedTask.totalInProgressFormatted }} / {{ selectedTask.budgetHours }}h
                </span>
                <span
                  class="text-xs font-semibold"
                  :class="burnTextClass(burnHeatTier(selectedTask.burnPercentage))"
                >
                  {{ selectedTask.burnPercentage }}%
                </span>
                <span
                  v-if="selectedTask.isOverBudget || selectedTask.burnPercentage >= 100"
                  class="badge-custom badge-red text-[10px] px-1 py-0 font-bold"
                >
                  OVER BUDGET
                </span>
              </div>
              <!-- Burn bar -->
              <div class="burn-track w-100 my-1">
                <div
                  class="burn-fill"
                  :class="burnBarClass(burnHeatTier(selectedTask.burnPercentage))"
                  :style="{ width: `${Math.min(selectedTask.burnPercentage, 100)}%` }"
                />
                <div
                  v-if="selectedTask.isOverBudget || selectedTask.burnPercentage >= 100"
                  class="burn-over-pip"
                />
              </div>
              <div class="text-[10px] text-slate-500 dark:text-slate-400">
                Complexity: {{ selectedTask.complexity }} ({{ selectedTask.budgetHours }}h) · Used: {{ selectedTask.burnPercentage }}%
              </div>
            </div>
          </div>
        </div>

        <!-- Description -->
        <div class="mb-4">
          <h6 class="text-uppercase text-slate-500 dark:text-slate-400 fs-11 fw-bold letter-spacing-1 mb-1">Description</h6>
          <p class="text-slate-700 dark:text-slate-300 small mb-0 lh-base">
            {{ selectedTask.description || 'No detailed description provided.' }}
          </p>
        </div>

        <!-- Quick Metrics Grid (Strictly labeled as Complexity) -->
        <div class="row g-3 py-3 border-top border-bottom dark:border-slate-800 mb-4 text-xs">
          <div class="col-6">
            <span class="text-uppercase text-slate-500 dark:text-slate-400 fs-11 fw-semibold d-block letter-spacing-1">Assigned Owner</span>
            <span class="fw-semibold text-slate-900 dark:text-white mt-1 d-block">
              {{ selectedPerson?.personName || '—' }}
            </span>
          </div>
          <div class="col-6">
            <span class="text-uppercase text-slate-500 dark:text-slate-400 fs-11 fw-semibold d-block letter-spacing-1">Customer</span>
            <span class="fw-semibold text-slate-900 dark:text-white mt-1 d-block text-truncate">
              {{ resolveCustomerDisplay(selectedTask) }}
            </span>
          </div>
          <div class="col-6">
            <span class="text-uppercase text-slate-500 dark:text-slate-400 fs-11 fw-semibold d-block letter-spacing-1">Type</span>
            <span class="fw-semibold text-slate-900 dark:text-white mt-1 d-block">
              {{ selectedTask.requestType || 'General' }}
            </span>
          </div>
          <div class="col-6">
            <span class="text-uppercase text-slate-500 dark:text-slate-400 fs-11 fw-semibold d-block letter-spacing-1">Complexity</span>
            <span class="fw-semibold text-slate-900 dark:text-white mt-1 d-block">
              Complexity: {{ selectedTask.complexity }} ({{ selectedTask.budgetHours }}h)
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

/* ── SIGNAL 1 – pure elapsed heat (absolute hours) ── */
.eh-ok {
  color: #22c55e;
}
.eh-warn {
  color: #f59e0b;
}
.eh-crit {
  color: #ef4444;
}
.glow-warn {
  text-shadow: 0 0 8px rgba(245, 158, 11, 0.65);
}
.glow-red {
  text-shadow: 0 0 8px rgba(239, 68, 68, 0.75);
}

/* ── SIGNAL 2 – effort burn heat (percent of budget) ── */
.bh-ok {
  background-color: #22c55e;
}
.bh-warn {
  background-color: #f59e0b;
}
.bh-crit {
  background-color: #ef4444;
}
.bh-text-ok {
  color: #22c55e;
}
.bh-text-warn {
  color: #f59e0b;
}
.bh-text-crit {
  color: #ef4444;
}

/* ── Burn bar track ── */
.burn-track {
  height: 4px;
  border-radius: 99px;
  background-color: rgba(148, 163, 184, 0.2);
  overflow: visible;
  position: relative;
}
.burn-fill {
  height: 100%;
  border-radius: 99px;
  transition: width 0.4s ease;
  max-width: 100%;
}
.burn-over-pip {
  position: absolute;
  right: -4px;
  top: -2px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background-color: #ef4444;
  border: 1.5px solid #1e293b;
  animation: pip-pulse 1s ease-in-out infinite;
}
@keyframes pip-pulse {
  0%, 100% {
    opacity: 1;
    transform: scale(1);
  }
  50% {
    opacity: 0.5;
    transform: scale(0.7);
  }
}

/* ── Row workload triage signals (left border) ── */
.ok-row {
  border-left-color: rgba(34, 197, 94, 0.45) !important;
}
.busy-row {
  border-left-color: #f59e0b !important;
}
.crit-row {
  border-left-color: #ef4444 !important;
}
.idle-row {
  border-left-color: rgba(148, 163, 184, 0.2) !important;
}

/* ── Row background heat tinting ── */
.over-bg {
  background-color: rgba(239, 68, 68, 0.05) !important;
}
.warn-bg {
  background-color: rgba(245, 158, 11, 0.05) !important;
}

/* ── Elapsed large display ── */
.elapsed-lg {
  font-size: 1.15rem;
  font-weight: 800;
  font-variant-numeric: tabular-nums;
  letter-spacing: -0.03em;
  line-height: 1;
}

/* ── Complexity pill ── */
.effort-pip {
  font-size: 10px;
  font-weight: 700;
  padding: 0 5px;
  border-radius: 3px;
  border: 1px solid rgba(148, 163, 184, 0.3);
  color: #94a3b8;
  line-height: 1.6;
}

/* ── Semantic badges ── */
.badge-custom {
  font-size: 11px;
  padding: 1px 6px;
  border-radius: 4px;
  font-weight: 600;
  line-height: 1.5;
  display: inline-flex;
  align-items: center;
}
.badge-red {
  background-color: rgba(239, 68, 68, 0.15);
  color: #ef4444;
}
.badge-amber {
  background-color: rgba(245, 158, 11, 0.15);
  color: #f59e0b;
}
.badge-green {
  background-color: rgba(34, 197, 94, 0.15);
  color: #22c55e;
}
.badge-blue {
  background-color: rgba(99, 179, 237, 0.15);
  color: #38bdf8;
}
.badge-slate {
  background-color: rgba(148, 163, 184, 0.15);
  color: #94a3b8;
}

/* ── Feed scroll container ── */
.board-scroll {
  max-height: 650px;
  overflow-y: auto;
}
.board-scroll::-webkit-scrollbar {
  width: 4px;
}
.board-scroll::-webkit-scrollbar-thumb {
  background-color: rgba(148, 163, 184, 0.25);
  border-radius: 4px;
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
