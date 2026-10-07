<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import {
  getOperationsCockpit,
  type OperationsCockpitDto,
  type WorkPackageTelemetryDto,
  type HealthState,
  type PressureTier,
} from '@/api/workpackages'
import BaseBadge from '@/components/base/BaseBadge.vue'
import BaseCard from '@/components/base/BaseCard.vue'
import PageHeader from '@/components/base/PageHeader.vue'
import StatusStrip, { type StatusStripItem } from '@/components/base/StatusStrip.vue'

/**
 * SCR-WP-002: Executive Operations Cockpit Screen
 * (Architecture CR-025 §4 TD-005, TD-006; CR-026 P3-S05).
 *
 * Provides executive operational awareness through:
 * - Macro telemetry ribbon with organization daily throughput C_org and aggregate load %.
 * - Interactive 2D Pressure × Health Triage Matrix with clickable cell filtering.
 * - Factual Operational Feed displaying exception packages (invariant violations + impossible pressure).
 * - Collapsible section for nominal and flowing work packages.
 * - 14-day flow activity barcode pulse for each package.
 * - BOD Infographic executive dark styling with high-contrast JetBrains Mono telemetry.
 * - Strictly cold factual reporting with zero automated advice.
 */

interface MatrixRowDef {
  key: HealthState
  label: string
  sublabel: string
}

interface MatrixColDef {
  key: PressureTier
  label: string
  sublabel: string
}

const MATRIX_ROWS: readonly MatrixRowDef[] = [
  { key: 'DEADLINE_BREACHED', label: 'DEADLINE BREACHED', sublabel: 'Working days ≤ 0 & requests > 0' },
  { key: 'ACTIVE_BLOCKERS', label: 'ACTIVE BLOCKERS', sublabel: 'Paused requests > 0' },
  { key: 'DORMANT', label: 'DORMANT', sublabel: 'Inactive > 5 business days' },
  { key: 'WIP_STAGNANT', label: 'WIP STAGNANT', sublabel: 'Active request age > 14 days' },
  { key: 'FLOWING', label: 'FLOWING', sublabel: 'Nominal flow & no invariant violations' },
] as const

const MATRIX_COLUMNS: readonly MatrixColDef[] = [
  { key: 'UNPLANNED', label: 'UNPLANNED', sublabel: 'No target deadline' },
  { key: 'NOMINAL', label: 'NOMINAL (< 20%)', sublabel: '< 20% Org Output' },
  { key: 'ELEVATED', label: 'ELEVATED (20% - 50%)', sublabel: '20% - 50% Org Output' },
  { key: 'CRITICAL', label: 'CRITICAL (50% - 100%)', sublabel: '50% - 100% Org Output' },
  { key: 'IMPOSSIBLE', label: 'IMPOSSIBLE (> 100%)', sublabel: '> 100% Org Output' },
] as const

const router = useRouter()

// Data state
const cockpitData = ref<OperationsCockpitDto | null>(null)
const isLoading = ref(true)
const errorMessage = ref<string | null>(null)

// Interactive Matrix Filter state
const selectedRow = ref<HealthState | null>(null)
const selectedCol = ref<PressureTier | null>(null)

// Collapsible Nominal Section state
const isNominalExpanded = ref(false)

async function loadData() {
  isLoading.value = true
  errorMessage.value = null
  try {
    const data = await getOperationsCockpit()
    cockpitData.value = data
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || err?.message || 'Failed to load Operations Cockpit telemetry.'
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  loadData()
})

// Top Ribbon Status Strip Items
const kpiItems = computed<StatusStripItem[]>(() => {
  const metrics = cockpitData.value?.portfolioMetrics
  if (!metrics) return []

  const isOverloaded = metrics.portfolioAggregateLoadPercentage > 100.0

  return [
    {
      label: 'Org Throughput (C_org)',
      value: `${metrics.orgDailyThroughput.toFixed(1)} pts/day`,
      color: 'primary',
      dataTestId: 'kpi-org-throughput',
      testId: 'kpi-org-throughput',
    },
    {
      label: 'Portfolio Load',
      value: `${metrics.portfolioAggregateLoadPercentage.toFixed(1)}%`,
      color: isOverloaded ? 'danger' : 'primary',
      dot: isOverloaded,
      dataTestId: 'kpi-portfolio-load',
      testId: 'kpi-portfolio-load',
    },
    {
      label: 'Active Packages',
      value: metrics.totalActiveWorkPackages,
      color: 'primary',
      dataTestId: 'kpi-active-packages',
      testId: 'kpi-active-packages',
    },
    {
      label: 'With Deadline',
      value: metrics.withDeadlineCount,
      color: 'success',
      dataTestId: 'kpi-with-deadline',
      testId: 'kpi-with-deadline',
    },
    {
      label: 'Unplanned (No Deadline)',
      value: metrics.withoutDeadlineCount,
      color: metrics.withoutDeadlineCount > 0 ? 'warning' : 'secondary',
      dataTestId: 'kpi-without-deadline',
      testId: 'kpi-without-deadline',
    },
    {
      label: 'Invariant Violations',
      value: metrics.totalInvariantViolationsCount,
      color: metrics.totalInvariantViolationsCount > 0 ? 'danger' : 'success',
      dataTestId: 'kpi-invariant-violations',
      testId: 'kpi-invariant-violations',
    },
  ]
})

// Safe Matrix cell lookup (handles case variations)
function getCellCount(health: HealthState, tier: PressureTier): number {
  if (!cockpitData.value?.matrix?.cells) return 0
  const cells = cockpitData.value.matrix.cells
  const rowKey = Object.keys(cells).find((k) => k.toUpperCase() === health.toUpperCase())
  if (!rowKey || !cells[rowKey]) return 0
  const colKey = Object.keys(cells[rowKey]).find((k) => k.toUpperCase() === tier.toUpperCase())
  if (!colKey) return 0
  return cells[rowKey][colKey] ?? 0
}

function getRowTotal(health: HealthState): number {
  if (cockpitData.value?.matrix?.rowTotals) {
    const totals = cockpitData.value.matrix.rowTotals
    const rowKey = Object.keys(totals).find((k) => k.toUpperCase() === health.toUpperCase())
    if (rowKey) return totals[rowKey]
  }
  return MATRIX_COLUMNS.reduce((sum, col) => sum + getCellCount(health, col.key), 0)
}

function getColTotal(tier: PressureTier): number {
  if (cockpitData.value?.matrix?.columnTotals) {
    const totals = cockpitData.value.matrix.columnTotals
    const colKey = Object.keys(totals).find((k) => k.toUpperCase() === tier.toUpperCase())
    if (colKey) return totals[colKey]
  }
  return MATRIX_ROWS.reduce((sum, row) => sum + getCellCount(row.key, tier), 0)
}

// Matrix interaction
const isCellSelected = (row: HealthState, col: PressureTier): boolean => {
  return selectedRow.value === row && selectedCol.value === col
}

function toggleCell(row: HealthState, col: PressureTier) {
  if (isCellSelected(row, col)) {
    selectedRow.value = null
    selectedCol.value = null
  } else {
    selectedRow.value = row
    selectedCol.value = col
  }
}

function clearFilter() {
  selectedRow.value = null
  selectedCol.value = null
}

const isFilterActive = computed(() => selectedRow.value !== null && selectedCol.value !== null)

const activeFilterLabel = computed(() => {
  if (!isFilterActive.value) return ''
  const row = MATRIX_ROWS.find((r) => r.key === selectedRow.value)
  const col = MATRIX_COLUMNS.find((c) => c.key === selectedCol.value)
  return `${row?.label ?? selectedRow.value} × ${col?.label ?? selectedCol.value}`
})

// Package classification
function isPackageException(pkg: WorkPackageTelemetryDto): boolean {
  const health = pkg.healthState?.toUpperCase()
  const pressure = pkg.pressureTier?.toUpperCase()
  return health !== 'FLOWING' || pressure === 'IMPOSSIBLE'
}

function isPackageNominal(pkg: WorkPackageTelemetryDto): boolean {
  const health = pkg.healthState?.toUpperCase()
  const pressure = pkg.pressureTier?.toUpperCase()
  return health === 'FLOWING' && pressure !== 'IMPOSSIBLE'
}

// Sorting helper: highest capacity share or breached packages first
function sortPackages(list: WorkPackageTelemetryDto[]): WorkPackageTelemetryDto[] {
  return [...list].sort((a, b) => {
    if (a.deadlineBreached !== b.deadlineBreached) {
      return a.deadlineBreached ? -1 : 1
    }
    const shareA = a.orgCapacityShare ?? -1
    const shareB = b.orgCapacityShare ?? -1
    if (shareA !== shareB) {
      return shareB - shareA
    }
    return b.remainingComplexity - a.remainingComplexity
  })
}

// Packages for Operational Exception Feed
const feedPackages = computed<WorkPackageTelemetryDto[]>(() => {
  const all = cockpitData.value?.packages ?? []
  if (isFilterActive.value) {
    const row = selectedRow.value?.toUpperCase()
    const col = selectedCol.value?.toUpperCase()
    return sortPackages(
      all.filter((p) => p.healthState?.toUpperCase() === row && p.pressureTier?.toUpperCase() === col),
    )
  }
  // Default view: all invariant violations + impossible pressure
  return sortPackages(all.filter(isPackageException))
})

// Packages for Nominal & Flowing Section
const nominalPackages = computed<WorkPackageTelemetryDto[]>(() => {
  const all = cockpitData.value?.packages ?? []
  return sortPackages(all.filter(isPackageNominal))
})

function navigateToPackage(id: string) {
  router.push(`/work-packages/${id}`)
}

function formatDate(iso?: string | null): string {
  if (!iso) return '—'
  const parsed = new Date(iso)
  if (Number.isNaN(parsed.getTime())) return iso
  return parsed.toISOString().slice(0, 10)
}

function getPressureBadgeVariant(tier?: string): 'danger' | 'warning' | 'success' | 'neutral' {
  switch (tier?.toUpperCase()) {
    case 'IMPOSSIBLE':
    case 'CRITICAL':
      return 'danger'
    case 'ELEVATED':
      return 'warning'
    case 'NOMINAL':
      return 'success'
    default:
      return 'neutral'
  }
}

function getCellSeverityClass(row: HealthState, col: PressureTier, count: number): string {
  if (count === 0) return 'matrix-cell--empty'
  if (row === 'DEADLINE_BREACHED' || col === 'IMPOSSIBLE') return 'matrix-cell--critical'
  if (row === 'ACTIVE_BLOCKERS' || col === 'CRITICAL') return 'matrix-cell--warning-high'
  if (row === 'DORMANT' || row === 'WIP_STAGNANT' || col === 'ELEVATED') return 'matrix-cell--warning-mid'
  if (row === 'FLOWING' && col === 'NOMINAL') return 'matrix-cell--nominal'
  return 'matrix-cell--active-neutral'
}

function getBarcodeBarClass(day: { closedCount: number; stateMutationCount: number; blockedCount: number }): string {
  if (day.closedCount > 0) return 'barcode-pulse--closed'
  if (day.blockedCount > 0) return 'barcode-pulse--blocked'
  if (day.stateMutationCount > 0) return 'barcode-pulse--mutation'
  return 'barcode-pulse--idle'
}
</script>

<template>
  <div class="cockpit-container space-y-6">
    <!-- Screen Header with Live Telemetry Ribbon -->
    <PageHeader
      title="Operations Cockpit"
      subtitle="Factual scope pressure × operational health triage matrix and exception telemetry"
      screenId="SCR-WP-002"
      :live="true"
    >
      <template #stats>
        <StatusStrip v-if="!isLoading && cockpitData" :items="kpiItems" />
      </template>

      <template #actions>
        <button
          type="button"
          class="px-3 py-1.5 rounded-lg border border-slate-700 bg-slate-800/80 hover:bg-slate-700 text-slate-200 transition text-sm font-medium inline-flex items-center gap-1.5 shadow-sm"
          data-testid="refresh-cockpit-btn"
          :disabled="isLoading"
          @click="loadData"
        >
          <i class="bi bi-arrow-clockwise text-cyan-400" :class="{ 'spin-icon': isLoading }" aria-hidden="true"></i>
          <span>Refresh</span>
        </button>
      </template>
    </PageHeader>

    <!-- Error Alert Banner -->
    <div
      v-if="errorMessage"
      class="bg-rose-950/40 border border-rose-500/30 text-rose-300 rounded-xl p-4 flex items-center justify-between shadow-lg my-3"
      role="alert"
    >
      <div class="flex items-center gap-2">
        <i class="bi bi-exclamation-triangle-fill text-rose-400 text-lg" aria-hidden="true"></i>
        <span class="text-sm font-medium">{{ errorMessage }}</span>
      </div>
      <button
        type="button"
        class="px-3 py-1 text-xs font-semibold rounded-lg bg-rose-900/40 hover:bg-rose-800/50 text-rose-200 border border-rose-500/40 transition"
        @click="loadData"
      >
        Retry
      </button>
    </div>

    <!-- Loading Skeleton Indicator -->
    <div v-if="isLoading" class="cockpit-loading my-12 text-center">
      <div class="inline-block w-8 h-8 border-3 border-cyan-400 border-t-transparent rounded-full animate-spin" role="status">
        <span class="visually-hidden">Loading telemetry data...</span>
      </div>
      <div class="text-slate-400 mt-3 text-xs tracking-wide">Aggregating portfolio health invariants and demand density...</div>
    </div>

    <div v-else-if="cockpitData" class="cockpit-content space-y-6">
      <!-- Section: 2D Pressure × Health Triage Matrix -->
      <section class="cockpit-section matrix-section">
        <div class="section-header flex flex-col sm:flex-row sm:items-center justify-between gap-2 mb-3">
          <div>
            <h2 class="text-lg font-bold text-white tracking-tight flex items-center gap-2">
              Operational Triage Matrix
            </h2>
            <p class="text-xs text-slate-400 mt-0.5">
              Interactive 2D distribution of active Work Packages across scope pressure tiers and observable health invariants.
              Click any cell to filter the operational feed below.
            </p>
          </div>
          <div v-if="isFilterActive" class="matrix-filter-reset">
            <button
              type="button"
              class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition inline-flex items-center gap-1.5 shadow-sm"
              data-testid="clear-matrix-filter-btn"
              @click="clearFilter"
            >
              <i class="bi bi-x-circle text-cyan-400" aria-hidden="true"></i>
              <span>Clear Filter</span>
            </button>
          </div>
        </div>

        <!-- 2D Matrix Table Container -->
        <div class="matrix-table-wrapper rounded-xl border border-slate-800 bg-slate-900/90 shadow-lg overflow-x-auto">
          <table
            class="matrix-table w-full border-collapse text-center align-middle"
            data-testid="triage-matrix"
          >
            <thead>
              <tr class="matrix-header-row">
                <th class="matrix-corner-cell text-start p-3 bg-slate-950/60 border border-slate-800">
                  <div class="matrix-axis-label flex flex-col gap-1">
                    <span class="axis-y text-[10px] font-bold uppercase tracking-wider text-slate-400">Health Invariants ↓</span>
                    <span class="axis-x text-[10px] font-bold uppercase tracking-wider text-cyan-400">Scope Pressure →</span>
                  </div>
                </th>
                <th
                  v-for="col in MATRIX_COLUMNS"
                  :key="col.key"
                  class="matrix-col-header p-2.5 bg-slate-950/60 border border-slate-800"
                >
                  <div class="col-title text-xs font-bold text-slate-100 uppercase tracking-wider">{{ col.label }}</div>
                  <div class="col-sub text-[10px] text-slate-400 font-normal mt-0.5">{{ col.sublabel }}</div>
                </th>
                <th class="matrix-total-header p-2.5 bg-slate-950/60 border border-slate-800 text-xs font-bold text-slate-200 uppercase tracking-wider">
                  Total
                </th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in MATRIX_ROWS" :key="row.key" class="matrix-body-row">
                <!-- Row Header -->
                <th class="matrix-row-header text-start p-2.5 px-3 bg-slate-950/60 border border-slate-800">
                  <div class="row-title text-xs font-bold text-slate-100 uppercase tracking-wider">{{ row.label }}</div>
                  <div class="row-sub text-[10px] text-slate-400 font-normal mt-0.5">{{ row.sublabel }}</div>
                </th>

                <!-- Data Cells -->
                <td
                  v-for="col in MATRIX_COLUMNS"
                  :key="col.key"
                  class="matrix-cell border border-slate-800 p-2.5 transition duration-150 cursor-pointer select-none"
                  :class="[
                    getCellSeverityClass(row.key, col.key, getCellCount(row.key, col.key)),
                    { 'is-selected': isCellSelected(row.key, col.key) },
                  ]"
                  :data-testid="`matrix-cell-${row.key}-${col.key}`"
                  :data-count="getCellCount(row.key, col.key)"
                  :aria-label="`${row.label} and ${col.label}: ${getCellCount(row.key, col.key)} packages`"
                  role="button"
                  tabindex="0"
                  @click="toggleCell(row.key, col.key)"
                  @keydown.enter="toggleCell(row.key, col.key)"
                  @keydown.space.prevent="toggleCell(row.key, col.key)"
                >
                  <div class="cell-inner flex items-center justify-center gap-1.5">
                    <span class="cell-count font-mono font-bold text-sm">{{ getCellCount(row.key, col.key) }}</span>
                    <span v-if="isCellSelected(row.key, col.key)" class="selected-indicator text-cyan-400 text-xs">
                      <i class="bi bi-check-circle-fill" aria-hidden="true"></i>
                    </span>
                  </div>
                </td>

                <!-- Row Total -->
                <td class="matrix-row-total p-2.5 bg-slate-950/60 border border-slate-800 text-slate-300 font-bold font-mono text-sm">
                  {{ getRowTotal(row.key) }}
                </td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="matrix-footer-row">
                <th class="matrix-row-header text-start p-2.5 px-3 bg-slate-950/60 border border-slate-800 text-xs font-bold text-slate-200 uppercase tracking-wider">
                  Total
                </th>
                <td v-for="col in MATRIX_COLUMNS" :key="col.key" class="matrix-col-total p-2.5 bg-slate-950/60 border border-slate-800 text-slate-300 font-bold font-mono text-sm">
                  {{ getColTotal(col.key) }}
                </td>
                <td class="matrix-grand-total p-2.5 bg-slate-950/80 border border-slate-800 text-cyan-400 font-black font-mono text-base">
                  {{ cockpitData.portfolioMetrics.totalActiveWorkPackages }}
                </td>
              </tr>
            </tfoot>
          </table>
        </div>
      </section>

      <!-- Active Filter Banner -->
      <div
        v-if="isFilterActive"
        class="bg-gradient-to-r from-cyan-950/50 via-slate-900 to-indigo-950/50 border border-cyan-500/40 rounded-xl p-3.5 shadow-lg flex items-center justify-between text-cyan-200"
        data-testid="matrix-filter-banner"
      >
        <div class="flex items-center gap-2">
          <i class="bi bi-funnel-fill text-cyan-400 text-base" aria-hidden="true"></i>
          <div class="text-xs">
            <strong class="text-white">Filtered by:</strong>
            <span class="px-2.5 py-0.5 text-xs font-semibold rounded-full bg-cyan-500/20 text-cyan-300 border border-cyan-500/30 mx-1.5 font-mono">
              {{ activeFilterLabel }}
            </span>
            <span class="text-slate-400">({{ feedPackages.length }} work packages matching)</span>
          </div>
        </div>
        <button
          type="button"
          class="px-2.5 py-1 text-xs font-semibold rounded-lg bg-cyan-500/20 hover:bg-cyan-500/30 text-cyan-300 border border-cyan-500/30 transition"
          data-testid="clear-filter-btn"
          @click="clearFilter"
        >
          Clear Filter
        </button>
      </div>

      <!-- Section: Exception Operational Feed -->
      <section class="cockpit-section feed-section">
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 mb-3">
          <div>
            <h2 class="text-lg font-bold text-white tracking-tight flex items-center gap-2">
              {{ isFilterActive ? `Filtered Operational Feed` : `Operational Exceptions Feed` }}
            </h2>
            <p class="text-xs text-slate-400 mt-0.5">
              <template v-if="isFilterActive">
                Displaying packages matching selected matrix cell: {{ activeFilterLabel }}.
              </template>
              <template v-else>
                Showing invariant violations and impossible pressure packages requiring operational attention ({{ feedPackages.length }} items).
              </template>
            </p>
          </div>
          <span class="px-3 py-1 text-xs font-semibold tracking-wider uppercase rounded-full bg-slate-800 text-slate-300 border border-slate-700 self-start sm:self-auto">
            {{ feedPackages.length }} packages
          </span>
        </div>

        <!-- Feed List -->
        <div
          v-if="feedPackages.length > 0"
          class="feed-card-list space-y-3"
          data-testid="cockpit-operational-feed"
        >
          <BaseCard
            v-for="pkg in feedPackages"
            :key="pkg.id"
            variant="bordered"
            padding="md"
            hover-effect
            class="cockpit-card"
            :data-testid="`cockpit-wp-card-${pkg.id}`"
          >
            <!-- Card Main Content -->
            <div class="cockpit-card-inner space-y-3">
              <!-- Top Row: ID, Title, Badges -->
              <div class="card-head flex flex-col md:flex-row md:items-start justify-between gap-2">
                <div class="card-title-group">
                  <div class="flex items-center gap-2 mb-1">
                    <span class="px-2 py-0.5 text-xs font-mono font-semibold rounded bg-slate-950/80 text-cyan-400 border border-slate-800">
                      #{{ pkg.id.slice(0, 8) }}
                    </span>
                    <h3 class="text-base font-bold text-white tracking-tight m-0">{{ pkg.name || pkg.objective }}</h3>
                  </div>
                  <p v-if="pkg.name && pkg.objective && pkg.name !== pkg.objective" class="text-xs text-slate-400 mt-0.5 mb-0">
                    {{ pkg.objective }}
                  </p>
                </div>

                <div class="card-badges flex items-center gap-1.5 flex-wrap">
                  <!-- Scope Pressure Tier Badge -->
                  <BaseBadge
                    :variant="getPressureBadgeVariant(pkg.pressureTier)"
                    :style-type="pkg.pressureTier === 'IMPOSSIBLE' ? 'solid' : 'subtle'"
                    size="sm"
                    data-testid="wp-pressure-badge"
                  >
                    {{ pkg.pressureTier }}
                  </BaseBadge>

                  <!-- Primary Health State Badge -->
                  <BaseBadge
                    v-if="pkg.deadlineBreached"
                    variant="danger"
                    size="sm"
                  >
                    <i class="bi bi-exclamation-octagon-fill me-1" aria-hidden="true"></i>
                    Deadline Breached
                  </BaseBadge>
                  <BaseBadge
                    v-else-if="pkg.blockedRequestsCount > 0"
                    variant="danger"
                    size="sm"
                  >
                    <i class="bi bi-pause-circle-fill me-1" aria-hidden="true"></i>
                    ! {{ pkg.blockedRequestsCount }} Blocked
                  </BaseBadge>
                  <BaseBadge
                    v-else-if="pkg.isDormant"
                    variant="warning"
                    size="sm"
                  >
                    <i class="bi bi-moon-stars-fill me-1" aria-hidden="true"></i>
                    Dormant {{ pkg.dormantDays }}d
                  </BaseBadge>
                  <BaseBadge
                    v-else-if="pkg.isWipStagnant"
                    variant="warning"
                    size="sm"
                  >
                    <i class="bi bi-hourglass-bottom me-1" aria-hidden="true"></i>
                    WIP Stagnant
                  </BaseBadge>
                  <BaseBadge
                    v-else
                    variant="success"
                    size="sm"
                  >
                    <i class="bi bi-check-circle-fill me-1" aria-hidden="true"></i>
                    Flowing
                  </BaseBadge>
                </div>
              </div>

              <!-- Context Details: Owner, Customer, Product -->
              <div class="card-context flex flex-wrap items-center gap-3 text-xs text-slate-400">
                <span v-if="pkg.ownerName">
                  <i class="bi bi-person text-cyan-400 me-1" aria-hidden="true"></i>
                  <strong class="text-slate-300">Owner:</strong> {{ pkg.ownerName }}
                </span>
                <span v-else class="text-rose-400">
                  <i class="bi bi-person-x me-1" aria-hidden="true"></i>
                  <em>Unassigned</em>
                </span>

                <span v-if="pkg.customerName">
                  <i class="bi bi-building text-indigo-400 me-1" aria-hidden="true"></i>
                  <strong class="text-slate-300">Customer:</strong> {{ pkg.customerName }}
                </span>

                <span v-if="pkg.productName">
                  <i class="bi bi-box-seam text-cyan-400 me-1" aria-hidden="true"></i>
                  <strong class="text-slate-300">Product:</strong> {{ pkg.productName }}
                </span>

                <span class="text-slate-400">
                  <i class="bi bi-list-task me-1 text-slate-500" aria-hidden="true"></i>
                  {{ pkg.remainingComplexity }} / {{ pkg.totalComplexity }} pts remaining ({{ pkg.remainingRequestsCount }} / {{ pkg.totalRequestsCount }} requests)
                </span>
              </div>

              <!-- Metrics Grid -->
              <div class="card-metrics-grid bg-slate-950/60 border border-slate-800 rounded-xl p-3 grid grid-cols-2 lg:grid-cols-4 gap-3">
                <!-- Metric 1: Scope Demand -->
                <div class="metric-block flex flex-col">
                  <div class="metric-label text-[10px] uppercase tracking-wider font-semibold text-slate-400">Scope Demand</div>
                  <div class="metric-value font-mono text-white font-bold text-sm md:text-base mt-0.5">
                    <template v-if="pkg.requiredDailyBurn !== null && pkg.requiredDailyBurn !== undefined">
                      {{ pkg.requiredDailyBurn.toFixed(1) }} <span class="metric-unit text-xs text-slate-400 font-normal">pts/day</span>
                    </template>
                    <template v-else>
                      <span class="text-slate-500">Unplanned</span>
                    </template>
                  </div>
                </div>

                <!-- Metric 2: Org Capacity Share -->
                <div class="metric-block flex flex-col">
                  <div class="metric-label text-[10px] uppercase tracking-wider font-semibold text-slate-400">Capacity Share</div>
                  <div class="metric-value font-mono font-bold text-sm md:text-base mt-0.5" :class="(pkg.orgCapacityShare ?? 0) > 100 ? 'text-rose-400' : 'text-cyan-400'">
                    <template v-if="pkg.orgCapacityShare !== null && pkg.orgCapacityShare !== undefined">
                      {{ pkg.orgCapacityShare.toFixed(1) }}<span class="metric-unit text-xs font-normal text-slate-400">%</span>
                    </template>
                    <template v-else>
                      <span class="text-slate-500">—</span>
                    </template>
                  </div>
                </div>

                <!-- Metric 3: Remaining Runway -->
                <div class="metric-block flex flex-col">
                  <div class="metric-label text-[10px] uppercase tracking-wider font-semibold text-slate-400">Remaining Runway</div>
                  <div class="metric-value font-mono text-sm md:text-base font-bold mt-0.5">
                    <template v-if="pkg.deadlineBreached">
                      <span class="text-rose-400 font-bold">Breached (0 days)</span>
                    </template>
                    <template v-else-if="pkg.workingDaysRemaining !== null && pkg.workingDaysRemaining !== undefined">
                      <span class="text-white">{{ pkg.workingDaysRemaining }} working days</span>
                    </template>
                    <template v-else>
                      <span class="text-slate-500">Unplanned</span>
                    </template>
                  </div>
                  <div v-if="pkg.deadline" class="metric-sub text-[10px] text-slate-400 font-mono mt-0.5">
                    Target: {{ formatDate(pkg.deadline) }}
                  </div>
                </div>

                <!-- Metric 4: Health Invariant Details -->
                <div class="metric-block flex flex-col">
                  <div class="metric-label text-[10px] uppercase tracking-wider font-semibold text-slate-400">Invariant Facts</div>
                  <div class="invariant-facts flex flex-wrap gap-1 mt-1">
                    <span v-if="pkg.blockedRequestsCount > 0" class="px-2 py-0.5 text-xs font-medium rounded bg-rose-500/10 text-rose-400 border border-rose-500/20">
                      {{ pkg.blockedRequestsCount }} paused
                    </span>
                    <span v-if="pkg.isDormant" class="px-2 py-0.5 text-xs font-medium rounded bg-amber-500/10 text-amber-400 border border-amber-500/20">
                      dormant {{ pkg.dormantDays }}d
                    </span>
                    <span v-if="pkg.activeWipCount > 0" class="px-2 py-0.5 text-xs font-medium rounded bg-cyan-500/10 text-cyan-400 border border-cyan-500/20">
                      wip {{ pkg.activeWipCount }} (oldest {{ pkg.oldestActiveWipDays }}d)
                    </span>
                    <span v-if="!pkg.deadlineBreached && pkg.blockedRequestsCount === 0 && !pkg.isDormant && !pkg.isWipStagnant" class="px-2 py-0.5 text-xs font-medium rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                      nominal
                    </span>
                  </div>
                </div>
              </div>

              <!-- Barcode & Action Strip -->
              <div class="card-footer-strip flex flex-wrap items-center justify-between gap-3 pt-3 border-t border-slate-800">
                <!-- 14-day Flow Barcode -->
                <div class="barcode-container flex items-center gap-2">
                  <span class="barcode-label text-slate-400 text-xs">14d Flow Barcode:</span>
                  <div class="barcode-strip bg-slate-950 border border-slate-800 px-2 py-1 rounded-lg inline-flex items-center gap-1" :title="`14-day flow activity. Outflow: ${pkg.outflow14dCount} completed.`">
                    <div
                      v-for="(day, idx) in pkg.flowBarcode"
                      :key="idx"
                      class="barcode-bar w-1.5 h-4.5 rounded-sm transition-transform duration-100 hover:scale-y-125"
                      :class="getBarcodeBarClass(day)"
                      :title="`${day.date}: ${day.closedCount} completed, ${day.stateMutationCount} mutations, ${day.blockedCount} blocked`"
                    ></div>
                  </div>
                  <span class="text-slate-400 text-xs ms-1">
                    (Outflow: <strong class="text-emerald-400 font-mono">{{ pkg.outflow14dCount }}</strong> completed)
                  </span>
                </div>

                <!-- Deep-link action button -->
                <button
                  type="button"
                  class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-800/80 hover:bg-slate-700 text-cyan-300 border border-slate-700 transition inline-flex items-center gap-1.5 shadow-sm"
                  :data-testid="`open-wp-${pkg.id}-btn`"
                  @click="navigateToPackage(pkg.id)"
                >
                  <span>Open Package</span>
                  <i class="bi bi-box-arrow-up-right text-xs" aria-hidden="true"></i>
                </button>
              </div>
            </div>
          </BaseCard>
        </div>

        <!-- Empty Feed State -->
        <div v-else class="empty-feed-card p-8 text-center bg-slate-900/90 border border-slate-800 rounded-xl shadow-lg">
          <i class="bi bi-check2-circle text-emerald-400 text-4xl mb-2 inline-block" aria-hidden="true"></i>
          <h3 class="text-base font-bold text-white mb-1">No packages match the active filter</h3>
          <p class="text-slate-400 text-xs mb-3 max-w-md mx-auto">
            <template v-if="isFilterActive">
              There are no active work packages in cell {{ activeFilterLabel }}.
            </template>
            <template v-else>
              All active work packages are currently flowing nominally with zero invariant violations.
            </template>
          </p>
          <button
            v-if="isFilterActive"
            type="button"
            class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white transition shadow-sm"
            @click="clearFilter"
          >
            Clear Filter
          </button>
        </div>
      </section>

      <!-- Section: Collapsible Nominal & Flowing Packages -->
      <section
        v-if="!isFilterActive"
        class="cockpit-section nominal-section"
        data-testid="nominal-packages-section"
      >
        <div
          class="nominal-toggle-header flex items-center justify-between p-4 bg-slate-900/80 hover:bg-slate-900 border border-slate-800 rounded-xl cursor-pointer transition shadow-md"
          data-testid="nominal-section-toggle"
          role="button"
          tabindex="0"
          @click="isNominalExpanded = !isNominalExpanded"
          @keydown.enter="isNominalExpanded = !isNominalExpanded"
          @keydown.space.prevent="isNominalExpanded = !isNominalExpanded"
        >
          <div class="flex items-center gap-2.5">
            <i
              class="bi text-cyan-400"
              :class="isNominalExpanded ? 'bi-chevron-down' : 'bi-chevron-right'"
              aria-hidden="true"
            ></i>
            <span class="font-bold text-sm text-white">Nominal &amp; Flowing Packages</span>
            <span class="px-2.5 py-0.5 text-xs font-semibold rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
              {{ nominalPackages.length }} packages
            </span>
          </div>
          <span class="text-slate-400 text-xs">
            {{ isNominalExpanded ? 'Click to collapse' : 'Click to expand' }}
          </span>
        </div>

        <div v-if="isNominalExpanded" class="nominal-content mt-3">
          <div v-if="nominalPackages.length > 0" class="feed-card-list space-y-3">
            <BaseCard
              v-for="pkg in nominalPackages"
              :key="pkg.id"
              variant="bordered"
              padding="md"
              hover-effect
              class="cockpit-card opacity-90 hover:opacity-100"
              :data-testid="`nominal-wp-card-${pkg.id}`"
            >
              <div class="cockpit-card-inner space-y-3">
                <div class="card-head flex flex-col md:flex-row md:items-start justify-between gap-2">
                  <div class="card-title-group">
                    <div class="flex items-center gap-2 mb-1">
                      <span class="px-2 py-0.5 text-xs font-mono font-semibold rounded bg-slate-950/80 text-cyan-400 border border-slate-800">
                        #{{ pkg.id.slice(0, 8) }}
                      </span>
                      <h3 class="text-base font-bold text-white tracking-tight m-0">{{ pkg.name || pkg.objective }}</h3>
                    </div>
                  </div>
                  <div class="card-badges flex items-center gap-1.5 flex-wrap">
                    <BaseBadge variant="success" size="sm">Flowing</BaseBadge>
                    <BaseBadge variant="neutral" size="sm">{{ pkg.pressureTier }}</BaseBadge>
                  </div>
                </div>

                <div class="card-context flex flex-wrap items-center gap-3 text-xs text-slate-400">
                  <span v-if="pkg.ownerName">
                    <i class="bi bi-person text-cyan-400 me-1" aria-hidden="true"></i>
                    {{ pkg.ownerName }}
                  </span>
                  <span v-if="pkg.customerName">
                    <i class="bi bi-building text-indigo-400 me-1" aria-hidden="true"></i>
                    {{ pkg.customerName }}
                  </span>
                  <span>
                    {{ pkg.remainingComplexity }} / {{ pkg.totalComplexity }} pts remaining
                  </span>
                  <span v-if="pkg.workingDaysRemaining !== null && pkg.workingDaysRemaining !== undefined">
                    {{ pkg.workingDaysRemaining }} working days remaining
                  </span>
                </div>

                <!-- Footer Strip -->
                <div class="card-footer-strip flex flex-wrap items-center justify-between gap-3 pt-3 border-t border-slate-800">
                  <div class="barcode-container flex items-center gap-2">
                    <span class="barcode-label text-slate-400 text-xs">14d Flow:</span>
                    <div class="barcode-strip bg-slate-950 border border-slate-800 px-2 py-1 rounded-lg inline-flex items-center gap-1">
                      <div
                        v-for="(day, idx) in pkg.flowBarcode"
                        :key="idx"
                        class="barcode-bar w-1.5 h-4.5 rounded-sm"
                        :class="getBarcodeBarClass(day)"
                        :title="`${day.date}: ${day.closedCount} completed`"
                      ></div>
                    </div>
                  </div>
                  <button
                    type="button"
                    class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-800/80 hover:bg-slate-700 text-slate-200 border border-slate-700 transition"
                    @click="navigateToPackage(pkg.id)"
                  >
                    Open Package
                  </button>
                </div>
              </div>
            </BaseCard>
          </div>
          <div v-else class="text-center p-4 text-slate-400 text-xs">
            No nominal packages currently present.
          </div>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
.cockpit-container {
  padding: 1.5rem;
  max-width: 1440px;
  margin: 0 auto;
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

/* 2D Matrix Cell Severity Tints (Executive Dark Palette) */
.matrix-cell--empty {
  background-color: rgba(2, 6, 23, 0.4);
  color: #475569;
}

.matrix-cell--empty .cell-count {
  font-weight: 400;
  opacity: 0.5;
}

.matrix-cell--critical {
  background-color: rgba(136, 19, 55, 0.35);
  color: #fda4af;
}

.matrix-cell--critical:hover {
  background-color: rgba(159, 18, 57, 0.5);
}

.matrix-cell--warning-high {
  background-color: rgba(124, 45, 18, 0.35);
  color: #fdba74;
}

.matrix-cell--warning-high:hover {
  background-color: rgba(154, 52, 18, 0.5);
}

.matrix-cell--warning-mid {
  background-color: rgba(113, 63, 18, 0.3);
  color: #fde047;
}

.matrix-cell--warning-mid:hover {
  background-color: rgba(133, 77, 14, 0.45);
}

.matrix-cell--nominal {
  background-color: rgba(6, 78, 59, 0.35);
  color: #6ee7b7;
}

.matrix-cell--nominal:hover {
  background-color: rgba(6, 95, 70, 0.5);
}

.matrix-cell--active-neutral {
  background-color: rgba(15, 23, 42, 0.9);
  color: #e2e8f0;
}

.matrix-cell--active-neutral:hover {
  background-color: rgba(30, 41, 59, 0.9);
}

/* Selected Cell */
.matrix-cell.is-selected {
  outline: 2px solid #22d3ee;
  outline-offset: -2px;
  box-shadow: inset 0 0 12px rgba(34, 211, 238, 0.25);
  color: #e0f2fe;
}

/* 14-day Flow Barcode Pulses */
.barcode-pulse--closed {
  background-color: #34d399; /* emerald-400 */
}

.barcode-pulse--blocked {
  background-color: #f43f5e; /* rose-500 */
}

.barcode-pulse--mutation {
  background-color: #22d3ee; /* cyan-400 */
}

.barcode-pulse--idle {
  background-color: #334155; /* slate-700 */
}
</style>
