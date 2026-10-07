<script setup lang="ts">
import { computed } from 'vue'

export interface BaseBadgeProps {
  variant?: 'neutral' | 'success' | 'warning' | 'danger' | 'brand' | 'info'
  styleType?: 'subtle' | 'solid' | 'outline'
  size?: 'sm' | 'md'
  pulse?: boolean
}

const props = withDefaults(defineProps<BaseBadgeProps>(), {
  variant: 'neutral',
  styleType: 'subtle',
  size: 'md',
  pulse: false,
})

const computedClasses = computed(() => [
  'base-badge',
  `base-badge--variant-${props.variant}`,
  `base-badge--style-${props.styleType}`,
  `base-badge--size-${props.size}`,
  { 'base-badge--has-pulse': props.pulse },
])
</script>

<template>
  <span :class="computedClasses">
    <span v-if="pulse" class="badge-pulse-dot" aria-hidden="true"></span>
    <slot />
  </span>
</template>

<style scoped>
.base-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  font-weight: 500;
  line-height: 1.25;
  border-radius: var(--cakra-radius-full, 9999px);
  white-space: nowrap;
  vertical-align: middle;
  box-sizing: border-box;
  transition: background-color 0.15s ease, color 0.15s ease, border-color 0.15s ease;
}

/* Sizes */
.base-badge--size-md {
  font-size: 0.75rem; /* 12px */
  padding: 0.125rem 0.5rem;
}

.base-badge--size-sm {
  font-size: 0.6875rem; /* 11px */
  padding: 0.0625rem 0.375rem;
  gap: 0.25rem;
}

/* Subtle Style (10% tint background) */
.base-badge--style-subtle.base-badge--variant-neutral {
  background-color: var(--cakra-bg-subtle, #f1f5f9);
  color: var(--cakra-text-muted, #475569);
  border: 1px solid var(--cakra-border, #e2e8f0);
}

.base-badge--style-subtle.base-badge--variant-success {
  background-color: rgba(16, 185, 129, 0.1);
  color: #059669;
  border: 1px solid transparent;
}

.base-badge--style-subtle.base-badge--variant-warning {
  background-color: rgba(245, 158, 11, 0.1);
  color: #d97706;
  border: 1px solid transparent;
}

.base-badge--style-subtle.base-badge--variant-danger {
  background-color: rgba(244, 63, 94, 0.1);
  color: #e11d48;
  border: 1px solid transparent;
}

.base-badge--style-subtle.base-badge--variant-brand {
  background-color: rgba(54, 79, 107, 0.1);
  color: var(--cakra-primary, #364f6b);
  border: 1px solid transparent;
}

.base-badge--style-subtle.base-badge--variant-info {
  background-color: rgba(63, 193, 201, 0.15);
  color: #0891b2;
  border: 1px solid transparent;
}

/* Solid Style */
.base-badge--style-solid.base-badge--variant-neutral {
  background-color: var(--cakra-slate-700, #334155);
  color: #ffffff;
  border: 1px solid transparent;
}

.base-badge--style-solid.base-badge--variant-success {
  background-color: var(--cakra-emerald, #10b981);
  color: #ffffff;
  border: 1px solid transparent;
}

.base-badge--style-solid.base-badge--variant-warning {
  background-color: var(--cakra-amber, #f59e0b);
  color: #ffffff;
  border: 1px solid transparent;
}

.base-badge--style-solid.base-badge--variant-danger {
  background-color: var(--cakra-rose, #f43f5e);
  color: #ffffff;
  border: 1px solid transparent;
}

.base-badge--style-solid.base-badge--variant-brand {
  background-color: var(--cakra-primary, #364f6b);
  color: #ffffff;
  border: 1px solid transparent;
}

.base-badge--style-solid.base-badge--variant-info {
  background-color: var(--cakra-secondary, #3fc1c9);
  color: #ffffff;
  border: 1px solid transparent;
}

/* Outline Style */
.base-badge--style-outline.base-badge--variant-neutral {
  background-color: transparent;
  color: var(--cakra-text-muted, #475569);
  border: 1px solid var(--cakra-border, #e2e8f0);
}

.base-badge--style-outline.base-badge--variant-success {
  background-color: transparent;
  color: #059669;
  border: 1px solid #10b981;
}

.base-badge--style-outline.base-badge--variant-warning {
  background-color: transparent;
  color: #d97706;
  border: 1px solid #f59e0b;
}

.base-badge--style-outline.base-badge--variant-danger {
  background-color: transparent;
  color: #e11d48;
  border: 1px solid #f43f5e;
}

.base-badge--style-outline.base-badge--variant-brand {
  background-color: transparent;
  color: var(--cakra-primary, #364f6b);
  border: 1px solid var(--cakra-primary, #364f6b);
}

.base-badge--style-outline.base-badge--variant-info {
  background-color: transparent;
  color: #0891b2;
  border: 1px solid var(--cakra-secondary, #3fc1c9);
}

/* Pulse Dot */
.badge-pulse-dot {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: var(--cakra-radius-full, 9999px);
  animation: live-pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite;
  flex-shrink: 0;
}

.base-badge--size-sm .badge-pulse-dot {
  width: 5px;
  height: 5px;
}

.base-badge--variant-neutral .badge-pulse-dot {
  background-color: var(--cakra-slate-500, #64748b);
}

.base-badge--variant-success .badge-pulse-dot {
  background-color: var(--cakra-emerald, #10b981);
}

.base-badge--variant-warning .badge-pulse-dot {
  background-color: var(--cakra-amber, #f59e0b);
}

.base-badge--variant-danger .badge-pulse-dot {
  background-color: var(--cakra-rose, #f43f5e);
}

.base-badge--variant-brand .badge-pulse-dot {
  background-color: var(--cakra-primary, #364f6b);
}

.base-badge--variant-info .badge-pulse-dot {
  background-color: var(--cakra-secondary, #3fc1c9);
}

.base-badge--style-solid .badge-pulse-dot {
  background-color: #ffffff;
}

@keyframes live-pulse {
  0%, 100% {
    opacity: 1;
    transform: scale(1);
  }
  50% {
    opacity: 0.4;
    transform: scale(0.85);
  }
}
</style>
