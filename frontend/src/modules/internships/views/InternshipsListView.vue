<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { UiAlert, UiBadge, UiBasePage, UiCard, UiTable } from '../../../shared/components'
import { getInternships } from '../api/internshipsApi'
import type { InternshipListItem } from '../types/internships'

interface InternshipRow extends InternshipListItem {
  actions?: string
}

const items = ref<InternshipRow[]>([])
const isLoading = ref(true)
const errorMessage = ref('')

const columns: { key: keyof InternshipRow & string; label: string }[] = [
  { key: 'studentControlNumber', label: 'Control' },
  { key: 'studentFullName', label: 'Alumno' },
  { key: 'careerCode', label: 'Carrera' },
  { key: 'companyName', label: 'Empresa' },
  { key: 'presentationLetterFolio', label: 'Folio Carta' },
  { key: 'documentProgressPercentage', label: 'Progreso' },
  { key: 'status', label: 'Estatus' },
]

async function loadData() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await getInternships()
    items.value = res.items || []
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || 'Error al cargar trámites de Prácticas Profesionales'
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
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Gestión de Prácticas Profesionales</h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">Control de solicitudes en Forms, cartas de presentación y acreditación laboral</p>
    </div>

    <UiAlert v-if="errorMessage" tone="danger" style="margin-bottom: 1rem;">{{ errorMessage }}</UiAlert>

    <UiCard>
      <UiTable label="Trámites de Prácticas Profesionales" :columns="columns" :rows="items">
        <template #cell-companyName="{ row }">
          <span>{{ row.companyName || 'Por asignar (Post-Aceptación)' }}</span>
        </template>
        <template #cell-presentationLetterFolio="{ row }">
          <span style="font-weight: 600;">{{ row.presentationLetterFolio || 'Pendiente' }}</span>
        </template>
        <template #cell-documentProgressPercentage="{ row }">
          <div style="display: flex; align-items: center; gap: 0.5rem;">
            <div style="flex: 1; height: 8px; background: #e5e7eb; border-radius: 4px; overflow: hidden; min-width: 60px;">
              <div :style="{ width: `${row.documentProgressPercentage}%`, height: '100%', background: '#16a34a' }"></div>
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

