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
        class="slide-over-drawer fixed inset-0 z-[1050] overflow-hidden"
        role="dialog"
        aria-modal="true"
        :aria-labelledby="title ? 'slide-over-drawer-title' : undefined"
      >
        <div
          class="slide-over-drawer__backdrop fixed inset-0 bg-slate-950/70 backdrop-blur-sm z-[1] transition-opacity duration-200"
          aria-hidden="true"
          @click="close"
        />

        <div
          class="slide-over-drawer__panel fixed top-0 right-0 bottom-0 h-full bg-white dark:bg-slate-900/95 dark:backdrop-blur border-l border-slate-200 dark:border-slate-800 shadow-2xl z-[2] flex flex-col overflow-hidden box-border transition-transform duration-250 ease-out text-slate-900 dark:text-slate-100"
          :class="`slide-over-drawer__panel--${width}`"
        >
          <header class="slide-over-drawer__header p-5 md:p-6 border-b border-slate-200 dark:border-slate-800 flex items-start justify-between gap-4 flex-shrink-0 bg-white dark:bg-slate-900/95">
            <slot name="header" :close="close">
              <div class="slide-over-drawer__header-lead flex-1 min-w-0">
                <h2 id="slide-over-drawer-title" class="slide-over-drawer__title text-lg font-bold tracking-tight text-slate-900 dark:text-white m-0 break-words">
                  {{ title }}
                </h2>
                <p v-if="subtitle" class="slide-over-drawer__subtitle text-xs text-slate-500 dark:text-slate-400 mt-1 mb-0 leading-relaxed">
                  {{ subtitle }}
                </p>
              </div>
              <button
                type="button"
                class="slide-over-drawer__close-btn inline-flex items-center justify-center w-8 h-8 p-0 bg-transparent border-0 rounded-lg text-slate-400 hover:text-slate-700 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800 transition cursor-pointer flex-shrink-0"
                aria-label="Close drawer"
                @click="close"
              >
                <svg
                  class="slide-over-drawer__close-icon w-5 h-5"
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

          <div class="slide-over-drawer__body p-6 overflow-y-auto flex-1 text-slate-800 dark:text-slate-200">
            <slot :close="close" />
          </div>

          <footer v-if="$slots.footer" class="slide-over-drawer__footer p-4 px-6 border-t border-slate-200 dark:border-slate-800 flex items-center justify-end gap-3 flex-shrink-0 bg-white dark:bg-slate-900/95">
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

/* Body Scrollbar */
.slide-over-drawer__body::-webkit-scrollbar {
  width: 6px;
}

.slide-over-drawer__body::-webkit-scrollbar-thumb {
  background: rgba(148, 163, 184, 0.3);
  border-radius: 9999px;
}

.slide-over-drawer__body::-webkit-scrollbar-thumb:hover {
  background: rgba(148, 163, 184, 0.5);
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
