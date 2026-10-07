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
  <header class="page-header">
    <div class="page-header__lead">
      <div class="page-header__title-row">
        <h1 class="page-header__title">{{ title }}</h1>
        <BaseBadge
          v-if="live"
          variant="success"
          :pulse="true"
          size="sm"
          class="page-header__live-badge"
        >
          LIVE
        </BaseBadge>
        <BaseBadge
          v-if="screenId"
          variant="neutral"
          size="sm"
          class="page-header__screen-id-badge"
          :data-screen-id="screenId"
        >
          {{ screenId }}
        </BaseBadge>
      </div>
      <p v-if="subtitle" class="page-header__subtitle">{{ subtitle }}</p>
    </div>

    <div v-if="$slots.stats || $slots.actions || $slots.default" class="page-header__controls">
      <div v-if="$slots.stats" class="page-header__stats">
        <slot name="stats" />
      </div>
      <div v-if="$slots.actions || $slots.default" class="page-header__actions">
        <slot name="actions">
          <slot />
        </slot>
      </div>
    </div>
  </header>
</template>

<style scoped>
.page-header {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid var(--cakra-border, #e2e8f0);
  box-sizing: border-box;
}

@media (min-width: 768px) {
  .page-header {
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
  }
}

.page-header__lead {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 0;
}

.page-header__title-row {
  display: flex;
  align-items: center;
  gap: 0.625rem;
  flex-wrap: wrap;
}

.page-header__title {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 700;
  line-height: 1.3;
  color: var(--cakra-text-main, #0f172a);
  letter-spacing: -0.015em;
}

@media (max-width: 576px) {
  .page-header__title {
    font-size: 1.25rem;
  }
}

.page-header__subtitle {
  margin: 0;
  font-size: 0.875rem;
  line-height: 1.4;
  color: var(--cakra-text-muted, #475569);
}

.page-header__controls {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.625rem;
}

.page-header__stats {
  display: flex;
  align-items: center;
}

.page-header__actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.page-header__screen-id-badge {
  font-family: var(--bs-font-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace);
}
</style>
