<script setup lang="ts">
import { RouterView } from 'vue-router'
import { LogoutButton } from '../../modules/auth'
import { UiHeader, UiShell, UiSidebar, UiToggle } from '../../shared/components'
import { useTheme } from '../../shared/composables/useTheme'
import { headerNavigation, mainLayoutConfig, sidebarNavigation } from './mainLayout.config'

const { dark } = useTheme()
</script>

<template>
  <UiShell :show-header="mainLayoutConfig.showHeader" :show-sidebar="mainLayoutConfig.showSidebar">
    <template #header>
      <UiHeader v-bind="mainLayoutConfig.header" :items="headerNavigation">
        <UiToggle v-model="dark" type="switch" label="Modo oscuro" class="theme" />
        <LogoutButton v-if="!mainLayoutConfig.showSidebar" />
      </UiHeader>
    </template>
    <template #sidebar>
      <UiSidebar
        v-bind="mainLayoutConfig.sidebar"
        :items="sidebarNavigation"
        note="Lineamientos de Servicio Social y Prácticas Profesionales vigentes."
      >
        <div v-if="!mainLayoutConfig.showHeader" class="theme-control-box">
          <UiToggle v-model="dark" type="switch" label="Modo oscuro" class="theme" />
        </div>
        <LogoutButton />
      </UiSidebar>
    </template>
    <RouterView />
    <template #footer>
      <span><strong>Tecnológico Nacional de México</strong> · Campus Monclova · Clave 05DIT0001V</span>
      <span>Sistema Institucional SSPP · SGC ISO 9001:2015</span>
      <LogoutButton v-if="!mainLayoutConfig.showHeader && !mainLayoutConfig.showSidebar" />
    </template>
  </UiShell>
</template>

<style scoped>
.theme-control-box {
  padding: 2px 10px;
  border-radius: 8px;
  background: var(--color-surface-white);
  border: 1px solid var(--color-border);
}

.theme-control-box :deep(.theme) {
  width: 100%;
  justify-content: space-between;
  min-height: 36px;
  font-size: 12.5px;
  font-weight: 600;
  color: var(--color-text-primary);
}
</style>
