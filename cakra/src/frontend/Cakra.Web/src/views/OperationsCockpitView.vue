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
 * (Architecture CR-025 §4 TD-005, TD-006; P3-S06).
 *
 * Provides executive operational awareness through:
 * - Macro telemetry ribbon with organization daily throughput C_org and aggregate load %.
 * - Interactive 2D Pressure × Health Triage Matrix with clickable cell filtering.
 * - Factual Operational Feed displaying exception packages (invariant violations + impossible pressure).
 * - Collapsible section for nominal and flowing work packages.
 * - 14-day flow activity barcode pulse for each package.
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
  <div class="cockpit-container">
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
          class="btn btn-outline-secondary btn-sm d-inline-flex align-items-center gap-1"
          data-testid="refresh-cockpit-btn"
          :disabled="isLoading"
          @click="loadData"
        >
          <i class="bi bi-arrow-clockwise" :class="{ 'spin-icon': isLoading }" aria-hidden="true"></i>
          <span>Refresh</span>
        </button>
      </template>
    </PageHeader>

    <!-- Error Alert Banner -->
    <div
      v-if="errorMessage"
      class="alert alert-danger d-flex align-items-center justify-content-between my-3"
      role="alert"
    >
      <div class="d-flex align-items-center gap-2">
        <i class="bi bi-exclamation-triangle-fill text-danger fs-5" aria-hidden="true"></i>
        <span>{{ errorMessage }}</span>
      </div>
      <button type="button" class="btn btn-sm btn-outline-danger" @click="loadData">
        Retry
      </button>
    </div>

    <!-- Loading Skeleton Indicator -->
    <div v-if="isLoading" class="cockpit-loading my-5 text-center">
      <div class="spinner-border text-primary" role="status">
        <span class="visually-hidden">Loading telemetry data...</span>
      </div>
      <div class="text-muted mt-2 small">Aggregating portfolio health invariants and demand density...</div>
    </div>

    <div v-else-if="cockpitData" class="cockpit-content mt-4">
      <!-- Section: 2D Pressure × Health Triage Matrix -->
      <section class="cockpit-section matrix-section mb-4">
        <div class="section-header d-flex align-items-center justify-content-between mb-2">
          <div>
            <h2 class="section-title h5 mb-0">Operational Triage Matrix</h2>
            <p class="section-subtitle text-muted small mb-0">
              Interactive 2D distribution of active Work Packages across scope pressure tiers and observable health invariants.
              Click any cell to filter the operational feed below.
            </p>
          </div>
          <div v-if="isFilterActive" class="matrix-filter-reset">
            <button
              type="button"
              class="btn btn-sm btn-outline-secondary d-inline-flex align-items-center gap-1"
              data-testid="clear-matrix-filter-btn"
              @click="clearFilter"
            >
              <i class="bi bi-x-circle" aria-hidden="true"></i>
              <span>Clear Filter</span>
            </button>
          </div>
        </div>

        <!-- 2D Matrix Table Container -->
        <div class="table-responsive matrix-table-wrapper">
          <table
            class="table table-bordered matrix-table align-middle text-center mb-0"
            data-testid="triage-matrix"
          >
            <thead>
              <tr class="matrix-header-row">
                <th class="matrix-corner-cell text-start">
                  <div class="matrix-axis-label">
                    <span class="axis-y">Health Invariants ↓</span>
                    <span class="axis-x">Scope Pressure →</span>
                  </div>
                </th>
                <th
                  v-for="col in MATRIX_COLUMNS"
                  :key="col.key"
                  class="matrix-col-header"
                >
                  <div class="col-title">{{ col.label }}</div>
                  <div class="col-sub">{{ col.sublabel }}</div>
                </th>
                <th class="matrix-total-header">Total</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in MATRIX_ROWS" :key="row.key" class="matrix-body-row">
                <!-- Row Header -->
                <th class="matrix-row-header text-start">
                  <div class="row-title">{{ row.label }}</div>
                  <div class="row-sub">{{ row.sublabel }}</div>
                </th>

                <!-- Data Cells -->
                <td
                  v-for="col in MATRIX_COLUMNS"
                  :key="col.key"
                  class="matrix-cell"
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
                  <div class="cell-inner">
                    <span class="cell-count">{{ getCellCount(row.key, col.key) }}</span>
                    <span v-if="isCellSelected(row.key, col.key)" class="selected-indicator">
                      <i class="bi bi-check-circle-fill" aria-hidden="true"></i>
                    </span>
                  </div>
                </td>

                <!-- Row Total -->
                <td class="matrix-row-total fw-bold">
                  {{ getRowTotal(row.key) }}
                </td>
              </tr>
            </tbody>
            <tfoot>
              <tr class="matrix-footer-row">
                <th class="matrix-row-header text-start">Total</th>
                <td v-for="col in MATRIX_COLUMNS" :key="col.key" class="matrix-col-total fw-bold">
                  {{ getColTotal(col.key) }}
                </td>
                <td class="matrix-grand-total fw-bold text-primary">
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
        class="alert alert-primary d-flex align-items-center justify-content-between mb-4 matrix-filter-banner"
        data-testid="matrix-filter-banner"
      >
        <div class="d-flex align-items-center gap-2">
          <i class="bi bi-funnel-fill text-primary" aria-hidden="true"></i>
          <div>
            <strong>Filtered by:</strong>
            <span class="badge bg-primary text-white mx-1">{{ activeFilterLabel }}</span>
            <span class="text-muted small">({{ feedPackages.length }} work packages matching)</span>
          </div>
        </div>
        <button
          type="button"
          class="btn btn-sm btn-outline-primary"
          data-testid="clear-filter-btn"
          @click="clearFilter"
        >
          Clear Filter
        </button>
      </div>

      <!-- Section: Exception Operational Feed -->
      <section class="cockpit-section feed-section mb-4">
        <div class="d-flex align-items-center justify-content-between mb-3">
          <div>
            <h2 class="section-title h5 mb-0">
              {{ isFilterActive ? `Filtered Operational Feed` : `Operational Exceptions Feed` }}
            </h2>
            <p class="section-subtitle text-muted small mb-0">
              <template v-if="isFilterActive">
                Displaying packages matching selected matrix cell: {{ activeFilterLabel }}.
              </template>
              <template v-else>
                Showing invariant violations and impossible pressure packages requiring operational attention ({{ feedPackages.length }} items).
              </template>
            </p>
          </div>
          <span class="badge bg-secondary-subtle text-secondary-emphasis">
            {{ feedPackages.length }} packages
          </span>
        </div>

        <!-- Feed List -->
        <div
          v-if="feedPackages.length > 0"
          class="feed-card-list"
          data-testid="cockpit-operational-feed"
        >
          <BaseCard
            v-for="pkg in feedPackages"
            :key="pkg.id"
            variant="bordered"
            padding="md"
            hover-effect
            class="cockpit-card mb-3"
            :data-testid="`cockpit-wp-card-${pkg.id}`"
          >
            <!-- Card Main Row -->
            <div class="cockpit-card-inner">
              <!-- Top Row: ID, Title, Badges -->
              <div class="card-head d-flex flex-wrap align-items-start justify-content-between gap-2 mb-2">
                <div class="card-title-group">
                  <div class="d-flex align-items-center gap-2 mb-1">
                    <span class="badge bg-light text-dark font-monospace border">
                      #{{ pkg.id.slice(0, 8) }}
                    </span>
                    <h3 class="card-title h6 mb-0">{{ pkg.name || pkg.objective }}</h3>
                  </div>
                  <p v-if="pkg.name && pkg.objective && pkg.name !== pkg.objective" class="card-objective text-muted small mb-0">
                    {{ pkg.objective }}
                  </p>
                </div>

                <div class="card-badges d-flex align-items-center gap-1 flex-wrap">
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
              <div class="card-context d-flex flex-wrap align-items-center gap-3 text-muted small mb-3">
                <span v-if="pkg.ownerName">
                  <i class="bi bi-person me-1" aria-hidden="true"></i>
                  <strong>Owner:</strong> {{ pkg.ownerName }}
                </span>
                <span v-else class="text-danger">
                  <i class="bi bi-person-x me-1" aria-hidden="true"></i>
                  <em>Unassigned</em>
                </span>

                <span v-if="pkg.customerName">
                  <i class="bi bi-building me-1" aria-hidden="true"></i>
                  <strong>Customer:</strong> {{ pkg.customerName }}
                </span>

                <span v-if="pkg.productName">
                  <i class="bi bi-box-seam me-1" aria-hidden="true"></i>
                  <strong>Product:</strong> {{ pkg.productName }}
                </span>

                <span>
                  <i class="bi bi-list-task me-1" aria-hidden="true"></i>
                  {{ pkg.remainingComplexity }} / {{ pkg.totalComplexity }} pts remaining ({{ pkg.remainingRequestsCount }} / {{ pkg.totalRequestsCount }} requests)
                </span>
              </div>

              <!-- Metrics Grid -->
              <div class="card-metrics-grid mb-3">
                <!-- Metric 1: Scope Demand -->
                <div class="metric-block">
                  <div class="metric-label">Scope Demand</div>
                  <div class="metric-value font-monospace">
                    <template v-if="pkg.requiredDailyBurn !== null && pkg.requiredDailyBurn !== undefined">
                      {{ pkg.requiredDailyBurn.toFixed(1) }} <span class="metric-unit">pts/day</span>
                    </template>
                    <template v-else>
                      <span class="text-muted">Unplanned</span>
                    </template>
                  </div>
                </div>

                <!-- Metric 2: Org Capacity Share -->
                <div class="metric-block">
                  <div class="metric-label">Capacity Share</div>
                  <div class="metric-value font-monospace" :class="{ 'text-danger': (pkg.orgCapacityShare ?? 0) > 100 }">
                    <template v-if="pkg.orgCapacityShare !== null && pkg.orgCapacityShare !== undefined">
                      {{ pkg.orgCapacityShare.toFixed(1) }}<span class="metric-unit">%</span>
                    </template>
                    <template v-else>
                      <span class="text-muted">—</span>
                    </template>
                  </div>
                </div>

                <!-- Metric 3: Remaining Runway -->
                <div class="metric-block">
                  <div class="metric-label">Remaining Runway</div>
                  <div class="metric-value">
                    <template v-if="pkg.deadlineBreached">
                      <span class="text-danger fw-semibold">Breached (0 days)</span>
                    </template>
                    <template v-else-if="pkg.workingDaysRemaining !== null && pkg.workingDaysRemaining !== undefined">
                      <span>{{ pkg.workingDaysRemaining }} working days</span>
                    </template>
                    <template v-else>
                      <span class="text-muted">Unplanned</span>
                    </template>
                  </div>
                  <div v-if="pkg.deadline" class="metric-sub text-muted">
                    Target: {{ formatDate(pkg.deadline) }}
                  </div>
                </div>

                <!-- Metric 4: Health Invariant Details -->
                <div class="metric-block">
                  <div class="metric-label">Invariant Facts</div>
                  <div class="invariant-facts d-flex flex-wrap gap-1 mt-1">
                    <span v-if="pkg.blockedRequestsCount > 0" class="badge bg-danger-subtle text-danger-emphasis">
                      {{ pkg.blockedRequestsCount }} paused
                    </span>
                    <span v-if="pkg.isDormant" class="badge bg-warning-subtle text-warning-emphasis">
                      dormant {{ pkg.dormantDays }}d
                    </span>
                    <span v-if="pkg.activeWipCount > 0" class="badge bg-info-subtle text-info-emphasis">
                      wip {{ pkg.activeWipCount }} (oldest {{ pkg.oldestActiveWipDays }}d)
                    </span>
                    <span v-if="!pkg.deadlineBreached && pkg.blockedRequestsCount === 0 && !pkg.isDormant && !pkg.isWipStagnant" class="badge bg-success-subtle text-success-emphasis">
                      nominal
                    </span>
                  </div>
                </div>
              </div>

              <!-- Barcode & Action Strip -->
              <div class="card-footer-strip d-flex flex-wrap align-items-center justify-content-between gap-3 pt-2 border-top">
                <!-- 14-day Flow Barcode -->
                <div class="barcode-container d-flex align-items-center gap-2">
                  <span class="barcode-label text-muted small">14d Flow Barcode:</span>
                  <div class="barcode-strip" :title="`14-day flow activity. Outflow: ${pkg.outflow14dCount} completed.`">
                    <div
                      v-for="(day, idx) in pkg.flowBarcode"
                      :key="idx"
                      class="barcode-bar"
                      :class="getBarcodeBarClass(day)"
                      :title="`${day.date}: ${day.closedCount} completed, ${day.stateMutationCount} mutations, ${day.blockedCount} blocked`"
                    ></div>
                  </div>
                  <span class="text-muted small ms-1">
                    (Outflow: <strong>{{ pkg.outflow14dCount }}</strong> completed)
                  </span>
                </div>

                <!-- Deep-link action button -->
                <button
                  type="button"
                  class="btn btn-sm btn-outline-primary d-inline-flex align-items-center gap-1"
                  :data-testid="`open-wp-${pkg.id}-btn`"
                  @click="navigateToPackage(pkg.id)"
                >
                  <span>Open Package</span>
                  <i class="bi bi-box-arrow-up-right" aria-hidden="true"></i>
                </button>
              </div>
            </div>
          </BaseCard>
        </div>

        <!-- Empty Feed State -->
        <div v-else class="empty-feed-card p-5 text-center bg-white border rounded">
          <i class="bi bi-check2-circle text-success fs-1 mb-2 d-block" aria-hidden="true"></i>
          <h3 class="h6 fw-bold">No packages match the active filter</h3>
          <p class="text-muted small mb-3">
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
            class="btn btn-sm btn-outline-primary"
            @click="clearFilter"
          >
            Clear Filter
          </button>
        </div>
      </section>

      <!-- Section: Collapsible Nominal & Flowing Packages -->
      <section
        v-if="!isFilterActive"
        class="cockpit-section nominal-section mb-4"
        data-testid="nominal-packages-section"
      >
        <div
          class="nominal-toggle-header d-flex align-items-center justify-content-between p-3 bg-light border rounded cursor-pointer"
          data-testid="nominal-section-toggle"
          role="button"
          tabindex="0"
          @click="isNominalExpanded = !isNominalExpanded"
          @keydown.enter="isNominalExpanded = !isNominalExpanded"
          @keydown.space.prevent="isNominalExpanded = !isNominalExpanded"
        >
          <div class="d-flex align-items-center gap-2">
            <i
              class="bi"
              :class="isNominalExpanded ? 'bi-chevron-down' : 'bi-chevron-right'"
              aria-hidden="true"
            ></i>
            <span class="fw-semibold">Nominal &amp; Flowing Packages</span>
            <span class="badge bg-success-subtle text-success-emphasis">
              {{ nominalPackages.length }} packages
            </span>
          </div>
          <span class="text-muted small">
            {{ isNominalExpanded ? 'Click to collapse' : 'Click to expand' }}
          </span>
        </div>

        <div v-if="isNominalExpanded" class="nominal-content mt-3">
          <div v-if="nominalPackages.length > 0" class="feed-card-list">
            <BaseCard
              v-for="pkg in nominalPackages"
              :key="pkg.id"
              variant="bordered"
              padding="md"
              hover-effect
              class="cockpit-card mb-3 opacity-90"
              :data-testid="`nominal-wp-card-${pkg.id}`"
            >
              <div class="cockpit-card-inner">
                <div class="card-head d-flex flex-wrap align-items-start justify-content-between gap-2 mb-2">
                  <div class="card-title-group">
                    <div class="d-flex align-items-center gap-2 mb-1">
                      <span class="badge bg-light text-dark font-monospace border">
                        #{{ pkg.id.slice(0, 8) }}
                      </span>
                      <h3 class="card-title h6 mb-0">{{ pkg.name || pkg.objective }}</h3>
                    </div>
                  </div>
                  <div class="card-badges d-flex align-items-center gap-1 flex-wrap">
                    <BaseBadge variant="success" size="sm">Flowing</BaseBadge>
                    <BaseBadge variant="neutral" size="sm">{{ pkg.pressureTier }}</BaseBadge>
                  </div>
                </div>

                <div class="card-context d-flex flex-wrap align-items-center gap-3 text-muted small mb-2">
                  <span v-if="pkg.ownerName">
                    <i class="bi bi-person me-1" aria-hidden="true"></i>
                    {{ pkg.ownerName }}
                  </span>
                  <span v-if="pkg.customerName">
                    <i class="bi bi-building me-1" aria-hidden="true"></i>
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
                <div class="card-footer-strip d-flex flex-wrap align-items-center justify-content-between gap-3 pt-2 border-top">
                  <div class="barcode-container d-flex align-items-center gap-2">
                    <span class="barcode-label text-muted small">14d Flow:</span>
                    <div class="barcode-strip">
                      <div
                        v-for="(day, idx) in pkg.flowBarcode"
                        :key="idx"
                        class="barcode-bar"
                        :class="getBarcodeBarClass(day)"
                        :title="`${day.date}: ${day.closedCount} completed`"
                      ></div>
                    </div>
                  </div>
                  <button
                    type="button"
                    class="btn btn-sm btn-outline-secondary"
                    @click="navigateToPackage(pkg.id)"
                  >
                    Open Package
                  </button>
                </div>
              </div>
            </BaseCard>
          </div>
          <div v-else class="text-center p-3 text-muted small">
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

/* 2D Matrix Styling */
.matrix-table-wrapper {
  background: var(--cakra-bg-surface, #ffffff);
  border: 1px solid var(--cakra-border, #e2e8f0);
  border-radius: var(--cakra-radius-lg, 12px);
  overflow: hidden;
  box-shadow: var(--cakra-shadow-xs, 0 1px 2px 0 rgb(0 0 0 / 0.04));
}

.matrix-table {
  font-size: 0.8125rem;
  border-collapse: separate;
  border-spacing: 0;
}

.matrix-corner-cell {
  background-color: var(--cakra-slate-100, #f1f5f9);
  width: 220px;
  vertical-align: middle;
  padding: 0.75rem 1rem;
}

.matrix-axis-label {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.7rem;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--cakra-slate-600, #475569);
  letter-spacing: 0.04em;
}

.matrix-col-header {
  background-color: var(--cakra-slate-100, #f1f5f9);
  padding: 0.625rem 0.5rem;
  min-width: 120px;
  font-weight: 600;
  color: var(--cakra-slate-800, #1e293b);
}

.col-title {
  font-size: 0.75rem;
  font-weight: 700;
}

.col-sub {
  font-size: 0.6875rem;
  font-weight: 400;
  color: var(--cakra-slate-500, #64748b);
}

.matrix-row-header {
  background-color: var(--cakra-slate-50, #f8fafc);
  padding: 0.625rem 1rem;
  font-weight: 600;
  color: var(--cakra-slate-800, #1e293b);
  border-right: 1px solid var(--cakra-border, #e2e8f0);
}

.row-title {
  font-size: 0.75rem;
  font-weight: 700;
}

.row-sub {
  font-size: 0.6875rem;
  font-weight: 400;
  color: var(--cakra-slate-500, #64748b);
}

.matrix-total-header,
.matrix-row-total,
.matrix-col-total,
.matrix-grand-total {
  background-color: var(--cakra-slate-100, #f1f5f9);
  padding: 0.625rem 0.75rem;
  font-size: 0.8125rem;
}

.matrix-cell {
  position: relative;
  cursor: pointer;
  padding: 0.75rem 0.5rem;
  transition: all 0.15s ease-in-out;
  user-select: none;
}

.matrix-cell:hover {
  filter: brightness(0.95);
  box-shadow: inset 0 0 0 2px var(--cakra-primary, #364f6b);
}

.cell-inner {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.25rem;
}

.cell-count {
  font-size: 0.9375rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

/* Matrix Heatmap Severity Tints */
.matrix-cell--empty {
  background-color: #ffffff;
  color: var(--cakra-slate-400, #94a3b8);
}

.matrix-cell--empty .cell-count {
  font-weight: 400;
  opacity: 0.5;
}

.matrix-cell--critical {
  background-color: #fee2e2;
  color: #b91c1c;
}

.matrix-cell--warning-high {
  background-color: #ffedd5;
  color: #c2410c;
}

.matrix-cell--warning-mid {
  background-color: #fef3c7;
  color: #b45309;
}

.matrix-cell--nominal {
  background-color: #ecfdf5;
  color: #047857;
}

.matrix-cell--active-neutral {
  background-color: #f1f5f9;
  color: #334155;
}

/* Selected Cell */
.matrix-cell.is-selected {
  outline: 3px solid var(--cakra-primary, #364f6b);
  outline-offset: -3px;
  box-shadow: 0 0 0 4px rgba(54, 79, 107, 0.25);
  font-weight: 800;
}

.selected-indicator {
  font-size: 0.75rem;
  color: var(--cakra-primary, #364f6b);
}

/* Card metrics */
.card-metrics-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 0.75rem;
  background-color: var(--cakra-slate-50, #f8fafc);
  border: 1px solid var(--cakra-slate-200, #e2e8f0);
  border-radius: var(--cakra-radius-md, 8px);
  padding: 0.75rem 1rem;
}

.metric-block {
  display: flex;
  flex-direction: column;
}

.metric-label {
  font-size: 0.6875rem;
  text-transform: uppercase;
  letter-spacing: 0.03em;
  font-weight: 600;
  color: var(--cakra-slate-500, #64748b);
  margin-bottom: 0.125rem;
}

.metric-value {
  font-size: 0.9375rem;
  font-weight: 700;
  color: var(--cakra-slate-900, #0f172a);
}

.metric-unit {
  font-size: 0.75rem;
  font-weight: 400;
  color: var(--cakra-slate-600, #475569);
}

.metric-sub {
  font-size: 0.6875rem;
}

/* 14-day Flow Barcode */
.barcode-strip {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  background: var(--cakra-slate-100, #f1f5f9);
  padding: 3px 4px;
  border-radius: 4px;
  border: 1px solid var(--cakra-slate-300, #cbd5e1);
}

.barcode-bar {
  width: 6px;
  height: 18px;
  border-radius: 1px;
  transition: transform 0.1s ease;
}

.barcode-bar:hover {
  transform: scaleY(1.2);
}

.barcode-pulse--closed {
  background-color: #10b981; /* emerald */
}

.barcode-pulse--blocked {
  background-color: #fc5185; /* pink/coral */
}

.barcode-pulse--mutation {
  background-color: #3fc1c9; /* cyan */
}

.barcode-pulse--idle {
  background-color: #cbd5e1; /* slate-300 */
}

.cursor-pointer {
  cursor: pointer;
}
</style>
