<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch as watchValidation } from 'vue'
import { useFormValidation } from '../../../shared/composables/useFormValidation'
import { useRealtimeRefresh } from '../../../shared/composables/useRealtime'
import UiFormErrors from '../../../shared/components/UiFormErrors.vue'
import { problemMessage } from '../../../shared/api/problemDetails'
import { socialDocumentLabel, socialDocumentOrder, socialDocumentPeriod } from '../../../shared/utils/socialServiceDocuments'
import ExpedientChecklist from '../../expedients/components/ExpedientChecklist.vue'
import DocumentReviewDialog from '../../expedients/components/DocumentReviewDialog.vue'
import { http } from '../../../shared/api/http'
import {
  UiAlert,
  UiBadge,
  UiBasePage,
  UiButton,
  UiCard,
  UiDialog,
  UiField,
  UiTable,
} from '../../../shared/components'
import { openPrintableDocument } from '../../../shared/utils/printDocument'
import {
  getDocumentBlob,
  getStudentDocuments,
} from '../../expedients/api/expedientsApi'
import type { ExpedientDocument } from '../../expedients/types/expedients'
import {
  activateSocialService,
  completeSocialService,
  getSocialServiceById,
  getSocialServicePrintData,
  getSocialServices,
  registerSocialServiceAcceptance,
  releaseSocialService,
  reviewSocialServiceApplication,
  sendSocialServiceSchoolAct,
  updateSocialServiceImpact,
} from '../api/socialServiceApi'
import type { SocialServiceDetail, SocialServiceListItem } from '../types/socialService'

interface SocialServiceRow extends SocialServiceListItem {
  actions?: string
}

const items = ref<SocialServiceRow[]>([])
const allItemsCache = ref<SocialServiceRow[]>([])
const isLoading = ref(true)
const errorMessage = ref('')
const successMessage = ref('')

// Paginación estandarizada (10 por página)
const currentPage = ref(1)
const pageSize = 10
const totalItems = ref(0)
const totalPages = ref(1)

// Filtros y Autocomplete con Debounce
const searchQuery = ref('')
const statusFilter = ref('')
const isAutocompleteOpen = ref(false)
let debounceTimer: ReturnType<typeof setTimeout> | null = null

// KPIs animados compactos (sin parpadeo ni reinicio a 0)
const animatedKpis = reactive({
  total: 0,
  requested: 0,
  active: 0,
  released: 0,
})
let kpiRafId = 0

function updateAnimatedKpis(): void {
  const source = allItemsCache.value.length > 0 ? allItemsCache.value : items.value
  const targets = {
    total: allItemsCache.value.length || totalItems.value || source.length,
    requested: source.filter((s) => s.status === 'requested').length,
    active: source.filter((s) => s.status === 'active' || s.status === 'approved' || s.status === 'accepted').length,
    released: source.filter((s) => s.status === 'released').length,
  }

  if (kpiRafId) {
    cancelAnimationFrame(kpiRafId)
    kpiRafId = 0
  }

  const prefersReduced =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches

  if (
    prefersReduced ||
    (animatedKpis.total === targets.total &&
      animatedKpis.requested === targets.requested &&
      animatedKpis.active === targets.active &&
      animatedKpis.released === targets.released)
  ) {
    Object.assign(animatedKpis, targets)
    return
  }

  const startValues = { ...animatedKpis }
  const duration = 360
  const start = performance.now()
  function tick(now: number): void {
    const progress = Math.min((now - start) / duration, 1)
    const ease = 1 - Math.pow(1 - progress, 3)
    animatedKpis.total = Math.round(startValues.total + (targets.total - startValues.total) * ease)
    animatedKpis.requested = Math.round(startValues.requested + (targets.requested - startValues.requested) * ease)
    animatedKpis.active = Math.round(startValues.active + (targets.active - startValues.active) * ease)
    animatedKpis.released = Math.round(startValues.released + (targets.released - startValues.released) * ease)
    if (progress < 1) {
      kpiRafId = requestAnimationFrame(tick)
    } else {
      kpiRafId = 0
    }
  }
  kpiRafId = requestAnimationFrame(tick)
}

const autocompleteSuggestions = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()
  if (!query) return []
  const map = new Map<number, SocialServiceRow>()
  for (const s of allItemsCache.value) map.set(s.id, s)
  for (const s of items.value) map.set(s.id, s)
  const pool = Array.from(map.values())

  return pool
    .filter(
      (s) =>
        s.studentControlNumber.toLowerCase().includes(query) ||
        s.studentFullName.toLowerCase().includes(query) ||
        s.careerCode.toLowerCase().includes(query) ||
        (s.companyName && s.companyName.toLowerCase().includes(query)),
    )
    .slice(0, 7)
})

function handleSearchInput(): void {
  isAutocompleteOpen.value = searchQuery.value.trim().length > 0
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    currentPage.value = 1
    void loadData()
  }, 260)
}

function handleSearchFocus(): void {
  if (searchQuery.value.trim().length > 0) {
    isAutocompleteOpen.value = true
  }
}

function handleSearchBlur(): void {
  setTimeout(() => {
    isAutocompleteOpen.value = false
  }, 200)
}

function selectSuggestion(suggestion: SocialServiceListItem): void {
  if (debounceTimer) clearTimeout(debounceTimer)
  searchQuery.value = suggestion.studentFullName
  isAutocompleteOpen.value = false
  currentPage.value = 1
  void loadData()
}

function clearSearch(): void {
  if (debounceTimer) clearTimeout(debounceTimer)
  searchQuery.value = ''
  isAutocompleteOpen.value = false
  currentPage.value = 1
  void loadData()
}

function resetFilters(): void {
  if (debounceTimer) clearTimeout(debounceTimer)
  searchQuery.value = ''
  statusFilter.value = ''
  isAutocompleteOpen.value = false
  currentPage.value = 1
  void loadData()
}

function toggleKpiStatus(targetStatus: string): void {
  statusFilter.value = statusFilter.value === targetStatus ? '' : targetStatus
  currentPage.value = 1
  void loadData()
}

// Modal de Gestión de Trámite
const isDetailOpen = ref(false)
const detailTab = ref<'uploaded' | 'checklist'>('uploaded')
const selectedDetail = ref<SocialServiceDetail | null>(null)
const studentDocs = ref<ExpedientDocument[]>([])
const isActionRunning = ref(false)

// Modal de Observaciones y Dictamen de Apertura (Anexo XVIII) + Notas del Expediente
const isObservationsModalOpen = ref(false)
const observationsModalMode = ref<'application' | 'general'>('general')
const reviewObservations = ref('Cumple con requisitos y vigencia IMSS.')
const generalObservations = ref('')
const isSavingObservations = ref(false)
const applicationDecision = ref<'approve' | 'reject'>('approve')

const observationsValidation = useFormValidation(() => {
  const errors: Record<string, string[]> = {}
  if (observationsModalMode.value === 'application') {
    const trimmed = reviewObservations.value.trim()
    if (applicationDecision.value === 'reject' && !trimmed) {
      errors.rejectionReason = ['Indica el motivo de observación o rechazo de la solicitud.']
    } else if (trimmed.length > 1000) {
      errors.rejectionReason = ['Las observaciones no pueden exceder 1000 caracteres.']
    }
  } else {
    if (generalObservations.value.trim().length > 2000) {
      errors.observations = ['Las observaciones del expediente admiten máximo 2000 caracteres.']
    }
  }
  return errors
})

// Modal de Dictamen / Observación de Documento individual
const isDocReviewOpen = ref(false)
const selectedDocForReview = ref<ExpedientDocument | null>(null)

function openObservationsModal(mode: 'application' | 'general'): void {
  if (!selectedDetail.value) return
  observationsModalMode.value = mode
  observationsValidation.reset()
  if (mode === 'application') {
    reviewObservations.value =
      selectedDetail.value.application?.officeObservations ||
      selectedDetail.value.application?.rejectionReason ||
      'Cumple con requisitos y vigencia IMSS.'
  } else {
    generalObservations.value = selectedDetail.value.observations || ''
  }
  isObservationsModalOpen.value = true
}

function openDocReviewModal(doc: ExpedientDocument): void {
  selectedDocForReview.value = doc
  isDocReviewOpen.value = true
}

async function handleSaveGeneralObservations(): Promise<void> {
  if (!selectedDetail.value || isSavingObservations.value) return
  if (!observationsValidation.validate()) return
  isSavingObservations.value = true
  errorMessage.value = ''
  try {
    await updateSocialServiceImpact(selectedDetail.value.id, {
      beneficiariesCount: selectedDetail.value.beneficiariesCount ?? 0,
      hasInclusionActivities: Boolean(selectedDetail.value.hasInclusionActivities),
      targetCommunity: selectedDetail.value.targetCommunity ?? null,
      observations: generalObservations.value.trim() || null,
    })
    successMessage.value = 'Observaciones del expediente guardadas correctamente.'
    isObservationsModalOpen.value = false
    await refreshCurrentDetail()
  } catch (err: any) {
    observationsValidation.apply(err, 'No se pudieron guardar las observaciones.')
  } finally {
    isSavingObservations.value = false
  }
}

const columns: { key: keyof SocialServiceRow & string; label: string }[] = [
  { key: 'studentControlNumber', label: 'Control' },
  { key: 'studentFullName', label: 'Alumno' },
  { key: 'careerCode', label: 'Carrera' },
  { key: 'companyName', label: 'Dependencia' },
  { key: 'modality', label: 'Modalidad' },
  { key: 'documentProgressPercentage', label: 'Avance Doc.' },
  { key: 'status', label: 'Estatus' },
  { key: 'actions', label: 'Acciones' },
]

const statusLabels: Record<string, string> = {
  requested: 'Solicitud Recibida',
  approved: 'Solicitud aprobada',
  accepted: 'Carta Aceptación OK',
  active: 'En Curso (Activo)',
  completed_pending_review: 'Listo para Liberar',
  released: 'Liberado (SSC)',
  cancelled: 'Cancelado',
  non_compliant: 'PNC',
}

function formatStatus(status: string): string {
  return statusLabels[status] || status
}

async function loadData(): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await getSocialServices({
      page: currentPage.value,
      pageSize,
      status: statusFilter.value || undefined,
      search: searchQuery.value.trim() || undefined,
    })
    items.value = res.items || []
    totalItems.value = res.totalItems ?? items.value.length
    totalPages.value = Math.max(1, res.totalPages ?? Math.ceil(totalItems.value / pageSize))

    if (allItemsCache.value.length === 0 && items.value.length > 0) {
      allItemsCache.value = [...items.value]
    } else if (!searchQuery.value && !statusFilter.value && items.value.length > 0) {
      allItemsCache.value = [...items.value]
    }
    updateAnimatedKpis()
  } catch (err: any) {
    errorMessage.value =
      problemMessage(err, 'Error al cargar trámites de Servicio Social.')
  } finally {
    isLoading.value = false
  }
}

async function openDetailModal(row: SocialServiceListItem): Promise<void> {
  errorMessage.value = ''
  try {
    const [detail, docs] = await Promise.all([
      getSocialServiceById(row.id),
      getStudentDocuments(row.studentId, { tramiteType: 'social_service', tramiteId: row.id }).catch(
        () => [],
      ),
    ])
    selectedDetail.value = detail
    studentDocs.value = docs
    detailTab.value = docs.length > 0 ? 'uploaded' : 'checklist'
    reviewObservations.value =
      detail.application?.officeObservations ||
      detail.application?.rejectionReason ||
      'Cumple con requisitos y vigencia IMSS.'
    generalObservations.value = detail.observations || ''
    isDetailOpen.value = true
  } catch (err: any) {
    errorMessage.value = problemMessage(err, 'No se pudo cargar el detalle del trámite.')
  }
}

async function refreshCurrentDetail(): Promise<void> {
  if (!selectedDetail.value) return
  const id = selectedDetail.value.id
  const studentId = selectedDetail.value.student.id
  const [detail, docs] = await Promise.all([
    getSocialServiceById(id),
    getStudentDocuments(studentId, { tramiteType: 'social_service', tramiteId: id }).catch(
      () => [],
    ),
  ])
  selectedDetail.value = detail
  studentDocs.value = docs
  await loadData()
}

async function handleApproveApplication(isAccepted: boolean): Promise<void> {
  if (!selectedDetail.value) return
  if (isActionRunning.value) return
  applicationDecision.value = isAccepted ? 'approve' : 'reject'
  observationsModalMode.value = 'application'
  if (!observationsValidation.validate()) return
  actionValidation.reset()
  isActionRunning.value = true
  errorMessage.value = ''
  try {
    await reviewSocialServiceApplication(selectedDetail.value.id, {
      isAccepted,
      rejectionReason: isAccepted ? null : reviewObservations.value.trim(),
      officeObservations: reviewObservations.value.trim(),
    })
    successMessage.value = isAccepted
      ? 'Solicitud aprobada. La Carta de Presentación firmada se carga y revisa en el expediente.'
      : 'Solicitud rechazada con observaciones.'
    isObservationsModalOpen.value = false
    await refreshCurrentDetail()
  } catch (err: any) {
    observationsValidation.apply(err, 'No se pudo dictaminar la solicitud.')
    actionValidation.apply(err, 'No se pudo completar la operación.')
    errorMessage.value = problemMessage(err, 'No se pudo dictaminar la solicitud.')
  } finally {
    isActionRunning.value = false
  }
}

async function handleDocReviewed(reviewedDoc: ExpedientDocument): Promise<void> {
  successMessage.value = `Documento ${reviewedDoc.documentCode} ${reviewedDoc.status === 'approved' ? 'aprobado' : 'observado'}.`
  await refreshCurrentDetail()
}

async function handleViewDocPdf(docId: number): Promise<void> {
  try {
    const blob = await getDocumentBlob(docId)
    const url = window.URL.createObjectURL(blob)
    window.open(url, '_blank')
  } catch {
    errorMessage.value = 'Archivo PDF de demostración (sin binario en disco local).'
  }
}

const isActDownloading = ref(false)
async function downloadIndividualAct(): Promise<void> {
  if (!selectedDetail.value || isActDownloading.value) return
  isActDownloading.value = true
  actionValidation.reset()
  try {
    const id = selectedDetail.value.id
    const response = await http.get(`/social-services/${id}/pdf/individual_act`, { responseType: 'blob' })
    const url = URL.createObjectURL(response.data)
    const link = document.createElement('a')
    link.href = url
    link.download = `SS_${id}_acta_individual.pdf`
    link.click()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch (err: any) {
    if (err.response?.data instanceof Blob) {
      try { err.response.data = JSON.parse(await err.response.data.text()) } catch { /* Preserve transport error. */ }
    }
    actionValidation.apply(err, 'No se pudo descargar el Acta Individual.')
    errorMessage.value = problemMessage(err, 'No se pudo descargar el Acta Individual.')
  } finally {
    isActDownloading.value = false
  }
}

async function handlePrint(docKey: string): Promise<void> {
  if (!selectedDetail.value) return
  try {
    if (docKey !== 'release_certificate') {
      const response = await http.get(`/social-services/${selectedDetail.value.id}/pdf/${docKey}`, { responseType: 'blob' })
      const url = URL.createObjectURL(response.data)
      const link = document.createElement('a')
      link.href = url
      link.download = `${docKey}.pdf`
      link.click()
      URL.revokeObjectURL(url)
      return
    }
    const data = await getSocialServicePrintData(selectedDetail.value.id, docKey)
    openPrintableDocument(data)
  } catch (err: any) {
    actionValidation.apply(err, 'No se pudo completar la operación.')
    errorMessage.value = problemMessage(err, 'Plantilla aún no disponible.')
  }
}

async function handleAdvanceAcceptance(): Promise<void> {
  if (!selectedDetail.value) return
  if (isActionRunning.value) return
  actionValidation.reset()
  isActionRunning.value = true
  try {
    await registerSocialServiceAcceptance(
      selectedDetail.value.id,
      new Date().toISOString().slice(0, 10),
    )
    successMessage.value = 'Carta de Aceptación registrada en el trámite.'
    await refreshCurrentDetail()
  } catch (err: any) {
    actionValidation.apply(err, 'No se pudo completar la operación.')
    errorMessage.value = problemMessage(err, 'Verifica que el documento CA esté aprobado.')
  } finally {
    isActionRunning.value = false
  }
}

async function handleActivate(): Promise<void> {
  if (!selectedDetail.value) return
  if (isActionRunning.value) return
  actionValidation.reset()
  isActionRunning.value = true
  try {
    await activateSocialService(selectedDetail.value.id)
    successMessage.value = 'Servicio Social activado formalmente.'
    await refreshCurrentDetail()
  } catch (err: any) {
    actionValidation.apply(err, 'No se pudo completar la operación.')
    errorMessage.value = problemMessage(err, 'Verifica que la Carta Compromiso (CC) esté aprobada.')
  } finally {
    isActionRunning.value = false
  }
}

async function handleComplete(): Promise<void> {
  if (!selectedDetail.value) return
  if (isActionRunning.value) return
  actionValidation.reset()
  isActionRunning.value = true
  try {
    await completeSocialService(selectedDetail.value.id, new Date().toISOString().slice(0, 10))
    successMessage.value = 'Documentación final validada. Trámite listo para liberar.'
    await refreshCurrentDetail()
  } catch (err: any) {
    actionValidation.apply(err, 'No se pudo completar la operación.')
    errorMessage.value =
      problemMessage(err, 'Verifica que R1, R2, R3 y CT estén aprobados.')
  } finally {
    isActionRunning.value = false
  }
}

async function handleRelease(): Promise<void> {
  if (!selectedDetail.value) return
  if (isActionRunning.value) return
  actionValidation.reset()
  isActionRunning.value = true
  try {
    await releaseSocialService(selectedDetail.value.id, {
      finalGradeLevel: 'excellent',
      finalScore: 100,
      originalTerminationLetterReceived: true,
    })
    successMessage.value = 'Constancia de Terminación (SSC) emitida exitosamente.'
    await refreshCurrentDetail()
  } catch (err: any) {
    actionValidation.apply(err, 'No se pudo completar la operación.')
    errorMessage.value =
      problemMessage(err, 'Verifica que todos los documentos obligatorios estén aprobados.')
  } finally {
    isActionRunning.value = false
  }
}

async function handleSendSchoolAct(): Promise<void> {
  if (!selectedDetail.value) return
  if (isActionRunning.value) return
  actionValidation.reset()
  isActionRunning.value = true
  try {
    await sendSocialServiceSchoolAct(selectedDetail.value.id)
    successMessage.value = 'Acta turnada a Servicios Escolares con folio DGTV.'
    await refreshCurrentDetail()
  } catch (err: any) {
    actionValidation.apply(err, 'No se pudo completar la operación.')
    errorMessage.value = problemMessage(err, 'No se pudo generar el acta.')
  } finally {
    isActionRunning.value = false
  }
}

onBeforeUnmount(() => {
  if (debounceTimer) clearTimeout(debounceTimer)
})

onMounted(() => {
  void loadData()
})

useRealtimeRefresh(
  (evt) => evt.tramiteType === 'social_service' || evt.tramiteType === 'general',
  () => (isDetailOpen.value && selectedDetail.value ? refreshCurrentDetail() : loadData()),
)

const actionValidation = useFormValidation()
watchValidation(() => [isDetailOpen.value, selectedDetail.value?.id], () => actionValidation.reset())
watchValidation(isObservationsModalOpen, () => observationsValidation.reset())
</script>

<template>
  <UiBasePage :fluid="true" class="fullscreen-page">
    <!-- Encabezado Ejecutivo Compacto -->
    <header class="exec-header">
      <div class="exec-header__left">
        <div class="exec-eyebrow">
          <span class="exec-badge">DEPARTAMENTO DE GESTIÓN TECNOLÓGICA Y VINCULACIÓN</span>
          <span class="exec-period">· Servicio Social (480 h)</span>
        </div>
        <h1 class="exec-title">Gestión de Servicio Social (Vinculación)</h1>
        <p class="exec-subtitle">
          Dictamen de solicitudes Anexo XVIII, revisión de Cartas de Presentación firmadas, validación de expediente documental y liberación
        </p>
      </div>

      <div class="exec-kpis">
        <button
          type="button"
          class="mini-kpi mini-kpi--total"
          :class="{ 'mini-kpi--active': statusFilter === '' }"
          @click="resetFilters"
        >
          <span class="mini-kpi__label">Total Padrón</span>
          <strong class="mini-kpi__value">{{ animatedKpis.total }}</strong>
        </button>
        <button
          type="button"
          class="mini-kpi mini-kpi--gold"
          :class="{ 'mini-kpi--active': statusFilter === 'requested' }"
          @click="toggleKpiStatus('requested')"
        >
          <span class="mini-kpi__label">Por Dictaminar</span>
          <strong class="mini-kpi__value">{{ animatedKpis.requested }}</strong>
        </button>
        <button
          type="button"
          class="mini-kpi mini-kpi--navy"
          :class="{ 'mini-kpi--active': statusFilter === 'active' }"
          @click="toggleKpiStatus('active')"
        >
          <span class="mini-kpi__label">En Curso</span>
          <strong class="mini-kpi__value">{{ animatedKpis.active }}</strong>
        </button>
        <button
          type="button"
          class="mini-kpi mini-kpi--emerald"
          :class="{ 'mini-kpi--active': statusFilter === 'released' }"
          @click="toggleKpiStatus('released')"
        >
          <span class="mini-kpi__label">Liberados</span>
          <strong class="mini-kpi__value">{{ animatedKpis.released }}</strong>
        </button>
      </div>
    </header>

    <UiAlert v-if="errorMessage && !(actionValidation.hasErrors.value || observationsValidation.hasErrors.value)" tone="danger" class="compact-alert">
      {{ errorMessage }}
    </UiAlert>
    <UiAlert v-if="successMessage" tone="success" class="compact-alert">
      {{ successMessage }}
    </UiAlert>

    <UiCard class="workspace-card">
      <div class="filters-bar">
        <div class="search-box">
          <label class="filter-label" for="ss-search-input">Buscar por Matrícula, Alumno, Carrera o Dependencia</label>
          <div class="autocomplete-container">
            <div class="input-wrapper">
              <svg class="search-icon" viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" stroke-width="2" fill="none" aria-hidden="true">
                <circle cx="11" cy="11" r="8" />
                <line x1="21" y1="21" x2="16.65" y2="16.65" />
              </svg>
              <input
                id="ss-search-input"
                v-model="searchQuery"
                type="text"
                class="modern-input"
                placeholder="Ej. I22050114, Mariana, IINF o Presidencia..."
                autocomplete="off"
                @input="handleSearchInput"
                @focus="handleSearchFocus"
                @blur="handleSearchBlur"
                @keyup.enter="isAutocompleteOpen = false; currentPage = 1; loadData()"
              />
              <button
                v-if="searchQuery"
                type="button"
                class="clear-search-btn"
                aria-label="Limpiar búsqueda"
                @click="clearSearch"
              >
                ✕
              </button>
            </div>

            <!-- Sugerencias Flotantes Autocomplete -->
            <div v-if="isAutocompleteOpen && autocompleteSuggestions.length > 0" class="suggestions-dropdown" role="listbox">
              <div
                v-for="item in autocompleteSuggestions"
                :key="item.id"
                class="suggestion-item"
                role="option"
                @mousedown.prevent="selectSuggestion(item)"
              >
                <div class="suggestion-left">
                  <span class="suggestion-control">{{ item.studentControlNumber }}</span>
                  <span class="suggestion-name">{{ item.studentFullName }}</span>
                </div>
                <div class="suggestion-right">
                  <span class="suggestion-career">{{ item.careerCode }}</span>
                  <span class="suggestion-progress">{{ item.documentProgressPercentage }}%</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="select-box">
          <label class="filter-label" for="ss-status-select">Filtrar por Estatus</label>
          <div class="select-wrapper">
            <select
              id="ss-status-select"
              v-model="statusFilter"
              class="modern-select"
              @change="currentPage = 1; loadData()"
            >
              <option value="">Todos los estatus</option>
              <option value="requested">Solicitud Recibida (Por Dictaminar)</option>
              <option value="approved">Solicitud aprobada</option>
              <option value="accepted">Carta Aceptación Validada</option>
              <option value="active">Activo (Subiendo Reportes)</option>
              <option value="completed_pending_review">Listo para Liberar</option>
              <option value="released">Liberado (100%)</option>
            </select>
            <svg class="select-chevron" viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none" aria-hidden="true">
              <polyline points="6 9 12 15 18 9" />
            </svg>
          </div>
        </div>

        <div class="filter-actions">
          <UiButton
            v-if="searchQuery || statusFilter"
            variant="secondary"
            size="sm"
            class="reset-btn"
            title="Restablecer filtros"
            @click="resetFilters"
          >
            Limpiar
          </UiButton>
          <UiButton variant="primary" size="sm" class="filter-btn" :disabled="isLoading" @click="currentPage = 1; loadData()">
            {{ isLoading ? 'Buscando...' : 'Buscar' }}
          </UiButton>
        </div>
      </div>

      <UiTable class="compact-table" label="Trámites de Servicio Social" :columns="columns" :rows="items">
        <template #cell-studentControlNumber="{ row }">
          <span class="control-code">{{ row.studentControlNumber }}</span>
        </template>

        <template #cell-studentFullName="{ row }">
          <span class="student-name-cell">{{ row.studentFullName }}</span>
        </template>

        <template #cell-companyName="{ row }">
          <span class="company-name-cell" :title="row.companyName">{{ row.companyName }}</span>
        </template>

        <template #cell-modality="{ row }">
          <UiBadge :tone="row.modality === 'internal' ? 'info' : 'purple'">
            {{ row.modality === 'internal' ? 'Local (TecNM)' : 'Externo' }}
          </UiBadge>
        </template>

        <template #cell-documentProgressPercentage="{ row }">
          <div class="progress-cell">
            <div class="progress-track">
              <div
                class="progress-fill"
                :style="{ width: `${row.documentProgressPercentage}%` }"
              />
            </div>
            <span class="progress-text">{{ row.documentProgressPercentage }}%</span>
          </div>
        </template>

        <template #cell-status="{ row }">
          <UiBadge :tone="row.status === 'released' ? 'success' : row.status === 'requested' ? 'info' : 'warning'">
            {{ formatStatus(row.status) }}
          </UiBadge>
        </template>

        <template #cell-actions="{ row }">
          <UiButton variant="primary" size="sm" @click="openDetailModal(row)">
            Gestionar Expediente
          </UiButton>
        </template>
      </UiTable>

      <!-- Barra de Paginación Estandarizada (10 por página) -->
      <nav class="pagination-bar" aria-label="Paginación de trámites de Servicio Social">
        <span class="pagination-info">
          Mostrando <strong>{{ items.length }}</strong> de <strong>{{ totalItems }}</strong> trámites · Página <strong>{{ currentPage }}</strong> de <strong>{{ totalPages }}</strong>
        </span>
        <div class="pagination-controls">
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="isLoading || currentPage <= 1"
            @click="currentPage--; loadData()"
          >
            Anterior
          </UiButton>
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="isLoading || currentPage >= totalPages"
            @click="currentPage++; loadData()"
          >
            Siguiente
          </UiButton>
        </div>
      </nav>
    </UiCard>

    <!-- ================================================================== -->
    <!-- MODAL 1: GESTIÓN INTEGRAL DEL TRÁMITE DE SERVICIO SOCIAL           -->
    <!-- ================================================================== -->
    <UiDialog
      v-model="isDetailOpen"
      size="full"
      :title="
        selectedDetail
          ? `Expediente SS · ${selectedDetail.student.controlNumber} — ${selectedDetail.student.fullName}`
          : 'Detalle de Servicio Social'
      "
    >
      <div v-if="selectedDetail" class="detail-modal-stack">
        <UiFormErrors :validation="actionValidation" />

        <!-- Resumen Ejecutivo de Folios, Estatus y Avance -->
        <div class="summary-grid">
          <div class="summary-item">
            <small>Estatus Actual</small>
            <div class="summary-badge-row">
              <UiBadge
                :tone="
                  selectedDetail.status === 'released'
                    ? 'success'
                    : selectedDetail.status === 'requested'
                      ? 'info'
                      : 'warning'
                "
              >
                {{ formatStatus(selectedDetail.status) }}
              </UiBadge>
              <span class="summary-pct">{{ selectedDetail.documentProgressPercentage }}% avance</span>
            </div>
          </div>
          <div class="summary-item">
            <small>Dependencia Receptora</small>
            <strong>{{ selectedDetail.company.name }}</strong>
          </div>
          <div class="summary-item">
            <small>Folio Carta Presentación</small>
            <strong class="mono-folio">{{ selectedDetail.presentationLetterFolio || 'Pendiente' }}</strong>
          </div>
          <div class="summary-item">
            <small>Folio Constancia Terminación</small>
            <strong class="mono-folio text-emerald">
              {{ selectedDetail.releaseCertificateFolio || 'Pendiente' }}
            </strong>
          </div>
        </div>

        <!-- Barra Unificada de Formatos, Dictamen y Pestañas de Vista -->
        <div class="detail-toolbar">
          <div class="detail-toolbar__group">
            <UiButton variant="secondary" size="sm" @click="handlePrint('application')">
              Ver {{ socialDocumentLabel('SOL') }}
            </UiButton>
            <UiButton
              v-if="selectedDetail.presentationLetterFolio"
              variant="secondary"
              size="sm"
              @click="handlePrint('presentation_letter')"
            >
              Imprimir Carta Presentación (CP)
            </UiButton>
            <UiButton
              v-if="selectedDetail.status === 'released'"
              variant="secondary"
              size="sm"
              @click="handlePrint('release_certificate')"
            >
              Imprimir Constancia Liberación (SSC)
            </UiButton>
            <UiButton
              v-if="selectedDetail.status === 'released'"
              variant="secondary"
              size="sm"
              :disabled="isActDownloading"
              @click="downloadIndividualAct"
            >
              {{ isActDownloading ? 'Descargando Acta…' : 'Descargar Acta Individual' }}
            </UiButton>
            <UiButton
              v-if="selectedDetail.status === 'requested'"
              variant="primary"
              size="sm"
              @click="openObservationsModal('application')"
            >
              Dictaminar Solicitud (Anexo XVIII)
            </UiButton>
            <UiButton
              variant="secondary"
              size="sm"
              @click="openObservationsModal('general')"
            >
              Observaciones del Expediente
            </UiButton>
          </div>

          <div class="modal-tabs" role="tablist" aria-label="Secciones del expediente">
            <button
              type="button"
              class="modal-tab"
              :class="{ 'modal-tab--active': detailTab === 'uploaded' }"
              @click="detailTab = 'uploaded'"
            >
              PDFs Subidos ({{ studentDocs.length }})
            </button>
            <button
              type="button"
              class="modal-tab"
              :class="{ 'modal-tab--active': detailTab === 'checklist' }"
              @click="detailTab = 'checklist'"
            >
              Checklist Normativo ({{ selectedDetail.documentRequirements.length }})
            </button>
          </div>
        </div>

        <!-- Resumen de Observaciones vigentes (si existen) -->
        <div
          v-if="
            selectedDetail.observations ||
            selectedDetail.application?.officeObservations ||
            selectedDetail.application?.rejectionReason
          "
          class="observations-banner"
        >
          <div class="observations-banner__text">
            <strong>Observaciones registradas:</strong>
            <span v-if="selectedDetail.observations">{{ selectedDetail.observations }}</span>
            <span v-else>
              {{
                selectedDetail.application?.officeObservations ||
                selectedDetail.application?.rejectionReason
              }}
            </span>
          </div>
          <UiButton
            variant="secondary"
            size="sm"
            @click="
              openObservationsModal(
                selectedDetail.status === 'requested' ? 'application' : 'general',
              )
            "
          >
            Editar observaciones
          </UiButton>
        </div>

        <!-- Pestaña 1: PDFs Subidos por el Alumno para Dictamen -->
        <div v-if="detailTab === 'uploaded'" class="modal-tab-panel">
          <div v-if="studentDocs.length === 0" class="empty-docs-note">
            El alumno aún no ha subido archivos PDF a este trámite.
          </div>
          <div v-else class="uploaded-docs-grid">
            <article
              v-for="doc in [...studentDocs].sort(
                (a, b) => socialDocumentOrder(a.documentCode) - socialDocumentOrder(b.documentCode),
              )"
              :key="doc.id"
              class="uploaded-doc-card"
            >
              <div class="uploaded-doc-top">
                <div class="uploaded-doc-title">
                  <span class="doc-code-pill">{{ doc.documentCode }}</span>
                  <strong>{{ socialDocumentLabel(doc.documentCode) }}</strong>
                </div>
                <UiBadge
                  :tone="
                    doc.status === 'approved'
                      ? 'success'
                      : doc.status === 'rejected'
                        ? 'danger'
                        : 'warning'
                  "
                >
                  {{
                    doc.status === 'approved'
                      ? 'Aprobado (SI)'
                      : doc.status === 'rejected'
                        ? 'Observado'
                        : 'En Revisión'
                  }}
                </UiBadge>
              </div>

              <div class="uploaded-doc-file" :title="doc.originalFileName">
                {{ doc.originalFileName }}
                <template v-if="socialDocumentPeriod(doc.documentCode)">
                  · {{ socialDocumentPeriod(doc.documentCode) }}
                </template>
              </div>

              <small v-if="doc.rejectionReason" class="doc-rejection-note">
                Observación: {{ doc.rejectionReason }}
              </small>

              <div class="uploaded-doc-actions">
                <UiButton variant="secondary" size="sm" @click="handleViewDocPdf(doc.id)">
                  Ver PDF
                </UiButton>
                <UiButton
                  v-if="doc.status === 'pending'"
                  variant="primary"
                  size="sm"
                  :disabled="isActionRunning"
                  @click="openDocReviewModal(doc)"
                >
                  Dictaminar / Observar
                </UiButton>
                <UiButton
                  v-else
                  variant="secondary"
                  size="sm"
                  @click="openDocReviewModal(doc)"
                >
                  Ver Dictamen
                </UiButton>
              </div>
            </article>
          </div>
        </div>

        <!-- Pestaña 2: Checklist Normativo TecNM en 2 Columnas -->
        <div v-else class="modal-tab-panel">
          <ExpedientChecklist
            :application-accepted="selectedDetail.application?.isAcceptedByOffice"
            :requirements="selectedDetail.documentRequirements"
            :documents="studentDocs"
            tramite-type="social_service"
            :tramite-id="selectedDetail.id"
            :student-id="selectedDetail.student.id"
            :can-edit="false"
            @changed="refreshCurrentDetail"
          />
        </div>
      </div>

      <template #footer>
        <div v-if="selectedDetail" class="modal-footer-actions">
          <div class="stage-footer-group">
            <UiButton
              v-if="selectedDetail.status === 'approved'"
              variant="primary"
              size="sm"
              :disabled="isActionRunning"
              @click="handleAdvanceAcceptance"
            >
              Confirmar Recepción de Carta Aceptación (CA)
            </UiButton>

            <UiButton
              v-if="selectedDetail.status === 'accepted'"
              variant="primary"
              size="sm"
              :disabled="isActionRunning"
              @click="handleActivate"
            >
              Activar Servicio Social (Con CC aprobada)
            </UiButton>

            <UiButton
              v-if="selectedDetail.status === 'active'"
              variant="primary"
              size="sm"
              :disabled="isActionRunning"
              @click="handleComplete"
            >
              Validar Cierre Documental (R1..R3 y CT)
            </UiButton>

            <UiButton
              v-if="
                selectedDetail.status === 'completed_pending_review' ||
                selectedDetail.status === 'active'
              "
              variant="primary"
              size="sm"
              :disabled="isActionRunning"
              @click="handleRelease"
            >
              Emitir Constancia de Liberación (Folio SSC)
            </UiButton>

            <UiButton
              v-if="selectedDetail.status === 'released' && !selectedDetail.schoolServicesActSent"
              variant="secondary"
              size="sm"
              :disabled="isActionRunning"
              @click="handleSendSchoolAct"
            >
              Generar Acta para Servicios Escolares (DGTV)
            </UiButton>
          </div>

          <UiButton variant="secondary" size="sm" @click="isDetailOpen = false">Cerrar</UiButton>
        </div>
      </template>
    </UiDialog>

    <!-- ================================================================== -->
    <!-- MODAL 2: GESTIÓN DE OBSERVACIONES Y DICTAMEN DE SOLICITUD          -->
    <!-- ================================================================== -->
    <UiDialog
      v-model="isObservationsModalOpen"
      size="md"
      :title="
        observationsModalMode === 'application'
          ? 'Dictamen y Observaciones de Apertura (Anexo XVIII)'
          : 'Observaciones del Expediente de Servicio Social'
      "
    >
      <div v-if="selectedDetail" class="observations-modal-body">
        <UiFormErrors :validation="observationsValidation" />

        <div class="observations-student-pill">
          <strong>{{ selectedDetail.student.controlNumber }}</strong> · {{ selectedDetail.student.fullName }}
          <span>({{ selectedDetail.company.name }})</span>
        </div>

        <template v-if="observationsModalMode === 'application'">
          <p class="observations-helper">
            Revisa la Solicitud (Anexo XVIII) y la vigencia IMSS del estudiante. Registra las observaciones de Vinculación para aprobar o rechazar la apertura del trámite.
          </p>

          <div v-if="selectedDetail.application?.reviewedByName" class="previous-review-box">
            <small>Última revisión por {{ selectedDetail.application.reviewedByName }}:</small>
            <p>{{ selectedDetail.application.officeObservations || selectedDetail.application.rejectionReason || 'Sin observaciones previas.' }}</p>
          </div>

          <UiField
            v-model="reviewObservations"
            as="textarea"
            label="Observaciones de Vinculación *"
            placeholder="Describe las observaciones del dictamen o el motivo de rechazo..."
            maxlength="1000"
            v-bind="observationsValidation.field('rejectionReason', 'Observaciones de Vinculación')"
            :disabled="isActionRunning"
          />
        </template>

        <template v-else>
          <p class="observations-helper">
            Consulta, redacta o actualiza las observaciones administrativas de seguimiento del expediente de Servicio Social.
          </p>

          <UiField
            v-model="generalObservations"
            as="textarea"
            label="Observaciones Administrativas del Expediente"
            placeholder="Escribe notas de seguimiento, acuerdos con la dependencia o aclaraciones..."
            maxlength="2000"
            v-bind="observationsValidation.field('observations', 'Observaciones del expediente')"
            :disabled="isSavingObservations"
          />
        </template>
      </div>

      <template #footer>
        <UiButton
          variant="secondary"
          :disabled="isActionRunning || isSavingObservations"
          @click="isObservationsModalOpen = false"
        >
          Cancelar
        </UiButton>

        <template v-if="observationsModalMode === 'application'">
          <UiButton
            variant="danger"
            :disabled="isActionRunning"
            :loading="isActionRunning && applicationDecision === 'reject'"
            @click="handleApproveApplication(false)"
          >
            Rechazar con Observaciones
          </UiButton>
          <UiButton
            variant="primary"
            :disabled="isActionRunning"
            :loading="isActionRunning && applicationDecision === 'approve'"
            @click="handleApproveApplication(true)"
          >
            Aprobar Solicitud
          </UiButton>
        </template>

        <template v-else>
          <UiButton
            variant="primary"
            :disabled="isSavingObservations"
            :loading="isSavingObservations"
            @click="handleSaveGeneralObservations"
          >
            {{ isSavingObservations ? 'Guardando...' : 'Guardar Observaciones' }}
          </UiButton>
        </template>
      </template>
    </UiDialog>

    <!-- ================================================================== -->
    <!-- MODAL 3: DICTAMEN Y OBSERVACIONES DE DOCUMENTO INDIVIDUAL (PDF)    -->
    <!-- ================================================================== -->
    <DocumentReviewDialog
      v-model="isDocReviewOpen"
      :document="selectedDocForReview"
      :student-name="selectedDetail?.student.fullName"
      @reviewed="handleDocReviewed"
    />
  </UiBasePage>
</template>

<style scoped>
.fullscreen-page {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}

.exec-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.85rem;
  margin-bottom: 0.65rem;
  flex-wrap: wrap;
  flex-shrink: 0;
}

.exec-header__left {
  flex: 1;
  min-width: 260px;
}

.exec-eyebrow {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  margin-bottom: 0.15rem;
  flex-wrap: wrap;
}

.exec-badge {
  font-size: 0.65rem;
  font-weight: 800;
  letter-spacing: 0.06em;
  padding: 0.14rem 0.5rem;
  border-radius: 5px;
  background: var(--color-accent-light);
  color: var(--color-accent);
  border: 1px solid color-mix(in srgb, var(--color-accent) 40%, transparent);
}

.exec-period {
  font-size: 0.74rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.exec-title {
  font-size: 1.22rem;
  font-weight: 800;
  margin: 0;
  color: var(--color-text-primary);
  letter-spacing: -0.015em;
  line-height: 1.2;
}

.exec-subtitle {
  color: var(--color-text-secondary);
  margin: 0.12rem 0 0;
  font-size: 0.79rem;
  line-height: 1.3;
}

.exec-kpis {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

/* KPIs institucionales con fondo neutro y borde dorado al estar activo */
.mini-kpi {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  padding: 0.42rem 0.85rem;
  min-width: 112px;
  border-radius: 8px;
  border: 1px solid var(--color-border-medium);
  background: var(--color-surface-white);
  color: var(--color-text-primary);
  cursor: pointer;
  transition: background-color 180ms ease, border-color 180ms ease, box-shadow 180ms ease, transform 180ms cubic-bezier(0.22, 1, 0.36, 1);
}

.mini-kpi:hover {
  background: var(--secondary-hover);
  border-color: color-mix(in srgb, var(--color-accent) 70%, var(--color-border-medium));
  transform: translateY(-2px);
}

.mini-kpi:hover .mini-kpi__label,
.mini-kpi--active .mini-kpi__label {
  color: var(--color-text-primary);
}

.mini-kpi__label {
  font-size: 0.65rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
  transition: color 180ms ease;
}

.mini-kpi__value {
  font-size: 1.18rem;
  font-weight: 800;
  color: var(--color-text-primary);
  line-height: 1.15;
}

.mini-kpi--gold .mini-kpi__value {
  color: var(--color-warning);
}

.mini-kpi--navy .mini-kpi__value {
  color: var(--color-info);
}

.mini-kpi--emerald .mini-kpi__value {
  color: var(--color-success);
}

.mini-kpi--active {
  background: var(--kpi-active-bg);
  border-color: var(--color-accent) !important;
  box-shadow: 0 0 0 1px var(--color-accent) !important;
}

.compact-alert {
  margin-bottom: 0.5rem;
  padding: 0.55rem 0.85rem;
  flex-shrink: 0;
}

/* Tarjeta principal ajustada para pantalla completa sin scroll externo */
.workspace-card {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  padding: 12px 16px !important;
  border: 1px solid var(--color-border);
  background: var(--color-surface-white);
}

/* Barra de filtros con superficie institucional contrastada */
.filters-bar {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
  align-items: flex-end;
  margin-bottom: 0.6rem;
  padding: 0.55rem 0.75rem;
  border-radius: 8px;
  background: var(--surface-toolbar);
  border: 1px solid var(--color-border);
  flex-shrink: 0;
}

.search-box {
  flex: 1.7;
  min-width: 240px;
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.select-box {
  flex: 1.1;
  min-width: 200px;
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.filter-label {
  font-size: 0.73rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.input-wrapper,
.select-wrapper {
  position: relative;
  display: flex;
  align-items: center;
  width: 100%;
}

.search-icon {
  position: absolute;
  left: 0.75rem;
  color: var(--color-text-secondary);
  pointer-events: none;
  z-index: 2;
}

input.modern-input {
  width: 100%;
  min-height: 34px;
  height: 34px;
  border-radius: 7px;
  border: 1px solid var(--input-border) !important;
  background: var(--input-bg) !important;
  padding: 0 2rem 0 2.35rem !important;
  font-size: 0.81rem;
  color: var(--color-text-primary) !important;
  outline: none;
  box-sizing: border-box;
  transition: border-color 160ms ease, background-color 160ms ease, box-shadow 160ms ease;
}

input.modern-input:hover,
select.modern-select:hover {
  border-color: var(--color-gray-400) !important;
}

input.modern-input:focus {
  border-color: var(--color-border-focus) !important;
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-border-focus) 22%, transparent);
}

select.modern-select {
  width: 100%;
  min-height: 34px;
  height: 34px;
  border-radius: 7px;
  border: 1px solid var(--input-border) !important;
  background: var(--input-bg) !important;
  padding: 0 2rem 0 0.75rem !important;
  font-size: 0.81rem;
  color: var(--color-text-primary) !important;
  outline: none;
  appearance: none;
  cursor: pointer;
  box-sizing: border-box;
  transition: border-color 160ms ease, background-color 160ms ease, box-shadow 160ms ease;
}

select.modern-select:focus {
  border-color: var(--color-border-focus) !important;
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-border-focus) 22%, transparent);
}

.select-chevron {
  position: absolute;
  right: 0.75rem;
  color: var(--color-text-secondary);
  pointer-events: none;
}

.filter-actions {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  height: 34px;
}

.reset-btn,
.filter-btn {
  min-height: 34px !important;
  height: 34px !important;
  padding: 0 0.9rem !important;
  font-size: 0.8rem !important;
}

.autocomplete-container {
  position: relative;
  width: 100%;
}

.clear-search-btn {
  position: absolute;
  right: 0.5rem;
  background: transparent;
  border: none;
  color: var(--color-text-muted);
  cursor: pointer;
  font-size: 0.78rem;
  padding: 0.2rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
}

.clear-search-btn:hover {
  color: var(--color-text-primary);
  background: var(--secondary-hover);
}

.suggestions-dropdown {
  position: absolute;
  top: calc(100% + 4px);
  left: 0;
  right: 0;
  background: var(--floating-bg);
  border: 1px solid var(--floating-border);
  border-radius: 8px;
  box-shadow: var(--floating-shadow);
  z-index: 999;
  max-height: 220px;
  overflow-y: auto;
  box-sizing: border-box;
}

.suggestion-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.45rem 0.75rem;
  cursor: pointer;
  border-bottom: 1px solid var(--color-border-light);
  transition: background-color 140ms ease;
}

.suggestion-item:last-child {
  border-bottom: none;
}

.suggestion-item:hover {
  background: var(--color-primary-subtle);
}

.suggestion-left {
  display: flex;
  flex-direction: column;
  gap: 0.08rem;
}

.suggestion-control {
  font-family: monospace;
  font-weight: 700;
  font-size: 0.78rem;
  color: var(--color-info-text);
}

.suggestion-name {
  font-size: 0.8rem;
  color: var(--color-text-primary);
}

.suggestion-right {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.suggestion-career {
  font-size: 0.7rem;
  font-weight: 700;
  padding: 0.1rem 0.4rem;
  background: var(--color-primary-subtle);
  border-radius: 4px;
  color: var(--color-info-text);
}

.suggestion-progress {
  font-size: 0.75rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

/* Tabla compacta institucional que se ajusta a la vista completa */
.compact-table {
  flex: 1;
  min-height: 0;
  margin: 0 -16px !important;
  border-top: 1px solid var(--color-border);
  border-radius: 0 !important;
}

.compact-table :deep(th) {
  padding: 8px 12px;
  font-size: 0.69rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  background: var(--surface-table-head);
  color: var(--color-text-secondary);
  border-bottom: 1px solid var(--color-border-medium);
  position: sticky;
  top: 0;
  z-index: 1;
}

.compact-table :deep(td) {
  padding: 7px 12px;
  font-size: 0.81rem;
  white-space: normal;
  vertical-align: middle;
  border-bottom: 1px solid var(--color-border-light);
}

.compact-table :deep(tbody tr) {
  transition: background-color 150ms ease;
}

.compact-table :deep(tbody tr:hover) {
  background: var(--color-primary-subtle);
}

.control-code {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-weight: 700;
  font-size: 0.8rem;
  white-space: nowrap;
  color: var(--color-text-primary);
}

.student-name-cell {
  font-weight: 600;
  line-height: 1.25;
  color: var(--color-text-primary);
}

.company-name-cell {
  display: -webkit-box;
  -webkit-line-clamp: 1;
  -webkit-box-orient: vertical;
  overflow: hidden;
  max-width: 260px;
  line-height: 1.3;
  color: var(--color-text-secondary);
  font-size: 0.79rem;
}

.progress-cell {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  min-width: 98px;
}

.progress-track {
  flex: 1;
  height: 6px;
  background: var(--color-gray-200);
  border-radius: 999px;
  overflow: hidden;
  min-width: 48px;
}

.progress-fill {
  height: 100%;
  background: var(--color-info);
  border-radius: 999px;
  transition: width 300ms ease;
}

.progress-text {
  font-size: 0.76rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.pagination-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  padding-top: 0.6rem;
  margin-top: auto;
  border-top: 1px solid var(--color-border);
  flex-wrap: wrap;
  flex-shrink: 0;
}

.pagination-info {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
}

.pagination-controls {
  display: flex;
  gap: 0.4rem;
}

/* Modal Detalle y Observaciones */
.detail-modal-stack {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
  gap: 0.75rem;
  background: var(--surface-toolbar);
  padding: 0.75rem 0.95rem;
  border-radius: 9px;
  border: 1px solid var(--color-border);
}

.summary-item {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.summary-item small {
  color: var(--color-text-secondary);
  font-size: 0.72rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.03em;
}

.summary-item strong {
  font-size: 0.88rem;
  color: var(--color-text-primary);
}

.summary-badge-row {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  flex-wrap: wrap;
}

.summary-pct {
  font-size: 0.76rem;
  font-weight: 700;
  color: var(--color-text-secondary);
}

.mono-folio {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
}

.text-emerald {
  color: var(--color-success-text) !important;
}

.detail-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.65rem;
  flex-wrap: wrap;
}

.detail-toolbar__group {
  display: flex;
  gap: 0.45rem;
  flex-wrap: wrap;
}

.modal-tabs {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  padding: 3px;
  border-radius: 7px;
  background: var(--input-bg);
  border: 1px solid var(--input-border);
}

.modal-tab {
  min-height: 28px;
  padding: 0 11px;
  border-radius: 5px;
  border: 1px solid transparent;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 0.76rem;
  font-weight: 600;
  cursor: pointer;
  white-space: nowrap;
  transition: background-color 140ms ease, color 140ms ease, border-color 140ms ease;
}

.modal-tab:hover {
  color: var(--color-text-primary);
  background: var(--secondary-hover);
}

.modal-tab--active {
  background: var(--color-primary);
  color: var(--color-white);
  border-color: var(--color-accent);
}

.modal-tab-panel {
  min-height: 0;
}

.observations-banner {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  padding: 0.6rem 0.9rem;
  border-radius: 8px;
  background: var(--color-warning-bg);
  border: 1px solid var(--color-warning-border);
  color: var(--color-warning-text);
  font-size: 0.83rem;
  flex-wrap: wrap;
}

.observations-banner__text {
  display: flex;
  flex-direction: column;
  gap: 0.12rem;
}

.empty-docs-note {
  padding: 1.5rem;
  text-align: center;
  border-radius: 8px;
  background: var(--surface-toolbar);
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  font-size: 0.85rem;
}

.uploaded-docs-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.6rem;
}

.uploaded-doc-card {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  padding: 0.7rem 0.85rem;
  border: 1px solid var(--color-border);
  background: var(--surface-toolbar);
  border-radius: 8px;
  transition: border-color 140ms ease;
}

.uploaded-doc-card:hover {
  border-color: var(--color-border-medium);
}

.uploaded-doc-top {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
}

.uploaded-doc-title {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex-wrap: wrap;
  min-width: 0;
}

.uploaded-doc-title strong {
  font-size: 0.82rem;
  color: var(--color-text-primary);
}

.doc-code-pill {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.68rem;
  font-weight: 800;
  padding: 0.08rem 0.38rem;
  border-radius: 4px;
  background: var(--color-info-badge);
  color: var(--color-info-text);
  border: 1px solid var(--color-info-border);
  flex-shrink: 0;
}

.uploaded-doc-file {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.73rem;
  color: var(--color-text-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.doc-rejection-note {
  color: var(--color-danger-text);
  font-size: 0.75rem;
}

.uploaded-doc-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem;
  margin-top: auto;
  padding-top: 0.25rem;
}

.modal-footer-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  width: 100%;
  flex-wrap: wrap;
}

.stage-footer-group {
  display: flex;
  flex-wrap: wrap;
  gap: 0.45rem;
}

@media (max-width: 780px) {
  .uploaded-docs-grid {
    grid-template-columns: 1fr;
  }
}

.observations-modal-body {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}

.observations-student-pill {
  padding: 0.55rem 0.75rem;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  font-size: 0.84rem;
  display: flex;
  gap: 0.4rem;
  flex-wrap: wrap;
}

.observations-helper {
  margin: 0;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
}

.previous-review-box {
  padding: 0.55rem 0.75rem;
  border-left: 3px solid var(--color-accent);
  background: var(--color-primary-subtle);
  border-radius: 4px;
  font-size: 0.82rem;
}

.previous-review-box p {
  margin: 0.2rem 0 0;
}

@media (max-width: 900px) {
  .fullscreen-page {
    height: auto !important;
    overflow: visible !important;
    padding: 4px 0 12px !important;
    min-width: 0;
    max-width: 100%;
  }

  .workspace-card {
    min-width: 0;
    max-width: 100%;
    overflow: hidden;
    padding: 10px 12px !important;
  }

  .compact-table {
    margin: 0 -12px !important;
    max-width: calc(100% + 24px);
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
  }

  .compact-table :deep(table) {
    min-width: 700px;
  }
}

@media (max-width: 640px) {
  .exec-header {
    flex-direction: column;
    align-items: stretch;
    gap: 0.6rem;
  }

  .exec-header__left {
    min-width: 0;
  }

  .exec-title {
    font-size: 1.05rem;
  }

  .exec-kpis {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    width: 100%;
    gap: 0.45rem;
  }

  .mini-kpi {
    min-width: 0;
    padding: 0.45rem 0.65rem;
  }

  .filters-bar {
    flex-direction: column;
    align-items: stretch;
    gap: 0.6rem;
    padding: 0.6rem;
  }

  .search-box,
  .select-box {
    min-width: 0;
    width: 100%;
    flex: 1 1 auto;
  }

  input.modern-input,
  select.modern-select {
    min-height: 40px;
    height: 40px;
    font-size: 16px;
  }

  .filter-actions {
    width: 100%;
    height: auto;
  }

  .filter-actions :deep(.btn) {
    flex: 1;
    min-height: 38px !important;
    height: 38px !important;
  }

  .pagination-bar {
    flex-direction: column;
    align-items: stretch;
    gap: 0.5rem;
  }

  .pagination-info {
    text-align: center;
  }

  .pagination-controls {
    width: 100%;
  }

  .pagination-controls :deep(.btn) {
    flex: 1;
  }

  .summary-grid {
    grid-template-columns: 1fr;
    padding: 0.65rem 0.75rem;
    gap: 0.55rem;
  }

  .detail-toolbar {
    flex-direction: column;
    align-items: stretch;
  }

  .detail-toolbar__group {
    flex-direction: column;
    align-items: stretch;
  }

  .detail-toolbar__group :deep(.btn) {
    width: 100%;
    justify-content: center;
    white-space: normal;
  }

  .modal-tabs {
    display: flex;
    width: 100%;
    max-width: 100%;
    overflow-x: auto;
    scrollbar-width: none;
  }

  .modal-tabs::-webkit-scrollbar {
    display: none;
  }

  .modal-tab {
    flex: 1 0 auto;
    text-align: center;
    min-height: 32px;
  }

  .observations-banner {
    flex-direction: column;
    align-items: stretch;
  }

  .observations-banner :deep(.btn) {
    width: 100%;
  }

  .modal-footer-actions {
    flex-direction: column-reverse;
    align-items: stretch;
    gap: 0.5rem;
  }

  .stage-footer-group {
    flex-direction: column;
    align-items: stretch;
  }

  .stage-footer-group :deep(.btn) {
    width: 100%;
    white-space: normal;
  }
}
</style>
