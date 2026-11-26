<script setup lang="ts">
import { problemMessage } from '../../../shared/api/problemDetails'
import { watch as watchValidation } from 'vue'
import { useFormValidation } from '../../../shared/composables/useFormValidation'
import { useRealtimeRefresh } from '../../../shared/composables/useRealtime'
import { validateFields, optionalEmail, dateOrder } from '../../../shared/utils/validationRules'
import UiFormErrors from '../../../shared/components/UiFormErrors.vue'
import DocumentViewerModal from '../../expedients/components/DocumentViewerModal.vue'

import ExpedientChecklist from '../../expedients/components/ExpedientChecklist.vue'
import { http } from '../../../shared/api/http'
import { onMounted, reactive, ref } from 'vue'
import {
  UiAlert,
  UiBadge,
  UiBasePage,
  UiButton,
  UiCard,
  UiDialog,
  UiField,
} from '../../../shared/components'
import { openPrintableDocument } from '../../../shared/utils/printDocument'
import {
  getCompanyOptions,
  getPeriodOptions,
  getSupervisorOptions,
} from '../../catalogs/api/careersApi'
import type { OptionItem } from '../../catalogs/types/careers'
import { getMyDocuments } from '../../expedients/api/expedientsApi'
import type { ExpedientDocument } from '../../expedients/types/expedients'
import { getMyStudentProfile, uploadMyImssFile } from '../../students/api/studentsApi'
import type { StudentDetail } from '../../students/types/students'
import {
  getInternshipPrintData,
  getMyInternship,
  registerInternshipAcceptance,
  requestInternship,
} from '../api/internshipsApi'
import type { CreateInternshipPayload, InternshipDetail } from '../types/internships'

const imssPreviewOpen = ref(false)
const internship = ref<InternshipDetail | null>(null)
const studentProfile = ref<StudentDetail | null>(null)
const uploadedDocs = ref<ExpedientDocument[]>([])

const isLoading = ref(true)
const isSubmitting = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

// Catálogos para Solicitud (Forms) y Registro de Aceptación (CA)
const periodOptions = ref<OptionItem[]>([])
const companyOptions = ref<OptionItem[]>([])
const supervisorOptions = ref<OptionItem[]>([])
const imssFileForRequest = ref<File | null>(null)

// Formulario Paso 1: Reemplazo del Microsoft Forms para solicitar Carta de Presentación
const requestForm = reactive<CreateInternshipPayload>({
  periodId: 0,
  currentSemester: 7,
  shift: 'morning',
  phoneNumber: '',
  institutionalEmail: '',
})

// Modal Paso 2: Registrar Empresa y Oficio de Aceptación (CA)
const isAcceptanceModalOpen = ref(false)
const acceptanceForm = reactive({
  companyId: 0,
  supervisorId: 0,
  startDate: '2026-02-20',
  endDate: '2026-06-20',
  scheduleDescription: 'Lunes a Viernes de 08:00 a 13:00 hrs',
  requiredHours: 240,
  acceptanceLetterDate: new Date().toISOString().slice(0, 10),
})

// Modal Paso 3: Prellenar e Imprimir Anexo XVII (Productividad Laboral)
const isEvalModalOpen = ref(false)
const evalForm = reactive({
  evaluationStartDate: '2026-02-20',
  evaluationEndDate: '2026-06-20',
})

async function loadCatalogs(): Promise<void> {
  const [periods, companies] = await Promise.all([
    getPeriodOptions().catch(() => []),
    getCompanyOptions(false).catch(() => []),
  ])
  periodOptions.value = periods
  companyOptions.value = companies

  const activePeriod = periods.find((p) => Boolean((p as any).metadata?.isActive)) ?? periods[0]
  if (activePeriod && !requestForm.periodId) {
    requestForm.periodId = activePeriod.id
  }
  if (companies.length > 0 && !acceptanceForm.companyId) {
    acceptanceForm.companyId = companies[0].id
    await handleCompanyChange()
  }
}

async function handleCompanyChange(): Promise<void> {
  if (!acceptanceForm.companyId) return
  const sups = await getSupervisorOptions(acceptanceForm.companyId).catch(() => [])
  supervisorOptions.value = sups
  if (sups.length > 0) {
    acceptanceForm.supervisorId = sups[0].id
  }
}

async function loadData(): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const [profile, pp, docs] = await Promise.all([
      getMyStudentProfile().catch(() => null),
      getMyInternship().catch(() => null),
      getMyDocuments({ tramiteType: 'internship' }).catch(() => []),
    ])
    studentProfile.value = profile
    internship.value = pp
    uploadedDocs.value = docs
    evalForm.evaluationStartDate = pp?.evaluationFormatStartDate || pp?.startDate || ''
    evalForm.evaluationEndDate = pp?.evaluationFormatEndDate || pp?.endDate || ''

    if (profile) {
      requestForm.currentSemester = profile.currentSemester
      requestForm.shift = profile.shift
      requestForm.phoneNumber = profile.phoneNumber || ''
      requestForm.institutionalEmail =
        profile.institutionalEmail || `L${profile.controlNumber}@monclova.tecnm.mx`
    }

    await loadCatalogs()
  } catch (err: any) {
    errorMessage.value =
      problemMessage(err, 'Error al cargar tus Prácticas Profesionales.')
  } finally {
    isLoading.value = false
  }
}

function handleImssFileChange(event: Event): void {
  const input = event.target as HTMLInputElement
  imssFileForRequest.value = input.files?.[0] ?? null
}

async function uploadAdmissionImss(): Promise<void> {
  if (!imssFileForRequest.value || isSubmitting.value) return
  isSubmitting.value = true; errorMessage.value = ''; successMessage.value = ''
  try {
    studentProfile.value = await uploadMyImssFile(imssFileForRequest.value)
    imssFileForRequest.value = null
    imssPreviewOpen.value = false
    successMessage.value = 'Constancia cargada. Pendiente de revisión; la apertura se habilitará cuando Vinculación la apruebe.'
  } catch (error: any) { errorMessage.value = problemMessage(error, 'No fue posible cargar la constancia.') }
  finally { isSubmitting.value = false }
}
async function refreshImss(): Promise<void> {
  try { studentProfile.value = await getMyStudentProfile() }
  catch { errorMessage.value = 'No fue posible actualizar el estado de tu constancia.' }
}

async function handleSubmitFormsRequest(): Promise<void> {
  if (isSubmitting.value || !requestValidation.validate()) return

  isSubmitting.value = true
  errorMessage.value = ''
  successMessage.value = ''
  try {
    if (!studentProfile.value?.hasValidImss) {
      errorMessage.value = 'Carga tu constancia y espera la aprobación de Vinculación antes de abrir el trámite.'
      return
    }
    const created = await requestInternship(requestForm)
    internship.value = created
    successMessage.value =
      'Solicitud de Carta de Presentación enviada a Vinculación exitosamente.'
    await loadData()
  } catch (err: any) {
    requestValidation.apply(err, 'No se pudo guardar el formulario.')
    errorMessage.value =
      problemMessage(err, 'Verifica que todos los datos del formulario y tu Constancia de Vigencia IMSS estén completos.')
  } finally {
    isSubmitting.value = false
  }
}

async function handlePrint(docKey: 'presentation_letter' | 'evaluation' | 'release_certificate'): Promise<void> {
  if (!internship.value) return
  errorMessage.value = ''
  try {
    if (docKey === 'evaluation') {
      const response = await http.get(`/internships/${internship.value.id}/pdf/evaluation`, { responseType: 'blob' })
      const url = URL.createObjectURL(response.data)
      const link = document.createElement('a'); link.href = url; link.download = 'evaluacion.pdf'; link.click(); URL.revokeObjectURL(url)
      return
    }
    const data = await getInternshipPrintData(internship.value.id, docKey)
    openPrintableDocument(data)
  } catch (err: any) {
    errorMessage.value =
      problemMessage(err, 'El formato aún no está disponible para impresión.')
  }
}

async function handleSaveAcceptance(): Promise<void> {
  if (isSubmitting.value || !acceptanceValidation.validate()) return

  if (!internship.value) return
  isSubmitting.value = true
  errorMessage.value = ''
  try {
    internship.value = await registerInternshipAcceptance(internship.value.id, acceptanceForm)
    isAcceptanceModalOpen.value = false
    successMessage.value =
      'Empresa receptora registrada. Vinculación incorporará el PDF recibido de la empresa al expediente.'
    await loadData()
  } catch (err: any) {
    acceptanceValidation.apply(err, 'No se pudo guardar el formulario.')
    errorMessage.value =
      problemMessage(err, 'Error al registrar los datos de la empresa receptora.')
  } finally {
    isSubmitting.value = false
  }
}

async function handleSaveAndPrintEvaluation(): Promise<void> {
  if (isSubmitting.value || !evaluationValidation.validate()) return

  if (!internship.value) return
  isSubmitting.value = true
  errorMessage.value = ''
  try {
    await http.put(`/internships/${internship.value.id}/evaluation-format`, {
      evaluationStartDate: evalForm.evaluationStartDate,
      evaluationEndDate: evalForm.evaluationEndDate,
    })
    isEvalModalOpen.value = false
    successMessage.value = 'Formato Anexo XVII guardado y listo para imprimir.'
    await handlePrint('evaluation')
    await loadData()
  } catch (err: any) {
    evaluationValidation.apply(err, 'No se pudo guardar el formulario.')
    errorMessage.value = problemMessage(err, 'Error al guardar la evaluación Anexo XVII.')
  } finally {
    isSubmitting.value = false
  }
}

onMounted(() => {
  void loadData()
})

useRealtimeRefresh(
  (evt) => evt.tramiteType === 'internship' || evt.tramiteType === 'general',
  async () => {
    if (isSubmitting.value) return
    if (!internship.value) {
      const [profile, pp] = await Promise.all([
        getMyStudentProfile().catch(() => null),
        getMyInternship().catch(() => null),
      ])
      if (profile) studentProfile.value = profile
      if (pp) await loadData()
      return
    }
    await loadData()
  },
)
const requestValidation = useFormValidation(() => validateFields(requestForm,{currentSemester:{required:true,integer:true,min:1,maxNumber:15},shift:{required:true,choices:['morning','afternoon','night']},phoneNumber:{required:true,max:20},institutionalEmail:{...optionalEmail,required:true}}))
const acceptanceValidation = useFormValidation(() => dateOrder(validateFields(acceptanceForm,{companyId:{required:true,min:1},supervisorId:{required:true,min:1},scheduleDescription:{max:150}}),acceptanceForm,'startDate','endDate'))
const evaluationValidation = useFormValidation(() => dateOrder(validateFields(evalForm,{evaluationStartDate:{required:true},evaluationEndDate:{required:true}}),evalForm,'evaluationStartDate','evaluationEndDate'))
watchValidation(isAcceptanceModalOpen, () => acceptanceValidation.reset())
watchValidation(isEvalModalOpen, () => evaluationValidation.reset())
</script>

<template>
  <UiBasePage :fluid="true">
    <div style="margin-bottom: 1.5rem;">
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Mis Prácticas Profesionales</h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">
        Solicitud automatizada de Carta de Presentación, recepción de Oficio de Aceptación y
        validación documental
      </p>
    </div>

    <UiAlert v-if="errorMessage && !(requestValidation.hasErrors.value || acceptanceValidation.hasErrors.value || evaluationValidation.hasErrors.value)" tone="danger" style="margin-bottom: 1rem;">
      {{ errorMessage }}
    </UiAlert>
    <UiAlert v-if="successMessage" tone="success" style="margin-bottom: 1rem;">
      {{ successMessage }}
    </UiAlert>

    <div v-if="isLoading" style="padding: 2rem; text-align: center;">Cargando trámite...</div>

    <!-- ==================================================================== -->
    <!-- PASO 1: SOLICITUD DE CARTA DE PRESENTACIÓN (REEMPLAZO DEL FORMS)     -->
    <!-- ==================================================================== -->
    <div v-else-if="!internship" style="display: flex; flex-direction: column; gap: 1.5rem;">
      <UiCard
        title="Solicitud de Carta de Presentación de Prácticas Profesionales (Formulario Automatizado + IMSS)"
      >
        <p style="margin: 0 0 1rem; font-size: 0.92rem; color: #4b5563;">
          Confirma tus datos escolares y adjunta tu
          <strong>Constancia de Vigencia de Derechos del IMSS</strong>. Una vez enviada tu
          solicitud, el Coordinador de Prácticas Profesionales generará tu
          <strong>Carta de Presentación (`Oficio No. EJ26/PR...`)</strong>.
        </p>

        <div class="form-grid">
          <UiField label="Matrícula / No. de Control" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" :value="studentProfile?.controlNumber || ''" type="text" disabled />
          </UiField>

          <UiField label="Nombre Completo del Alumno" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs"
              :value="
                studentProfile
                  ? `${studentProfile.firstName} ${studentProfile.firstSurname} ${studentProfile.secondSurname || ''}`.trim()
                  : ''
              "
              type="text"
              disabled
            />
          </UiField>

          <UiField label="Carrera" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" :value="studentProfile?.careerName || ''" type="text" disabled />
          </UiField>

          <UiFormErrors :validation="requestValidation" />
          <UiField label="Periodo Escolar *" v-bind="requestValidation.field('periodId', 'Periodo Escolar *')" v-slot="{ controlAttrs }">
            <select v-bind="controlAttrs" v-model.number="requestForm.periodId">
              <option v-for="p in periodOptions" :key="p.id" :value="p.id">{{ p.label }}</option>
            </select>
          </UiField>

          <UiField label="Semestre que Cursa (1-15) *" v-bind="requestValidation.field('currentSemester', 'Semestre que Cursa (1-15) *')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" v-model.number="requestForm.currentSemester" type="number" min="1" max="15" />
          </UiField>

          <UiField label="Turno *" v-bind="requestValidation.field('shift', 'Turno *')" v-slot="{ controlAttrs }">
            <select v-bind="controlAttrs" v-model="requestForm.shift">
              <option value="morning">Matutino</option>
              <option value="night">Nocturno</option>
              <option value="afternoon">Vespertino</option>
            </select>
          </UiField>

          <UiField label="Teléfono de Contacto *" v-bind="requestValidation.field('phoneNumber', 'Teléfono de Contacto *')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" v-model="requestForm.phoneNumber" type="text" placeholder="Ej. 8661458920" />
          </UiField>

          <UiField label="Correo Electrónico INSTITUCIONAL *" v-bind="requestValidation.field('institutionalEmail', 'Correo Electrónico INSTITUCIONAL *')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs"
              v-model="requestForm.institutionalEmail" readonly
              type="email"
              placeholder="L23050363@monclova.tecnm.mx"
            />
          </UiField>
        </div>

        <div class="imss-banner">
          <div>
            <strong>Constancia de Vigencia de Derechos del IMSS (PDF) *</strong>
            <div style="font-size: 0.85rem; color: #4b5563;">
              Estado actual:
              <UiBadge :tone="studentProfile?.hasValidImss ? 'success' : 'warning'">
                {{
                  studentProfile?.hasValidImss
                    ? 'Vigencia IMSS ya cargada en perfil'
                    : 'Adjunta tu PDF de Vigencia IMSS'
                }}
              </UiBadge>
            </div>
          </div>
          <input type="file" accept="application/pdf" @change="handleImssFileChange" />
          <UiButton variant="secondary" :disabled="!imssFileForRequest || isSubmitting" @click="imssPreviewOpen = true">Subir constancia para revisión</UiButton>
          <UiButton variant="secondary" :disabled="isSubmitting" @click="refreshImss">Actualizar estado IMSS</UiButton>
          <p v-if="studentProfile?.imssStatus === 'pending'">Pendiente de revisión por Vinculación. Todavía no puedes abrir el trámite.</p>
          <UiAlert v-if="studentProfile?.imssRejectionReason" tone="danger">{{ studentProfile.imssRejectionReason }}</UiAlert>
        </div>

        <div style="margin-top: 1.25rem; display: flex; justify-content: flex-end;">
          <UiButton variant="primary" :disabled="isSubmitting || !studentProfile?.hasValidImss || !!imssFileForRequest" @click="handleSubmitFormsRequest">
            {{ isSubmitting ? 'Enviando Solicitud...' : 'Solicitar Carta de Presentación' }}
          </UiButton>
        </div>
      </UiCard>
    </div>

    <!-- ==================================================================== -->
    <!-- PASO 2 Y 3: SEGUIMIENTO DE PRÁCTICAS Y CARGA DE DOCUMENTOS           -->
    <!-- ==================================================================== -->
    <div v-else style="display: flex; flex-direction: column; gap: 1.5rem;">
      <UiCard title="Estado de Mis Prácticas Profesionales">
        <div class="summary-grid">
          <div>
            <span class="label">Folio Carta de Presentación (CP)</span>
            <strong>{{ internship.presentationLetterFolio || 'En emisión por Vinculación' }}</strong>
          </div>
          <div>
            <span class="label">Empresa Receptora</span>
            <strong>{{ internship.company?.name || 'Por registrar con Oficio de Aceptación' }}</strong>
          </div>
          <div>
            <span class="label">Progreso Documental</span>
            <strong style="font-size: 1.15rem; color: #16a34a;">
              {{ internship.documentProgressPercentage }}%
            </strong>
          </div>
          <div>
            <span class="label">Constancia de Liberación (CC)</span>
            <strong style="color: #16a34a;">
              {{ internship.releaseCertificateFolio || 'Al concluir expediente' }}
            </strong>
          </div>
        </div>

        <UiAlert tone="info" style="margin-top: 1rem;">
          <strong>Instrucción para el Oficio de Aceptación:</strong> Una vez entregada tu Carta de
          Presentación, la empresa debe enviar el <strong>Oficio de Aceptación</strong> en
          <strong>hoja membretada, con firma y sello</strong> al correo
          <strong><code>arturo.martinez@monclova.tecnm.mx</code></strong>. Después, sube aquí tus
          documentos para que Vinculación los valide.
        </UiAlert>

        <div class="toolbar">
          <UiButton
            v-if="internship.presentationLetterFolio"
            variant="primary"
            size="sm"
            @click="handlePrint('presentation_letter')"
          >
            Descargar Carta de Presentación ({{ internship.presentationLetterFolio }})
          </UiButton>

          <UiButton variant="secondary" size="sm" @click="isAcceptanceModalOpen = true">
            Registrar Empresa Receptora (CA)
          </UiButton>

          <UiButton variant="secondary" size="sm" @click="isEvalModalOpen = true">
            Prellenar e Imprimir Evaluación Anexo XVII
          </UiButton>

          <UiButton
            v-if="internship.status === 'released'"
            variant="primary"
            size="sm"
            @click="handlePrint('release_certificate')"
          >
            Descargar Constancia de Acreditación ({{ internship.releaseCertificateFolio }})
          </UiButton>
        </div>
      </UiCard>

      <!-- Checklist y Carga de Documentos de Prácticas -->
      <ExpedientChecklist :requirements="internship.documentRequirements" :documents="uploadedDocs.filter(d => d.tramiteId === internship!.id)" tramite-type="internship" :tramite-id="internship.id" :can-edit="!['released', 'cancelled', 'non_compliant'].includes(internship.status)" @changed="loadData" @edit="isEvalModalOpen = true" />
    </div>

    <!-- ==================================================================== -->
    <!-- MODAL 1: REGISTRAR EMPRESA RECEPTORA Y PERIODO DE ACEPTACIÓN         -->
    <!-- ==================================================================== -->
    <UiDialog
      v-model="isAcceptanceModalOpen"
      size="lg"
      title="Registrar Datos del Oficio de Aceptación (Empresa Receptora)"
    >
      <div style="display: flex; flex-direction: column; gap: 0.85rem;">
        <UiFormErrors :validation="acceptanceValidation" />
        <UiField label="Empresa Receptora *" v-bind="acceptanceValidation.field('companyId', 'Empresa Receptora *')" v-slot="{ controlAttrs }">
          <select v-bind="controlAttrs" v-model.number="acceptanceForm.companyId" @change="handleCompanyChange">
            <option v-for="c in companyOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
          </select>
        </UiField>

        <UiField label="Responsable / Supervisor en la Empresa *" v-bind="acceptanceValidation.field('supervisorId', 'Responsable / Supervisor en la Empresa *')" v-slot="{ controlAttrs }">
          <select v-bind="controlAttrs" v-model.number="acceptanceForm.supervisorId">
            <option v-for="s in supervisorOptions" :key="s.id" :value="s.id">{{ s.label }}</option>
          </select>
        </UiField>

        <div class="modal-dates-grid" style="display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem;">
          <UiField label="Fecha de Inicio *" v-bind="acceptanceValidation.field('startDate', 'Fecha de Inicio *')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" v-model="acceptanceForm.startDate" type="date" />
          </UiField>
          <UiField label="Fecha de Término *" v-bind="acceptanceValidation.field('endDate', 'Fecha de Término *')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" v-model="acceptanceForm.endDate" type="date" />
          </UiField>
        </div>

        <UiField label="Horario de Prácticas" v-bind="acceptanceValidation.field('scheduleDescription', 'Horario de Prácticas')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" v-model="acceptanceForm.scheduleDescription" type="text" />
        </UiField>
      </div>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 0.5rem;">
          <UiButton variant="secondary" @click="isAcceptanceModalOpen = false">Cancelar</UiButton>
          <UiButton variant="primary" :disabled="isSubmitting" @click="handleSaveAcceptance">
            Guardar Datos de Empresa
          </UiButton>
        </div>
      </template>
    </UiDialog>

    <!-- ==================================================================== -->
    <!-- MODAL 2: PRELLENAR E IMPRIMIR ANEXO XVII (PRODUCTIVIDAD LABORAL)     -->
    <!-- ==================================================================== -->
    <UiDialog
      v-model="isEvalModalOpen"
      size="lg"
      title="Prellenar e Imprimir Anexo XVII — Evaluación de Productividad Laboral"
    >
      <div style="display: flex; flex-direction: column; gap: 0.85rem;">
        <div class="modal-dates-grid" style="display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem;">
          <UiFormErrors :validation="evaluationValidation" />
          <UiField label="Fecha Inicio Evaluación *" v-bind="evaluationValidation.field('evaluationStartDate', 'Fecha Inicio Evaluación *')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" v-model="evalForm.evaluationStartDate" type="date" />
          </UiField>
          <UiField label="Fecha Fin Evaluación *" v-bind="evaluationValidation.field('evaluationEndDate', 'Fecha Fin Evaluación *')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" v-model="evalForm.evaluationEndDate" type="date" />
          </UiField>
        </div>


      </div>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 0.5rem;">
          <UiButton variant="secondary" @click="isEvalModalOpen = false">Cancelar</UiButton>
          <UiButton
            variant="primary"
            :disabled="isSubmitting"
            @click="handleSaveAndPrintEvaluation"
          >
            Guardar e Imprimir Anexo XVII
          </UiButton>
        </div>
      </template>
    </UiDialog>
  </UiBasePage>
  <DocumentViewerModal v-model="imssPreviewOpen" title="Verifica tu constancia IMSS" :blob="imssFileForRequest" confirm-upload :busy="isSubmitting" :error="errorMessage" @confirm="uploadAdmissionImss" />
</template>

<style scoped>
.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(230px, 1fr));
  gap: 1rem;
}

.imss-banner {
  margin-top: 1.25rem;
  padding: 1rem;
  border-radius: 8px;
  background: #f0fdf4;
  border: 1px dashed #16a34a;
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 1rem;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
  align-items: center;
}

.label {
  display: block;
  font-size: 0.8rem;
  color: #6b7280;
  margin-bottom: 0.2rem;
}

.toolbar {
  margin-top: 1rem;
  display: flex;
  flex-wrap: wrap;
  gap: 0.6rem;
}

.steps-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.step-card {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 1rem;
  padding: 0.9rem 1rem;
  border: 1px solid var(--color-border, #e5e7eb);
  border-radius: 8px;
}

.step-info {
  flex: 1;
  min-width: 260px;
}

.step-header {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  flex-wrap: wrap;
}

.step-hint {
  margin: 0.25rem 0 0;
  font-size: 0.84rem;
  color: #6b7280;
}

.step-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.upload-label {
  display: inline-flex;
  align-items: center;
  padding: 0.4rem 0.8rem;
  background: #16a34a;
  color: #fff;
  border-radius: 6px;
  font-size: 0.82rem;
  font-weight: 600;
  cursor: pointer;
}

.upload-label:hover {
  background: #15803d;
}

@media (max-width: 640px) {
  .form-grid,
  .summary-grid,
  .modal-dates-grid {
    grid-template-columns: 1fr !important;
    gap: 0.75rem;
  }

  .imss-banner {
    padding: 0.85rem;
    flex-direction: column;
    align-items: stretch;
    gap: 0.65rem;
  }

  .imss-banner input[type='file'] {
    max-width: 100%;
    font-size: 0.84rem;
  }

  .imss-banner :deep(.btn),
  .toolbar :deep(.btn) {
    flex: 1 1 100%;
    white-space: normal;
    text-align: center;
  }
}
</style>
