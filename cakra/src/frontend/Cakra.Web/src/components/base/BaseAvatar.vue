<script setup lang="ts">
import { computed } from 'vue'

export interface BaseAvatarProps {
  name: string
  size?: 'sm' | 'md' | 'lg'
  status?: 'online' | 'busy' | 'paused' | 'offline'
}

const props = withDefaults(defineProps<BaseAvatarProps>(), {
  size: 'md',
  status: undefined,
})

const initials = computed(() => {
  if (!props.name) return ''
  const parts = props.name.trim().split(/\s+/).filter(Boolean)
  if (parts.length === 0) return ''
  if (parts.length === 1) {
    return parts[0].charAt(0).toUpperCase()
  }
  return (parts[0].charAt(0) + parts[1].charAt(0)).toUpperCase()
})

const computedClasses = computed(() => [
  'base-avatar',
  `base-avatar--size-${props.size}`,
  { 'base-avatar--has-status': !!props.status },
])
</script>

<template>
  <div
    :class="computedClasses"
    :title="name"
    :aria-label="name"
    role="img"
  >
    <div class="base-avatar__circle">
      <slot>{{ initials }}</slot>
    </div>
    <span
      v-if="status"
      class="base-avatar__status-dot"
      :class="`base-avatar__status-dot--${status}`"
      :aria-label="`Status: ${status}`"
    />
  </div>
</template>

<style scoped>
.base-avatar {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  vertical-align: middle;
  flex-shrink: 0;
  box-sizing: border-box;
}

.base-avatar__circle {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  border-radius: var(--cakra-radius-full, 9999px);
  background-color: var(--cakra-slate-100, #f1f5f9);
  color: var(--cakra-text-main, #0f172a);
  border: 1px solid var(--cakra-border, #e2e8f0);
  font-weight: 700;
  line-height: 1;
  user-select: none;
  box-sizing: border-box;
  overflow: hidden;
}

/* Sizes */
.base-avatar--size-sm {
  width: 28px;
  height: 28px;
}

.base-avatar--size-sm .base-avatar__circle {
  font-size: 0.6875rem; /* 11px */
}

.base-avatar--size-md {
  width: 36px;
  height: 36px;
}

.base-avatar--size-md .base-avatar__circle {
  font-size: 0.75rem; /* 12px */
}

.base-avatar--size-lg {
  width: 44px;
  height: 44px;
}

.base-avatar--size-lg .base-avatar__circle {
  font-size: 0.875rem; /* 14px */
}

/* Presence Beacon Dot */
.base-avatar__status-dot {
  position: absolute;
  border-radius: var(--cakra-radius-full, 9999px);
  box-sizing: content-box;
  flex-shrink: 0;
}

.base-avatar--size-sm .base-avatar__status-dot {
  width: 8px;
  height: 8px;
  bottom: -1px;
  right: -1px;
  border: 1.5px solid var(--cakra-bg-surface, #ffffff);
}

.base-avatar--size-md .base-avatar__status-dot {
  width: 10px;
  height: 10px;
  bottom: -1px;
  right: -1px;
  border: 2px solid var(--cakra-bg-surface, #ffffff);
}

.base-avatar--size-lg .base-avatar__status-dot {
  width: 12px;
  height: 12px;
  bottom: 0px;
  right: 0px;
  border: 2px solid var(--cakra-bg-surface, #ffffff);
}

/* Status Colors */
.base-avatar__status-dot--online {
  background-color: var(--cakra-emerald, #10b981);
}

.base-avatar__status-dot--busy {
  background-color: var(--cakra-amber, #f59e0b);
}

.base-avatar__status-dot--paused {
  background-color: var(--cakra-amber, #f59e0b);
}

.base-avatar__status-dot--offline {
  background-color: var(--cakra-slate-400, #94a3b8);
}
</style>
