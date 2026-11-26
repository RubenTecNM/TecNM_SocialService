<script setup lang="ts">
import { problemMessage } from '../../../shared/api/problemDetails'
import { watch as watchValidation } from 'vue'
import { useFormValidation } from '../../../shared/composables/useFormValidation'
import { useRealtimeRefresh } from '../../../shared/composables/useRealtime'
import { validateFields, optionalEmail, dateOrder } from '../../../shared/utils/validationRules'
import UiFormErrors from '../../../shared/components/UiFormErrors.vue'
import DocumentViewerModal from '../../expedients/components/DocumentViewerModal.vue'

import { socialDocumentLabel } from '../../../shared/utils/socialServiceDocuments'
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
  getProgramOptions,
  getSupervisorOptions,
} from '../../catalogs/api/careersApi'
import type { OptionItem } from '../../catalogs/types/careers'
import { getMyDocuments } from '../../expedients/api/expedientsApi'
import type { ExpedientDocument } from '../../expedients/types/expedients'
import { getMyStudentProfile } from '../../students/api/studentsApi'
import type { StudentDetail } from '../../students/types/students'
import {
  getMySocialService,
  getSocialServicePrintData,
  submitSocialService,
  submitBimonthlyReport,
  upsertCommitment,
} from '../api/socialServiceApi'
import type { CreateSocialServicePayload, SocialServiceDetail } from '../types/socialService'

const imssPreviewOpen = ref(false)
const service = ref<SocialServiceDetail | null>(null)
const studentProfile = ref<StudentDetail | null>(null)
const uploadedDocs = ref<ExpedientDocument[]>([])

const isLoading = ref(true)
const isSubmitting = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

// Catálogos para el Asistente de Apertura Anexo XVIII
const periodOptions = ref<OptionItem[]>([])
const programOptions = ref<OptionItem[]>([])
const companyOptions = ref<OptionItem[]>([])
const supervisorOptions = ref<OptionItem[]>([])
const imssFileForApplication = ref<File | null>(null)

const openForm = reactive<CreateSocialServicePayload>({
  periodId: 0,
  programId: 0,
  companyId: 0,
  supervisorId: 0,
  modality: 'internal',
  startDate: '2026-02-16',
  endDate: '2026-08-16',
  targetCommunity: 'Comunidad Estudiantil y Regional',
  application: {
    studentAddress: '',
    studentPhone: '',
    periodType: 'february_july',
    dependencyName: '',
    dependencyHeadName: '',
    dependencyHeadPosition: '',
    activitiesOutline: '',
  },
})

// Modal Carta Compromiso (Anexo XX)
const isCommitmentModalOpen = ref(false)
const commitmentForm = reactive({
  studentAddress: '',
  studentPhone: '',
  dependencyAddress: 'Av. Tecnológico No. 4801, Monclova, Coahuila',
  commitmentDate: new Date().toISOString().slice(0, 10),
  city: 'Monclova',
})

// Modal Reporte Bimestral (Anexo XXII)
const isReportModalOpen = ref(false)
const activeReportNumber = ref<number>(1)
const reportForm = reactive({
  periodStartDate: '2026-02-16',
  periodEndDate: '2026-04-16',
  activitiesSummary: '',
  reportedHours: 160,
})

async function loadCatalogsForApplication(): Promise<void> {
  const [periods, programs] = await Promise.all([
    getPeriodOptions().catch(() => []),
    getProgramOptions().catch(() => []),
  ])
  periodOptions.value = periods
  programOptions.value = programs

  const activePeriod = periods.find((p) => Boolean((p as any).metadata?.isActive)) ?? periods[0]
  if (activePeriod) {
    openForm.periodId = activePeriod.id
  }
  if (programs.length > 0) openForm.programId = programs[0].id
  await handleModalityChange()
}

async function handleModalityChange(): Promise<void> {
  const isInternal = openForm.modality === 'internal'
  const companies = await getCompanyOptions(isInternal).catch(() => [])
  companyOptions.value = companies
  if (companies.length > 0) {
    openForm.companyId = companies[0].id
    openForm.application.dependencyName = companies[0].label
    await handleCompanyChange()
  } else {
    openForm.companyId = 0
    supervisorOptions.value = []
  }
}

async function handleCompanyChange(): Promise<void> {
  if (!openForm.companyId) return
  const comp = companyOptions.value.find((c) => c.id === openForm.companyId)
  if (comp) openForm.application.dependencyName = comp.label

  const sups = await getSupervisorOptions(openForm.companyId).catch(() => [])
  supervisorOptions.value = sups
  if (sups.length > 0) {
    openForm.supervisorId = sups[0].id
    handleSupervisorChange()
  }
}

function handleSupervisorChange(): void {
  const sup = supervisorOptions.value.find((s) => s.id === openForm.supervisorId)
  if (sup) {
    const parts = sup.label.split('—').map((p) => p.trim())
    openForm.application.dependencyHeadName = parts[0] || sup.label
    openForm.application.dependencyHeadPosition = parts[1] || 'Responsable de Área'
  }
}

async function loadData(): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const [profile, ss, docs] = await Promise.all([
      getMyStudentProfile().catch(() => null),
      getMySocialService().catch(() => null),
      getMyDocuments({ tramiteType: 'social_service' }).catch(() => []),
    ])
    studentProfile.value = profile
    service.value = ss
    uploadedDocs.value = docs

    if (profile) {
      openForm.application.studentPhone = profile.phoneNumber || ''
    }

    if (!ss) {
      await loadCatalogsForApplication()
    } else {
      commitmentForm.studentAddress =
        ss.commitment?.studentAddress || ss.application?.studentAddress || ''
      commitmentForm.studentPhone =
        ss.commitment?.studentPhone || ss.application?.studentPhone || profile?.phoneNumber || ''
      commitmentForm.commitmentDate = ss.commitment?.commitmentDate || ss.startDate
    }
  } catch (err: any) {
    errorMessage.value = problemMessage(err, 'Error al cargar tu Servicio Social.')
  } finally {
    isLoading.value = false
  }
}

function handleImssFileSelect(event: Event): void {
  const input = event.target as HTMLInputElement
  imssFileForApplication.value = input.files?.[0] ?? null
}

function prepareApplicationSubmission(): void {
  if (isSubmitting.value || !applicationValidation.validate()) return
  if (imssFileForApplication.value) imssPreviewOpen.value = true
  else void handleSubmitApplication()
}
async function handleSubmitApplication(): Promise<void> {
  if (isSubmitting.value) return
  if (!applicationValidation.validate()) { imssPreviewOpen.value = false; return }

  isSubmitting.value = true
  errorMessage.value = ''
  successMessage.value = ''
  try {
    if (!imssFileForApplication.value && !['pending', 'approved'].includes(studentProfile.value?.imssStatus || '')) {
      errorMessage.value = 'Adjunta tu constancia IMSS para enviar la solicitud.'
      return
    }
    const created = await submitSocialService(openForm, imssFileForApplication.value)
    imssPreviewOpen.value = false
    imssFileForApplication.value = null
    service.value = created
    successMessage.value =
      `${socialDocumentLabel('SOL')} generada y enviada a Vinculación.`
    await loadData()
  } catch (err: any) {
    applicationValidation.apply(err, 'No se pudo guardar el formulario.')
    if (applicationValidation.entries.value.length) imssPreviewOpen.value = false
    errorMessage.value =
      problemMessage(err, 'Verifica todos los campos de la solicitud y tu vigencia IMSS.')
  } finally {
    isSubmitting.value = false
  }
}

async function handlePrintFormat(documentKey: string): Promise<void> {
  if (!service.value) return
  errorMessage.value = ''
  try {
    if (documentKey !== 'release_certificate') {
      const response = await http.get(`/social-services/${service.value.id}/pdf/${documentKey}`, { responseType: 'blob' })
      const url = URL.createObjectURL(response.data)
      const link = document.createElement('a'); link.href = url; link.download = `${documentKey}.pdf`; link.click(); URL.revokeObjectURL(url)
      return
    }
    const printData = await getSocialServicePrintData(service.value.id, documentKey)
    openPrintableDocument(printData)
  } catch (err: any) {
    errorMessage.value =
      problemMessage(err, 'El formato aún no está disponible para impresión.')
  }
}

function handleOpenStepModal(modalKey: string): void {
  if (!service.value) return
  if (modalKey === 'commitment') {
    isCommitmentModalOpen.value = true
  } else if (modalKey.startsWith('report_')) {
    const num = Number(modalKey.split('_')[1] || 1)
    activeReportNumber.value = num
    const existing = service.value.reports?.find((r) => r.reportNumber === num)
    reportForm.periodStartDate = existing?.periodStartDate || service.value.startDate
    reportForm.periodEndDate = existing?.periodEndDate || service.value.endDate
    reportForm.activitiesSummary =
      existing?.activitiesSummary || service.value.application?.activitiesOutline || ''
    reportForm.reportedHours = existing?.reportedHours || 160
    isReportModalOpen.value = true
  }
}

async function handleSaveAndPrintCommitment(): Promise<void> {
  if (isSubmitting.value || !commitmentValidation.validate()) return

  if (!service.value) return
  isSubmitting.value = true
  errorMessage.value = ''
  try {
    service.value = await upsertCommitment(service.value.id, commitmentForm)
    isCommitmentModalOpen.value = false
    successMessage.value = `${socialDocumentLabel('CC')}: datos guardados.`
    await handlePrintFormat('commitment')
  } catch (err: any) {
    commitmentValidation.apply(err, 'No se pudo guardar el formulario.')
    errorMessage.value = problemMessage(err, 'Error al guardar la Carta Compromiso.')
  } finally {
    isSubmitting.value = false
  }
}

async function handleSaveAndPrintReport(): Promise<void> {
  if (isSubmitting.value || !reportValidation.validate()) return

  if (!service.value) return
  isSubmitting.value = true
  errorMessage.value = ''
  try {
    await submitBimonthlyReport(service.value.id, activeReportNumber.value, {
      periodStartDate: reportForm.periodStartDate,
      periodEndDate: reportForm.periodEndDate,
      activitiesSummary: reportForm.activitiesSummary,
      reportedHours: reportForm.reportedHours,
      evaluatorSupervisorId: service.value.supervisor?.id ?? null,
    })
    isReportModalOpen.value = false
    successMessage.value = `${socialDocumentLabel(`R${activeReportNumber.value}`)} guardado y listo para imprimir.`
    await handlePrintFormat(`report_${activeReportNumber.value}`)
    await loadData()
  } catch (err: any) {
    reportValidation.apply(err, 'No se pudo guardar el formulario.')
    errorMessage.value = problemMessage(err, 'Error al guardar el Reporte Bimestral.')
  } finally {
    isSubmitting.value = false
  }
}

onMounted(() => {
  void loadData()
})

useRealtimeRefresh(
  (evt) => evt.tramiteType === 'social_service' || evt.tramiteType === 'general',
  async () => {
    if (isSubmitting.value) return
    if (!service.value) {
      const [profile, ss] = await Promise.all([
        getMyStudentProfile().catch(() => null),
        getMySocialService().catch(() => null),
      ])
      if (profile) studentProfile.value = profile
      if (ss) await loadData()
      return
    }
    await loadData()
  },
)
const applicationValidation = useFormValidation(() => {
 const errors = validateFields(openForm, {programId:{required:true,min:1},companyId:{required:true,min:1},supervisorId:{required:true,min:1},startDate:{required:true},endDate:{required:true},'application.studentAddress':{required:true},'application.studentPhone':{required:true,max:20},'application.dependencyName':{required:true,max:200},'application.dependencyHeadName':{required:true,max:180},'application.dependencyHeadPosition':{required:true,max:150},'application.activitiesOutline':{required:true},'application.periodType':{required:true,choices:['february_july','august_january']}})
 return dateOrder(errors, openForm, 'startDate', 'endDate')
})
const reportValidation = useFormValidation(() => dateOrder(validateFields(reportForm, {activitiesSummary:{required:true}}), reportForm,'periodStartDate','periodEndDate'))
const commitmentValidation = useFormValidation(() => validateFields(commitmentForm,{studentAddress:{required:true},studentPhone:{required:true,max:20},dependencyAddress:{required:true}}))
watchValidation(isReportModalOpen, () => reportValidation.reset())
watchValidation(isCommitmentModalOpen, () => commitmentValidation.reset())
</script>

<template>
  <UiBasePage :fluid="true">
    <div style="margin-bottom: 1.5rem;">
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Mi Servicio Social</h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">
        Descarga de formatos oficiales, carta de aceptación de la dependencia y
        seguimiento por documentos firmados y sellados
      </p>
    </div>

    <UiAlert v-if="errorMessage && !(applicationValidation.hasErrors.value || reportValidation.hasErrors.value || commitmentValidation.hasErrors.value)" tone="danger" style="margin-bottom: 1rem;">
      {{ errorMessage }}
    </UiAlert>
    <UiAlert v-if="successMessage" tone="success" style="margin-bottom: 1rem;">
      {{ successMessage }}
    </UiAlert>

    <div v-if="isLoading" style="padding: 2rem; text-align: center;">Cargando trámite...</div>

    <!-- ==================================================================== -->
    <!-- CASO 1: ALUMNO SIN TRÁMITE -> ASISTENTE DE APERTURA (ANEXO XVIII)    -->
    <!-- ==================================================================== -->
    <div v-else-if="!service" style="display: flex; flex-direction: column; gap: 1.5rem;">
      <UiCard :title="socialDocumentLabel('SOL')">
        <p style="margin: 0 0 1rem; color: #4b5563; font-size: 0.92rem;">
          Llena tus datos y adjunta tu <strong>Constancia de Vigencia de Derechos del IMSS</strong>.
          El sistema generará automáticamente tu formato oficial <strong>{{ socialDocumentLabel('SOL') }}</strong> y
          enviará tu solicitud al departamento de Vinculación. Después podrás adjuntar tu Carta de Presentación firmada.
        </p>

        <div class="form-grid">
          <UiFormErrors :validation="applicationValidation" />
          <UiField label="Modalidad del Servicio Social *" v-bind="applicationValidation.field('modality', 'Modalidad del Servicio Social *')" v-slot="{ controlAttrs }">
            <select v-bind="controlAttrs" v-model="openForm.modality" @change="handleModalityChange">
              <option value="internal">
                Local en el TecNM (Genera Carta de Aceptación automática)
              </option>
              <option value="external">Dependencia Externa (Gobierno / Asociación Civil)</option>
            </select>
          </UiField>

          <UiField label="Periodo Escolar *" v-bind="applicationValidation.field('periodId', 'Periodo Escolar *')" v-slot="{ controlAttrs }">
            <select v-bind="controlAttrs" v-model.number="openForm.periodId">
              <option v-for="p in periodOptions" :key="p.id" :value="p.id">{{ p.label }}</option>
            </select>
          </UiField>

          <UiField label="Programa de Servicio Social *" v-bind="applicationValidation.field('programId', 'Programa de Servicio Social *')" v-slot="{ controlAttrs }">
            <select v-bind="controlAttrs" v-model.number="openForm.programId">
              <option v-for="prog in programOptions" :key="prog.id" :value="prog.id">
                {{ prog.label }}
              </option>
            </select>
          </UiField>

          <UiField label="Dependencia / Departamento Receptor *" v-bind="applicationValidation.field('companyId', 'Dependencia / Departamento Receptor *')" v-slot="{ controlAttrs }">
            <select v-bind="controlAttrs" v-model.number="openForm.companyId" @change="handleCompanyChange">
              <option v-for="c in companyOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
            </select>
          </UiField>

          <UiField label="Titular / Responsable del Área *" v-bind="applicationValidation.field('supervisorId', 'Titular / Responsable del Área *')" v-slot="{ controlAttrs }">
            <select v-bind="controlAttrs" v-model.number="openForm.supervisorId" @change="handleSupervisorChange">
              <option v-for="s in supervisorOptions" :key="s.id" :value="s.id">{{ s.label }}</option>
            </select>
          </UiField>

          <UiField label="Teléfono del Estudiante *" v-bind="applicationValidation.field('application.studentPhone', 'Teléfono del Estudiante *')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs"
              v-model="openForm.application.studentPhone"
              type="text"
              placeholder="Ej. 8661458920"
            />
          </UiField>

          <UiField label="Domicilio Completo del Estudiante *" v-bind="applicationValidation.field('application.studentAddress', 'Domicilio Completo del Estudiante *')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs"
              v-model="openForm.application.studentAddress"
              type="text"
              placeholder="Calle, Número, Colonia y Ciudad"
            />
          </UiField>

          <UiField label="Fecha de Inicio *" v-bind="applicationValidation.field('startDate', 'Fecha de Inicio *')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" v-model="openForm.startDate" type="date" />
          </UiField>

          <UiField label="Fecha de Término (Mín. 6 meses) *" v-bind="applicationValidation.field('endDate', 'Fecha de Término (Mín. 6 meses) *')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" v-model="openForm.endDate" type="date" />
          </UiField>
        </div>

        <div style="margin-top: 1rem;">
          <UiField label="Actividades a Desarrollar en el Programa (Anexo XVIII) *" v-bind="applicationValidation.field('application.activitiesOutline', 'Actividades a Desarrollar en el Programa (Anexo XVIII) *')" v-slot="{ controlAttrs }">
            <textarea v-bind="controlAttrs"
              v-model="openForm.application.activitiesOutline"
              rows="3"
              style="width: 100%; padding: 0.5rem; border: 1px solid #cbd5e1; border-radius: 6px;"
              placeholder="Describe brevemente las actividades que realizarás durante tu servicio social..."
            ></textarea>
          </UiField>
        </div>

        <div class="imss-upload-banner">
          <div>
            <strong>Constancia de Vigencia de Derechos del IMSS (PDF)</strong>
            <div style="font-size: 0.85rem; color: #4b5563;">
              Estado actual:
              <UiBadge :tone="studentProfile?.hasValidImss ? 'success' : 'warning'">
                {{
                  studentProfile?.hasValidImss
                    ? 'Constancia aprobada registrada'
                    : studentProfile?.imssStatus === 'pending' ? 'Constancia pendiente de revisión' : 'Adjunta tu constancia IMSS'
                }}
              </UiBadge>
            </div>
          </div>
          <input type="file" accept="application/pdf" @change="handleImssFileSelect" />


          <p v-if="studentProfile?.imssStatus === 'pending'">La constancia se validará junto con tu solicitud.</p>
          <UiAlert v-if="studentProfile?.imssRejectionReason" tone="danger">{{ studentProfile.imssRejectionReason }}</UiAlert>
        </div>

        <div style="margin-top: 1.25rem; display: flex; justify-content: flex-end;">
          <UiButton variant="primary" :disabled="isSubmitting || (!imssFileForApplication && !['pending', 'approved'].includes(studentProfile?.imssStatus || ''))" @click="prepareApplicationSubmission">
            {{
              isSubmitting
                ? 'Generando Solicitud...'
                : 'Enviar Solicitud e IMSS a revisión'
            }}
          </UiButton>
        </div>
      </UiCard>
    </div>

    <!-- ==================================================================== -->
    <!-- CASO 2: ALUMNO CON TRÁMITE EN CURSO O LIBERADO                       -->
    <!-- ==================================================================== -->
    <div v-else style="display: flex; flex-direction: column; gap: 1.5rem;">
      <!-- Resumen y Botones Rápidos de Plantillas -->
      <UiCard title="Resumen de Mi Trámite y Descarga de Cartas Oficiales">
        <div class="summary-grid">
          <div>
            <span class="label">Dependencia Receptora</span>
            <strong>{{ service.company.name }}</strong>
          </div>
          <div>
            <span class="label">Modalidad</span>
            <UiBadge :tone="service.modality === 'internal' ? 'info' : 'purple'">
              {{
                service.modality === 'internal'
                  ? 'Local en el TecNM (Interno)'
                  : 'Dependencia Externa'
              }}
            </UiBadge>
          </div>
          <div>
            <span class="label">Folio {{ socialDocumentLabel('CP') }}</span>
            <strong>{{ service.presentationLetterFolio || 'Consultar carga y dictamen en el expediente' }}</strong>
          </div>
          <div>
            <span class="label">Avance por Documentos Validados</span>
            <strong style="font-size: 1.15rem; color: #0284c7;">
              {{ service.documentProgressPercentage }}%
            </strong>
          </div>
        </div>

        <div class="templates-toolbar">
          <UiButton variant="secondary" size="sm" @click="handlePrintFormat('application')">
            Imprimir {{ socialDocumentLabel('SOL') }}
          </UiButton>

          <UiButton
            v-if="service.presentationLetterFolio"
            variant="primary"
            size="sm"
            @click="handlePrintFormat('presentation_letter')"
          >
            Descargar {{ socialDocumentLabel('CP') }} ({{ service.presentationLetterFolio }})
          </UiButton>



          <UiButton
            v-if="service.status === 'released'"
            variant="primary"
            size="sm"
            @click="handlePrintFormat('release_certificate')"
          >
            Descargar Constancia de Liberación ({{ service.releaseCertificateFolio }})
          </UiButton>
        </div>
      </UiCard>

      <!-- Paso a Paso de Documentos y Prellenado de Formatos -->
      <ExpedientChecklist :application-accepted="service.application?.isAcceptedByOffice" :requirements="service.documentRequirements" :documents="uploadedDocs.filter(d => d.tramiteId === service!.id)" tramite-type="social_service" :tramite-id="service.id" :can-edit="!['released', 'cancelled', 'non_compliant'].includes(service.status)" @changed="loadData" @edit="handleOpenStepModal($event)" />
    </div>

    <!-- ==================================================================== -->
    <!-- MODAL 1: PRELLENAR CARTA COMPROMISO (ANEXO XX)                       -->
    <!-- ==================================================================== -->
    <UiDialog
      v-model="isCommitmentModalOpen"
      size="lg"
      :title="socialDocumentLabel('CC')"
    >
      <div style="display: flex; flex-direction: column; gap: 0.85rem;">
        <UiFormErrors :validation="commitmentValidation" />
        <UiField label="Domicilio del Estudiante *" v-bind="commitmentValidation.field('studentAddress', 'Domicilio del Estudiante *')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" v-model="commitmentForm.studentAddress" type="text" />
        </UiField>
        <UiField label="Teléfono del Estudiante *" v-bind="commitmentValidation.field('studentPhone', 'Teléfono del Estudiante *')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" v-model="commitmentForm.studentPhone" type="text" />
        </UiField>
        <UiField label="Domicilio de la Dependencia *" v-bind="commitmentValidation.field('dependencyAddress', 'Domicilio de la Dependencia *')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" v-model="commitmentForm.dependencyAddress" type="text" />
        </UiField>
        <UiField label="Fecha de Compromiso *" v-bind="commitmentValidation.field('commitmentDate', 'Fecha de Compromiso *')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" v-model="commitmentForm.commitmentDate" type="date" />
        </UiField>
      </div>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 0.5rem;">
          <UiButton variant="secondary" @click="isCommitmentModalOpen = false">Cancelar</UiButton>
          <UiButton
            variant="primary"
            :disabled="isSubmitting"
            @click="handleSaveAndPrintCommitment"
          >
            Guardar e imprimir {{ socialDocumentLabel('CC') }}
          </UiButton>
        </div>
      </template>
    </UiDialog>

    <!-- ==================================================================== -->
    <!-- MODAL 2: PRELLENAR REPORTE BIMESTRAL (ANEXO XXII)                    -->
    <!-- ==================================================================== -->
    <UiDialog
      v-model="isReportModalOpen"
      size="lg"
      :title="socialDocumentLabel(`R${activeReportNumber}`)"
    >
      <div style="display: flex; flex-direction: column; gap: 0.85rem;">
        <div class="modal-dates-grid" style="display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem;">
          <UiFormErrors :validation="reportValidation" />
          <UiField label="Fecha Inicio del Bimestre *" v-bind="reportValidation.field('periodStartDate', 'Fecha Inicio del Bimestre *')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" v-model="reportForm.periodStartDate" type="date" />
          </UiField>
          <UiField label="Fecha Fin del Bimestre *" v-bind="reportValidation.field('periodEndDate', 'Fecha Fin del Bimestre *')" v-slot="{ controlAttrs }">
            <input v-bind="controlAttrs" v-model="reportForm.periodEndDate" type="date" />
          </UiField>
        </div>
        <UiField label="Horas Reportadas en el Bimestre (Informativo para impresión) *" v-bind="reportValidation.field('reportedHours', 'Horas Reportadas en el Bimestre (Informativo para impresión) *')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" v-model.number="reportForm.reportedHours" type="number" min="1" max="480" />
        </UiField>
        <UiField label="Resumen de Actividades Realizadas *" v-bind="reportValidation.field('activitiesSummary', 'Resumen de Actividades Realizadas *')" v-slot="{ controlAttrs }">
          <textarea v-bind="controlAttrs"
            v-model="reportForm.activitiesSummary"
            rows="4"
            style="width: 100%; padding: 0.5rem; border: 1px solid #cbd5e1; border-radius: 6px;"
          ></textarea>
        </UiField>
      </div>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 0.5rem;">
          <UiButton variant="secondary" @click="isReportModalOpen = false">Cancelar</UiButton>
          <UiButton variant="primary" :disabled="isSubmitting" @click="handleSaveAndPrintReport">
            Guardar e Imprimir Formato Lleno
          </UiButton>
        </div>
      </template>
    </UiDialog>

    <!-- ==================================================================== -->
    <!-- MODAL 3: PRELLENAR EVALUACIONES CUALITATIVAS (ANEXOS XXIII, XXIV, XXV)-->
    <!-- ==================================================================== -->

  </UiBasePage>
  <DocumentViewerModal v-model="imssPreviewOpen" title="Confirma tu Solicitud y constancia IMSS" :blob="imssFileForApplication" confirm-upload :busy="isSubmitting" :error="errorMessage" @confirm="handleSubmitApplication" />
</template>

<style scoped>
.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(230px, 1fr));
  gap: 1rem;
}

.imss-upload-banner {
  margin-top: 1.25rem;
  padding: 1rem;
  border-radius: 8px;
  background: #f8fafc;
  border: 1px dashed #0284c7;
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

.templates-toolbar {
  margin-top: 1.25rem;
  padding-top: 1rem;
  border-top: 1px solid #e5e7eb;
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
  background: #0284c7;
  color: #fff;
  border-radius: 6px;
  font-size: 0.82rem;
  font-weight: 600;
  cursor: pointer;
}

.upload-label:hover {
  background: #0369a1;
}

@media (max-width: 640px) {
  .form-grid,
  .summary-grid,
  .modal-dates-grid {
    grid-template-columns: 1fr !important;
    gap: 0.75rem;
  }

  .imss-upload-banner {
    padding: 0.85rem;
    flex-direction: column;
    align-items: stretch;
    gap: 0.75rem;
    background: var(--surface-toolbar, #f8fafc);
  }

  .imss-upload-banner input[type='file'] {
    max-width: 100%;
    font-size: 0.84rem;
  }

  .templates-toolbar :deep(.btn) {
    flex: 1 1 100%;
    white-space: normal;
    text-align: center;
  }
}
</style>
