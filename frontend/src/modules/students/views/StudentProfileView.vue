<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { UiAlert, UiBasePage, UiButton, UiCard, UiField } from '../../../shared/components'
import { getMyStudentProfile, updateMyImss } from '../api/studentsApi'
import type { Student } from '../types/students'

const profile = ref<Student | null>(null)
const isLoading = ref(true)
const isSaving = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

const imssNumber = ref('')
const imssClinic = ref('')

async function loadProfile() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const data = await getMyStudentProfile()
    profile.value = data
    imssNumber.value = data.imssNumber || ''
    imssClinic.value = data.imssClinic || ''
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || 'No se pudo cargar la información del alumno'
  } finally {
    isLoading.value = false
  }
}

async function handleSaveImss() {
  isSaving.value = true
  errorMessage.value = ''
  successMessage.value = ''
  try {
    const updated = await updateMyImss({
      imssNumber: imssNumber.value,
      imssClinic: imssClinic.value,
    })
    profile.value = updated
    successMessage.value = 'Datos de seguro médico IMSS actualizados exitosamente'
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || 'Error al actualizar datos de IMSS'
  } finally {
    isSaving.value = false
  }
}

onMounted(() => {
  loadProfile()
})
</script>

<template>
  <UiBasePage :fluid="false">
    <div style="margin-bottom: 1.5rem;">
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Mi Perfil de Estudiante</h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">Expediente personal y datos de seguridad social requeridos para trámites</p>
    </div>

    <UiAlert v-if="errorMessage" tone="danger" style="margin-bottom: 1rem;">{{ errorMessage }}</UiAlert>
    <UiAlert v-if="successMessage" tone="success" style="margin-bottom: 1rem;">{{ successMessage }}</UiAlert>

    <div v-if="isLoading" style="padding: 2rem; text-align: center;">Cargando expediente...</div>

    <div v-else-if="profile" style="display: flex; flex-direction: column; gap: 1.5rem;">
      <UiCard title="Datos Académicos">
        <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem;">
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">No. Control</strong>
            <span style="font-size: 1.1rem; font-weight: 600;">{{ profile.controlNumber }}</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">Nombre Completo</strong>
            <span style="font-size: 1.1rem; font-weight: 600;">{{ profile.fullName }}</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">CURP</strong>
            <span>{{ profile.curp }}</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">Carrera</strong>
            <span>{{ profile.careerCode }} - {{ profile.careerName }}</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">Semestre Actual</strong>
            <span>Semestre {{ profile.currentSemester }}</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.85rem; color: #6c757d;">Avance de Créditos</strong>
            <span style="font-weight: 600; color: #0284c7;">{{ profile.percentageApprovedCredits }}%</span>
          </div>
        </div>
      </UiCard>

      <UiCard title="Afiliación Médica (IMSS)">
        <p style="font-size: 0.9rem; color: #4b5563; margin-bottom: 1rem;">
          El Número de Seguridad Social (NSS) y la clínica asignada son obligatorios para generar cartas de presentación de Servicio Social y Prácticas.
        </p>
        <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 1rem;">
          <UiField label="Número de Seguridad Social (NSS)" hint="11 dígitos">
            <input v-model="imssNumber" type="text" maxlength="11" placeholder="Ej. 12345678901" />
          </UiField>
          <UiField label="Clínica / UMF Asignada" hint="Ej. UMF 84 Monclova">
            <input v-model="imssClinic" type="text" placeholder="Ej. UMF 84" />
          </UiField>
        </div>
        <div style="margin-top: 1rem; display: flex; justify-content: flex-end;">
          <UiButton variant="primary" :disabled="isSaving" @click="handleSaveImss">
            {{ isSaving ? 'Guardando...' : 'Guardar Datos IMSS' }}
          </UiButton>
        </div>
      </UiCard>
    </div>
  </UiBasePage>
</template>

