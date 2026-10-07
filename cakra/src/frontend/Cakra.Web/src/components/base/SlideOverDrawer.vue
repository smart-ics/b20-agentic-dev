<script setup lang="ts">
import { watch, onBeforeUnmount } from 'vue'

export interface SlideOverDrawerProps {
  modelValue: boolean
  title: string
  subtitle?: string
  width?: 'sm' | 'md' | 'lg'
  teleport?: boolean
}

const props = withDefaults(defineProps<SlideOverDrawerProps>(), {
  subtitle: undefined,
  width: 'md',
  teleport: true,
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'close'): void
}>()

function close() {
  emit('update:modelValue', false)
  emit('close')
}

let previousOverflow: string | null = null

function lockScroll() {
  if (typeof document !== 'undefined') {
    if (previousOverflow === null) {
      previousOverflow = document.body.style.overflow
    }
    document.body.style.overflow = 'hidden'
  }
}

function unlockScroll() {
  if (typeof document !== 'undefined') {
    if (previousOverflow !== null) {
      document.body.style.overflow = previousOverflow
      previousOverflow = null
    }
  }
}

function handleKeydown(event: KeyboardEvent) {
  if (event.key === 'Escape' && props.modelValue) {
    close()
  }
}

watch(
  () => props.modelValue,
  (isOpen) => {
    if (typeof window === 'undefined') return
    if (isOpen) {
      window.addEventListener('keydown', handleKeydown)
      lockScroll()
    } else {
      window.removeEventListener('keydown', handleKeydown)
      unlockScroll()
    }
  },
  { immediate: true }
)

onBeforeUnmount(() => {
  if (typeof window !== 'undefined') {
    window.removeEventListener('keydown', handleKeydown)
  }
  unlockScroll()
})
</script>

<template>
  <Teleport to="body" :disabled="!teleport">
    <Transition name="slide-over" :duration="250">
      <div
        v-if="modelValue"
        class="slide-over-drawer"
        role="dialog"
        aria-modal="true"
        :aria-labelledby="title ? 'slide-over-drawer-title' : undefined"
      >
        <div
          class="slide-over-drawer__backdrop"
          aria-hidden="true"
          @click="close"
        />

        <div
          class="slide-over-drawer__panel"
          :class="`slide-over-drawer__panel--${width}`"
        >
          <header class="slide-over-drawer__header">
            <slot name="header" :close="close">
              <div class="slide-over-drawer__header-lead">
                <h2 id="slide-over-drawer-title" class="slide-over-drawer__title">
                  {{ title }}
                </h2>
                <p v-if="subtitle" class="slide-over-drawer__subtitle">
                  {{ subtitle }}
                </p>
              </div>
              <button
                type="button"
                class="slide-over-drawer__close-btn"
                aria-label="Close drawer"
                @click="close"
              >
                <svg
                  class="slide-over-drawer__close-icon"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  stroke-width="2"
                  aria-hidden="true"
                >
                  <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </slot>
          </header>

          <div class="slide-over-drawer__body">
            <slot :close="close" />
          </div>

          <footer v-if="$slots.footer" class="slide-over-drawer__footer">
            <slot name="footer" :close="close" />
          </footer>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.slide-over-drawer {
  position: fixed;
  inset: 0;
  z-index: 1050;
  overflow: hidden;
}

.slide-over-drawer__backdrop {
  position: fixed;
  inset: 0;
  background-color: rgba(15, 23, 42, 0.4);
  backdrop-filter: blur(4px);
  -webkit-backdrop-filter: blur(4px);
  z-index: 1;
  transition: opacity 0.25s ease;
}

.slide-over-drawer__panel {
  position: fixed;
  top: 0;
  right: 0;
  bottom: 0;
  height: 100%;
  background-color: var(--cakra-bg-surface, #ffffff);
  border-left: 1px solid var(--cakra-border, #e2e8f0);
  box-shadow: var(--cakra-shadow-xl, 0 20px 25px -5px rgb(0 0 0 / 0.1), 0 8px 10px -6px rgb(0 0 0 / 0.05));
  z-index: 2;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
  transition: transform 0.25s cubic-bezier(0.16, 1, 0.3, 1);
}

/* Panel Widths */
.slide-over-drawer__panel--sm {
  width: 100%;
  max-width: 380px;
}

.slide-over-drawer__panel--md {
  width: 100%;
  max-width: 480px;
}

.slide-over-drawer__panel--lg {
  width: 100%;
  max-width: 640px;
}

/* Header */
.slide-over-drawer__header {
  padding: 1.25rem 1.5rem;
  border-bottom: 1px solid var(--cakra-border, #e2e8f0);
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  flex-shrink: 0;
  background-color: var(--cakra-bg-surface, #ffffff);
}

.slide-over-drawer__header-lead {
  flex: 1 1 auto;
  min-width: 0;
}

.slide-over-drawer__title {
  font-size: 1.125rem; /* 18px */
  font-weight: 700;
  line-height: 1.35;
  color: var(--cakra-text-main, #0f172a);
  margin: 0;
  word-break: break-word;
}

.slide-over-drawer__subtitle {
  font-size: 0.8125rem; /* 13px */
  color: var(--cakra-text-muted, #475569);
  margin: 0.25rem 0 0 0;
  line-height: 1.4;
}

.slide-over-drawer__close-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 2rem;
  height: 2rem;
  padding: 0;
  background: transparent;
  border: none;
  border-radius: var(--cakra-radius-md, 8px);
  color: var(--cakra-text-muted, #475569);
  cursor: pointer;
  transition: background-color 0.15s ease, color 0.15s ease;
  flex-shrink: 0;
}

.slide-over-drawer__close-btn:hover {
  background-color: var(--cakra-bg-subtle, #f1f5f9);
  color: var(--cakra-text-main, #0f172a);
}

.slide-over-drawer__close-icon {
  width: 1.25rem;
  height: 1.25rem;
}

/* Body */
.slide-over-drawer__body {
  padding: 1.5rem;
  overflow-y: auto;
  flex: 1 1 auto;
  color: var(--cakra-text-main, #0f172a);
}

.slide-over-drawer__body::-webkit-scrollbar {
  width: 6px;
}

.slide-over-drawer__body::-webkit-scrollbar-thumb {
  background: rgba(148, 163, 184, 0.4);
  border-radius: var(--cakra-radius-full, 9999px);
}

.slide-over-drawer__body::-webkit-scrollbar-thumb:hover {
  background: rgba(148, 163, 184, 0.6);
}

/* Footer */
.slide-over-drawer__footer {
  padding: 1rem 1.5rem;
  border-top: 1px solid var(--cakra-border, #e2e8f0);
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.75rem;
  flex-shrink: 0;
  background-color: var(--cakra-bg-surface, #ffffff);
}

/* Slide Over Transitions */
.slide-over-enter-active,
.slide-over-leave-active {
  transition: opacity 0.25s ease;
}

.slide-over-enter-from .slide-over-drawer__backdrop,
.slide-over-leave-to .slide-over-drawer__backdrop {
  opacity: 0;
}

.slide-over-enter-from .slide-over-drawer__panel,
.slide-over-leave-to .slide-over-drawer__panel {
  transform: translateX(100%);
}
</style>
