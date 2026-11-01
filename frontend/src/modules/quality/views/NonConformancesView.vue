<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { UiAlert, UiBadge, UiBasePage, UiCard, UiTable } from '../../../shared/components'
import { getNonConformances } from '../api/qualityApi'
import type { NonConformance } from '../types/quality'

interface NonConformanceRow extends NonConformance {
  actions?: string
}

const items = ref<NonConformanceRow[]>([])
const isLoading = ref(true)
const errorMessage = ref('')

const columns: { key: keyof NonConformanceRow & string; label: string }[] = [
  { key: 'incidentCode', label: 'Código PNC' },
  { key: 'periodCode', label: 'Periodo' },
  { key: 'tramiteType', label: 'Trámite' },
  { key: 'studentControlNumber', label: 'Alumno' },
  { key: 'category', label: 'Categoría' },
  { key: 'description', label: 'Descripción' },
  { key: 'status', label: 'Estatus' },
]

async function loadData() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    items.value = await getNonConformances()
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || 'Error al cargar incidentes de PNC'
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
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Producto No Conforme (PNC - ISO 9001)</h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">Registro de desvíos operativos, planes de acción y seguimiento de calidad</p>
    </div>

    <UiAlert v-if="errorMessage" tone="danger" style="margin-bottom: 1rem;">{{ errorMessage }}</UiAlert>

    <UiCard>
      <UiTable label="Incidencias PNC" :columns="columns" :rows="items">
        <template #cell-status="{ row }">
          <UiBadge :tone="row.status === 'closed' ? 'success' : row.status === 'in_progress' ? 'warning' : 'danger'">
            {{ row.status === 'closed' ? 'Cerrado' : row.status === 'in_progress' ? 'En Atención' : 'Abierto' }}
          </UiBadge>
        </template>
      </UiTable>
    </UiCard>
  </UiBasePage>
</template>

