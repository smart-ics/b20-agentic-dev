<script setup lang="ts">
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
</script>

<template>
  <div class="container py-4">
    <!-- Screen Header & Operational Awareness Banner -->
    <div class="d-flex justify-content-between align-items-center mb-4 pb-2 border-bottom">
      <div>
        <div class="d-flex align-items-center gap-2 mb-1">
          <span class="badge bg-primary text-uppercase font-monospace px-2 py-1">SCR-FEED-001</span>
          <h2 class="h4 mb-0 fw-bold">Operational Feed</h2>
        </div>
        <p class="text-muted small mb-0">
          Real-time collaborative stream of operational activities, state transitions, and request updates.
        </p>
      </div>
      <div>
        <span class="badge bg-success-subtle text-success border border-success-subtle px-3 py-2">
          <i class="bi bi-broadcast me-1"></i> Live Activity
        </span>
      </div>
    </div>

    <!-- Active User Welcome Banner -->
    <div class="card bg-white border-0 shadow-sm mb-4">
      <div class="card-body p-4">
        <div class="d-flex align-items-center justify-content-between flex-wrap gap-3">
          <div class="d-flex align-items-center gap-3">
            <div class="bg-primary-subtle text-primary rounded-circle d-flex align-items-center justify-content-center" style="width: 52px; height: 52px;">
              <i class="bi bi-person-check-fill fs-3"></i>
            </div>
            <div>
              <h5 class="card-title fw-bold mb-1">
                Welcome, {{ authStore.user?.username || 'Team Member' }}
              </h5>
              <div class="d-flex align-items-center gap-2 text-muted small">
                <span><i class="bi bi-fingerprint me-1"></i>Person ID: <code>{{ authStore.user?.personId || 'Active' }}</code></span>
                <span>&bull;</span>
                <span>
                  <i class="bi bi-shield-shaded me-1"></i>
                  <span v-if="authStore.userRoles.length > 0" class="badge bg-secondary-subtle text-secondary me-1" v-for="r in authStore.userRoles" :key="r">
                    {{ r }}
                  </span>
                  <span v-else class="badge bg-light text-dark border">Standard User</span>
                </span>
              </div>
            </div>
          </div>
          <div class="d-flex gap-2">
            <button class="btn btn-outline-secondary btn-sm" disabled>
              <i class="bi bi-filter me-1"></i> Filter Feed
            </button>
            <button class="btn btn-primary btn-sm" disabled>
              <i class="bi bi-pencil-square me-1"></i> Post Update
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Feed Placeholder Items / Upcoming Stream -->
    <div class="row">
      <div class="col-lg-8">
        <div class="card shadow-sm border-0 mb-3">
          <div class="card-header bg-white border-bottom py-3">
            <h6 class="mb-0 fw-semibold text-secondary">
              <i class="bi bi-activity me-1 text-primary"></i> Recent Operational Activity
            </h6>
          </div>
          <div class="card-body p-4 text-center py-5">
            <i class="bi bi-chat-square-text text-muted fs-1 mb-3 d-block"></i>
            <h5 class="fw-semibold">Operational Feed Stream Initialized</h5>
            <p class="text-muted small max-w-md mx-auto mb-3">
              Authentication session verified successfully. Live domain events and post feeds will be populated in subsequent module milestones (SCR-POST-001 & SCR-REQ-001..005).
            </p>
            <div class="badge bg-info-subtle text-info border border-info-subtle px-3 py-2">
              <i class="bi bi-check2-circle me-1"></i> Session Active &bull; Cookie Auth Verified (Architecture §19.5)
            </div>
          </div>
        </div>
      </div>

      <!-- Sidebar Metadata -->
      <div class="col-lg-4">
        <div class="card shadow-sm border-0 mb-3">
          <div class="card-header bg-white border-bottom py-3">
            <h6 class="mb-0 fw-semibold text-secondary">
              <i class="bi bi-info-circle me-1 text-primary"></i> Screen Architecture
            </h6>
          </div>
          <div class="card-body p-3">
            <ul class="list-unstyled mb-0 small text-muted">
              <li class="mb-2">
                <strong class="text-dark">Screen ID:</strong> SCR-FEED-001
              </li>
              <li class="mb-2">
                <strong class="text-dark">Default Landing:</strong> Post-login destination
              </li>
              <li class="mb-2">
                <strong class="text-dark">Authentication:</strong> Cookie-based server session
              </li>
              <li>
                <strong class="text-dark">Target Latency:</strong> &lt; 50ms read projection (§20)
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
