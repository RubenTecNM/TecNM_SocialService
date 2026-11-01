<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { UiAlert, UiBasePage, UiCard } from '../../../shared/components'
import { get911Report, getSemesterStatistics } from '../api/reportsApi'

const stats = ref<any>(null)
const rep911 = ref<any>(null)
const isLoading = ref(true)
const errorMessage = ref('')

async function loadData() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const [sData, rData] = await Promise.all([
      getSemesterStatistics(1),
      get911Report(1),
    ])
    stats.value = sData
    rep911.value = rData
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || 'Error al cargar estadísticas institucionales'
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
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Estadísticas Semestrales & Formato 911</h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">Cifras oficiales para auditoría SEP/TecNM y reportes estadísticos</p>
    </div>

    <UiAlert v-if="errorMessage" tone="danger" style="margin-bottom: 1rem;">{{ errorMessage }}</UiAlert>

    <div v-if="isLoading" style="padding: 2rem; text-align: center;">Cargando estadísticas...</div>

    <div v-else style="display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 1.5rem;">
      <UiCard title="Estadística Semestral por Carrera">
        <div v-if="stats && stats.careers" style="display: flex; flex-direction: column; gap: 0.5rem;">
          <div v-for="c in stats.careers" :key="c.careerCode" style="display: flex; justify-content: space-between; padding: 0.5rem 0; border-bottom: 1px solid #f3f4f6;">
            <span>{{ c.careerCode }} - {{ c.careerName }}</span>
            <span>SS: <strong>{{ c.socialServiceCount }}</strong> | PP: <strong>{{ c.internshipCount }}</strong></span>
          </div>
        </div>
        <div v-else style="color: #6c757d; font-size: 0.9rem;">Sin desglose disponible para el periodo</div>
      </UiCard>

      <UiCard title="Reporte Oficial 911 (SEP / TecNM)">
        <div v-if="rep911" style="display: flex; flex-direction: column; gap: 0.75rem;">
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">Servicio Social (Sección XII)</strong>
            <span>Total inscritos: <strong>{{ rep911.socialServiceTotal || 0 }}</strong> alumnos</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">Prácticas Profesionales (Sección XIII)</strong>
            <span>Total acreditados: <strong>{{ rep911.internshipTotal || 0 }}</strong> alumnos</span>
          </div>
        </div>
      </UiCard>
    </div>
  </UiBasePage>
</template>

