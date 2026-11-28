<script setup lang="ts">
import { problemMessage } from '../../../shared/api/problemDetails'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { defaultProcess, canReadProcess } from '../../../shared/composables/useSession'
import { useRealtimeRefresh } from '../../../shared/composables/useRealtime'
import {
  UiAlert,
  UiBadge,
  UiBasePage,
  UiButton,
  UiCard,
  UiDialog,
} from '../../../shared/components'
import {
  getActivityTypeOptions,
  getCareerOptions,
  getCompanyOptions,
  getPeriodOptions,
  getSchoolYearOptions,
} from '../../catalogs/api/careersApi'
import type { OptionItem, SchoolYearOption } from '../../catalogs/types/careers'
import { getTrackingData } from '../api/reportsApi'
import type { TrackingItem } from '../types/reports'
import {
  socialServiceDocuments,
  socialDocumentLabel,
} from '../../../shared/utils/socialServiceDocuments'
import TrackingExpedient from '../components/TrackingExpedient.vue'
import { exportTrackingCsvSafe, exportTrackingExcel } from '../utils/exportReports'

const items = ref<TrackingItem[]>([])
const periodOptions = ref<OptionItem[]>([])
const schoolYearOptions = ref<SchoolYearOption[]>([])
const careerOptions = ref<OptionItem[]>([])
const companyOptions = ref<OptionItem[]>([])
const activityTypeOptions = ref<OptionItem[]>([])

const isLoading = ref(true)
const errorMessage = ref('')
let dataRequest = 0

const tramiteType = ref<'social_service' | 'internship'>(defaultProcess.value)
const selectedPeriodId = ref<number | undefined>(undefined)
const selectedSchoolYear = ref<string>('')
const selectedCareerId = ref<number | undefined>(undefined)
const selectedSemester = ref<number | undefined>(undefined)
const selectedGender = ref<string>('')
const selectedCompanyId = ref<number | undefined>(undefined)
const selectedActivityTypeId = ref<number | undefined>(undefined)
const selectedStatus = ref<string>('')
const searchQuery = ref('')
const debouncedQuery = ref('')
const kpiFilter = ref<'all' | 'in_progress' | 'complete' | 'released'>('all')

// Autocompletado con debounce de 280 ms
const isAutocompleteOpen = ref(false)
let debounceTimer: ReturnType<typeof setTimeout> | undefined

// Paginación estandarizada (10 registros por página)
const currentPage = ref(1)
const pageSize = 10

const isExpedientOpen = ref(false)
const selectedRow = ref<TrackingItem | null>(null)
const selectedProcess = ref<'social_service' | 'internship'>(tramiteType.value)

function openExpedient(row: TrackingItem): void {
  selectedRow.value = row
  selectedProcess.value = tramiteType.value
  isExpedientOpen.value = true
}

const ssDocKeys = socialServiceDocuments
  .filter((item) => item.trackingKey)
  .map((item) => ({
    key: item.trackingKey!,
    label: socialDocumentLabel(item.codes[0]!),
  }))

const ppDocKeys = [
  { key: 'cp', label: 'CP' },
  { key: 'ca', label: 'CA' },
  { key: 'ev', label: 'EV' },
  { key: 'ct', label: 'CT' },
  { key: 'cc', label: 'CC' },
] as const

const activeDocKeys = computed(() =>
  tramiteType.value === 'social_service' ? ssDocKeys : ppDocKeys,
)

// Métricas ejecutivas de los 4 KPIs superiores
const kpiMetrics = computed(() => {
  const all = items.value
  return {
    total: all.length,
    inProgress: all.filter((r) => (r.progressPercentage ?? 0) < 100).length,
    complete: all.filter((r) => (r.progressPercentage ?? 0) >= 100).length,
    released: all.filter((r) => Boolean(r.releaseCertificateFolio)).length,
  }
})

// Filtrado reactivo con búsqueda y KPI activo
const filteredItems = computed(() => {
  const q = debouncedQuery.value.trim().toLowerCase()
  return items.value.filter((row) => {
    if (kpiFilter.value === 'in_progress' && (row.progressPercentage ?? 0) >= 100) return false
    if (kpiFilter.value === 'complete' && (row.progressPercentage ?? 0) < 100) return false
    if (kpiFilter.value === 'released' && !row.releaseCertificateFolio) return false

    if (!q) return true
    return (
      (row.controlNumber || '').toLowerCase().includes(q) ||
      (row.studentFullName || '').toLowerCase().includes(q) ||
      (row.careerCode || '').toLowerCase().includes(q) ||
      (row.companyName || '').toLowerCase().includes(q) ||
      (row.presentationLetterFolio || '').toLowerCase().includes(q) ||
      (row.releaseCertificateFolio || '').toLowerCase().includes(q)
    )
  })
})

const autocompleteSuggestions = computed(() => {
  const q = debouncedQuery.value.trim().toLowerCase()
  if (!q) return []
  return items.value
    .filter(
      (row) =>
        (row.controlNumber || '').toLowerCase().includes(q) ||
        (row.studentFullName || '').toLowerCase().includes(q) ||
        (row.careerCode || '').toLowerCase().includes(q) ||
        (row.companyName || '').toLowerCase().includes(q),
    )
    .slice(0, 6)
})

const totalPages = computed(() =>
  Math.max(1, Math.ceil(filteredItems.value.length / pageSize)),
)

const pagedItems = computed(() => {
  const start = (currentPage.value - 1) * pageSize
  return filteredItems.value.slice(start, start + pageSize)
})

watch(
  [
    kpiFilter,
    selectedPeriodId,
    selectedSchoolYear,
    selectedCareerId,
    selectedSemester,
    selectedGender,
    selectedCompanyId,
    selectedActivityTypeId,
    selectedStatus,
    tramiteType,
  ],
  () => {
    currentPage.value = 1
  },
)

function handleSearchInput(): void {
  clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    debouncedQuery.value = searchQuery.value
    currentPage.value = 1
    isAutocompleteOpen.value = searchQuery.value.trim().length > 0
  }, 280)
}

function selectSuggestion(row: TrackingItem): void {
  searchQuery.value = row.controlNumber
  debouncedQuery.value = row.controlNumber
  isAutocompleteOpen.value = false
  currentPage.value = 1
}

function handleSearch(): void {
  clearTimeout(debounceTimer)
  debouncedQuery.value = searchQuery.value
  isAutocompleteOpen.value = false
  currentPage.value = 1
  void loadData()
}

function handleClearSearch(): void {
  clearTimeout(debounceTimer)
  searchQuery.value = ''
  debouncedQuery.value = ''
  isAutocompleteOpen.value = false
  currentPage.value = 1
  void loadData()
}

function handleSearchFocus(): void {
  if (searchQuery.value.trim().length > 0) {
    isAutocompleteOpen.value = true
  }
}

function handleSearchBlur(): void {
  setTimeout(() => {
    isAutocompleteOpen.value = false
  }, 180)
}

function switchProcess(nextProcess: 'social_service' | 'internship'): void {
  if (tramiteType.value === nextProcess) return
  tramiteType.value = nextProcess
  currentPage.value = 1
  void loadData()
}

async function loadCatalogs(): Promise<void> {
  const [periods, schoolYears, careers, companies, activities] = await Promise.all([
    getPeriodOptions().catch(() => []),
    getSchoolYearOptions().catch(() => []),
    getCareerOptions().catch(() => []),
    getCompanyOptions().catch(() => []),
    getActivityTypeOptions(tramiteType.value).catch(() => []),
  ])
  periodOptions.value = periods
  schoolYearOptions.value = schoolYears
  careerOptions.value = careers
  companyOptions.value = companies
  activityTypeOptions.value = activities
}

async function loadData(): Promise<void> {
  const request = ++dataRequest
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await getTrackingData({
      periodId: selectedSchoolYear.value ? undefined : selectedPeriodId.value,
      schoolYear: selectedSchoolYear.value || undefined,
      tramiteType: tramiteType.value,
      careerId: selectedCareerId.value,
      semester: selectedSemester.value,
      gender: selectedGender.value || undefined,
      companyId: selectedCompanyId.value,
      activityTypeId: selectedActivityTypeId.value,
      status: selectedStatus.value || undefined,
    })
    if (request !== dataRequest) return
    items.value = (res.items || []).map((i) => ({ ...i, id: i.tramiteId }))
  } catch (err: any) {
    if (request !== dataRequest) return
    errorMessage.value = problemMessage(
      err,
      'Error al cargar la matriz de seguimiento en vivo.',
    )
  } finally {
    if (request === dataRequest) isLoading.value = false
  }
}

useRealtimeRefresh(
  (evt) => evt.tramiteType === tramiteType.value || evt.tramiteType === 'general',
  () => loadData(),
)

function handleExportCsv(): void {
  exportTrackingCsvSafe(filteredItems.value, activeDocKeys.value, tramiteType.value)
}

async function handleExportExcel(): Promise<void> {
  await exportTrackingExcel(filteredItems.value, activeDocKeys.value, tramiteType.value)
}

onBeforeUnmount(() => {
  clearTimeout(debounceTimer)
  dataRequest++
})

onMounted(async () => {
  await Promise.all([loadCatalogs(), loadData()])
})
</script>

<template>
  <UiBasePage :fluid="true" class="fullscreen-page">
    <!-- Encabezado Ejecutivo Compacto con 4 KPIs Superiores (solo borde dorado activo) -->
    <header class="exec-header">
      <div class="exec-header__left">
        <div class="exec-eyebrow">
          <span class="exec-badge">MATRIZ DE CONTROL DOCUMENTAL EN VIVO</span>
          <span class="exec-period">
            · SGC ISO 9001:2015 ({{
              tramiteType === 'social_service'
                ? 'Servicio Social · 10 Hitos'
                : 'Prácticas Profesionales · 5 Hitos'
            }})
          </span>
        </div>
        <div class="title-row">
          <h1 class="exec-title">
            Sábana de Seguimiento de Vinculación (`SEGUIMIENTO VINCULACIÓN1.xlsx`)
          </h1>
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="filteredItems.length === 0"
            @click="handleExportExcel"
          >
            Exportar Excel (.xlsx)
          </UiButton>
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="filteredItems.length === 0"
            @click="handleExportCsv"
          >
            Exportar .CSV
          </UiButton>
        </div>
      </div>

      <!-- 4 KPIs con superficie neutra y borde dorado únicamente cuando está activo -->
      <div class="exec-kpis" aria-label="Indicadores de la sábana de seguimiento">
        <button
          type="button"
          class="mini-kpi"
          :class="{ 'mini-kpi--active': kpiFilter === 'all' }"
          @click="kpiFilter = 'all'"
        >
          <span class="mini-kpi__label">Total Matriz</span>
          <strong class="mini-kpi__value">{{ kpiMetrics.total }}</strong>
        </button>

        <button
          type="button"
          class="mini-kpi mini-kpi--gold"
          :class="{ 'mini-kpi--active': kpiFilter === 'in_progress' }"
          @click="kpiFilter = kpiFilter === 'in_progress' ? 'all' : 'in_progress'"
        >
          <span class="mini-kpi__label">En Proceso</span>
          <strong class="mini-kpi__value">{{ kpiMetrics.inProgress }}</strong>
        </button>

        <button
          type="button"
          class="mini-kpi mini-kpi--navy"
          :class="{ 'mini-kpi--active': kpiFilter === 'complete' }"
          @click="kpiFilter = kpiFilter === 'complete' ? 'all' : 'complete'"
        >
          <span class="mini-kpi__label">Avance 100%</span>
          <strong class="mini-kpi__value">{{ kpiMetrics.complete }}</strong>
        </button>

        <button
          type="button"
          class="mini-kpi mini-kpi--emerald"
          :class="{ 'mini-kpi--active': kpiFilter === 'released' }"
          @click="kpiFilter = kpiFilter === 'released' ? 'all' : 'released'"
        >
          <span class="mini-kpi__label">Con Constancia</span>
          <strong class="mini-kpi__value">{{ kpiMetrics.released }}</strong>
        </button>
      </div>
    </header>

    <UiAlert v-if="errorMessage" tone="danger" class="compact-alert">
      {{ errorMessage }}
    </UiAlert>

    <UiCard class="matrix-card">
      <!-- Barra de Herramientas y Buscador con Autocompletado -->
      <div class="filters-bar">
        <!-- Selector de Hoja (Servicio Social vs Prácticas) -->
        <div class="sheet-tabs-box">
          <span class="filter-label">Hoja de seguimiento</span>
          <div class="sheet-tabs" role="tablist" aria-label="Seleccionar hoja de seguimiento">
            <button
              v-if="canReadProcess('social_service')"
              type="button"
              class="sheet-tab"
              :class="{ 'sheet-tab--active': tramiteType === 'social_service' }"
              @click="switchProcess('social_service')"
            >
              Servicio Social (10 Hitos)
            </button>
            <button
              v-if="canReadProcess('internship')"
              type="button"
              class="sheet-tab"
              :class="{ 'sheet-tab--active': tramiteType === 'internship' }"
              @click="switchProcess('internship')"
            >
              Prácticas Profesionales (5 Hitos)
            </button>
          </div>
        </div>

        <!-- Buscador con Autocompletado y Debounce (280 ms) -->
        <div class="search-box">
          <label class="filter-label" for="tracking-search-input">
            Buscar por Matrícula, Alumno, Carrera o Dependencia
          </label>
          <div class="autocomplete-container">
            <div class="input-wrapper">
              <svg
                viewBox="0 0 24 24"
                aria-hidden="true"
                class="search-icon"
                width="15"
                height="15"
                stroke="currentColor"
                stroke-width="2"
                fill="none"
              >
                <circle cx="11" cy="11" r="8" />
                <line x1="21" y1="21" x2="16.65" y2="16.65" />
              </svg>
              <input
                id="tracking-search-input"
                v-model="searchQuery"
                type="search"
                class="modern-input"
                placeholder="Ej. I23050618, Mariana, IND o empresa..."
                aria-label="Buscar matrícula o alumno en la matriz"
                autocomplete="off"
                @input="handleSearchInput"
                @focus="handleSearchFocus"
                @blur="handleSearchBlur"
                @keyup.enter="handleSearch"
              />
              <button
                v-if="searchQuery"
                type="button"
                class="clear-search-btn"
                aria-label="Limpiar búsqueda"
                @click="handleClearSearch"
              >
                ✕
              </button>
            </div>

            <!-- Lista desplegable de sugerencias (Autocomplete) -->
            <div
              v-if="isAutocompleteOpen && autocompleteSuggestions.length > 0"
              class="suggestions-dropdown"
              role="listbox"
            >
              <div
                v-for="suggestion in autocompleteSuggestions"
                :key="suggestion.tramiteId"
                class="suggestion-item"
                role="option"
                @mousedown.prevent="selectSuggestion(suggestion)"
              >
                <div class="suggestion-left">
                  <span class="suggestion-control">{{ suggestion.controlNumber }}</span>
                  <span class="suggestion-name">{{ suggestion.studentFullName }}</span>
                </div>
                <div class="suggestion-right">
                  <span class="suggestion-career">{{ suggestion.careerCode }}</span>
                  <span class="suggestion-progress">{{ suggestion.progressPercentage }}%</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Selector de Periodo -->
        <div class="select-box">
          <label class="filter-label" for="tracking-period-select">Periodo</label>
          <div class="select-wrapper">
            <select
              id="tracking-period-select"
              v-model.number="selectedPeriodId"
              class="modern-select"
              @change="loadData"
            >
              <option :value="undefined">Periodo Activo</option>
              <option :value="0">Todos los Periodos</option>
              <option v-for="p in periodOptions" :key="p.id" :value="p.id">
                {{ p.label }}
              </option>
            </select>
            <svg
              class="select-chevron"
              viewBox="0 0 24 24"
              width="14"
              height="14"
              stroke="currentColor"
              stroke-width="2"
              fill="none"
              aria-hidden="true"
            >
              <polyline points="6 9 12 15 18 9" />
            </svg>
          </div>
        </div>

        <!-- Selector de Carrera -->
        <div class="select-box">
          <label class="filter-label" for="tracking-career-select">Carrera</label>
          <div class="select-wrapper">
            <select
              id="tracking-career-select"
              v-model.number="selectedCareerId"
              class="modern-select"
              @change="loadData"
            >
              <option :value="undefined">Todas las Carreras</option>
              <option v-for="c in careerOptions" :key="c.id" :value="c.id">
                {{ c.label }}
              </option>
            </select>
            <svg
              class="select-chevron"
              viewBox="0 0 24 24"
              width="14"
              height="14"
              stroke="currentColor"
              stroke-width="2"
              fill="none"
              aria-hidden="true"
            >
              <polyline points="6 9 12 15 18 9" />
            </svg>
          </div>
        </div>

        <!-- Selector de Ciclo Escolar -->
        <div class="select-box">
          <label class="filter-label" for="tracking-sy-select">Ciclo Escolar</label>
          <div class="select-wrapper">
            <select
              id="tracking-sy-select"
              v-model="selectedSchoolYear"
              class="modern-select"
              @change="loadData"
            >
              <option value="">Por periodo individual</option>
              <option v-for="sy in schoolYearOptions" :key="sy.schoolYear" :value="sy.schoolYear">
                Ciclo {{ sy.schoolYear }}
              </option>
            </select>
          </div>
        </div>

        <!-- Selector de Semestre -->
        <div class="select-box">
          <label class="filter-label" for="tracking-sem-select">Semestre</label>
          <div class="select-wrapper">
            <select
              id="tracking-sem-select"
              v-model="selectedSemester"
              class="modern-select"
              @change="loadData"
            >
              <option :value="undefined">Todos</option>
              <option v-for="n in 12" :key="n" :value="n">{{ n }}°</option>
            </select>
          </div>
        </div>

        <!-- Selector de Sexo -->
        <div class="select-box">
          <label class="filter-label" for="tracking-gender-select">Sexo</label>
          <div class="select-wrapper">
            <select
              id="tracking-gender-select"
              v-model="selectedGender"
              class="modern-select"
              @change="loadData"
            >
              <option value="">Todos</option>
              <option value="M">Hombres</option>
              <option value="F">Mujeres</option>
              <option value="unspecified">No especificado</option>
            </select>
          </div>
        </div>

        <!-- Selector de Tipo de Actividad -->
        <div class="select-box">
          <label class="filter-label" for="tracking-act-select">Tipo de Actividad</label>
          <div class="select-wrapper">
            <select
              id="tracking-act-select"
              v-model="selectedActivityTypeId"
              class="modern-select"
              @change="loadData"
            >
              <option :value="undefined">Todas</option>
              <option :value="0">Sin clasificar</option>
              <option v-for="a in activityTypeOptions" :key="a.id" :value="a.id">{{ a.label }}</option>
            </select>
          </div>
        </div>

        <!-- Selector de Empresa -->
        <div class="select-box">
          <label class="filter-label" for="tracking-comp-select">Empresa</label>
          <div class="select-wrapper">
            <select
              id="tracking-comp-select"
              v-model="selectedCompanyId"
              class="modern-select"
              @change="loadData"
            >
              <option :value="undefined">Todas</option>
              <option :value="0">Sin empresa (PP)</option>
              <option v-for="c in companyOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
            </select>
          </div>
        </div>

        <!-- Selector de Estatus -->
        <div class="select-box">
          <label class="filter-label" for="tracking-status-select">Estatus</label>
          <div class="select-wrapper">
            <select
              id="tracking-status-select"
              v-model="selectedStatus"
              class="modern-select"
              @change="loadData"
            >
              <option value="">Todos</option>
              <option value="requested">Solicitado</option>
              <option value="approved">CP Emitida</option>
              <option value="accepted">Aceptado</option>
              <option value="active">Activo</option>
              <option value="completed_pending_review">Por Liberar</option>
              <option value="released">Liberado</option>
              <option value="non_compliant">PNC</option>
              <option value="cancelled">Cancelado</option>
            </select>
          </div>
        </div>

        <div class="toolbar-actions">
          <UiButton variant="primary" size="sm" @click="handleSearch">
            Buscar
          </UiButton>
        </div>
      </div>

      <!-- Tabla tipo Excel con scroll interno (horizontal y vertical) -->
      <div class="matrix-table-wrap" role="region" aria-label="Matriz de seguimiento de vinculación">
        <table class="excel-table">
          <thead>
            <tr v-if="tramiteType === 'social_service'" class="colgroup-row">
              <th colspan="9" />
              <th colspan="3" scope="colgroup" class="colgroup-header">
                4. {{ socialServiceDocuments.find((item) => item.number === '4')?.title }}
              </th>
              <th colspan="7" />
            </tr>
            <tr>
              <th>No. Control</th>
              <th>Nombre del Alumno</th>
              <th>Carrera</th>
              <th>Dependencia / Empresa</th>
              <th>Folio C.P.</th>
              <th
                v-for="col in activeDocKeys"
                :key="col.key"
                class="th-doc-code"
              >
                {{ col.label }}
              </th>
              <th>Avance %</th>
              <th>Const. Term.</th>
              <th>Acta Escolares</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="isLoading">
              <td :colspan="8 + activeDocKeys.length" class="empty-cell">
                Cargando sábana de seguimiento...
              </td>
            </tr>
            <tr v-else-if="pagedItems.length === 0">
              <td :colspan="8 + activeDocKeys.length" class="empty-cell">
                No se encontraron trámites con los filtros seleccionados.
              </td>
            </tr>
            <tr v-for="row in pagedItems" :key="row.tramiteId" class="matrix-tr">
              <td>
                <strong class="control-mono">{{ row.controlNumber }}</strong>
              </td>
              <td>
                <div class="student-cell">
                  <span class="student-name">{{ row.studentFullName }}</span>
                  <UiButton
                    class="expedient-button"
                    variant="secondary"
                    size="sm"
                    title="Ver expediente"
                    :aria-label="`Ver expediente de ${row.studentFullName} (${row.controlNumber})`"
                    aria-haspopup="dialog"
                    :disabled="isLoading || !!errorMessage"
                    @click="openExpedient(row)"
                  >
                    <svg
                      aria-hidden="true"
                      viewBox="0 0 24 24"
                      width="15"
                      height="15"
                      fill="none"
                      stroke="currentColor"
                      stroke-width="2"
                    >
                      <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z" />
                      <circle cx="12" cy="12" r="3" />
                    </svg>
                  </UiButton>
                </div>
              </td>
              <td>
                <UiBadge tone="info">{{ row.careerCode }}</UiBadge>
              </td>
              <td class="company-cell" :title="row.companyName || 'Pendiente de CA'">
                {{ row.companyName || 'Pendiente de CA' }}
              </td>
              <td>
                <small class="folio-code">{{ row.presentationLetterFolio || '—' }}</small>
              </td>
              <td
                v-for="col in activeDocKeys"
                :key="col.key"
                class="td-doc-code"
              >
                <span
                  :class="[
                    'doc-cell-pill',
                    (row.documents as any)?.[col.key] ? 'si' : 'no',
                  ]"
                >
                  {{ (row.documents as any)?.[col.key] ? 'SI' : '—' }}
                </span>
              </td>
              <td class="progress-td">
                <div class="progress-cell">
                  <div class="progress-track">
                    <div
                      class="progress-fill"
                      :class="{ 'progress-fill--complete': row.progressPercentage === 100 }"
                      :style="{ width: `${row.progressPercentage}%` }"
                    />
                  </div>
                  <strong class="progress-text">{{ row.progressPercentage }}%</strong>
                </div>
              </td>
              <td>
                <strong
                  :class="row.releaseCertificateFolio ? 'folio-released' : 'folio-empty'"
                >
                  {{ row.releaseCertificateFolio || '—' }}
                </strong>
              </td>
              <td>
                <small class="folio-code">{{ row.schoolServicesActFolio || '—' }}</small>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Barra de Paginación Estandarizada (10 registros por página) -->
      <footer class="pagination-bar">
        <span class="pagination-info">
          Mostrando <strong>{{ pagedItems.length }}</strong> de
          <strong>{{ filteredItems.length }}</strong> trámites · Página
          <strong>{{ currentPage }}</strong> de <strong>{{ totalPages }}</strong>
        </span>
        <div class="pagination-controls">
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="currentPage <= 1 || isLoading"
            @click="currentPage--"
          >
            Anterior
          </UiButton>
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="currentPage >= totalPages || isLoading"
            @click="currentPage++"
          >
            Siguiente
          </UiButton>
        </div>
      </footer>
    </UiCard>

    <UiDialog
      v-model="isExpedientOpen"
      :title="`Expediente · ${selectedRow?.studentFullName || ''}`"
      size="xl"
    >
      <TrackingExpedient
        v-if="selectedRow"
        :tramite-id="selectedRow.tramiteId"
        :tramite-type="selectedProcess"
      />
      <template #footer>
        <UiButton variant="secondary" size="sm" @click="isExpedientOpen = false">
          Cerrar expediente
        </UiButton>
      </template>
    </UiDialog>
  </UiBasePage>
</template>

<style scoped>
/* Estructura a pantalla completa (100dvh) sin scroll exterior */
.fullscreen-page {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  overflow: hidden;
  padding: 12px 22px 10px !important;
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
  min-width: 280px;
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

.title-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.exec-title {
  font-size: 1.18rem;
  font-weight: 800;
  margin: 0;
  color: var(--color-text-primary);
  letter-spacing: -0.015em;
  line-height: 1.2;
}

/* 4 KPIs con superficie neutra y borde dorado únicamente al estar activo */
.exec-kpis {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

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
  transition:
    background-color 180ms ease,
    border-color 180ms ease,
    box-shadow 180ms ease,
    transform 180ms cubic-bezier(0.22, 1, 0.36, 1);
}

.mini-kpi:hover {
  background: var(--secondary-hover);
  border-color: color-mix(in srgb, var(--color-accent) 70%, var(--color-border-medium));
  transform: translateY(-2px);
}

.mini-kpi__label {
  font-size: 0.65rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.mini-kpi__value {
  font-size: 1.18rem;
  font-weight: 800;
  color: var(--color-text-primary);
  line-height: 1.15;
  font-variant-numeric: tabular-nums;
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
  margin-bottom: 0.55rem;
  flex-shrink: 0;
}

.matrix-card {
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
  gap: 0.65rem;
  flex-wrap: wrap;
  align-items: flex-end;
  margin-bottom: 0.6rem;
  padding: 0.55rem 0.75rem;
  border-radius: 8px;
  background: var(--surface-toolbar);
  border: 1px solid var(--color-border);
  flex-shrink: 0;
}

.sheet-tabs-box {
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.sheet-tabs {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  padding: 3px;
  height: 34px;
  border-radius: 7px;
  background: var(--input-bg);
  border: 1px solid var(--input-border);
}

.sheet-tab {
  min-height: 26px !important;
  height: 26px;
  padding: 0 10px;
  border: 1px solid transparent;
  border-radius: 5px;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 0.74rem;
  font-weight: 600;
  cursor: pointer;
  white-space: nowrap;
  transition: background-color 150ms ease, color 150ms ease, border-color 150ms ease;
}

.sheet-tab:hover {
  color: var(--color-text-primary);
  background: var(--secondary-hover);
}

.sheet-tab--active {
  background: var(--color-primary);
  color: var(--color-white);
  border-color: var(--color-accent);
}

.search-box {
  flex: 1.5;
  min-width: 220px;
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.select-box {
  flex: 0.85;
  min-width: 155px;
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.toolbar-actions {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.filter-label {
  font-size: 0.72rem;
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

input.modern-input::-webkit-search-cancel-button,
input.modern-input::-webkit-search-decoration {
  -webkit-appearance: none;
  appearance: none;
  display: none;
}

input.modern-input:hover,
select.modern-select:hover {
  border-color: var(--color-gray-400) !important;
}

input.modern-input:focus,
select.modern-select:focus {
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

.select-chevron {
  position: absolute;
  right: 0.75rem;
  color: var(--color-text-secondary);
  pointer-events: none;
}

.autocomplete-container {
  position: relative;
  width: 100%;
}

.clear-search-btn {
  position: absolute;
  right: 0.55rem;
  background: transparent;
  border: none;
  color: var(--color-primary);
  cursor: pointer;
  font-size: 0.85rem;
  font-weight: 800;
  padding: 0.2rem 0.3rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: background-color 140ms ease, color 140ms ease, transform 140ms ease;
}

.clear-search-btn:hover {
  color: var(--color-primary-hover);
  background: var(--color-primary-subtle);
  transform: scale(1.08);
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
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
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
  font-size: 0.72rem;
  font-weight: 700;
  color: var(--color-text-secondary);
}

/* Tabla de matriz con scroll interno (horizontal y vertical) */
.matrix-table-wrap {
  flex: 1;
  min-height: 0;
  overflow: auto;
  margin: 0 -16px;
  border-top: 1px solid var(--color-border);
}

.excel-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.79rem;
}

.excel-table th,
.excel-table td {
  border: 1px solid var(--color-border);
  padding: 0.38rem 0.55rem;
  text-align: left;
  white-space: nowrap;
  vertical-align: middle;
}

.excel-table thead th {
  background: var(--surface-table-head);
  font-size: 0.68rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
  position: sticky;
  top: 0;
  z-index: 2;
}

.colgroup-row th {
  padding: 0.25rem 0.5rem;
  font-size: 0.65rem;
  background: var(--surface-toolbar);
}

.colgroup-header {
  text-align: center !important;
  color: var(--color-accent) !important;
}

.th-doc-code,
.td-doc-code {
  text-align: center !important;
}

.matrix-tr {
  transition: background-color 140ms ease;
}

.matrix-tr:hover {
  background: var(--color-primary-subtle);
}

.control-mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.78rem;
  color: var(--color-info-text);
}

.student-cell {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.student-name {
  font-weight: 600;
  color: var(--color-text-primary);
}

.expedient-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 26px;
  min-height: 26px !important;
  height: 26px;
  padding: 0 !important;
}

.company-cell {
  max-width: 210px;
  overflow: hidden;
  text-overflow: ellipsis;
  color: var(--color-text-secondary);
}

.folio-code {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.74rem;
  color: var(--color-text-secondary);
}

.folio-released {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.76rem;
  color: var(--color-success-text);
}

.folio-empty {
  color: var(--color-text-muted);
}

/* Pills SI / — adaptados a modo claro y oscuro mate */
.doc-cell-pill {
  display: inline-block;
  padding: 2px 7px;
  border-radius: 4px;
  font-size: 0.7rem;
  font-weight: 700;
  border: 1px solid transparent;
}

.doc-cell-pill.si {
  background: var(--color-success-badge);
  color: var(--color-success-text);
  border-color: var(--color-success-border);
}

.doc-cell-pill.no {
  background: var(--color-bg-light);
  color: var(--color-text-muted);
  border-color: var(--color-border-light);
}

/* Barra de progreso mate */
.progress-td {
  min-width: 112px;
}

.progress-cell {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.progress-track {
  flex: 1;
  height: 7px;
  background: var(--color-border);
  border-radius: 4px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: var(--color-info);
  border-radius: 4px;
  transition: width 220ms ease;
}

.progress-fill--complete {
  background: var(--color-success);
}

.progress-text {
  font-size: 0.76rem;
  font-variant-numeric: tabular-nums;
  color: var(--color-text-primary);
  min-width: 34px;
  text-align: right;
}

.empty-cell {
  text-align: center !important;
  padding: 1.5rem !important;
  color: var(--color-text-secondary);
}

/* Paginación inferior fija */
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

@media (max-width: 900px) {
  .fullscreen-page {
    height: auto !important;
    overflow: visible !important;
    padding: 4px 0 12px !important;
    min-width: 0;
    max-width: 100%;
  }

  .matrix-card {
    min-width: 0;
    max-width: 100%;
    overflow: hidden;
    padding: 10px 12px !important;
  }

  .matrix-table-wrap {
    margin: 0 -12px;
    max-width: calc(100% + 24px);
    max-height: 68dvh;
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
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

  .title-row {
    justify-content: space-between;
  }

  .exec-title {
    font-size: 1.05rem;
    overflow-wrap: anywhere;
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
    min-width: 0;
    max-width: 100%;
    box-sizing: border-box;
  }

  .sheet-tabs-box,
  .search-box,
  .select-box,
  .toolbar-actions {
    width: 100%;
    max-width: 100%;
    min-width: 0;
    flex: 1 1 auto;
  }

  .sheet-tabs {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    width: 100%;
    max-width: 100%;
    height: auto;
    min-height: 38px;
    padding: 3px;
    gap: 3px;
    box-sizing: border-box;
  }

  .sheet-tab {
    min-width: 0;
    width: 100%;
    min-height: 32px !important;
    height: auto !important;
    padding: 5px 6px;
    font-size: 0.72rem;
    line-height: 1.2;
    white-space: normal;
    text-align: center;
    box-sizing: border-box;
  }

  .toolbar-actions :deep(.btn) {
    flex: 1;
    min-height: 38px;
  }

  input.modern-input,
  select.modern-select {
    min-height: 40px;
    height: 40px;
    font-size: 16px;
  }

  .expedient-button {
    width: 32px;
    min-height: 32px !important;
    height: 32px;
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
}
</style>
