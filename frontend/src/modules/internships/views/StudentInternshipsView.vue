<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { UiAlert, UiBadge, UiBasePage, UiCard } from '../../../shared/components'
import { getMyInternship } from '../api/internshipsApi'
import type { InternshipDetail } from '../types/internships'

const internship = ref<InternshipDetail | null>(null)
const isLoading = ref(true)
const errorMessage = ref('')

async function loadData() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    internship.value = await getMyInternship()
  } catch (err: any) {
    if (err.response?.status === 404) {
      errorMessage.value = 'Aún no has registrado una solicitud de Prácticas Profesionales.'
    } else {
      errorMessage.value = err.response?.data?.message || 'Error al cargar tus Prácticas Profesionales'
    }
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  loadData()
})
</script>

<template>
  <UiBasePage :fluid="false">
    <div style="margin-bottom: 1.5rem;">
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Mis Prácticas Profesionales</h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">Seguimiento a carta de presentación, vinculación con empresa y liberación</p>
    </div>

    <UiAlert v-if="errorMessage" :tone="errorMessage.includes('Aún no') ? 'info' : 'danger'" style="margin-bottom: 1rem;">
      {{ errorMessage }}
    </UiAlert>

    <div v-if="isLoading" style="padding: 2rem; text-align: center;">Cargando trámite...</div>

    <div v-else-if="internship" style="display: flex; flex-direction: column; gap: 1.5rem;">
      <UiCard title="Información del Trámite">
        <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem; align-items: center;">
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">Folio Carta Presentación</strong>
            <span style="font-weight: 600;">{{ internship.presentationLetterFolio || 'Generándose...' }}</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">Empresa Asignada</strong>
            <span>{{ internship.company?.name || 'Pendiente de Carta de Aceptación' }}</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">Horas Cumplidas</strong>
            <span>{{ internship.completedHours }} / {{ internship.requiredHours }} hrs</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">Progreso Documental</strong>
            <span style="font-weight: 700; color: #16a34a;">{{ internship.documentProgressPercentage }}%</span>
          </div>
        </div>
      </UiCard>

      <UiCard title="Expediente de Documentos Requeridos">
        <div style="display: flex; flex-direction: column; gap: 0.75rem;">
          <div
            v-for="item in internship.checklist"
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

