<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { UiAlert, UiBadge, UiBasePage, UiButton, UiCard } from '../../../shared/components'
import { getAcceptanceLetterPrintData, getMySocialService } from '../api/socialServiceApi'
import type { SocialServiceDetail } from '../types/socialService'

const service = ref<SocialServiceDetail | null>(null)
const isLoading = ref(true)
const errorMessage = ref('')

async function loadData() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    service.value = await getMySocialService()
  } catch (err: any) {
    if (err.response?.status === 404) {
      errorMessage.value = 'Aún no has iniciado un trámite de Servicio Social.'
    } else {
      errorMessage.value = err.response?.data?.message || 'Error al cargar tu Servicio Social'
    }
  } finally {
    isLoading.value = false
  }
}

async function handleDownloadAcceptanceTemplate() {
  if (!service.value) return
  try {
    const data = await getAcceptanceLetterPrintData(service.value.id)
    alert(`Datos cargados para carta de aceptación interna: Alumno ${data.studentName}, Responsable: ${data.responsibleName}`)
  } catch {
    alert('No se pudo obtener el formato de carta de aceptación')
  }
}

onMounted(() => {
  loadData()
})
</script>

<template>
  <UiBasePage :fluid="false">
    <div style="margin-bottom: 1.5rem;">
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Mi Servicio Social</h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">Control de horas, avance porcentual y carga de evidencias bimestrales</p>
    </div>

    <UiAlert v-if="errorMessage" :tone="errorMessage.includes('Aún no') ? 'info' : 'danger'" style="margin-bottom: 1rem;">
      {{ errorMessage }}
    </UiAlert>

    <div v-if="isLoading" style="padding: 2rem; text-align: center;">Cargando trámite...</div>

    <div v-else-if="service" style="display: flex; flex-direction: column; gap: 1.5rem;">
      <UiCard title="Estado del Trámite">
        <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; align-items: center;">
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">Folio Carta Presentación</strong>
            <span style="font-weight: 600;">{{ service.presentationLetterFolio || 'Pendiente' }}</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">Modalidad</strong>
            <UiBadge :tone="service.modality === 'internal' ? 'info' : 'purple'">
              {{ service.modality === 'internal' ? 'Interno TecNM' : 'Dependencia Externa' }}
            </UiBadge>
          </div>
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">Horas Acumuladas</strong>
            <span>{{ service.accumulatedHours }} / {{ service.requiredHours }} hrs</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">Progreso Documental</strong>
            <span style="font-weight: 700; color: #0284c7;">{{ service.documentProgressPercentage }}%</span>
          </div>
        </div>

        <div v-if="service.modality === 'internal'" style="margin-top: 1.5rem; padding-top: 1rem; border-top: 1px solid #e5e7eb;">
          <UiButton variant="secondary" @click="handleDownloadAcceptanceTemplate">
            Descargar Carta de Aceptación Interna
          </UiButton>
        </div>
      </UiCard>

      <UiCard title="Checklist Documental Requerido">
        <div style="display: flex; flex-direction: column; gap: 0.75rem;">
          <div
            v-for="item in service.checklist"
            :key="item.documentCode"
            style="display: flex; justify-content: space-between; align-items: center; padding: 0.75rem; border: 1px solid #e5e7eb; border-radius: 6px;"
          >
            <div>
              <strong style="display: block;">{{ item.documentCode }} - {{ item.name }}</strong>
              <small v-if="item.rejectionReason" style="color: #dc2626;">Motivo rechazo: {{ item.rejectionReason }}</small>
            </div>
            <UiBadge :tone="item.status === 'approved' ? 'success' : item.status === 'rejected' ? 'danger' : 'warning'">
              {{ item.status === 'approved' ? 'Aprobado' : item.status === 'pending' ? 'En Revisión' : item.status === 'rejected' ? 'Rechazado' : 'Pendiente' }}
            </UiBadge>
          </div>
        </div>
      </UiCard>
    </div>
  </UiBasePage>
</template>

