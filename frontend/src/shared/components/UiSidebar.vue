<script setup lang="ts">
import UiBrand from './UiBrand.vue'
import UiNavigation from './UiNavigation.vue'
import type { BrandOptions, NavigationItem } from './navigation'

withDefaults(
  defineProps<BrandOptions & { items: NavigationItem[]; note?: string }>(),
  { showTitle: true, showLogo: true },
)
</script>

<template>
  <aside class="ui-sidebar">
    <div class="sidebar-top">
      <UiBrand
        :title="title"
        :subtitle="subtitle"
        :logo="logo"
        :show-title="showTitle"
        :show-logo="showLogo"
      />

      <div class="sidebar-section-bar">
        <span class="sidebar-section-label">MÓDULOS OFICIALES</span>
        <span class="sidebar-year-pill">2026</span>
      </div>

      <UiNavigation :items="items" label="Navegación lateral" />
    </div>

    <div class="sidebar-bottom">
      <div v-if="note" class="normateca-card">
        <div class="normateca-card__head">
          <span class="normateca-status">
            <span class="normateca-dot" aria-hidden="true" />
            Normateca TecNM
          </span>
          <span class="normateca-rev">REV 2026</span>
        </div>
        <p class="side-note ui-text">{{ note }}</p>
      </div>

      <div class="sidebar-controls">
        <slot />
      </div>
    </div>
  </aside>
</template>

<style scoped>
.ui-sidebar {
  position: sticky;
  top: 0;
  align-self: start;
  height: 100dvh;
  overflow-y: auto;
  overscroll-behavior-y: contain;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  gap: 16px;
  padding: 18px 14px;
  background: var(--color-bg-light);
  border-right: 1px solid var(--color-border);
}

.sidebar-top {
  display: flex;
  flex-direction: column;
}

.ui-sidebar :deep(.brand) {
  padding: 8px 10px 14px;
  margin-bottom: 10px;
  border-bottom: 1px solid var(--color-border);
  font-size: 15px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.ui-sidebar :deep(.brand small) {
  color: var(--color-accent);
  font-size: 10.5px;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  margin-top: 2px;
}

.sidebar-section-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 4px 10px 8px;
}

.sidebar-section-label {
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.08em;
  color: var(--color-text-muted);
}

.sidebar-year-pill {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10px;
  font-weight: 700;
  color: var(--color-accent);
  background: var(--color-accent-light);
  border: 1px solid color-mix(in srgb, var(--color-accent) 40%, transparent);
  padding: 1px 6px;
  border-radius: 4px;
}

.sidebar-bottom {
  display: grid;
  gap: 10px;
  padding-top: 10px;
  border-top: 1px solid var(--color-border);
}

.normateca-card {
  padding: 10px 12px;
  border-radius: 10px;
  background: var(--color-surface-white);
  border: 1px solid var(--color-border);
  box-shadow: var(--card-shadow);
}

.normateca-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 6px;
  margin-bottom: 4px;
}

.normateca-status {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 11px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.normateca-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--color-success);
}

.normateca-rev {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 9.5px;
  font-weight: 700;
  color: var(--color-accent);
}

.side-note {
  margin: 0 !important;
  padding: 0 !important;
  border: 0 !important;
  font-size: 11px !important;
  line-height: 1.35;
  color: var(--color-text-secondary);
}

.sidebar-controls {
  display: grid;
  gap: 8px;
}

@media (max-width: 640px) {
  .ui-sidebar {
    position: static;
    height: auto;
    overflow-y: visible;
  }
}
</style>
