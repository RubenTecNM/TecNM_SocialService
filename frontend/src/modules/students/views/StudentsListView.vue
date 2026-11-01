<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { UiAlert, UiBadge, UiBasePage, UiButton, UiCard, UiField, UiTable } from '../../../shared/components'
import { downloadImportTemplate, getStudents } from '../api/studentsApi'
import type { Student } from '../types/students'

interface StudentRow extends Student {
  actions?: string
}

const students = ref<StudentRow[]>([])
const isLoading = ref(true)
const errorMessage = ref('')
const searchQuery = ref('')
const currentPage = ref(1)

const columns: { key: keyof StudentRow & string; label: string }[] = [
  { key: 'controlNumber', label: 'No. Control' },
  { key: 'fullName', label: 'Nombre Completo' },
  { key: 'careerCode', label: 'Carrera' },
  { key: 'currentSemester', label: 'Sem.' },
  { key: 'percentageApprovedCredits', label: '% Créditos' },
  { key: 'imssNumber', label: 'NSS IMSS' },
  { key: 'isVigente', label: 'Estatus' },
]

async function loadData() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await getStudents({ page: currentPage.value, pageSize: 15, search: searchQuery.value || undefined })
    students.value = res.items
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || 'Error al cargar padrón de alumnos'
  } finally {
    isLoading.value = false
  }
}

async function handleDownloadTemplate() {
  try {
    const blob = await downloadImportTemplate()
    const url = window.URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'plantilla_padron_alumnos.xlsx'
    a.click()
    window.URL.revokeObjectURL(url)
  } catch {
    errorMessage.value = 'No se pudo descargar la plantilla de importación'
  }
}

onMounted(() => {
  loadData()
})
</script>

<template>
  <UiBasePage :fluid="true">
    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; flex-wrap: wrap; gap: 1rem;">
      <div>
        <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Padrón de Estudiantes</h1>
        <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">Gestión de alumnos inscritos, avance crediticio y afiliación médica IMSS</p>
      </div>
      <div style="display: flex; gap: 0.5rem;">
        <UiButton variant="secondary" @click="handleDownloadTemplate">Descargar Plantilla XLSX</UiButton>
      </div>
    </div>

    <UiAlert v-if="errorMessage" tone="danger" style="margin-bottom: 1rem;">{{ errorMessage }}</UiAlert>

    <UiCard>
      <div style="display: flex; gap: 1rem; margin-bottom: 1rem; align-items: flex-end;">
        <div style="flex: 1; max-width: 400px;">
          <UiField label="Buscar alumno" hint="No. Control o Nombre">
            <input v-model="searchQuery" type="text" placeholder="Ej. I23050363" @keyup.enter="loadData" />
          </UiField>
        </div>
        <UiButton variant="primary" :disabled="isLoading" @click="loadData">Buscar</UiButton>
      </div>

      <UiTable label="Listado de Estudiantes" :columns="columns" :rows="students">
        <template #cell-isVigente="{ row }">
          <UiBadge :tone="row.isVigente ? 'success' : 'danger'">
            {{ row.isVigente ? 'Vigente' : 'Baja' }}
          </UiBadge>
        </template>
        <template #cell-percentageApprovedCredits="{ row }">
          <span>{{ row.percentageApprovedCredits }}%</span>
        </template>
      </UiTable>
    </UiCard>
  </UiBasePage>
</template>

