<script setup lang="ts">
import BaseBadge from './BaseBadge.vue'

export interface PageHeaderProps {
  title: string
  subtitle?: string
  screenId?: string
  live?: boolean
}

withDefaults(defineProps<PageHeaderProps>(), {
  subtitle: undefined,
  screenId: undefined,
  live: false,
})
</script>

<template>
  <header class="page-header flex flex-col md:flex-row md:items-center md:justify-between gap-4 pb-4 border-b border-slate-200 dark:border-slate-800 box-border">
    <div class="page-header__lead flex flex-col gap-1 min-w-0">
      <div class="page-header__title-row flex items-center gap-2.5 flex-wrap">
        <span
          class="inline-block w-2.5 h-2.5 rounded-full bg-cyan-400 shadow-[0_0_8px_rgba(6,182,212,0.6)] flex-shrink-0"
          aria-hidden="true"
        />
        <h1 class="page-header__title m-0 text-2xl font-bold tracking-tight text-slate-900 dark:text-white leading-tight">
          {{ title }}
        </h1>
        <BaseBadge
          v-if="live"
          variant="emerald"
          :pulse="true"
          size="sm"
          class="page-header__live-badge"
        >
          LIVE
        </BaseBadge>
        <BaseBadge
          v-if="screenId"
          variant="slate"
          size="sm"
          class="page-header__screen-id-badge font-mono uppercase tracking-wider text-[11px]"
          :data-screen-id="screenId"
        >
          {{ screenId }}
        </BaseBadge>
      </div>
      <p v-if="subtitle" class="page-header__subtitle m-0 text-sm text-slate-500 dark:text-slate-400 leading-normal">
        {{ subtitle }}
      </p>
    </div>

    <div v-if="$slots.stats || $slots.actions || $slots.default" class="page-header__controls flex items-center flex-wrap gap-2.5">
      <div v-if="$slots.stats" class="page-header__stats flex items-center">
        <slot name="stats" />
      </div>
      <div v-if="$slots.actions || $slots.default" class="page-header__actions flex items-center gap-2">
        <slot name="actions">
          <slot />
        </slot>
      </div>
    </div>
  </header>
</template>

<style scoped>
.page-header {
  box-sizing: border-box;
}

:global(.dark) .page-header {
  border-bottom-color: #1e293b;
}

:global(.dark) .page-header__title {
  color: #ffffff;
}

:global(.dark) .page-header__subtitle {
  color: #94a3b8;
}

.page-header__screen-id-badge {
  font-family: var(--bs-font-monospace, 'JetBrains Mono', SFMono-Regular, Menlo, Monaco, Consolas, monospace);
}
</style>
