<script setup lang="ts">
export interface StatusStripItem {
  label: string
  value: string | number
  color?: 'primary' | 'success' | 'warning' | 'danger' | 'secondary'
  dot?: boolean
  testId?: string
  dataTestId?: string
}

export interface StatusStripProps {
  items: StatusStripItem[]
}

defineProps<StatusStripProps>()

function itemColorClass(color?: StatusStripItem['color']): string {
  if (!color) return ''
  return `status-strip__color--${color}`
}
</script>

<template>
  <div class="status-strip">
    <template v-for="(item, index) in items" :key="index">
      <span v-if="index > 0" class="status-strip__divider" aria-hidden="true">|</span>
      <div class="status-strip__item">
        <slot name="item" :item="item" :index="index">
          <span
            v-if="item.dot"
            class="status-strip__dot"
            :class="itemColorClass(item.color)"
            aria-hidden="true"
          />
          <span
            class="status-strip__value"
            :class="itemColorClass(item.color)"
            :data-testid="item.testId || item.dataTestId"
          >
            {{ item.value }}
          </span>
          <span class="status-strip__label">
            {{ item.label }}
          </span>
        </slot>
      </div>
    </template>
  </div>
</template>

<style scoped>
.status-strip {
  display: inline-flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.375rem 0.75rem;
  background-color: var(--cakra-bg-surface, #ffffff);
  border: 1px solid var(--cakra-border, #e2e8f0);
  border-radius: var(--cakra-radius-md, 8px);
  box-shadow: var(--cakra-shadow-xs, 0 1px 2px 0 rgb(0 0 0 / 0.04));
  font-size: 0.75rem;
  line-height: 1.25;
  color: var(--cakra-text-main, #0f172a);
  box-sizing: border-box;
}

.status-strip__item {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  white-space: nowrap;
}

.status-strip__divider {
  color: var(--cakra-border-strong, #cbd5e1);
  font-weight: 300;
  user-select: none;
  font-size: 0.75rem;
  line-height: 1;
}

.status-strip__value {
  font-weight: 700;
  color: var(--cakra-text-main, #0f172a);
  font-feature-settings: 'tnum';
  font-variant-numeric: tabular-nums;
}

.status-strip__label {
  color: var(--cakra-text-muted, #475569);
  font-weight: 400;
}

/* Color Semantics */
.status-strip__color--primary {
  color: var(--cakra-primary, #364f6b);
}

.status-strip__color--success {
  color: #059669;
}

.status-strip__color--warning {
  color: #d97706;
}

.status-strip__color--danger {
  color: #e11d48;
}

.status-strip__color--secondary {
  color: var(--cakra-text-muted, #475569);
}

/* Dots */
.status-strip__dot {
  width: 6px;
  height: 6px;
  border-radius: var(--cakra-radius-full, 9999px);
  background-color: currentColor;
  flex-shrink: 0;
}
</style>
