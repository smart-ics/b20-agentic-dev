<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'

import type { FeedItem } from '@/views/FeedView.vue'

/**
 * SCR-FEED-001: High-Density Operational Event Ledger Row
 * Strictly aligned with CAKRA Operational Principles:
 * - 1-line scannable record displaying Time | Severity | Ref | Owner (P.12) | Context | Title & Delta | Signals.
 * - Allows operators & leadership to scan 20+ operational events per screen during shift handovers.
 */

const props = withDefaults(
  defineProps<{
    item: FeedItem
    isSelected?: boolean
    customerNameMap?: Record<string, string>
    productNameMap?: Record<string, string>
    customerCodeMap?: Record<string, string>
    productCodeMap?: Record<string, string>
  }>(),
  {
    isSelected: false,
    customerNameMap: () => ({}),
    productNameMap: () => ({}),
    customerCodeMap: () => ({}),
    productCodeMap: () => ({}),
  },
)

const emit = defineEmits<{
  (e: 'select', item: FeedItem): void
  (e: 'open-modal', item: FeedItem): void
  (e: 'quick-ack', item: FeedItem): void
}>()

const resolvedPostId = computed<string>(() => (props.item.postId || props.item.id || props.item.feedItemId || '').trim())

const effectiveRequestId = computed<string | null>(() => {
  if (props.item.requestId && props.item.requestId.trim().length > 0) {
    return props.item.requestId.trim()
  }
  if (
    (props.item.referenceType ?? '').toUpperCase() === 'REQUEST' &&
    props.item.referenceId &&
    props.item.referenceId.trim().length > 0
  ) {
    return props.item.referenceId.trim()
  }
  return null
})

const effectiveRequestDisplay = computed<string>(() => {
  if (props.item.referenceDisplay && props.item.referenceDisplay.trim().length > 0) {
    return props.item.referenceDisplay.trim()
  }
  const reqId = effectiveRequestId.value
  return reqId ? `REQ #${reqId.slice(0, 8)}` : 'SYS'
})

function parseFormattedCode(str?: string | null): { code: string; name: string } | null {
  if (!str) return null
  const trimmed = str.trim()
  const match = trimmed.match(/^(.*?)\s*\(([^()]+)\)$/)
  if (match) {
    return { name: match[1].trim(), code: match[2].trim() }
  }
  return null
}

const customerDisplayCode = computed<string>(() => {
  const direct = props.item.customerCode?.trim()
  if (direct) return direct

  const custId = props.item.customerId?.trim().toLowerCase()
  if (custId) {
    const fromCode = props.customerCodeMap[custId] || props.customerCodeMap[props.item.customerId!]
    if (fromCode?.trim()) return fromCode.trim()

    const fromName = props.customerNameMap[custId] || props.customerNameMap[props.item.customerId!]
    if (fromName) {
      const parsed = parseFormattedCode(fromName)
      if (parsed?.code) return parsed.code
    }
  }

  const raw = props.item.customer ?? props.item.customerName
  if (raw) {
    const parsed = parseFormattedCode(raw)
    if (parsed?.code) return parsed.code

    const rawLower = raw.trim().toLowerCase()
    for (const [id, formatted] of Object.entries(props.customerNameMap)) {
      const p = parseFormattedCode(formatted)
      if (p && (p.name.toLowerCase() === rawLower || p.code.toLowerCase() === rawLower)) {
        return p.code
      }
      if (formatted.toLowerCase() === rawLower) {
        const cCode = props.customerCodeMap[id] || props.customerCodeMap[id.toLowerCase()]
        if (cCode) return cCode
      }
    }
  }

  if (props.item.customerId) {
    return `CUST-${props.item.customerId.slice(0, 6).toUpperCase()}`
  }

  if (raw && raw.trim().length > 0) {
    const words = raw.trim().split(/\s+/).filter(Boolean)
    if (words.length > 1) {
      return words.map((w) => w[0]).join('').toUpperCase()
    }
    return raw.trim().slice(0, 6).toUpperCase()
  }

  return ''
})

const customerTooltipName = computed<string>(() => {
  const custId = props.item.customerId?.trim().toLowerCase()
  let fullName = ''

  if (custId) {
    const fromName = props.customerNameMap[custId] || props.customerNameMap[props.item.customerId!]
    if (fromName) {
      const parsed = parseFormattedCode(fromName)
      fullName = parsed ? parsed.name : fromName
    }
  }

  if (!fullName) {
    const raw = props.item.customer ?? props.item.customerName
    if (raw) {
      const parsed = parseFormattedCode(raw)
      fullName = parsed ? parsed.name : raw.trim()
    }
  }

  const code = customerDisplayCode.value.trim()
  if (fullName && code && fullName.toLowerCase() !== code.toLowerCase()) {
    return `${fullName} (${code})`
  }
  return fullName || code || 'Customer'
})

const productDisplayCode = computed<string>(() => {
  const direct = props.item.productCode?.trim()
  if (direct) return direct

  const prodId = props.item.productId?.trim().toLowerCase()
  if (prodId) {
    const fromCode = props.productCodeMap[prodId] || props.productCodeMap[props.item.productId!]
    if (fromCode?.trim()) return fromCode.trim()

    const fromName = props.productNameMap[prodId] || props.productNameMap[props.item.productId!]
    if (fromName) {
      const parsed = parseFormattedCode(fromName)
      if (parsed?.code) return parsed.code
    }
  }

  const raw = props.item.product ?? props.item.productName
  if (raw) {
    const parsed = parseFormattedCode(raw)
    if (parsed?.code) return parsed.code

    const rawLower = raw.trim().toLowerCase()
    for (const [id, formatted] of Object.entries(props.productNameMap)) {
      const p = parseFormattedCode(formatted)
      if (p && (p.name.toLowerCase() === rawLower || p.code.toLowerCase() === rawLower)) {
        return p.code
      }
      if (formatted.toLowerCase() === rawLower) {
        const pCode = props.productCodeMap[id] || props.productCodeMap[id.toLowerCase()]
        if (pCode) return pCode
      }
    }
  }

  if (props.item.productId) {
    return `PROD-${props.item.productId.slice(0, 6).toUpperCase()}`
  }

  if (raw && raw.trim().length > 0) {
    const words = raw.trim().split(/\s+/).filter(Boolean)
    if (words.length > 1) {
      return words.map((w) => w[0]).join('').toUpperCase()
    }
    return raw.trim().slice(0, 6).toUpperCase()
  }

  return ''
})

const productTooltipName = computed<string>(() => {
  const prodId = props.item.productId?.trim().toLowerCase()
  let fullName = ''

  if (prodId) {
    const fromName = props.productNameMap[prodId] || props.productNameMap[props.item.productId!]
    if (fromName) {
      const parsed = parseFormattedCode(fromName)
      fullName = parsed ? parsed.name : fromName
    }
  }

  if (!fullName) {
    const raw = props.item.product ?? props.item.productName
    if (raw) {
      const parsed = parseFormattedCode(raw)
      fullName = parsed ? parsed.name : raw.trim()
    }
  }

  const code = productDisplayCode.value.trim()
  if (fullName && code && fullName.toLowerCase() !== code.toLowerCase()) {
    return `${fullName} (${code})`
  }
  return fullName || code || 'Product'
})

const ownerDisplay = computed<string>(() => {
  const explicitOwner = props.item.ownerName
  if (explicitOwner && explicitOwner.trim().length > 0) {
    return explicitOwner.trim()
  }
  const author = props.item.author ?? props.item.authorName
  if (author && author.trim().length > 0 && author.toUpperCase() !== 'SYSTEM') {
    return author.trim()
  }
  return 'Unassigned'
})

const isUnowned = computed<boolean>(() => {
  return ownerDisplay.value === 'Unassigned' || !props.item.ownerName
})

const ackCount = computed<number>(() => {
  if (props.item.reactionCounts && typeof props.item.reactionCounts === 'object') {
    return props.item.reactionCounts['SEEN'] ?? props.item.reactionCounts['seen'] ?? 0
  }
  return 0
})

const expCount = computed<number>(() => {
  if (props.item.reactionCounts && typeof props.item.reactionCounts === 'object') {
    return props.item.reactionCounts['EXPERIENCED'] ?? props.item.reactionCounts['experienced'] ?? 0
  }
  return 0
})

function formatTimeOnly(value?: string | null): string {
  if (!value) return '—'
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return value
  return parsed.toLocaleTimeString(undefined, {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  })
}
</script>

<template>
  <tr
    class="op-ledger-row cursor-pointer transition border-b border-slate-200 dark:border-slate-800/80"
    :class="{
      'bg-cyan-500/10 dark:bg-cyan-500/15': isSelected,
      'border-start border-4 border-rose-500': item.isException,
    }"
    :data-testid="`feed-ledger-row-${resolvedPostId}`"
    @click="emit('select', item)"
  >
    <!-- Timestamp -->
    <td class="font-mono fs-11 text-slate-500 dark:text-slate-400 text-nowrap py-2 px-3">
      {{ formatTimeOnly(item.createdAt) }}
    </td>

    <!-- Severity & Decision Badge -->
    <td class="text-nowrap py-2 px-3">
      <span
        v-if="item.isException"
        class="badge bg-rose-500/15 text-rose-400 border border-rose-500/30 font-mono fs-11"
      >
        {{ item.exceptionType || 'EXCEPTION' }}
      </span>
      <span
        v-else-if="item.source === 'SYSTEM_GENERATED'"
        class="badge bg-cyan-500/15 text-cyan-400 border border-cyan-500/30 font-mono fs-11"
      >
        STATE FACT
      </span>
      <span
        v-else
        class="badge bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 border border-slate-300 dark:border-slate-700 font-mono fs-11"
      >
        OPS POST
      </span>
    </td>

    <!-- Reference Link -->
    <td class="font-mono fs-11 text-nowrap py-2 px-3">
      <router-link
        v-if="effectiveRequestId"
        :to="`/requests/${effectiveRequestId}`"
        class="fw-bold text-decoration-none text-cyan-600 dark:text-cyan-400 hover:underline"
        @click.stop
      >
        {{ effectiveRequestDisplay }}
      </router-link>
      <span v-else class="text-slate-400 dark:text-slate-500">
        {{ effectiveRequestDisplay }}
      </span>
    </td>

    <!-- Accountable Owner (Principle 12) -->
    <td class="text-nowrap py-2 px-3">
      <span v-if="isUnowned" class="badge bg-amber-500/15 text-amber-400 border border-amber-500/30 font-mono fs-11">
        ⚠️ UNOWNED
      </span>
      <span v-else class="text-slate-800 dark:text-slate-200 fs-12 text-truncate d-inline-block max-w-120 font-medium" :title="ownerDisplay">
        {{ ownerDisplay }}
      </span>
    </td>

    <!-- Customer & Product Context -->
    <td class="py-2 px-3 text-nowrap">
      <span
        v-if="customerDisplayCode"
        class="badge bg-slate-100 dark:bg-slate-800/80 text-slate-700 dark:text-slate-300 border border-slate-300 dark:border-slate-700 text-truncate font-mono fs-11 d-inline-block me-1"
        style="max-width: 120px; cursor: help;"
        :title="customerTooltipName"
      >
        <i class="bi bi-building me-1 text-slate-400 dark:text-slate-500" aria-hidden="true"></i>
        {{ customerDisplayCode }}
      </span>
      <span
        v-if="productDisplayCode"
        class="badge bg-slate-100 dark:bg-slate-800/80 text-slate-700 dark:text-slate-300 border border-slate-300 dark:border-slate-700 text-truncate font-mono fs-11 d-inline-block"
        style="max-width: 120px; cursor: help;"
        :title="productTooltipName"
      >
        <i class="bi bi-box-seam me-1 text-slate-400 dark:text-slate-500" aria-hidden="true"></i>
        {{ productDisplayCode }}
      </span>
    </td>

    <!-- Event Summary & Title -->
    <td class="py-2 px-3">
      <div class="fw-semibold text-slate-900 dark:text-white fs-12 text-truncate max-w-350" :title="item.title">
        {{ item.title }}
      </div>
      <div v-if="item.contentExcerpt" class="text-slate-500 dark:text-slate-400 fs-11 text-truncate max-w-350" :title="item.contentExcerpt">
        {{ item.contentExcerpt }}
      </div>
    </td>

    <!-- Operational Signals -->
    <td class="font-mono fs-11 text-nowrap py-2 px-3">
      <span
        class="badge me-1"
        :class="ackCount > 0 ? 'bg-emerald-500/15 text-emerald-400 border border-emerald-500/30' : 'bg-slate-100 dark:bg-slate-800/80 text-slate-400 dark:text-slate-500 border border-slate-200 dark:border-slate-700/80'"
        title="Acknowledged"
      >
        ACK:{{ ackCount }}
      </span>
      <span
        v-if="expCount > 0"
        class="badge bg-cyan-500/15 text-cyan-400 border border-cyan-500/30"
        title="Experienced"
      >
        EXP:{{ expCount }}
      </span>
    </td>

    <!-- Quick Actions -->
    <td class="text-end text-nowrap py-2 px-3">
      <button
        type="button"
        class="btn btn-sm py-0.5 px-2 fs-11 rounded border border-slate-300 dark:border-slate-700 bg-slate-100 dark:bg-slate-800 text-slate-700 dark:text-slate-300 hover:border-cyan-500 dark:hover:border-cyan-400 hover:text-cyan-600 dark:hover:text-cyan-400 transition"
        @click.stop="emit('open-modal', item)"
      >
        Inspect
      </button>
    </td>
  </tr>
</template>

<style scoped>
.max-w-120 { max-width: 120px; }
.max-w-140 { max-width: 140px; }
.max-w-350 { max-width: 350px; }
.op-ledger-row:hover {
  background-color: #f1f5f9;
}
:global(.dark) .op-ledger-row:hover {
  background-color: rgba(30, 41, 59, 0.5);
}
</style>
