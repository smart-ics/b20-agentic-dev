<script setup lang="ts">
import { computed } from 'vue'

export type BadgeVariant =
  | 'neutral'
  | 'success'
  | 'warning'
  | 'danger'
  | 'brand'
  | 'info'
  | 'cyan'
  | 'indigo'
  | 'emerald'
  | 'purple'
  | 'amber'
  | 'rose'
  | 'slate'

export interface BaseBadgeProps {
  variant?: BadgeVariant
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

// Normalize semantic variant to executive color family
const resolvedColor = computed((): 'cyan' | 'indigo' | 'emerald' | 'purple' | 'amber' | 'rose' | 'slate' => {
  switch (props.variant) {
    case 'cyan':
    case 'info':
      return 'cyan'
    case 'indigo':
    case 'brand':
      return 'indigo'
    case 'emerald':
    case 'success':
      return 'emerald'
    case 'purple':
      return 'purple'
    case 'amber':
    case 'warning':
      return 'amber'
    case 'rose':
    case 'danger':
      return 'rose'
    case 'slate':
    case 'neutral':
    default:
      return 'slate'
  }
})

const sizeClasses = computed(() => {
  return props.size === 'sm'
    ? 'text-[10px] px-2 py-0.5 gap-1.5'
    : 'text-xs px-2.5 py-1 gap-1.5'
})

const colorClasses = computed(() => {
  const color = resolvedColor.value
  const style = props.styleType

  if (style === 'solid') {
    switch (color) {
      case 'cyan':
        return 'bg-cyan-500 text-slate-950 font-bold border border-transparent'
      case 'indigo':
        return 'bg-indigo-600 text-white font-bold border border-transparent'
      case 'emerald':
        return 'bg-emerald-600 text-white font-bold border border-transparent'
      case 'purple':
        return 'bg-purple-600 text-white font-bold border border-transparent'
      case 'amber':
        return 'bg-amber-500 text-slate-950 font-bold border border-transparent'
      case 'rose':
        return 'bg-rose-600 text-white font-bold border border-transparent'
      case 'slate':
        return 'bg-slate-700 text-white font-bold border border-transparent'
    }
  }

  if (style === 'outline') {
    switch (color) {
      case 'cyan':
        return 'bg-transparent text-cyan-600 dark:text-cyan-400 border border-cyan-500/40'
      case 'indigo':
        return 'bg-transparent text-indigo-600 dark:text-indigo-400 border border-indigo-500/40'
      case 'emerald':
        return 'bg-transparent text-emerald-600 dark:text-emerald-400 border border-emerald-500/40'
      case 'purple':
        return 'bg-transparent text-purple-600 dark:text-purple-400 border border-purple-500/40'
      case 'amber':
        return 'bg-transparent text-amber-600 dark:text-amber-400 border border-amber-500/40'
      case 'rose':
        return 'bg-transparent text-rose-600 dark:text-rose-400 border border-rose-500/40'
      case 'slate':
        return 'bg-transparent text-slate-600 dark:text-slate-400 border border-slate-500/40'
    }
  }

  // Default subtle style (10% fill, 20% border)
  switch (color) {
    case 'cyan':
      return 'bg-cyan-500/10 text-cyan-600 dark:text-cyan-400 border border-cyan-500/20'
    case 'indigo':
      return 'bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 border border-indigo-500/20'
    case 'emerald':
      return 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border border-emerald-500/20'
    case 'purple':
      return 'bg-purple-500/10 text-purple-600 dark:text-purple-400 border border-purple-500/20'
    case 'amber':
      return 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border border-amber-500/20'
    case 'rose':
      return 'bg-rose-500/10 text-rose-600 dark:text-rose-400 border border-rose-500/20'
    case 'slate':
    default:
      return 'bg-slate-500/10 text-slate-600 dark:text-slate-400 border border-slate-500/20'
  }
})

const dotColorClass = computed(() => {
  if (props.styleType === 'solid') return 'bg-white'
  switch (resolvedColor.value) {
    case 'cyan':
      return 'bg-cyan-400'
    case 'indigo':
      return 'bg-indigo-400'
    case 'emerald':
      return 'bg-emerald-400'
    case 'purple':
      return 'bg-purple-400'
    case 'amber':
      return 'bg-amber-400'
    case 'rose':
      return 'bg-rose-400'
    case 'slate':
    default:
      return 'bg-slate-400'
  }
})

const computedClasses = computed(() => [
  'base-badge',
  `base-badge--variant-${props.variant}`,
  `base-badge--style-${props.styleType}`,
  `base-badge--size-${props.size}`,
  { 'base-badge--has-pulse': props.pulse },
  'inline-flex items-center rounded-full font-semibold uppercase tracking-wider whitespace-nowrap align-middle transition-colors select-none',
  sizeClasses.value,
  colorClasses.value,
])
</script>

<template>
  <span :class="computedClasses">
    <span
      v-if="pulse"
      class="badge-pulse-dot inline-block rounded-full flex-shrink-0"
      :class="[
        size === 'sm' ? 'w-1.5 h-1.5' : 'w-2 h-2',
        dotColorClass,
      ]"
      aria-hidden="true"
    />
    <slot />
  </span>
</template>

<style scoped>
.base-badge {
  box-sizing: border-box;
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

.badge-pulse-dot {
  animation: live-pulse 2s cubic-bezier(0.4, 0, 0.6, 1) infinite;
}
</style>
