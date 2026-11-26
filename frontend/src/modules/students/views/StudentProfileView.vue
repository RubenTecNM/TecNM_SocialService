<script setup lang="ts">
import { problemMessage } from '../../../shared/api/problemDetails'
import { watch as watchValidation } from 'vue'
import { useFormValidation } from '../../../shared/composables/useFormValidation'
import { useRealtimeRefresh } from '../../../shared/composables/useRealtime'
import { validateFields, optionalEmail } from '../../../shared/utils/validationRules'
import UiFormErrors from '../../../shared/components/UiFormErrors.vue'
import DocumentViewerModal from '../../expedients/components/DocumentViewerModal.vue'

import { computed, onMounted, reactive, ref } from 'vue'
import { UiAlert, UiBadge, UiBasePage, UiButton, UiCard, UiField } from '../../../shared/components'
import {
  getMyEligibility,
  getMyImssBlob,
  getMyStudentProfile,
  updateMyContact,
  uploadMyImssFile,
} from '../api/studentsApi'
import type { StudentDetail, StudentEligibility, StudentShift } from '../types/students'

const imssPreviewOpen = ref(false)
const profile = ref<StudentDetail | null>(null)
const eligibility = ref<StudentEligibility | null>(null)
const isLoading = ref(true)
const isSavingContact = ref(false)
const isUploadingImss = ref(false)
const errorMessage = ref('')
const successMessage = ref('')
const selectedImssFile = ref<File | null>(null)

const contactForm = reactive<{
  phoneNumber: string
  institutionalEmail: string
  personalEmail: string
  currentSemester: number
  shift: StudentShift
}>({
  phoneNumber: '',
  institutionalEmail: '',
  personalEmail: '',
  currentSemester: 6,
  shift: 'morning',
})

const fullName = computed(() => {
  if (!profile.value) return ''
  return [
    profile.value.firstName,
    profile.value.firstSurname,
    profile.value.secondSurname,
  ]
    .filter(Boolean)
    .join(' ')
})

async function loadProfile(): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const [data, elig] = await Promise.all([
      getMyStudentProfile(),
      getMyEligibility().catch(() => null),
    ])
    profile.value = data
    eligibility.value = elig
    contactForm.phoneNumber = data.phoneNumber || ''
    contactForm.institutionalEmail = data.institutionalEmail || ''
    contactForm.personalEmail = data.personalEmail || ''
    contactForm.currentSemester = data.currentSemester
    contactForm.shift = data.shift
  } catch (err: any) {
    errorMessage.value =
      problemMessage(err, 'No se pudo cargar la información del expediente del alumno.')
  } finally {
    isLoading.value = false
  }
}

async function handleSaveContact(): Promise<void> {
  if (isSavingContact.value || !contactValidation.validate()) return

  isSavingContact.value = true
  errorMessage.value = ''
  successMessage.value = ''
  try {
    const updated = await updateMyContact({
      phoneNumber: contactForm.phoneNumber.trim() || null,
      institutionalEmail: contactForm.institutionalEmail.trim() || null,
      personalEmail: contactForm.personalEmail.trim() || null,
      currentSemester: contactForm.currentSemester,
      shift: contactForm.shift,
    })
    profile.value = updated
    successMessage.value = 'Datos de contacto y turno actualizados exitosamente.'
  } catch (err: any) {
    contactValidation.apply(err, 'No se pudo guardar el formulario.')
    errorMessage.value = problemMessage(err, 'Error al actualizar los datos de contacto.')
  } finally {
    isSavingContact.value = false
  }
}

function handleFileChange(event: Event): void {
  const input = event.target as HTMLInputElement
  selectedImssFile.value = input.files?.[0] ?? null
}

async function handleUploadImss(): Promise<void> {
  if (!selectedImssFile.value || isUploadingImss.value) return
  isUploadingImss.value = true
  errorMessage.value = ''
  successMessage.value = ''
  try {
    const updated = await uploadMyImssFile(selectedImssFile.value)
    profile.value = updated
    selectedImssFile.value = null
    imssPreviewOpen.value = false
    eligibility.value = await getMyEligibility().catch(() => null)
    successMessage.value =
      'Constancia IMSS cargada. Vinculación debe revisarla y aprobarla.'
  } catch (err: any) {
    errorMessage.value =
      problemMessage(err, 'Error al subir la Constancia de Vigencia del IMSS.')
  } finally {
    isUploadingImss.value = false
  }
}

async function handleViewMyImss(): Promise<void> {
  try {
    const blob = await getMyImssBlob()
    const url = window.URL.createObjectURL(blob)
    window.open(url, '_blank')
  } catch {
    errorMessage.value = 'Aún no hay un archivo PDF de vigencia IMSS disponible para visualizar.'
  }
}

onMounted(() => {
  void loadProfile()
})

useRealtimeRefresh(
  (evt) =>
    evt.tramiteType === 'general' ||
    evt.documentCode === 'IMSS' ||
    evt.eventType === 'tramite.updated',
  async () => {
    if (isSavingContact.value || isUploadingImss.value) return
    const [data, elig] = await Promise.all([
      getMyStudentProfile().catch(() => null),
      getMyEligibility().catch(() => null),
    ])
    if (data) profile.value = data
    if (elig) eligibility.value = elig
  },
)
const contactValidation = useFormValidation(() => validateFields(contactForm, {phoneNumber:{max:20},institutionalEmail:optionalEmail,personalEmail:optionalEmail,currentSemester:{required:true,integer:true,min:1,maxNumber:15},shift:{required:true,choices:['morning','afternoon','night']}}))
</script>

<template>
  <UiBasePage :fluid="false">
    <div style="margin-bottom: 1.5rem;">
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Mi Perfil y Vigencia de Derechos IMSS</h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">
        Expediente escolar, datos de contacto para formatos oficiales y carga de Constancia de Vigencia IMSS
      </p>
    </div>

    <UiAlert v-if="errorMessage && !(contactValidation.hasErrors.value)" tone="danger" style="margin-bottom: 1rem;">
      {{ errorMessage }}
    </UiAlert>
    <UiAlert v-if="successMessage" tone="success" style="margin-bottom: 1rem;">
      {{ successMessage }}
    </UiAlert>

    <div v-if="isLoading" style="padding: 2rem; text-align: center;">Cargando expediente...</div>

    <div v-else-if="profile" style="display: flex; flex-direction: column; gap: 1.5rem;">
      <!-- Tarjeta 1: Datos Académicos y Elegibilidad -->
      <UiCard title="Datos Académicos y Elegibilidad">
        <div
          style="display: grid; grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); gap: 1rem;"
        >
          <div>
            <strong style="display: block; font-size: 0.82rem; color: #6c757d;">No. Control</strong>
            <span style="font-size: 1.05rem; font-weight: 700;">{{ profile.controlNumber }}</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.82rem; color: #6c757d;">Nombre Completo</strong>
            <span style="font-size: 1.05rem; font-weight: 600;">{{ fullName }}</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.82rem; color: #6c757d;">CURP</strong>
            <span>{{ profile.curp || 'No registrado' }}</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.82rem; color: #6c757d;">Carrera</strong>
            <span>{{ profile.careerName }}</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.82rem; color: #6c757d;">Semestre / Créditos</strong>
            <span>{{ profile.currentSemester }}° Semestre ({{ profile.completedCredits }} créditos)</span>
          </div>
          <div>
            <strong style="display: block; font-size: 0.82rem; color: #6c757d;">Avance de Créditos</strong>
            <span style="font-weight: 700; color: #0284c7;">
              {{ profile.curriculumProgressPercentage }}%
            </span>
          </div>
        </div>

        <div
          v-if="eligibility"
          style="margin-top: 1.25rem; padding-top: 1rem; border-top: 1px solid #e5e7eb; display: flex; flex-wrap: wrap; gap: 1rem;"
        >
          <div style="display: flex; align-items: center; gap: 0.5rem;">
            <span style="font-size: 0.88rem; font-weight: 600;">Servicio Social:</span>
            <UiBadge :tone="eligibility.socialService.eligible ? 'success' : 'warning'">
              {{ eligibility.socialService.eligible ? 'Elegible' : 'Pendiente requisito' }}
            </UiBadge>
          </div>
          <div style="display: flex; align-items: center; gap: 0.5rem;">
            <span style="font-size: 0.88rem; font-weight: 600;">Prácticas Profesionales:</span>
            <UiBadge :tone="eligibility.internship.eligible ? 'success' : 'warning'">
              {{ eligibility.internship.eligible ? 'Elegible' : 'Pendiente requisito' }}
            </UiBadge>
          </div>
        </div>
      </UiCard>

      <!-- Tarjeta 2: Constancia de Vigencia de Derechos del IMSS -->
      <UiCard title="Constancia de Vigencia de Derechos del IMSS (PDF)">
        <div style="display: flex; justify-content: space-between; align-items: center; flex-wrap: wrap; gap: 1rem; margin-bottom: 1rem;">
          <div>
            <p style="font-size: 0.9rem; color: #4b5563; margin: 0 0 0.5rem;">
              Adjunta tu Constancia de Vigencia de Derechos del IMSS en formato PDF. Este documento
              también se puede adjuntar directamente al solicitar tu apertura de Servicio Social o
              Carta de Presentación de Prácticas.
            </p>
            <div style="display: flex; align-items: center; gap: 0.5rem;">
              <UiBadge :tone="profile.hasValidImss ? 'success' : 'warning'">
                {{ profile.hasValidImss ? 'Vigencia IMSS aprobada' : profile.imssStatus === 'pending' ? 'Pendiente de revisión' : profile.imssStatus === 'rejected' ? 'Rechazado: consulta observaciones' : profile.imssStatus === 'approved' ? 'Constancia aprobada' : profile.hasImssFile ? 'Archivo histórico: requiere nueva carga' : 'Sin constancia IMSS' }}
              </UiBadge>
              <span v-if="profile.imssValidatedAt" style="font-size: 0.8rem; color: #6b7280;">
                Actualizado: {{ new Date(profile.imssValidatedAt).toLocaleDateString() }}
              </span>
            </div>
          </div>

          <UiButton v-if="profile.hasImssFile" variant="secondary" @click="handleViewMyImss">
            Ver / Descargar Mi Constancia IMSS
          </UiButton>
        </div>

        <div
          class="imss-dropzone"
          style="display: flex; align-items: center; gap: 1rem; flex-wrap: wrap; padding: 1rem; background: var(--surface-toolbar, #f8fafc); border: 1px dashed var(--color-border-medium, #cbd5e1); border-radius: 8px;"
        >
          <input type="file" accept="application/pdf" class="imss-file-input" @change="handleFileChange" />
          <UiButton
            variant="primary"
            class="imss-upload-btn"
            :disabled="!selectedImssFile || isUploadingImss"
            @click="imssPreviewOpen = true"
          >
            {{ isUploadingImss ? 'Subiendo PDF...' : 'Subir Constancia IMSS (PDF)' }}
          </UiButton>
        </div>
        <UiAlert v-if="profile.imssRejectionReason" tone="danger">{{ profile.imssRejectionReason }}</UiAlert>
      </UiCard>

      <!-- Tarjeta 3: Datos de Contacto y Turno (usados en prellenado de formatos) -->
      <UiCard title="Datos de Contacto y Turno (Para Prellenado Automático de Formatos)">
        <div
          style="display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem;"
        >
          <UiFormErrors :validation="contactValidation" />
          <UiField label="Teléfono de Contacto" hint="Se usa en Anexo XVIII, XX y Solicitud PP" v-bind="contactValidation.field('phoneNumber', 'Teléfono de Contacto')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" v-model="contactForm.phoneNumber" type="text" placeholder="Ej. 8661458920" />
          </UiField>

          <UiField v-model="contactForm.institutionalEmail" label="Correo Institucional (@monclova.tecnm.mx)" type="email" readonly hint="Administración gestiona el correo de acceso."  v-bind="contactValidation.field('institutionalEmail', 'Correo Institucional (@monclova.tecnm.mx)')" />

          <UiField label="Correo Personal" v-bind="contactValidation.field('personalEmail', 'Correo Personal')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" v-model="contactForm.personalEmail" type="email" placeholder="alumno@gmail.com" />
          </UiField>

          <UiField label="Semestre Actual" v-bind="contactValidation.field('currentSemester', 'Semestre Actual')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" v-model.number="contactForm.currentSemester" type="number" min="1" max="15" />
          </UiField>

          <UiField label="Turno" v-bind="contactValidation.field('shift', 'Turno')" v-slot="{ controlAttrs }">
            <select v-bind="controlAttrs" v-model="contactForm.shift">
              <option value="morning">Matutino</option>
              <option value="afternoon">Vespertino</option>
              <option value="night">Nocturno</option>
            </select>
          </UiField>
        </div>

        <div class="contact-save-row" style="margin-top: 1rem; display: flex; justify-content: flex-end;">
          <UiButton variant="primary" :disabled="isSavingContact" @click="handleSaveContact">
            {{ isSavingContact ? 'Guardando...' : 'Guardar Datos de Contacto' }}
          </UiButton>
        </div>
      </UiCard>
    </div>
  </UiBasePage>
  <DocumentViewerModal v-model="imssPreviewOpen" title="Verifica tu constancia IMSS" :blob="selectedImssFile" confirm-upload :busy="isUploadingImss" :error="errorMessage" @confirm="handleUploadImss" />
</template>

<style scoped>
@media (max-width: 640px) {
  .imss-dropzone {
    flex-direction: column;
    align-items: stretch !important;
    padding: 0.85rem !important;
    gap: 0.75rem !important;
  }

  .imss-file-input {
    max-width: 100%;
    font-size: 0.85rem;
  }

  .imss-upload-btn,
  .contact-save-row :deep(.btn) {
    width: 100%;
  }
}
</style>
