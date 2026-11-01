<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { UiAlert, UiBadge, UiBasePage, UiCard, UiTable } from '../../../shared/components'
import { getSocialServices } from '../api/socialServiceApi'
import type { SocialServiceListItem } from '../types/socialService'

interface SocialServiceRow extends SocialServiceListItem {
  actions?: string
}

const items = ref<SocialServiceRow[]>([])
const isLoading = ref(true)
const errorMessage = ref('')

const columns: { key: keyof SocialServiceRow & string; label: string }[] = [
  { key: 'studentControlNumber', label: 'Control' },
  { key: 'studentFullName', label: 'Alumno' },
  { key: 'careerCode', label: 'Carrera' },
  { key: 'programName', label: 'Programa' },
  { key: 'modality', label: 'Modalidad' },
  { key: 'documentProgressPercentage', label: 'Progreso' },
  { key: 'status', label: 'Estatus' },
]

async function loadData() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await getSocialServices()
    items.value = res.items || []
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || 'Error al cargar trámites de Servicio Social'
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
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Gestión de Servicio Social</h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">Seguimiento documental, avance de 480 horas, cartas de presentación y liberación</p>
    </div>

    <UiAlert v-if="errorMessage" tone="danger" style="margin-bottom: 1rem;">{{ errorMessage }}</UiAlert>

    <UiCard>
      <UiTable label="Trámites de Servicio Social" :columns="columns" :rows="items">
        <template #cell-modality="{ row }">
          <UiBadge :tone="row.modality === 'internal' ? 'info' : 'purple'">
            {{ row.modality === 'internal' ? 'Interno (TecNM)' : 'Externo' }}
          </UiBadge>
        </template>
        <template #cell-documentProgressPercentage="{ row }">
          <div style="display: flex; align-items: center; gap: 0.5rem;">
            <div style="flex: 1; height: 8px; background: #e5e7eb; border-radius: 4px; overflow: hidden; min-width: 60px;">
              <div :style="{ width: `${row.documentProgressPercentage}%`, height: '100%', background: '#0284c7' }"></div>
            </div>
            <span style="font-size: 0.85rem; font-weight: 600;">{{ row.documentProgressPercentage }}%</span>
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

