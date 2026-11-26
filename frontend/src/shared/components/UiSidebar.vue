<script setup lang="ts">
import UiBrand from './UiBrand.vue'
import UiNavigation from './UiNavigation.vue'
import type { BrandOptions, NavigationItem } from './navigation'

withDefaults(
  defineProps<
    BrandOptions & {
      items: NavigationItem[]
      note?: string
      mobileOpen?: boolean
      live?: boolean
    }
  >(),
  { showTitle: true, showLogo: true, mobileOpen: false, live: false },
)

const emit = defineEmits<{ close: [] }>()
</script>

<template>
  <aside id="app-mobile-sidebar" class="ui-sidebar" :class="{ 'is-mobile-open': mobileOpen }">
    <div class="sidebar-top">
      <div class="sidebar-brand-row">
        <UiBrand
          :title="title"
          :subtitle="subtitle"
          :logo="logo"
          :show-title="showTitle"
          :show-logo="showLogo"
        />
        <button
          type="button"
          class="sidebar-mobile-close"
          aria-label="Cerrar menú de navegación"
          @click="emit('close')"
        >
          <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2.2">
            <line x1="18" y1="6" x2="6" y2="18" />
            <line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </button>
      </div>

      <div class="sidebar-section-bar">
        <span class="sidebar-section-label">MÓDULOS OFICIALES</span>
        <div class="sidebar-section-pills">
          <span
            v-if="live"
            class="sidebar-live-pill"
            title="Sincronización en tiempo real activa (SignalR)"
          >
            <span class="sidebar-live-dot" aria-hidden="true" />
            EN VIVO
          </span>
          <span class="sidebar-year-pill">2026</span>
        </div>
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
  height: 100%;
  max-height: 100dvh;
  overflow: visible;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  gap: 6px;
  padding: 10px 10px;
  background: var(--color-bg-light);
  border-right: 1px solid var(--color-border);
  min-width: 0;
}

.sidebar-top {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.ui-sidebar :deep(.brand) {
  padding: 2px 6px 8px;
  margin-bottom: 4px;
  border-bottom: 1px solid var(--color-border);
  font-size: 14px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.ui-sidebar :deep(.brand-logo) {
  width: 34px;
  height: 46px;
}

.ui-sidebar :deep(.brand small) {
  color: var(--color-accent);
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  margin-top: 2px;
}

.sidebar-section-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 2px 6px 4px;
}

.sidebar-section-label {
  font-size: 9.5px;
  font-weight: 700;
  letter-spacing: 0.08em;
  color: var(--color-text-muted);
}

.sidebar-section-pills {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.sidebar-live-pill {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.03em;
  color: var(--color-success-text, var(--color-success));
  background: var(--color-success-badge, color-mix(in srgb, var(--color-success) 14%, transparent));
  border: 1px solid var(--color-success-border, color-mix(in srgb, var(--color-success) 38%, transparent));
  padding: 1px 5px;
  border-radius: 4px;
}

.sidebar-live-dot {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: var(--color-success);
}

.sidebar-year-pill {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 9.5px;
  font-weight: 700;
  color: var(--color-accent);
  background: var(--color-accent-light);
  border: 1px solid color-mix(in srgb, var(--color-accent) 40%, transparent);
  padding: 1px 5px;
  border-radius: 4px;
}

.sidebar-bottom {
  display: grid;
  gap: 6px;
  padding-top: 6px;
  border-top: 1px solid var(--color-border);
}

.normateca-card {
  padding: 8px 10px;
  border-radius: 8px;
  background: var(--color-surface-white);
  border: 1px solid var(--color-border);
  box-shadow: var(--card-shadow);
}

@media (max-height: 920px) {
  .normateca-card {
    display: none;
  }
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
  gap: 6px;
}

.sidebar-mobile-close {
  display: none;
}

@media (max-width: 900px) {
  .ui-sidebar {
    position: fixed;
    inset: 0 auto 0 0;
    width: min(290px, 85vw);
    height: 100dvh;
    max-height: 100dvh;
    overflow-y: auto;
    overscroll-behavior: contain;
    z-index: 70;
    padding: 12px 12px 16px;
    box-shadow: 0 20px 45px rgba(2, 6, 23, 0.45);
    transform: translateX(-105%);
    visibility: hidden;
    pointer-events: none;
    transition:
      transform 220ms cubic-bezier(0.22, 1, 0.36, 1),
      visibility 220ms ease;
  }

  .ui-sidebar.is-mobile-open {
    transform: translateX(0);
    visibility: visible;
    pointer-events: auto;
  }

  .sidebar-brand-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 8px;
    padding-bottom: 8px;
    margin-bottom: 6px;
    border-bottom: 1px solid var(--color-border);
  }

  .ui-sidebar :deep(.brand) {
    padding: 2px 4px;
    margin-bottom: 0;
    border-bottom: 0;
    flex: 1;
  }

  .sidebar-mobile-close {
    display: inline-grid;
    place-items: center;
    width: 36px;
    height: 36px;
    border-radius: 8px;
    border: 1px solid var(--color-border);
    background: var(--color-surface-white);
    color: var(--color-text-secondary);
    flex-shrink: 0;
    cursor: pointer;
  }

  .ui-sidebar :deep(.sidenav a) {
    min-height: 38px !important;
    padding: 6px 10px;
    font-size: 13px;
  }

  .ui-sidebar :deep(.navigation-group summary) {
    min-height: 32px;
    padding: 5px 8px;
    font-size: 10.5px;
  }
}
</style>
