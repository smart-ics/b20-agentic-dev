<script setup lang="ts">
import PostDetailModal, { type PostThreadDetails } from '@/views/PostDetailModal.vue'

/**
 * Re-export wrapper for SCR-POST-001 (`PostDetailModal.vue`) so consumers (such as `FeedView.vue`)
 * can import from either `@/components/PostDetailModal.vue` or `@/views/PostDetailModal.vue`.
 */
withDefaults(
  defineProps<{
    postId?: string | null
    show?: boolean
    initialRequestId?: string | null
    initialWorkPackageId?: string | null
  }>(),
  {
    postId: null,
    show: undefined,
    initialRequestId: null,
    initialWorkPackageId: null,
  },
)

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'updated', post?: PostThreadDetails | null): void
}>()
</script>

<template>
  <PostDetailModal
    :post-id="postId"
    :show="show"
    :initial-request-id="initialRequestId"
    :initial-work-package-id="initialWorkPackageId"
    @close="emit('close')"
    @updated="(post) => emit('updated', post)"
  />
</template>
