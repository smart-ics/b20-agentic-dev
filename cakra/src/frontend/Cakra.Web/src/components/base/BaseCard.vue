<script setup lang="ts">
import { computed } from 'vue'

export interface BaseCardProps {
  variant?: 'bordered' | 'flat' | 'ghost' | 'elevated'
  padding?: 'none' | 'sm' | 'md' | 'lg'
  rounded?: 'sm' | 'md' | 'lg' | 'xl'
  hoverEffect?: boolean
}

const props = withDefaults(defineProps<BaseCardProps>(), {
  variant: 'bordered',
  padding: 'md',
  rounded: 'lg',
  hoverEffect: false,
})

const computedClasses = computed(() => [
  'base-card',
  `base-card--${props.variant}`,
  `base-card--padding-${props.padding}`,
  `base-card--rounded-${props.rounded}`,
  { 'base-card--hover': props.hoverEffect },
])
</script>

<template>
  <div :class="computedClasses">
    <slot />
  </div>
</template>

<style scoped>
.base-card {
  box-sizing: border-box;
  color: var(--cakra-text-main, #0f172a);
}

/* Card Variants */
.base-card--bordered {
  background: var(--cakra-bg-surface, #ffffff);
  border: 1px solid var(--cakra-border, #e2e8f0);
  box-shadow: var(--cakra-shadow-xs, 0 1px 2px 0 rgb(0 0 0 / 0.04));
}

.base-card--flat {
  background: var(--cakra-bg-subtle, #f1f5f9);
  border: 1px solid transparent;
  box-shadow: none;
}

.base-card--ghost {
  background: transparent;
  border: 1px solid transparent;
  box-shadow: none;
}

.base-card--elevated {
  background: var(--cakra-bg-surface, #ffffff);
  border: 1px solid var(--cakra-border-subtle, #f1f5f9);
  box-shadow: var(--cakra-shadow-md, 0 4px 6px -1px rgb(0 0 0 / 0.08), 0 2px 4px -2px rgb(0 0 0 / 0.04));
}

/* Padding Variants */
.base-card--padding-none {
  padding: 0;
}

.base-card--padding-sm {
  padding: 0.5rem 0.75rem;
}

.base-card--padding-md {
  padding: 1rem;
}

.base-card--padding-lg {
  padding: 1.25rem;
}

/* Border Radius Variants */
.base-card--rounded-sm {
  border-radius: var(--cakra-radius-sm, 6px);
}

.base-card--rounded-md {
  border-radius: var(--cakra-radius-md, 8px);
}

.base-card--rounded-lg {
  border-radius: var(--cakra-radius-lg, 12px);
}

.base-card--rounded-xl {
  border-radius: var(--cakra-radius-xl, 16px);
}

/* Hover Effect */
.base-card--hover {
  transition: border-color 0.2s ease, box-shadow 0.2s ease, background-color 0.2s ease, transform 0.2s ease;
}

.base-card--bordered.base-card--hover:hover {
  border-color: var(--cakra-border-strong, #cbd5e1);
  box-shadow: var(--cakra-shadow-sm, 0 1px 3px 0 rgb(0 0 0 / 0.08));
}

.base-card--flat.base-card--hover:hover {
  background-color: var(--cakra-slate-100, #f1f5f9);
}

.base-card--elevated.base-card--hover:hover {
  box-shadow: var(--cakra-shadow-lg, 0 10px 15px -3px rgb(0 0 0 / 0.08));
  transform: translateY(-1px);
}
</style>
