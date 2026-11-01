<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { UiAlert, UiBadge, UiBasePage, UiCard, UiTable } from '../../../shared/components'
import { getTrackingData } from '../api/reportsApi'
import type { TrackingItem } from '../types/reports'

interface TrackingRow extends TrackingItem {
  actions?: string
}

const items = ref<TrackingRow[]>([])
const isLoading = ref(true)
const errorMessage = ref('')
const tramiteType = ref<'social_service' | 'internship'>('social_service')

const columns: { key: keyof TrackingRow & string; label: string }[] = [
  { key: 'controlNumber', label: 'Control' },
  { key: 'studentFullName', label: 'Estudiante' },
  { key: 'careerCode', label: 'Carrera' },
  { key: 'companyName', label: 'Empresa / Dependencia' },
  { key: 'presentationLetterFolio', label: 'Folio Carta' },
  { key: 'progressPercentage', label: 'Avance %' },
  { key: 'status', label: 'Estatus' },
  { key: 'releaseCertificateFolio', label: 'Folio Constancia' },
]

async function loadData() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await getTrackingData(1, tramiteType.value)
    items.value = (res.items || []).map((i) => ({ ...i, id: i.tramiteId }))
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || 'Error al cargar la matriz de seguimiento'
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
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Matriz de Seguimiento Vinculación</h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">
        Vista integral espejo de SEGUIMIENTO VINCULACIÓN1.xlsx con avance documental 0-100%
      </p>
    </div>

    <UiAlert v-if="errorMessage" tone="danger" style="margin-bottom: 1rem;">{{ errorMessage }}</UiAlert>

    <UiCard>
      <div style="display: flex; gap: 1rem; margin-bottom: 1rem;">
        <button
          :style="{
            padding: '0.5rem 1rem',
            borderRadius: '6px',
            border: 'none',
            cursor: 'pointer',
            fontWeight: '600',
            background: tramiteType === 'social_service' ? '#0284c7' : '#e5e7eb',
            color: tramiteType === 'social_service' ? '#fff' : '#374151',
          }"
          @click="tramiteType = 'social_service'; loadData()"
        >
          Servicio Social
        </button>
        <button
          :style="{
            padding: '0.5rem 1rem',
            borderRadius: '6px',
            border: 'none',
            cursor: 'pointer',
            fontWeight: '600',
            background: tramiteType === 'internship' ? '#16a34a' : '#e5e7eb',
            color: tramiteType === 'internship' ? '#fff' : '#374151',
          }"
          @click="tramiteType = 'internship'; loadData()"
        >
          Prácticas Profesionales
        </button>
      </div>

      <UiTable label="Matriz de Seguimiento" :columns="columns" :rows="items">
        <template #cell-progressPercentage="{ row }">
          <div style="display: flex; align-items: center; gap: 0.5rem;">
            <div style="flex: 1; height: 8px; background: #e5e7eb; border-radius: 4px; overflow: hidden; min-width: 60px;">
              <div :style="{ width: `${row.progressPercentage}%`, height: '100%', background: '#0284c7' }"></div>
            </div>
            <span style="font-size: 0.85rem; font-weight: 600;">{{ row.progressPercentage }}%</span>
          </div>
        </template>
        <template #cell-status="{ row }">
          <UiBadge :tone="row.status === 'released' ? 'success' : 'warning'">
            {{ row.status }}
          </UiBadge>
        </template>
      </UiTable>
    </UiCard>
  </UiBasePage>
</template>

