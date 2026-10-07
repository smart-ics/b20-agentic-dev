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

const variantClasses = computed(() => {
  switch (props.variant) {
    case 'bordered':
      return 'bg-white border border-slate-200 shadow-sm text-slate-900 dark:bg-slate-900/90 dark:border-slate-800 dark:shadow-lg dark:text-slate-100'
    case 'flat':
      return 'bg-slate-100 border border-transparent text-slate-900 dark:bg-slate-950/60 dark:border-slate-800/80 dark:text-slate-100'
    case 'ghost':
      return 'bg-transparent border border-transparent text-slate-900 dark:text-slate-100'
    case 'elevated':
      return 'bg-white border border-slate-200 shadow-md text-slate-900 dark:bg-slate-900 dark:border-slate-700/80 dark:shadow-2xl dark:text-slate-100'
    default:
      return 'bg-white border border-slate-200 shadow-sm text-slate-900 dark:bg-slate-900/90 dark:border-slate-800 dark:shadow-lg dark:text-slate-100'
  }
})

const paddingClasses = computed(() => {
  switch (props.padding) {
    case 'none':
      return 'p-0'
    case 'sm':
      return 'p-3'
    case 'lg':
      return 'p-5'
    case 'md':
    default:
      return 'p-4'
  }
})

const roundedClasses = computed(() => {
  switch (props.rounded) {
    case 'sm':
      return 'rounded-md'
    case 'md':
      return 'rounded-lg'
    case 'xl':
      return 'rounded-2xl'
    case 'lg':
    default:
      return props.variant === 'elevated' ? 'rounded-2xl' : 'rounded-xl'
  }
})

const hoverClasses = computed(() => {
  if (!props.hoverEffect) return ''
  return 'transition duration-200 ease-in-out hover:border-slate-300 dark:hover:border-slate-700 hover:shadow-md dark:hover:shadow-cyan-500/5 hover:-translate-y-0.5'
})

const computedClasses = computed(() => [
  'base-card',
  `base-card--${props.variant}`,
  `base-card--padding-${props.padding}`,
  `base-card--rounded-${props.rounded}`,
  { 'base-card--hover': props.hoverEffect },
  'box-border transition-colors',
  variantClasses.value,
  paddingClasses.value,
  roundedClasses.value,
  hoverClasses.value,
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
}

/* Scoped dark-mode fallbacks */
:global(.dark) .base-card--bordered {
  background-color: rgba(15, 23, 42, 0.9);
  border-color: #1e293b;
  box-shadow: 0 10px 15px -3px rgb(0 0 0 / 0.3), 0 4px 6px -4px rgb(0 0 0 / 0.3);
  color: #f1f5f9;
}

:global(.dark) .base-card--flat {
  background-color: rgba(2, 6, 23, 0.6);
  border-color: rgba(30, 41, 59, 0.8);
  color: #f1f5f9;
}

:global(.dark) .base-card--ghost {
  background-color: transparent;
  border-color: transparent;
  color: #f1f5f9;
}

:global(.dark) .base-card--elevated {
  background-color: #0f172a;
  border-color: rgba(51, 65, 85, 0.8);
  box-shadow: 0 25px 50px -12px rgb(0 0 0 / 0.5);
  color: #f1f5f9;
}

:global(.dark) .base-card--hover:hover {
  border-color: #334155;
  box-shadow: 0 10px 15px -3px rgba(6, 182, 212, 0.05);
}
</style>
