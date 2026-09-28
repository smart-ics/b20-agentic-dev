<script setup lang="ts">
// Root application component (placeholder scaffold for P1-S08).
// Business screens (SCR-*) are added to this shell by later slices.
</script>

<template>
  <div class="d-flex flex-column min-vh-100">
    <header class="cakra-header border-bottom bg-body-tertiary">
      <nav class="navbar navbar-expand-lg container-fluid px-4">
        <span class="navbar-brand mb-0 h1 d-flex align-items-center gap-2">
          <i class="bi bi-diagram-3-fill text-primary" aria-hidden="true"></i>
          <span>CAKRA - ICS Operational System</span>
        </span>
      </nav>
    </header>

    <main class="flex-grow-1 py-4">
      <div class="container-fluid px-4">
        <router-view />
      </div>
    </main>

    <footer class="cakra-footer border-top py-3 text-body-secondary">
      <div class="container-fluid px-4 small">
        CAKRA - ICS Operational System
      </div>
    </footer>
  </div>
</template>
