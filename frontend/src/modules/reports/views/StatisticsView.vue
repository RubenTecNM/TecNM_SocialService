<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import type { ChartData, ChartOptions } from 'chart.js'
import { problemMessage } from '../../../shared/api/problemDetails'
import { defaultProcess } from '../../../shared/composables/useSession'
import { useRealtimeRefresh } from '../../../shared/composables/useRealtime'
import { useTheme } from '../../../shared/composables/useTheme'
import { UiAlert, UiBadge, UiBasePage, UiButton, UiCard, UiChart } from '../../../shared/components'
import {
  getActivityTypeOptions,
  getCareerOptions,
  getCompanyOptions,
  getPeriodOptions,
  getSchoolYearOptions,
} from '../../catalogs/api/careersApi'
import type { OptionItem, SchoolYearOption } from '../../catalogs/types/careers'
import { get911Report, getAnalyticsReport, getSemesterStatistics } from '../api/reportsApi'
import type {
  AnalyticsFilters,
  AnalyticsReportResponse,
  AnalyticsStatusGroup,
  AnalyticsTimeScope,
  AnalyticsTramiteType,
  Report911Response,
  StatisticalReportResponse,
} from '../types/reports'
import AnalyticsDashboardPanel from '../components/AnalyticsDashboardPanel.vue'
import Report911Panel from '../components/Report911Panel.vue'
import { exportAnalyticsExcel, openAnalyticsPrintReport } from '../utils/exportReports'

const route = useRoute()
const router = useRouter()
const { dark } = useTheme()

const activeTab = ref<'analitico' | 'estadistico' | 'formato911'>('analitico')
const analytics = ref<AnalyticsReportResponse | null>(null)
const stats = ref<StatisticalReportResponse | null>(null)
const rep911 = ref<Report911Response | null>(null)

const periodOptions = ref<OptionItem[]>([])
const schoolYearOptions = ref<SchoolYearOption[]>([])
const careerOptions = ref<OptionItem[]>([])
const companyOptions = ref<OptionItem[]>([])
const activityTypeOptions = ref<OptionItem[]>([])

const isLoading = ref(true)
const isExporting = ref(false)
const errorMessage = ref('')
const analyticsPanelRef = ref<InstanceType<typeof AnalyticsDashboardPanel> | null>(null)

function parseQueryInt(val: unknown): number | undefined {
  if (val === undefined || val === null || val === '') return undefined
  const n = Number(val)
  return Number.isFinite(n) ? n : undefined
}

const filters = reactive<AnalyticsFilters>({
  tramiteType:
    route.query.tramiteType === 'internship' ||
    route.query.tramiteType === 'social_service' ||
    route.query.tramiteType === 'both'
      ? (route.query.tramiteType as AnalyticsTramiteType)
      : defaultProcess.value,
  timeScope:
    route.query.timeScope === 'school_year' || route.query.timeScope === 'all'
      ? (route.query.timeScope as AnalyticsTimeScope)
      : 'period',
  periodId: parseQueryInt(route.query.periodId),
  comparePeriodId: parseQueryInt(route.query.comparePeriodId),
  schoolYear: typeof route.query.schoolYear === 'string' && route.query.schoolYear ? route.query.schoolYear : undefined,
  compareSchoolYear:
    typeof route.query.compareSchoolYear === 'string' && route.query.compareSchoolYear
      ? route.query.compareSchoolYear
      : undefined,
  statusGroup:
    route.query.statusGroup === 'all_non_cancelled' ||
    route.query.statusGroup === 'released' ||
    route.query.statusGroup === 'all'
      ? (route.query.statusGroup as AnalyticsStatusGroup)
      : 'in_progress',
  status: typeof route.query.status === 'string' && route.query.status ? route.query.status : undefined,
  careerId: parseQueryInt(route.query.careerId),
  semester: parseQueryInt(route.query.semester),
  companyId: parseQueryInt(route.query.companyId),
  activityTypeId: parseQueryInt(route.query.activityTypeId),
  gender:
    route.query.gender === 'M' || route.query.gender === 'F' || route.query.gender === 'unspecified'
      ? route.query.gender
      : undefined,
})

const officialTramiteType = computed<'social_service' | 'internship'>(() =>
  filters.tramiteType === 'internship' ? 'internship' : 'social_service',
)

const processLabel = computed(() =>
  filters.tramiteType === 'both'
    ? 'Comparativa Servicio Social vs Prácticas Profesionales'
    : filters.tramiteType === 'internship'
      ? 'Prácticas Profesionales'
      : 'Servicio Social (480 h)',
)

const scopeLabel = computed(() => {
  if (filters.timeScope === 'all') return 'Histórico acumulado'
  if (filters.timeScope === 'school_year') {
    const sy = filters.schoolYear || analytics.value?.scope.schoolYear || 'Ciclo actual'
    return `Ciclo Escolar ${sy}`
  }
  return analytics.value?.scope.period?.code || 'Periodo Activo'
})

const statusLabels: Record<string, string> = {
  requested: 'Solicitado',
  approved: 'CP Emitida',
  accepted: 'Aceptado',
  active: 'Activo',
  completed_pending_review: 'Por Liberar',
  released: 'Liberado',
  non_compliant: 'PNC',
  cancelled: 'Cancelado',
}

interface ActiveChip {
  key: string
  label: string
  clear: () => void
}

const activeFilterChips = computed<ActiveChip[]>(() => {
  const chips: ActiveChip[] = []
  if (filters.careerId !== undefined) {
    const label = careerOptions.value.find((c) => c.id === filters.careerId)?.label ?? `Carrera #${filters.careerId}`
    chips.push({ key: 'career', label, clear: () => { filters.careerId = undefined } })
  }
  if (filters.semester !== undefined) {
    chips.push({ key: 'semester', label: `${filters.semester}° Semestre`, clear: () => { filters.semester = undefined } })
  }
  if (filters.activityTypeId !== undefined) {
    const label =
      filters.activityTypeId === 0
        ? 'Actividad: Sin clasificar'
        : activityTypeOptions.value.find((a) => a.id === filters.activityTypeId)?.label ?? `Actividad #${filters.activityTypeId}`
    chips.push({ key: 'activity', label, clear: () => { filters.activityTypeId = undefined } })
  }
  if (filters.companyId !== undefined) {
    const label =
      filters.companyId === 0
        ? 'Sin empresa asignada (PP)'
        : companyOptions.value.find((c) => c.id === filters.companyId)?.label ?? `Empresa #${filters.companyId}`
    chips.push({ key: 'company', label, clear: () => { filters.companyId = undefined } })
  }
  if (filters.gender) {
    const label = filters.gender === 'M' ? 'Hombres' : filters.gender === 'F' ? 'Mujeres' : 'Sexo no especificado'
    chips.push({ key: 'gender', label, clear: () => { filters.gender = undefined } })
  }
  if (filters.status) {
    chips.push({
      key: 'status',
      label: `Estatus: ${statusLabels[filters.status] || filters.status}`,
      clear: () => { filters.status = undefined },
    })
  }
  return chips
})

function resetDimensionalFilters(): void {
  filters.careerId = undefined
  filters.semester = undefined
  filters.activityTypeId = undefined
  filters.companyId = undefined
  filters.gender = undefined
  filters.status = undefined
}

function selectProcess(value: AnalyticsTramiteType): void {
  filters.tramiteType = value
  if (value === 'both' && activeTab.value !== 'analitico') {
    activeTab.value = 'analitico'
  }
}

function selectTimeScope(scope: AnalyticsTimeScope): void {
  filters.timeScope = scope
  if (scope === 'school_year' && !filters.schoolYear) {
    const activeSy = schoolYearOptions.value.find((s) => s.hasActivePeriod) ?? schoolYearOptions.value[0]
    filters.schoolYear = activeSy?.schoolYear
  }
}

function syncUrl(): void {
  const query: Record<string, string> = { tramiteType: filters.tramiteType, timeScope: filters.timeScope }
  if (filters.timeScope === 'period' && filters.periodId !== undefined) query.periodId = String(filters.periodId)
  if (filters.timeScope === 'period' && filters.comparePeriodId !== undefined) query.comparePeriodId = String(filters.comparePeriodId)
  if (filters.timeScope === 'school_year' && filters.schoolYear) query.schoolYear = filters.schoolYear
  if (filters.timeScope === 'school_year' && filters.compareSchoolYear) query.compareSchoolYear = filters.compareSchoolYear
  if (filters.statusGroup !== 'in_progress') query.statusGroup = filters.statusGroup
  if (filters.status) query.status = filters.status
  if (filters.careerId !== undefined) query.careerId = String(filters.careerId)
  if (filters.semester !== undefined) query.semester = String(filters.semester)
  if (filters.companyId !== undefined) query.companyId = String(filters.companyId)
  if (filters.activityTypeId !== undefined) query.activityTypeId = String(filters.activityTypeId)
  if (filters.gender) query.gender = filters.gender
  void router.replace({ query })
}

let sequence = 0
let controller: AbortController | undefined

async function loadData(): Promise<void> {
  controller?.abort()
  controller = new AbortController()
  const request = ++sequence
  isLoading.value = true
  errorMessage.value = ''

  try {
    const syParam = filters.timeScope === 'school_year' ? filters.schoolYear : undefined
    const cmpSyParam = filters.timeScope === 'school_year' ? filters.compareSchoolYear : undefined
    const pParam = filters.timeScope === 'period' ? filters.periodId : undefined
    const cmpPParam = filters.timeScope === 'period' ? filters.comparePeriodId : undefined

    const [aData, sData, rData] = await Promise.all([
      getAnalyticsReport({ ...filters }, controller.signal),
      getSemesterStatistics(pParam, officialTramiteType.value, cmpPParam, controller.signal, syParam, cmpSyParam),
      get911Report(pParam, officialTramiteType.value, cmpPParam, controller.signal, syParam),
    ])

    if (request !== sequence) return
    analytics.value = aData
    stats.value = sData
    rep911.value = rData
  } catch (err: any) {
    if (request !== sequence || err.code === 'ERR_CANCELED') return
    analytics.value = null
    stats.value = null
    rep911.value = null
    errorMessage.value = problemMessage(err, 'Error al cargar estadísticas institucionales.')
  } finally {
    if (request === sequence) isLoading.value = false
  }
}

watch(
  () => ({ ...filters }),
  () => {
    syncUrl()
    void loadData()
  },
  { deep: true },
)

watch(
  () => filters.tramiteType,
  async (nextType) => {
    activityTypeOptions.value = await getActivityTypeOptions(nextType).catch(() => [])
  },
)

// Gráfica de Tasa de Liberación en Tab 2 (Estadístico Oficial)
const releaseRateChartData = computed<ChartData<'bar'>>(() => {
  void dark.value
  const rows = stats.value?.byCareer ?? []
  return {
    labels: rows.map((r) => r.careerCode),
    datasets: [
      {
        label: '% Liberación',
        data: rows.map((r) => r.releaseRatePercentage),
        backgroundColor: rows.map((r) => (r.releaseRatePercentage >= 80 ? '#15803D' : '#1B396A')),
        borderRadius: 4,
      },
    ],
  }
})

const releaseRateChartOptions: ChartOptions<'bar'> = {
  indexAxis: 'y',
  scales: { x: { beginAtZero: true, max: 100 }, y: {} },
}

async function handleExportExcel(): Promise<void> {
  if (!analytics.value || isExporting.value) return
  isExporting.value = true
  try {
    await exportAnalyticsExcel(analytics.value, stats.value, rep911.value, { ...filters }, {
      processLabel: processLabel.value,
      scopeLabel: scopeLabel.value,
      activeFiltersSummary: activeFilterChips.value.map((c) => c.label).join(' · '),
    })
  } catch (err: any) {
    errorMessage.value = problemMessage(err, 'No se pudo generar el archivo Excel.')
  } finally {
    isExporting.value = false
  }
}

function handleExportPdf(): void {
  if (!analytics.value) return
  const images = analyticsPanelRef.value?.getChartImages() ?? []
  openAnalyticsPrintReport(analytics.value, images, {
    processLabel: processLabel.value,
    scopeLabel: scopeLabel.value,
    activeFiltersSummary: activeFilterChips.value.map((c) => c.label).join(' · '),
  })
}

function handleTrendSelect(payload: { key: string; isSchoolYear: boolean }): void {
  if (payload.isSchoolYear) {
    filters.timeScope = 'school_year'
    filters.schoolYear = payload.key
  } else {
    filters.timeScope = 'period'
    filters.periodId = Number(payload.key)
  }
}

onBeforeUnmount(() => {
  sequence++
  controller?.abort()
})

onMounted(async () => {
  const [periods, schoolYears, careers, companies, activities] = await Promise.all([
    getPeriodOptions().catch(() => []),
    getSchoolYearOptions().catch(() => []),
    getCareerOptions().catch(() => []),
    getCompanyOptions().catch(() => []),
    getActivityTypeOptions(filters.tramiteType).catch(() => []),
  ])
  periodOptions.value = periods
  schoolYearOptions.value = schoolYears
  careerOptions.value = careers
  companyOptions.value = companies
  activityTypeOptions.value = activities
  await loadData()
})

useRealtimeRefresh(
  (evt) => evt.tramiteType === filters.tramiteType || filters.tramiteType === 'both' || evt.eventType === 'tramite.updated',
  () => loadData(),
)
</script>

<template>
  <UiBasePage :fluid="true" class="analytics-page">
    <header class="exec-header">
      <div class="exec-header__left">
        <div class="exec-eyebrow">
          <span class="exec-badge">ESTADÍSTICA Y ANÁLISIS DE VINCULACIÓN</span>
          <span class="exec-period">· {{ processLabel }} · {{ scopeLabel }}</span>
        </div>
        <h1 class="exec-title">Centro Analítico, Estadístico Oficial y Formato 911.10</h1>
      </div>

      <div class="header-actions">
        <UiButton variant="secondary" size="sm" :disabled="isLoading" @click="loadData">
          Actualizar
        </UiButton>
        <UiButton
          variant="secondary"
          size="sm"
          :disabled="!analytics || isLoading || isExporting"
          @click="handleExportExcel"
        >
          {{ isExporting ? 'Exportando…' : 'Exportar Excel (.xlsx)' }}
        </UiButton>
        <UiButton
          variant="primary"
          size="sm"
          :disabled="!analytics || isLoading"
          @click="handleExportPdf"
        >
          Imprimir / PDF
        </UiButton>
      </div>
    </header>

    <UiAlert v-if="errorMessage" tone="danger">
      <div class="error-row">
        <span>{{ errorMessage }}</span>
        <UiButton variant="secondary" size="sm" @click="loadData">Reintentar</UiButton>
      </div>
    </UiAlert>

    <UiCard class="stats-shell">
      <!-- Fila 1 de filtros: Proceso, Alcance temporal, Periodo/Ciclo, Comparativa, Universo y Pestañas -->
      <div class="filters-row">
        <div class="filter-group">
          <span class="filter-label">Proceso</span>
          <div class="segmented-tabs" role="tablist" aria-label="Seleccionar proceso">
            <button
              type="button"
              class="seg-tab"
              :class="{ 'seg-tab--active': filters.tramiteType === 'social_service' }"
              @click="selectProcess('social_service')"
            >
              Servicio Social
            </button>
            <button
              type="button"
              class="seg-tab"
              :class="{ 'seg-tab--active': filters.tramiteType === 'internship' }"
              @click="selectProcess('internship')"
            >
              Prácticas Prof.
            </button>
            <button
              type="button"
              class="seg-tab"
              :class="{ 'seg-tab--active': filters.tramiteType === 'both' }"
              @click="selectProcess('both')"
            >
              Comparar SS vs PP
            </button>
          </div>
        </div>

        <div class="filter-group">
          <span class="filter-label">Agrupación temporal</span>
          <div class="segmented-tabs" role="tablist" aria-label="Seleccionar agrupación temporal">
            <button
              type="button"
              class="seg-tab"
              :class="{ 'seg-tab--active': filters.timeScope === 'period' }"
              @click="selectTimeScope('period')"
            >
              Por Periodo
            </button>
            <button
              type="button"
              class="seg-tab"
              :class="{ 'seg-tab--active': filters.timeScope === 'school_year' }"
              @click="selectTimeScope('school_year')"
            >
              Por Ciclo Escolar
            </button>
            <button
              type="button"
              class="seg-tab"
              :class="{ 'seg-tab--active': filters.timeScope === 'all' }"
              @click="selectTimeScope('all')"
            >
              Histórico
            </button>
          </div>
        </div>

        <div v-if="filters.timeScope === 'period'" class="select-box">
          <label class="filter-label" for="stats-period-select">Periodo Escolar</label>
          <select id="stats-period-select" v-model="filters.periodId" class="modern-select">
            <option :value="undefined">Periodo Activo</option>
            <option v-for="p in periodOptions" :key="p.id" :value="p.id">{{ p.label }}</option>
          </select>
        </div>

        <div v-if="filters.timeScope === 'period'" class="select-box">
          <label class="filter-label" for="stats-cmp-period">Comparar con</label>
          <select id="stats-cmp-period" v-model="filters.comparePeriodId" class="modern-select">
            <option :value="undefined">Sin comparación</option>
            <option v-for="p in periodOptions" :key="p.id" :value="p.id">{{ p.label }}</option>
          </select>
        </div>

        <div v-if="filters.timeScope === 'school_year'" class="select-box">
          <label class="filter-label" for="stats-sy-select">Ciclo Escolar</label>
          <select id="stats-sy-select" v-model="filters.schoolYear" class="modern-select">
            <option v-for="sy in schoolYearOptions" :key="sy.schoolYear" :value="sy.schoolYear">
              Ciclo {{ sy.schoolYear }} ({{ sy.periods.map((p) => p.code).join(' + ') }})
            </option>
          </select>
        </div>

        <div v-if="filters.timeScope === 'school_year'" class="select-box">
          <label class="filter-label" for="stats-cmp-sy">Comparar ciclo</label>
          <select id="stats-cmp-sy" v-model="filters.compareSchoolYear" class="modern-select">
            <option :value="undefined">Sin comparación</option>
            <option v-for="sy in schoolYearOptions" :key="sy.schoolYear" :value="sy.schoolYear">
              Ciclo {{ sy.schoolYear }}
            </option>
          </select>
        </div>

        <div class="select-box">
          <label class="filter-label" for="stats-universe">Universo</label>
          <select id="stats-universe" v-model="filters.statusGroup" class="modern-select">
            <option value="in_progress">En realización (activos)</option>
            <option value="all_non_cancelled">Todos (sin cancelados)</option>
            <option value="released">Solo liberados</option>
            <option value="all">Todos (incl. cancelados)</option>
          </select>
        </div>
      </div>

      <!-- Fila 2 de filtros dimensionales combinables -->
      <div class="filters-row filters-row--secondary">
        <div class="select-box">
          <label class="filter-label" for="f-career">Carrera</label>
          <select id="f-career" v-model="filters.careerId" class="modern-select">
            <option :value="undefined">Todas las carreras</option>
            <option v-for="c in careerOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
          </select>
        </div>

        <div class="select-box select-box--sm">
          <label class="filter-label" for="f-semester">Semestre</label>
          <select id="f-semester" v-model="filters.semester" class="modern-select">
            <option :value="undefined">Todos</option>
            <option v-for="n in 12" :key="n" :value="n">{{ n }}° semestre</option>
          </select>
        </div>

        <div class="select-box">
          <label class="filter-label" for="f-activity">Tipo de actividad</label>
          <select id="f-activity" v-model="filters.activityTypeId" class="modern-select">
            <option :value="undefined">Todas las actividades</option>
            <option :value="0">Sin clasificar</option>
            <option v-for="a in activityTypeOptions" :key="a.id" :value="a.id">{{ a.label }}</option>
          </select>
        </div>

        <div class="select-box">
          <label class="filter-label" for="f-company">Empresa / Dependencia</label>
          <select id="f-company" v-model="filters.companyId" class="modern-select">
            <option :value="undefined">Todas las empresas</option>
            <option :value="0">Sin empresa asignada (PP)</option>
            <option v-for="c in companyOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
          </select>
        </div>

        <div class="select-box select-box--sm">
          <label class="filter-label" for="f-gender">Sexo</label>
          <select id="f-gender" v-model="filters.gender" class="modern-select">
            <option :value="undefined">Todos</option>
            <option value="M">Hombres</option>
            <option value="F">Mujeres</option>
            <option value="unspecified">No especificado</option>
          </select>
        </div>

        <div class="filter-group filter-group--right">
          <span class="filter-label">Sección</span>
          <div class="segmented-tabs" role="tablist" aria-label="Seleccionar sección de reporte">
            <button
              type="button"
              class="seg-tab"
              :class="{ 'seg-tab--active': activeTab === 'analitico' }"
              @click="activeTab = 'analitico'"
            >
              Resumen Analítico
            </button>
            <button
              type="button"
              class="seg-tab"
              :disabled="filters.tramiteType === 'both'"
              :class="{ 'seg-tab--active': activeTab === 'estadistico' }"
              @click="activeTab = 'estadistico'"
            >
              Estadístico Oficial
            </button>
            <button
              type="button"
              class="seg-tab"
              :disabled="filters.tramiteType === 'both'"
              :class="{ 'seg-tab--active': activeTab === 'formato911' }"
              @click="activeTab = 'formato911'"
            >
              Formato 911.10
            </button>
          </div>
        </div>
      </div>

      <!-- Chips de filtros activos -->
      <div v-if="activeFilterChips.length > 0" class="active-chips-bar" aria-label="Filtros activos">
        <span class="chips-title">Filtros activos:</span>
        <button
          v-for="chip in activeFilterChips"
          :key="chip.key"
          type="button"
          class="active-chip"
          @click="chip.clear()"
        >
          {{ chip.label }} <span aria-hidden="true">×</span>
        </button>
        <button type="button" class="clear-all-link" @click="resetDimensionalFilters">
          Restablecer filtros
        </button>
      </div>

      <div v-if="isLoading" class="loading-state" role="status">
        Calculando indicadores en tiempo real…
      </div>

      <template v-else-if="analytics">
        <!-- Tab 1: Resumen Analítico Interactivo -->
        <AnalyticsDashboardPanel
          v-if="activeTab === 'analitico'"
          ref="analyticsPanelRef"
          :analytics="analytics"
          :filters="filters"
          @toggle-career="(id) => (filters.careerId = filters.careerId === id ? undefined : id)"
          @toggle-semester="(sem) => (filters.semester = filters.semester === sem ? undefined : sem)"
          @toggle-activity="(id) => (filters.activityTypeId = filters.activityTypeId === id ? undefined : id)"
          @toggle-company="(id) => (filters.companyId = filters.companyId === id ? undefined : id)"
          @toggle-status="(st) => (filters.status = filters.status === st ? undefined : st)"
          @toggle-gender="(g) => (filters.gender = filters.gender === g ? undefined : g)"
          @select-trend="handleTrendSelect"
        />

        <!-- Tab 2: Estadístico Oficial (Hoja ESTADÍSTICO + Ciclo Escolar) -->
        <div v-else-if="activeTab === 'estadistico' && stats" class="official-tab">
          <div v-if="stats.schoolYearComparison" class="comparison-banner">
            <strong>Consolidado comparativo ({{ stats.schoolYearComparison.firstPeriodCode }} + {{ stats.schoolYearComparison.secondPeriodCode }} · deduplicado):</strong>
            <span>En condiciones: <strong>{{ stats.schoolYearComparison.totalEligible }}</strong></span>
            <span>Solicitantes: <strong>{{ stats.schoolYearComparison.totalApplicants }}</strong></span>
            <span>Liberados: <strong>{{ stats.schoolYearComparison.totalReleased }}</strong></span>
            <span>PNC: <strong>{{ stats.schoolYearComparison.totalPnc }}</strong></span>
            <UiBadge tone="success">{{ stats.schoolYearComparison.schoolYearReleaseRatePercentage }}% Liberación</UiBadge>
          </div>

          <div class="table-scroll-wrap" role="region" aria-label="Hoja Estadístico por Carrera">
            <table class="stats-table">
              <thead>
                <tr>
                  <th>Carrera</th>
                  <th class="th-center">En Condiciones (C.P.)</th>
                  <th class="th-center">Solic. H</th>
                  <th class="th-center">Solic. M</th>
                  <th class="th-center">Total Solic.</th>
                  <th class="th-center">Lib. H</th>
                  <th class="th-center">Lib. M</th>
                  <th class="th-center">Total Lib.</th>
                  <th class="th-center">PNC</th>
                  <th class="th-center">% Liberación</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in stats.byCareer" :key="row.careerCode" class="stats-tr">
                  <td>
                    <span class="code-pill">{{ row.careerCode }}</span>
                    <span class="career-name">{{ row.careerName }}</span>
                  </td>
                  <td class="td-center num">{{ row.eligibleWithPresentationLetter }}</td>
                  <td class="td-center num">{{ row.applicantsMale }}</td>
                  <td class="td-center num">{{ row.applicantsFemale }}</td>
                  <td class="td-center num strong">{{ row.applicantsTotal }}</td>
                  <td class="td-center num">{{ row.releasedMale }}</td>
                  <td class="td-center num">{{ row.releasedFemale }}</td>
                  <td class="td-center num released-num">{{ row.releasedTotal }}</td>
                  <td class="td-center num pnc-num">{{ row.pncTotal }}</td>
                  <td class="td-center">
                    <UiBadge :tone="row.releaseRatePercentage >= 80 ? 'success' : 'info'">
                      {{ row.releaseRatePercentage }}%
                    </UiBadge>
                  </td>
                </tr>
                <tr class="totals-row">
                  <td><strong>TOTAL INSTITUCIONAL</strong></td>
                  <td class="td-center num">{{ stats.totals.eligibleWithPresentationLetter }}</td>
                  <td class="td-center num">{{ stats.totals.applicantsMale }}</td>
                  <td class="td-center num">{{ stats.totals.applicantsFemale }}</td>
                  <td class="td-center num">{{ stats.totals.applicantsTotal }}</td>
                  <td class="td-center num">{{ stats.totals.releasedMale }}</td>
                  <td class="td-center num">{{ stats.totals.releasedFemale }}</td>
                  <td class="td-center num released-num">{{ stats.totals.releasedTotal }}</td>
                  <td class="td-center num pnc-num">{{ stats.totals.pncTotal }}</td>
                  <td class="td-center num">{{ stats.totals.releaseRatePercentage }}%</td>
                </tr>
              </tbody>
            </table>
          </div>

          <UiChart
            type="bar"
            title="Tasa de Liberación por Carrera (%)"
            subtitle="Porcentaje de solicitantes liberados (verde ≥ 80%)"
            :data="releaseRateChartData"
            :options="releaseRateChartOptions"
            :height="220"
            :interactive="false"
          />
        </div>

        <!-- Tab 3: Formato Oficial SEP/TecNM 911.10 -->
        <Report911Panel
          v-else-if="activeTab === 'formato911' && rep911"
          :rep911="rep911"
          :tramite-type="officialTramiteType"
        />
      </template>
    </UiCard>
  </UiBasePage>
</template>

<style scoped>
.analytics-page {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
  padding: 14px 22px 24px !important;
}

.exec-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.85rem;
  flex-wrap: wrap;
}

.exec-eyebrow {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  flex-wrap: wrap;
  margin-bottom: 0.2rem;
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
  font-size: 0.76rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.exec-title {
  font-size: 1.24rem;
  font-weight: 800;
  margin: 0;
  color: var(--color-text-primary);
}

.header-actions {
  display: flex;
  gap: 0.45rem;
  flex-wrap: wrap;
}

.error-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.stats-shell {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
  padding: 14px 16px !important;
}

.filters-row {
  display: flex;
  gap: 0.65rem;
  flex-wrap: wrap;
  align-items: flex-end;
  padding: 0.6rem 0.75rem;
  border-radius: 8px;
  background: var(--surface-toolbar);
  border: 1px solid var(--color-border);
}

.filters-row--secondary {
  background: var(--color-bg-light);
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.filter-group--right {
  margin-left: auto;
}

.filter-label {
  font-size: 0.72rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.segmented-tabs {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  padding: 3px;
  height: 34px;
  border-radius: 7px;
  background: var(--input-bg);
  border: 1px solid var(--input-border);
  overflow-x: auto;
  max-width: 100%;
}

.seg-tab {
  height: 26px;
  padding: 0 10px;
  border: 1px solid transparent;
  border-radius: 5px;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 0.75rem;
  font-weight: 600;
  cursor: pointer;
  white-space: nowrap;
}

.seg-tab:hover:not(:disabled) {
  color: var(--color-text-primary);
  background: var(--secondary-hover);
}

.seg-tab:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.seg-tab--active {
  background: var(--color-primary);
  color: var(--color-white);
  border-color: var(--color-accent);
}

.select-box {
  min-width: 170px;
  flex: 1 1 170px;
  max-width: 240px;
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.select-box--sm {
  min-width: 130px;
  max-width: 165px;
}

select.modern-select {
  min-height: 34px !important;
  height: 34px;
  padding: 0 0.65rem !important;
  font-size: 0.8rem !important;
  border-radius: 7px;
}

.active-chips-bar {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  flex-wrap: wrap;
}

.chips-title {
  font-size: 0.75rem;
  font-weight: 700;
  color: var(--color-text-secondary);
}

.active-chip {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.22rem 0.6rem;
  border-radius: 99px;
  border: 1px solid var(--color-accent);
  background: var(--color-accent-light);
  color: var(--color-text-primary);
  font-size: 0.74rem;
  font-weight: 600;
  cursor: pointer;
}

.clear-all-link {
  background: none;
  border: none;
  color: var(--color-info);
  font-size: 0.76rem;
  font-weight: 700;
  cursor: pointer;
  padding: 0.2rem 0.4rem;
}

.loading-state {
  padding: 2.5rem 1rem;
  text-align: center;
  color: var(--color-text-secondary);
}

.official-tab {
  display: flex;
  flex-direction: column;
  gap: 0.9rem;
}

.comparison-banner {
  display: flex;
  gap: 0.9rem;
  align-items: center;
  flex-wrap: wrap;
  padding: 0.7rem 0.95rem;
  border-radius: 8px;
  background: var(--color-accent-light);
  border: 1px solid var(--color-accent);
  font-size: 0.82rem;
  color: var(--color-text-primary);
}

.table-scroll-wrap {
  overflow: auto;
  border: 1px solid var(--color-border);
  border-radius: 8px;
}

.stats-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.8rem;
}

.stats-table th {
  padding: 8px;
  font-size: 0.68rem;
  font-weight: 700;
  text-transform: uppercase;
  background: var(--surface-table-head);
  color: var(--color-text-secondary);
  border-bottom: 1px solid var(--color-border-medium);
  text-align: left;
}

.stats-table td {
  padding: 8px;
  border-bottom: 1px solid var(--color-border-light);
}

.code-pill {
  display: inline-block;
  margin-right: 0.45rem;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.72rem;
  font-weight: 800;
  padding: 0.1rem 0.42rem;
  border-radius: 4px;
  background: var(--color-info-badge);
  color: var(--color-info-text);
  border: 1px solid var(--color-info-border);
}

.career-name {
  font-weight: 600;
}

.th-center,
.td-center {
  text-align: center !important;
}

.num {
  font-variant-numeric: tabular-nums;
}

.strong {
  font-weight: 700;
}

.released-num {
  font-weight: 700;
  color: var(--color-success-text);
}

.pnc-num {
  font-weight: 700;
  color: var(--color-warning-text);
}

.totals-row td {
  background: var(--surface-toolbar);
  border-top: 2px solid var(--color-border-medium);
  font-weight: 700;
}

@media (max-width: 900px) {
  .analytics-page {
    padding: 12px !important;
  }

  .filter-group--right {
    margin-left: 0;
  }

  .select-box,
  .select-box--sm {
    max-width: none;
  }
}
</style>
