<script setup lang="ts">
import { onMounted } from 'vue'
import { useSystemStore } from '@/stores/system'

const systemStore = useSystemStore()

onMounted(async () => {
  systemStore.initialize()
  await systemStore.fetchModules()
})
</script>

<template>
  <div class="container py-4">
    <!-- Hero / Confirmation Card -->
    <div class="p-5 mb-4 bg-light rounded-3 shadow-sm border">
      <div class="container-fluid py-2">
        <div class="d-flex align-items-center mb-3">
          <i class="bi bi-cpu-fill text-primary fs-1 me-3"></i>
          <div>
            <h1 class="display-6 fw-bold mb-0">ICS Operational System</h1>
            <p class="text-muted mb-0">Modular Monolith Web Frontend Scaffold</p>
          </div>
        </div>
        <p class="col-md-9 fs-5">
          Frontend Single Page Application successfully scaffolded and connected to ASP.NET Core host.
        </p>
        <div class="d-flex gap-2 flex-wrap">
          <span class="badge bg-success d-flex align-items-center gap-1 py-2 px-3">
            <i class="bi bi-check-circle-fill"></i> Vue 3 Composition API
          </span>
          <span class="badge bg-primary d-flex align-items-center gap-1 py-2 px-3">
            <i class="bi bi-shield-check"></i> TypeScript
          </span>
          <span class="badge bg-purple d-flex align-items-center gap-1 py-2 px-3" style="background-color: #6f42c1; color: white;">
            <i class="bi bi-bootstrap-fill"></i> Bootstrap 5.3
          </span>
          <span class="badge bg-warning text-dark d-flex align-items-center gap-1 py-2 px-3">
            <i class="bi bi-signpost-split-fill"></i> Vue Router 4
          </span>
          <span class="badge bg-info text-dark d-flex align-items-center gap-1 py-2 px-3">
            <i class="bi bi-database-fill-gear"></i> Pinia State Store
          </span>
          <span class="badge bg-secondary d-flex align-items-center gap-1 py-2 px-3">
            <i class="bi bi-arrow-left-right"></i> Axios (/api/v1)
          </span>
        </div>
      </div>
    </div>

    <!-- System Modules & Architecture Info -->
    <div class="row g-4">
      <div class="col-md-6">
        <div class="card h-100 shadow-sm border">
          <div class="card-header bg-white py-3">
            <h5 class="card-title mb-0 d-flex align-items-center">
              <i class="bi bi-boxes text-primary me-2"></i>
              Registered Backend Modules
            </h5>
          </div>
          <div class="card-body">
            <div v-if="systemStore.loading" class="text-center py-3">
              <div class="spinner-border spinner-border-sm text-primary me-2" role="status"></div>
              <span>Querying backend modules...</span>
            </div>
            <div v-else-if="systemStore.errorMessage" class="alert alert-warning mb-0">
              <i class="bi bi-exclamation-triangle-fill me-2"></i>
              {{ systemStore.errorMessage }}
            </div>
            <ul v-else class="list-group list-group-flush">
              <li
                v-for="moduleName in systemStore.modules"
                :key="moduleName"
                class="list-group-item d-flex justify-content-between align-items-center"
              >
                <span><i class="bi bi-cube me-2 text-muted"></i>{{ moduleName }}</span>
                <span class="badge bg-primary-subtle text-primary border border-primary-subtle">Active</span>
              </li>
              <li v-if="systemStore.modules.length === 0" class="list-group-item text-muted">
                No active modules reported yet.
              </li>
            </ul>
          </div>
        </div>
      </div>

      <div class="col-md-6">
        <div class="card h-100 shadow-sm border">
          <div class="card-header bg-white py-3">
            <h5 class="card-title mb-0 d-flex align-items-center">
              <i class="bi bi-info-circle text-info me-2"></i>
              Frontend Architectural Specs
            </h5>
          </div>
          <div class="card-body">
            <table class="table table-sm table-borderless mb-0">
              <tbody>
                <tr>
                  <th scope="row" class="text-muted" style="width: 40%;">Architecture:</th>
                  <td><strong>§19.4</strong> Single Page Application</td>
                </tr>
                <tr>
                  <th scope="row" class="text-muted">Auth Strategy:</th>
                  <td><strong>§19.5</strong> Secure Cookie Session (withCredentials)</td>
                </tr>
                <tr>
                  <th scope="row" class="text-muted">Hosting Mode:</th>
                  <td><strong>§19.10</strong> ASP.NET Core In-Process (IIS / Kestrel)</td>
                </tr>
                <tr>
                  <th scope="row" class="text-muted">API Base URL:</th>
                  <td><code>/api/v1</code></td>
                </tr>
                <tr>
                  <th scope="row" class="text-muted">Asset Destination:</th>
                  <td><code>src/ICS.Web/wwwroot</code></td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
