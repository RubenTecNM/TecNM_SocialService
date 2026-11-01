<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { UiAlert, UiBasePage, UiCard } from '../../../shared/components'
import { getDashboardMetrics } from '../api/reportsApi'
import type { DashboardMetrics } from '../types/reports'

const data = ref<DashboardMetrics | null>(null)
const isLoading = ref(true)
const errorMessage = ref('')

async function loadData() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    data.value = await getDashboardMetrics()
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || 'Error al cargar métricas de Vinculación'
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  loadData()
})
</script>

<template>
  <UiBasePage :fluid="true">
    <div style="margin-bottom: 1.5rem;">
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Panel de Control de Vinculación</h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">Indicadores operativos de Servicio Social, Prácticas Profesionales y Calidad</p>
    </div>

    <UiAlert v-if="errorMessage" tone="danger" style="margin-bottom: 1rem;">{{ errorMessage }}</UiAlert>

    <div v-if="isLoading" style="padding: 2rem; text-align: center;">Cargando indicadores...</div>

    <div v-else-if="data" style="display: flex; flex-direction: column; gap: 1.5rem;">
      <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem;">
        <UiCard>
          <div style="font-size: 0.85rem; color: #6c757d; font-weight: 600;">REVISIONES PENDIENTES</div>
          <div style="font-size: 2rem; font-weight: 700; color: #dc2626; margin: 0.5rem 0;">{{ data.pendingDocumentReviews }}</div>
          <small>Documentos en espera de dictamen</small>
        </UiCard>

        <UiCard>
          <div style="font-size: 0.85rem; color: #6c757d; font-weight: 600;">CONVENIOS POR VENCER</div>
          <div style="font-size: 2rem; font-weight: 700; color: #f59e0b; margin: 0.5rem 0;">{{ data.agreementsExpiringSoon }}</div>
          <small>Próximos a vencer en &lt; 30 días</small>
        </UiCard>

        <UiCard>
          <div style="font-size: 0.85rem; color: #6c757d; font-weight: 600;">INCIDENCIAS PNC (ISO 9001)</div>
          <div style="font-size: 2rem; font-weight: 700; color: #7c3aed; margin: 0.5rem 0;">{{ data.openNonConformances }}</div>
          <small>Productos No Conformes abiertos</small>
        </UiCard>
      </div>

      <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 1.5rem;">
        <UiCard title="Servicio Social por Estatus">
          <div style="display: flex; flex-direction: column; gap: 0.5rem;">
            <div v-for="(count, status) in data.socialService" :key="status" style="display: flex; justify-content: space-between; padding: 0.5rem 0; border-bottom: 1px solid #f3f4f6;">
              <span style="text-transform: capitalize;">{{ status }}</span>
              <strong>{{ count }}</strong>
            </div>
          </div>
        </UiCard>

        <UiCard title="Prácticas Profesionales por Estatus">
          <div style="display: flex; flex-direction: column; gap: 0.5rem;">
            <div v-for="(count, status) in data.internships" :key="status" style="display: flex; justify-content: space-between; padding: 0.5rem 0; border-bottom: 1px solid #f3f4f6;">
              <span style="text-transform: capitalize;">{{ status }}</span>
              <strong>{{ count }}</strong>
            </div>
          </div>
        </UiCard>
      </div>
    </div>
  </UiBasePage>
</template>

