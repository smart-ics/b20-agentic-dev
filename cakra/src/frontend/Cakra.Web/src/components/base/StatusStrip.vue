<script setup lang="ts">
export interface StatusStripItem {
  label: string
  value: string | number
  color?: 'primary' | 'success' | 'warning' | 'danger' | 'secondary' | 'cyan' | 'indigo' | 'emerald' | 'amber' | 'rose'
  dot?: boolean
  progress?: number
  testId?: string
  dataTestId?: string
}

export interface StatusStripProps {
  items: StatusStripItem[]
}

defineProps<StatusStripProps>()

function resolveProgress(item: StatusStripItem): number {
  if (typeof item.progress === 'number') {
    return Math.min(Math.max(item.progress, 0), 100)
  }
  if (typeof item.value === 'string' && item.value.includes('%')) {
    const num = parseFloat(item.value)
    if (!Number.isNaN(num)) {
      return Math.min(Math.max(num, 0), 100)
    }
  }
  return 100
}

function valueColorClass(color?: StatusStripItem['color']): string {
  switch (color) {
    case 'cyan':
      return 'text-cyan-600 dark:text-cyan-400'
    case 'success':
    case 'emerald':
      return 'text-emerald-600 dark:text-emerald-400'
    case 'warning':
    case 'amber':
      return 'text-amber-600 dark:text-amber-400'
    case 'danger':
    case 'rose':
      return 'text-rose-600 dark:text-rose-400'
    case 'indigo':
      return 'text-indigo-600 dark:text-indigo-400'
    case 'secondary':
      return 'text-slate-600 dark:text-slate-300'
    case 'primary':
    default:
      return 'text-slate-900 dark:text-white'
  }
}

function dotColorClass(color?: StatusStripItem['color']): string {
  switch (color) {
    case 'cyan':
      return 'bg-cyan-500'
    case 'success':
    case 'emerald':
      return 'bg-emerald-500'
    case 'warning':
    case 'amber':
      return 'bg-amber-500'
    case 'danger':
    case 'rose':
      return 'bg-rose-500'
    case 'indigo':
      return 'bg-indigo-500'
    case 'secondary':
      return 'bg-slate-500'
    case 'primary':
    default:
      return 'bg-cyan-400'
  }
}

function gaugeBarColorClass(color?: StatusStripItem['color']): string {
  switch (color) {
    case 'cyan':
      return 'bg-cyan-500'
    case 'success':
    case 'emerald':
      return 'bg-emerald-500'
    case 'warning':
    case 'amber':
      return 'bg-amber-500'
    case 'danger':
    case 'rose':
      return 'bg-rose-500'
    case 'indigo':
      return 'bg-indigo-500'
    case 'secondary':
      return 'bg-slate-500'
    case 'primary':
    default:
      return 'bg-cyan-500'
  }
}
</script>

<template>
  <div class="status-strip flex flex-wrap items-stretch gap-3 w-full">
    <div
      v-for="(item, index) in items"
      :key="index"
      class="status-strip__item flex-1 min-w-[140px] bg-white border border-slate-200 dark:bg-slate-900/90 dark:border-slate-800 rounded-xl p-3.5 flex flex-col justify-between shadow-sm dark:shadow-lg dark:backdrop-blur-sm transition-all"
    >
      <slot name="item" :item="item" :index="index">
        <div class="flex items-center justify-between gap-2 mb-1.5">
          <span class="status-strip__label text-xs uppercase tracking-wider font-semibold text-slate-500 dark:text-slate-400 truncate">
            {{ item.label }}
          </span>
          <span
            v-if="item.dot"
            class="status-strip__dot w-2 h-2 rounded-full flex-shrink-0 animate-pulse"
            :class="dotColorClass(item.color)"
            aria-hidden="true"
          />
        </div>

        <div class="flex items-baseline gap-2 mb-2">
          <span
            class="status-strip__value text-3xl font-black tracking-tight"
            :class="valueColorClass(item.color)"
            :data-testid="item.testId || item.dataTestId"
          >
            {{ item.value }}
          </span>
        </div>

        <!-- Dark gauge track -->
        <div class="status-strip__gauge bg-slate-200 dark:bg-slate-800 h-1.5 rounded-full overflow-hidden w-full">
          <div
            class="h-full rounded-full transition-all duration-500"
            :class="gaugeBarColorClass(item.color)"
            :style="{ width: `${resolveProgress(item)}%` }"
          />
        </div>
      </slot>
    </div>
  </div>
</template>

<style scoped>
.status-strip {
  box-sizing: border-box;
}

:global(.dark) .status-strip__item {
  background-color: rgba(15, 23, 42, 0.9);
  border-color: #1e293b;
}

:global(.dark) .status-strip__value {
  font-feature-settings: 'tnum';
  font-variant-numeric: tabular-nums;
}
</style>
