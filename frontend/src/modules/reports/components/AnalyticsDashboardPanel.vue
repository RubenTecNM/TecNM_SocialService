<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import type { ChartData, ChartOptions } from 'chart.js'
import { UiAlert, UiBadge, UiButton, UiChart } from '../../../shared/components'
import type { UiChartTableRow } from '../../../shared/components/UiChart.vue'
import { useTheme } from '../../../shared/composables/useTheme'
import { problemMessage } from '../../../shared/api/problemDetails'
import { companyTypeLabels, type CompanyType } from '../../companies/types'
import { getAnalyticsCompanies } from '../api/reportsApi'
import type {
  AnalyticsFilters,
  AnalyticsReportResponse,
  CompanyAnalyticsRow,
} from '../types/reports'

const props = defineProps<{
  analytics: AnalyticsReportResponse
  filters: AnalyticsFilters
}>()

const emit = defineEmits<{
  (e: 'toggle-career', careerId: number): void
  (e: 'toggle-semester', semester: number): void
  (e: 'toggle-activity', activityTypeId: number): void
  (e: 'toggle-company', companyId: number): void
  (e: 'toggle-status', status: string): void
  (e: 'toggle-gender', gender: 'M' | 'F' | 'unspecified'): void
  (e: 'select-trend', payload: { key: string; isSchoolYear: boolean }): void
}>()

const { dark } = useTheme()

const careerChartMode = ref<'gender' | 'status'>('gender')
const careerChartRef = ref<InstanceType<typeof UiChart> | null>(null)
const semesterChartRef = ref<InstanceType<typeof UiChart> | null>(null)
const activityChartRef = ref<InstanceType<typeof UiChart> | null>(null)
const companyChartRef = ref<InstanceType<typeof UiChart> | null>(null)
const trendChartRef = ref<InstanceType<typeof UiChart> | null>(null)

const isCompareMode = computed(() => props.filters.tramiteType === 'both')

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

const sectorLabels: Record<string, string> = {
  educational: 'Educativo',
  public: 'Público',
  private: 'Privado',
  social: 'Social',
}

function token(name: string, fallback: string): string {
  if (typeof window === 'undefined') return fallback
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback
}

const palette = computed(() => {
  void dark.value
  return {
    male: dark.value ? token('--color-info', '#6CA0D8') : token('--color-primary', '#1B396A'),
    female: token('--color-accent', '#C5A059'),
    unspecified: token('--color-gray-400', '#94A3B8'),
    ss: dark.value ? token('--color-info', '#6CA0D8') : token('--color-primary', '#1B396A'),
    pp: token('--color-purple', '#8B5CF6'),
    inProgress: token('--color-info', '#3B82F6'),
    released: dark.value ? token('--color-success', '#52B788') : token('--color-success-strong', '#15803D'),
    pnc: token('--color-danger', '#EF4444'),
    warning: token('--color-warning', '#F59E0B'),
  }
})

// --- Sexo compacto en KPI ---
const genderTotal = computed(() => {
  const g = props.analytics.summary.byGender
  return g.male + g.female + g.unspecified
})
const malePct = computed(() => (genderTotal.value ? Math.round((props.analytics.summary.byGender.male * 100) / genderTotal.value) : 0))
const femalePct = computed(() => (genderTotal.value ? Math.round((props.analytics.summary.byGender.female * 100) / genderTotal.value) : 0))

const topActivityName = computed(
  () => props.analytics.byActivityType.find((a) => a.activityTypeId !== null)?.name ?? 'Sin clasificar',
)

// --- Embudo de estatus ---
const funnelTotal = computed(() => props.analytics.byStatus.reduce((acc, s) => acc + s.count, 0))

// --- Gráfica 1: Carrera × Sexo o Estatus ---
const careerChartData = computed<ChartData<'bar'>>(() => {
  const rows = props.analytics.byCareer
  const labels = rows.map((r) => `${r.careerCode}${r.isActive ? '' : ' (Inact.)'}`)
  if (isCompareMode.value) {
    return {
      labels,
      datasets: [
        { label: 'Servicio Social', data: rows.map((r) => r.socialServiceCount), backgroundColor: palette.value.ss, borderRadius: 4 },
        { label: 'Prácticas Prof.', data: rows.map((r) => r.internshipCount), backgroundColor: palette.value.pp, borderRadius: 4 },
      ],
    }
  }
  if (careerChartMode.value === 'status') {
    return {
      labels,
      datasets: [
        { label: 'En realización', data: rows.map((r) => r.inProgress), backgroundColor: palette.value.inProgress, borderRadius: 4 },
        { label: 'Liberados', data: rows.map((r) => r.released), backgroundColor: palette.value.released, borderRadius: 4 },
        { label: 'PNC', data: rows.map((r) => r.pnc), backgroundColor: palette.value.pnc, borderRadius: 4 },
      ],
    }
  }
  const datasets: ChartData<'bar'>['datasets'] = [
    { label: 'Hombres', data: rows.map((r) => r.male), backgroundColor: palette.value.male, borderRadius: 4 },
    { label: 'Mujeres', data: rows.map((r) => r.female), backgroundColor: palette.value.female, borderRadius: 4 },
  ]
  if (rows.some((r) => r.unspecified > 0)) {
    datasets.push({ label: 'No especificado', data: rows.map((r) => r.unspecified), backgroundColor: palette.value.unspecified, borderRadius: 4 })
  }
  return { labels, datasets }
})

const horizontalStackedOptions = computed<ChartOptions<'bar'>>(() => ({
  indexAxis: 'y',
  scales: {
    x: { stacked: !isCompareMode.value, beginAtZero: true },
    y: { stacked: !isCompareMode.value },
  },
}))

const careerTableRows = computed<UiChartTableRow[]>(() =>
  props.analytics.byCareer.map((r) => ({
    label: `${r.careerCode} · ${r.careerName}`,
    values: isCompareMode.value
      ? [r.socialServiceCount, r.internshipCount, r.total]
      : [r.male, r.female, r.unspecified, r.total],
  })),
)

// --- Gráfica 2: Semestre Académico ---
const semesterChartData = computed<ChartData<'bar'>>(() => {
  const rows = props.analytics.bySemester
  const labels = rows.map((r) => `${r.semester}°`)
  if (isCompareMode.value) {
    return {
      labels,
      datasets: [
        { label: 'Servicio Social', data: rows.map((r) => r.socialServiceCount), backgroundColor: palette.value.ss, borderRadius: 4 },
        { label: 'Prácticas Prof.', data: rows.map((r) => r.internshipCount), backgroundColor: palette.value.pp, borderRadius: 4 },
      ],
    }
  }
  const datasets: ChartData<'bar'>['datasets'] = [
    { label: 'Hombres', data: rows.map((r) => r.male), backgroundColor: palette.value.male, borderRadius: 4 },
    { label: 'Mujeres', data: rows.map((r) => r.female), backgroundColor: palette.value.female, borderRadius: 4 },
  ]
  if (rows.some((r) => r.unspecified > 0)) {
    datasets.push({ label: 'No especificado', data: rows.map((r) => r.unspecified), backgroundColor: palette.value.unspecified, borderRadius: 4 })
  }
  return { labels, datasets }
})

const verticalStackedOptions = computed<ChartOptions<'bar'>>(() => ({
  scales: {
    x: { stacked: !isCompareMode.value },
    y: { stacked: !isCompareMode.value, beginAtZero: true },
  },
}))

const semesterTableRows = computed<UiChartTableRow[]>(() =>
  props.analytics.bySemester.map((r) => ({
    label: `${r.semester}° semestre`,
    values: isCompareMode.value
      ? [r.socialServiceCount, r.internshipCount, r.total]
      : [r.male, r.female, r.unspecified, r.total],
  })),
)

// --- Gráfica 3: Tipo de Actividad ---
const activityChartData = computed<ChartData<'bar'>>(() => {
  const rows = props.analytics.byActivityType
  const labels = rows.map((r) => (r.name.length > 34 ? `${r.name.slice(0, 32)}…` : r.name))
  if (isCompareMode.value) {
    return {
      labels,
      datasets: [
        { label: 'Servicio Social', data: rows.map((r) => r.socialServiceCount), backgroundColor: palette.value.ss, borderRadius: 4 },
        { label: 'Prácticas Prof.', data: rows.map((r) => r.internshipCount), backgroundColor: palette.value.pp, borderRadius: 4 },
      ],
    }
  }
  return {
    labels,
    datasets: [
      {
        label: 'Estudiantes',
        data: rows.map((r) => r.total),
        backgroundColor: rows.map((r) => (r.activityTypeId === null ? palette.value.unspecified : palette.value.ss)),
        borderRadius: 4,
      },
    ],
  }
})

const horizontalSimpleOptions = computed<ChartOptions<'bar'>>(() => ({
  indexAxis: 'y',
  scales: { x: { beginAtZero: true }, y: {} },
}))

const activityTableRows = computed<UiChartTableRow[]>(() =>
  props.analytics.byActivityType.map((r) => ({
    label: r.name,
    values: [r.total, `${r.sharePercentage}%`, r.male, r.female],
  })),
)

// --- Gráfica 4: Top 10 Empresas / Dependencias ---
const companyChartData = computed<ChartData<'bar'>>(() => {
  const rows = props.analytics.topCompanies
  const labels = rows.map((r) => (r.companyName.length > 32 ? `${r.companyName.slice(0, 30)}…` : r.companyName))
  return {
    labels,
    datasets: [
      {
        label: 'Estudiantes',
        data: rows.map((r) => r.total),
        backgroundColor: rows.map((r) => (r.companyId === null ? palette.value.warning : palette.value.inProgress)),
        borderRadius: 4,
      },
    ],
  }
})

const companyTableRows = computed<UiChartTableRow[]>(() =>
  props.analytics.topCompanies.map((r) => ({
    label: r.companyName,
    values: [r.total, r.activeCount, r.releasedCount],
  })),
)

// --- Gráfica 5: Tendencia por Periodo o Ciclo Escolar ---
const trendChartType = computed<'bar' | 'line'>(() => (props.analytics.trend.length >= 4 ? 'line' : 'bar'))
const isSchoolYearTrend = computed(() => props.filters.timeScope !== 'period')

const trendChartData = computed<ChartData<'bar' | 'line'>>(() => {
  const rows = props.analytics.trend
  const labels = rows.map((r) => r.label)
  if (isCompareMode.value) {
    return {
      labels,
      datasets: [
        {
          label: 'Servicio Social',
          data: rows.map((r) => r.socialServiceCount),
          backgroundColor: palette.value.ss,
          borderColor: palette.value.ss,
          tension: 0.25,
          borderRadius: 4,
        },
        {
          label: 'Prácticas Prof.',
          data: rows.map((r) => r.internshipCount),
          backgroundColor: palette.value.pp,
          borderColor: palette.value.pp,
          tension: 0.25,
          borderRadius: 4,
        },
      ],
    }
  }
  return {
    labels,
    datasets: [
      {
        label: 'Total universo',
        data: rows.map((r) => r.total),
        backgroundColor: palette.value.ss,
        borderColor: palette.value.ss,
        tension: 0.25,
        borderRadius: 4,
      },
      {
        label: 'Liberados',
        data: rows.map((r) => r.releasedCount),
        backgroundColor: palette.value.released,
        borderColor: palette.value.released,
        borderDash: [4, 4],
        tension: 0.25,
        borderRadius: 4,
      },
    ],
  }
})

const trendTableRows = computed<UiChartTableRow[]>(() =>
  props.analytics.trend.map((r) => ({
    label: `${r.label} (${r.subLabel})`,
    values: isCompareMode.value
      ? [r.socialServiceCount, r.internshipCount, r.total]
      : [r.total, r.inProgressCount, r.releasedCount],
  })),
)

// --- Tabla completa paginada de Empresas Receptoras ---
const companySearch = ref('')
const companySort = ref('students_desc')
const companyPage = ref(1)
const companiesLoading = ref(false)
const companiesError = ref('')
const companiesRows = ref<CompanyAnalyticsRow[]>([])
const companiesTotalItems = ref(0)
const companiesTotalPages = ref(1)
let compSeq = 0
let compAbort: AbortController | undefined

async function loadCompaniesTable(): Promise<void> {
  compAbort?.abort()
  compAbort = new AbortController()
  const req = ++compSeq
  companiesLoading.value = true
  companiesError.value = ''
  try {
    const res = await getAnalyticsCompanies(
      props.filters,
      { search: companySearch.value, sort: companySort.value, page: companyPage.value, pageSize: 10 },
      compAbort.signal,
    )
    if (req !== compSeq) return
    companiesRows.value = res.items
    companiesTotalItems.value = res.totalItems
    companiesTotalPages.value = Math.max(res.totalPages, 1)
  } catch (e: any) {
    if (req === compSeq && e.code !== 'ERR_CANCELED') {
      companiesError.value = problemMessage(e, 'No se pudo cargar el padrón filtrado de empresas.')
    }
  } finally {
    if (req === compSeq) companiesLoading.value = false
  }
}

watch(
  () => [props.filters, companySort.value],
  () => {
    companyPage.value = 1
    void loadCompaniesTable()
  },
  { deep: true, immediate: true },
)

onBeforeUnmount(() => {
  compSeq++
  compAbort?.abort()
})

function onCareerSelect(payload: { index: number }): void {
  const row = props.analytics.byCareer[payload.index]
  if (row) emit('toggle-career', row.careerId)
}

function onSemesterSelect(payload: { index: number }): void {
  const row = props.analytics.bySemester[payload.index]
  if (row) emit('toggle-semester', row.semester)
}

function onActivitySelect(payload: { index: number }): void {
  const row = props.analytics.byActivityType[payload.index]
  if (row) emit('toggle-activity', row.activityTypeId ?? 0)
}

function onCompanySelect(payload: { index: number }): void {
  const row = props.analytics.topCompanies[payload.index]
  if (row) emit('toggle-company', row.companyId ?? 0)
}

function onTrendSelect(payload: { index: number }): void {
  const row = props.analytics.trend[payload.index]
  if (row) emit('select-trend', { key: row.key, isSchoolYear: isSchoolYearTrend.value })
}

function getChartImages(): { title: string; dataUrl: string }[] {
  const candidates: [string, InstanceType<typeof UiChart> | null][] = [
    ['Estudiantes por Carrera', careerChartRef.value],
    ['Distribución por Semestre Académico', semesterChartRef.value],
    ['Distribución por Tipo de Actividad', activityChartRef.value],
    ['Top 10 Empresas / Dependencias Receptoras', companyChartRef.value],
    [isSchoolYearTrend.value ? 'Evolución por Ciclo Escolar' : 'Evolución por Periodo Escolar', trendChartRef.value],
  ]
  return candidates
    .map(([title, refComp]) => ({ title, dataUrl: refComp?.toDataUrl() ?? '' }))
    .filter((item) => Boolean(item.dataUrl))
}

defineExpose({ getChartImages })
</script>

<template>
  <div class="analytics-root">
    <!-- 5 KPIs ejecutivos -->
    <section class="kpi-strip" aria-label="Indicadores clave del universo filtrado">
      <article class="kpi-card">
        <span class="kpi-label">Estudiantes únicos</span>
        <div class="kpi-main-row">
          <strong class="kpi-val">{{ analytics.summary.uniqueStudents }}</strong>
          <UiBadge
            v-if="analytics.summary.comparison"
            :tone="analytics.summary.comparison.deltaStudents >= 0 ? 'success' : 'warning'"
          >
            {{ analytics.summary.comparison.deltaStudents >= 0 ? '+' : '' }}{{ analytics.summary.comparison.deltaStudents }}
            <template v-if="analytics.summary.comparison.deltaPercentage !== null">
              ({{ analytics.summary.comparison.deltaPercentage >= 0 ? '+' : '' }}{{ analytics.summary.comparison.deltaPercentage }}%)
            </template>
          </UiBadge>
        </div>
        <small class="kpi-foot">
          <template v-if="isCompareMode">
            SS: {{ analytics.summary.socialServiceStudents }} · PP: {{ analytics.summary.internshipStudents }}
          </template>
          <template v-else-if="analytics.summary.totalTramites > analytics.summary.uniqueStudents">
            {{ analytics.summary.totalTramites }} registros en el alcance (deduplicado)
          </template>
          <template v-else-if="analytics.summary.comparison">
            vs {{ analytics.summary.comparison.previousLabel }} ({{ analytics.summary.comparison.previousUniqueStudents }})
          </template>
          <template v-else>
            Periodos: {{ analytics.scope.includedPeriodCodes.join(' + ') || 'Todos' }}
          </template>
        </small>
      </article>

      <article class="kpi-card kpi-card--info">
        <span class="kpi-label">En realización</span>
        <strong class="kpi-val">{{ analytics.summary.inProgressCount }}</strong>
        <small class="kpi-foot">Activos en ejecución: {{ analytics.summary.activeCount }}</small>
      </article>

      <article class="kpi-card kpi-card--success">
        <span class="kpi-label">Liberados</span>
        <div class="kpi-main-row">
          <strong class="kpi-val">{{ analytics.summary.releasedCount }}</strong>
          <UiBadge tone="success">{{ analytics.summary.releaseRatePercentage }}%</UiBadge>
        </div>
        <small class="kpi-foot">PNC en universo: {{ analytics.summary.pncCount }}</small>
      </article>

      <article class="kpi-card">
        <span class="kpi-label">Distribución por sexo</span>
        <div class="gender-numbers">
          <button
            type="button"
            class="gender-pill"
            :class="{ 'gender-pill--active': filters.gender === 'M' }"
            @click="emit('toggle-gender', 'M')"
          >
            H: <strong>{{ analytics.summary.byGender.male }}</strong> ({{ malePct }}%)
          </button>
          <button
            type="button"
            class="gender-pill gender-pill--female"
            :class="{ 'gender-pill--active': filters.gender === 'F' }"
            @click="emit('toggle-gender', 'F')"
          >
            M: <strong>{{ analytics.summary.byGender.female }}</strong> ({{ femalePct }}%)
          </button>
          <button
            v-if="analytics.summary.byGender.unspecified > 0"
            type="button"
            class="gender-pill gender-pill--muted"
            :class="{ 'gender-pill--active': filters.gender === 'unspecified' }"
            @click="emit('toggle-gender', 'unspecified')"
          >
            NE: <strong>{{ analytics.summary.byGender.unspecified }}</strong>
          </button>
        </div>
        <div class="gender-bar" aria-hidden="true">
          <span class="gender-bar__male" :style="{ width: `${malePct}%` }" />
          <span class="gender-bar__female" :style="{ width: `${femalePct}%` }" />
        </div>
      </article>

      <article class="kpi-card">
        <span class="kpi-label">Empresas receptoras</span>
        <strong class="kpi-val">{{ analytics.summary.distinctCompanies }}</strong>
        <small class="kpi-foot" :title="topActivityName">
          <template v-if="analytics.summary.unassignedCompanyCount > 0">
            {{ analytics.summary.unassignedCompanyCount }} sin empresa (PP)
          </template>
          <template v-else>
            Principal: {{ topActivityName.length > 26 ? `${topActivityName.slice(0, 24)}…` : topActivityName }}
          </template>
        </small>
      </article>
    </section>

    <!-- Embudo interactivo de estatus -->
    <section class="funnel-strip" aria-label="Estatus del trámite (clic para filtrar)">
      <div class="funnel-head">
        <span class="funnel-title">Distribución por estatus del proceso</span>
        <span class="funnel-hint">Haz clic en cualquier etapa para segmentar el tablero</span>
      </div>
      <div class="funnel-pills">
        <button
          v-for="st in analytics.byStatus"
          :key="st.status"
          type="button"
          class="status-pill"
          :class="{
            'status-pill--active': filters.status === st.status,
            'status-pill--empty': st.count === 0,
          }"
          @click="emit('toggle-status', st.status)"
        >
          <span class="status-pill__name">{{ statusLabels[st.status] || st.status }}</span>
          <strong class="status-pill__count">{{ st.count }}</strong>
          <small class="status-pill__pct">
            {{ funnelTotal > 0 ? Math.round((st.count * 100) / funnelTotal) : 0 }}%
          </small>
        </button>
      </div>
    </section>

    <!-- Gráficas Fila 1: Carrera × Sexo | Semestre Académico -->
    <div class="charts-grid-2">
      <UiChart
        ref="careerChartRef"
        type="bar"
        title="Estudiantes por Carrera"
        :subtitle="isCompareMode ? 'Comparativa Servicio Social vs Prácticas Profesionales · clic para filtrar' : 'Desglose interactivo · clic en barra para filtrar carrera'"
        :data="careerChartData"
        :options="horizontalStackedOptions"
        :height="270"
        :table-columns="isCompareMode ? ['Carrera', 'SS', 'PP', 'Total'] : ['Carrera', 'Hombres', 'Mujeres', 'NE', 'Total']"
        :table-rows="careerTableRows"
        @select="onCareerSelect"
      >
        <template v-if="!isCompareMode" #actions>
          <div class="mini-switch" role="group" aria-label="Agrupación por carrera">
            <button
              type="button"
              :class="{ active: careerChartMode === 'gender' }"
              @click="careerChartMode = 'gender'"
            >
              Sexo
            </button>
            <button
              type="button"
              :class="{ active: careerChartMode === 'status' }"
              @click="careerChartMode = 'status'"
            >
              Estatus
            </button>
          </div>
        </template>
      </UiChart>

      <UiChart
        ref="semesterChartRef"
        type="bar"
        title="Distribución por Semestre Académico"
        subtitle="Basado en el semestre cursado al inscribir el trámite · clic para filtrar"
        :data="semesterChartData"
        :options="verticalStackedOptions"
        :height="270"
        :table-columns="isCompareMode ? ['Semestre', 'SS', 'PP', 'Total'] : ['Semestre', 'Hombres', 'Mujeres', 'NE', 'Total']"
        :table-rows="semesterTableRows"
        @select="onSemesterSelect"
      />
    </div>

    <!-- Gráficas Fila 2: Tipo de Actividad | Top 10 Empresas -->
    <div class="charts-grid-2">
      <UiChart
        ref="activityChartRef"
        type="bar"
        title="Distribución por Tipo de Actividad"
        subtitle="Catálogo estandarizado de actividades · clic en barra para filtrar"
        :data="activityChartData"
        :options="horizontalSimpleOptions"
        :height="280"
        :table-columns="['Tipo de actividad', 'Total', '%', 'Hombres', 'Mujeres']"
        :table-rows="activityTableRows"
        @select="onActivitySelect"
      />

      <UiChart
        ref="companyChartRef"
        type="bar"
        title="Top 10 Empresas / Dependencias Receptoras"
        subtitle="Ordenadas por estudiantes únicos · clic en barra para filtrar"
        :data="companyChartData"
        :options="horizontalSimpleOptions"
        :height="280"
        :table-columns="['Empresa / Dependencia', 'Estudiantes', 'Activos', 'Liberados']"
        :table-rows="companyTableRows"
        @select="onCompanySelect"
      />
    </div>

    <!-- Gráfica Fila 3: Evolución Histórica por Periodo o Ciclo Escolar -->
    <UiChart
      ref="trendChartRef"
      :type="trendChartType"
      :title="isSchoolYearTrend ? 'Evolución Histórica por Ciclo Escolar (sin doble conteo)' : 'Evolución Histórica por Periodo Escolar'"
      :subtitle="isSchoolYearTrend ? 'Consolidación deduplicada por estudiante dentro de cada ciclo escolar · clic para seleccionar ciclo' : 'Estudiantes únicos por periodo escolar aplicando filtros activos · clic para seleccionar periodo'"
      :data="trendChartData"
      :height="245"
      :table-columns="isCompareMode ? ['Periodo / Ciclo', 'SS', 'PP', 'Total'] : ['Periodo / Ciclo', 'Total', 'En curso', 'Liberados']"
      :table-rows="trendTableRows"
      @select="onTrendSelect"
    />

    <!-- Tabla completa paginada y buscable de Empresas Receptoras -->
    <section class="companies-panel" aria-label="Padrón completo de empresas receptoras">
      <div class="companies-head">
        <div>
          <h3 class="companies-title">Padrón de Empresas y Dependencias Receptoras</h3>
          <p class="companies-sub">Haz clic en cualquier fila para filtrar el tablero por esa empresa</p>
        </div>
        <div class="companies-controls">
          <input
            v-model="companySearch"
            type="search"
            class="compact-input"
            placeholder="Buscar empresa o dependencia..."
            aria-label="Buscar empresa receptora"
            @keyup.enter="companyPage = 1; loadCompaniesTable()"
          />
          <select v-model="companySort" class="compact-select" aria-label="Ordenar empresas">
            <option value="students_desc">Mayor cantidad de estudiantes</option>
            <option value="active_desc">Más activos</option>
            <option value="released_desc">Más liberados</option>
            <option value="name_asc">Nombre (A–Z)</option>
          </select>
          <UiButton variant="secondary" size="sm" :disabled="companiesLoading" @click="companyPage = 1; loadCompaniesTable()">
            Buscar
          </UiButton>
        </div>
      </div>

      <UiAlert v-if="companiesError" tone="danger">{{ companiesError }}</UiAlert>

      <div class="companies-table-wrap">
        <table class="analytics-table">
          <thead>
            <tr>
              <th scope="col">Empresa / Dependencia</th>
              <th scope="col">Tipo</th>
              <th scope="col">Sector</th>
              <th scope="col">Actividad principal</th>
              <th scope="col" class="num-th">Estudiantes</th>
              <th scope="col" class="num-th">Activos</th>
              <th scope="col" class="num-th">Liberados</th>
              <th scope="col" class="num-th">H / M</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="companiesLoading">
              <td colspan="8" class="empty-td">Cargando empresas receptoras...</td>
            </tr>
            <tr v-else-if="companiesRows.length === 0">
              <td colspan="8" class="empty-td">No hay empresas que coincidan con los filtros actuales.</td>
            </tr>
            <tr
              v-for="row in companiesRows"
              :key="row.companyId ?? 0"
              class="clickable-row"
              :class="{ 'row-selected': filters.companyId === (row.companyId ?? 0) }"
              @click="emit('toggle-company', row.companyId ?? 0)"
            >
              <td class="strong-cell">{{ row.companyName }}</td>
              <td>
                <span class="meta-pill">
                  {{ row.companyType ? (companyTypeLabels[row.companyType as CompanyType] || row.companyType) : 'Pendiente' }}
                </span>
              </td>
              <td>{{ row.sector ? (sectorLabels[row.sector] || row.sector) : '—' }}</td>
              <td>{{ row.topActivityName || 'Sin clasificar' }}</td>
              <td class="num-td strong-cell">{{ row.total }}</td>
              <td class="num-td">{{ row.activeCount }}</td>
              <td class="num-td released-td">{{ row.releasedCount }}</td>
              <td class="num-td">{{ row.male }} / {{ row.female }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <footer class="companies-footer">
        <span class="page-info">
          Mostrando <strong>{{ companiesRows.length }}</strong> de <strong>{{ companiesTotalItems }}</strong> empresas ·
          Página <strong>{{ companyPage }}</strong> de <strong>{{ companiesTotalPages }}</strong>
        </span>
        <div class="page-btns">
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="companyPage <= 1 || companiesLoading"
            @click="companyPage--; loadCompaniesTable()"
          >
            Anterior
          </UiButton>
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="companyPage >= companiesTotalPages || companiesLoading"
            @click="companyPage++; loadCompaniesTable()"
          >
            Siguiente
          </UiButton>
        </div>
      </footer>
    </section>
  </div>
</template>

<style scoped>
.analytics-root {
  display: flex;
  flex-direction: column;
  gap: 0.95rem;
}

.kpi-strip {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
  gap: 0.7rem;
}

.kpi-card {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  padding: 0.8rem 0.95rem;
  border-radius: 10px;
  border: 1px solid var(--color-border);
  border-top: 3px solid var(--color-border-medium);
  background: var(--color-surface-white);
}

.kpi-card--info {
  border-top-color: var(--color-info);
}

.kpi-card--success {
  border-top-color: var(--color-success);
}

.kpi-label {
  font-size: 0.72rem;
  font-weight: 700;
  color: var(--color-text-secondary);
}

.kpi-main-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.kpi-val {
  font-size: 1.55rem;
  font-weight: 800;
  line-height: 1.15;
  color: var(--color-text-primary);
  font-variant-numeric: tabular-nums;
}

.kpi-foot {
  font-size: 0.74rem;
  color: var(--color-text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.gender-numbers {
  display: flex;
  gap: 0.35rem;
  flex-wrap: wrap;
}

.gender-pill {
  padding: 0.16rem 0.45rem;
  border-radius: 6px;
  border: 1px solid var(--color-border-medium);
  background: var(--surface-toolbar);
  color: var(--color-text-primary);
  font-size: 0.73rem;
  cursor: pointer;
}

.gender-pill--active {
  border-color: var(--color-accent);
  background: var(--color-accent-light);
}

.gender-bar {
  display: flex;
  height: 7px;
  border-radius: 99px;
  overflow: hidden;
  background: var(--color-border);
  margin-top: 0.15rem;
}

.gender-bar__male {
  background: var(--color-primary);
}

.gender-bar__female {
  background: var(--color-accent);
}

.funnel-strip {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.75rem 0.95rem;
  border-radius: 10px;
  border: 1px solid var(--color-border);
  background: var(--surface-toolbar);
}

.funnel-head {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.funnel-title {
  font-size: 0.8rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.funnel-hint {
  font-size: 0.72rem;
  color: var(--color-text-secondary);
}

.funnel-pills {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(112px, 1fr));
  gap: 0.45rem;
}

.status-pill {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.08rem;
  padding: 0.45rem 0.65rem;
  border-radius: 8px;
  border: 1px solid var(--color-border-medium);
  background: var(--color-surface-white);
  color: var(--color-text-primary);
  text-align: left;
  cursor: pointer;
  transition: border-color 140ms ease, background-color 140ms ease;
}

.status-pill:hover {
  border-color: var(--color-accent);
}

.status-pill--active {
  border-color: var(--color-accent);
  background: var(--kpi-active-bg);
  box-shadow: 0 0 0 1px var(--color-accent);
}

.status-pill--empty {
  opacity: 0.62;
}

.status-pill__name {
  font-size: 0.71rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.status-pill__count {
  font-size: 1.08rem;
  font-weight: 800;
  font-variant-numeric: tabular-nums;
}

.status-pill__pct {
  font-size: 0.69rem;
  color: var(--color-text-muted);
}

.charts-grid-2 {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.9rem;
}

.mini-switch {
  display: inline-flex;
  border-radius: 6px;
  border: 1px solid var(--color-border-medium);
  background: var(--surface-toolbar);
  padding: 2px;
  gap: 2px;
}

.mini-switch button {
  min-height: 24px;
  padding: 0 0.55rem;
  border: none;
  border-radius: 4px;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 0.72rem;
  font-weight: 600;
  cursor: pointer;
}

.mini-switch button.active {
  background: var(--color-primary);
  color: var(--color-white);
}

.companies-panel {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
  padding: 0.95rem 1.05rem;
  border-radius: 10px;
  border: 1px solid var(--color-border);
  background: var(--color-surface-white);
}

.companies-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.companies-title {
  margin: 0;
  font-size: 0.92rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.companies-sub {
  margin: 0.1rem 0 0;
  font-size: 0.74rem;
  color: var(--color-text-secondary);
}

.companies-controls {
  display: flex;
  gap: 0.45rem;
  flex-wrap: wrap;
  align-items: center;
}

.compact-input,
.compact-select {
  min-height: 34px !important;
  height: 34px;
  width: auto !important;
  padding: 0 0.65rem !important;
  font-size: 0.8rem !important;
}

.companies-table-wrap {
  overflow-x: auto;
  border: 1px solid var(--color-border);
  border-radius: 8px;
}

.analytics-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.8rem;
}

.analytics-table th {
  padding: 8px 10px;
  text-align: left;
  font-size: 0.69rem;
  font-weight: 700;
  text-transform: uppercase;
  background: var(--surface-table-head);
  color: var(--color-text-secondary);
  border-bottom: 1px solid var(--color-border-medium);
  white-space: nowrap;
}

.analytics-table td {
  padding: 8px 10px;
  border-bottom: 1px solid var(--color-border-light);
  color: var(--color-text-primary);
}

.clickable-row {
  cursor: pointer;
}

.clickable-row:hover {
  background: var(--color-primary-subtle);
}

.row-selected {
  background: var(--kpi-active-bg);
}

.num-th,
.num-td {
  text-align: center !important;
  font-variant-numeric: tabular-nums;
}

.strong-cell {
  font-weight: 700;
}

.released-td {
  color: var(--color-success-text);
  font-weight: 700;
}

.meta-pill {
  display: inline-block;
  padding: 0.1rem 0.45rem;
  border-radius: 5px;
  font-size: 0.72rem;
  font-weight: 600;
  background: var(--surface-toolbar);
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
}

.empty-td {
  text-align: center;
  padding: 1.4rem !important;
  color: var(--color-text-secondary);
}

.companies-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.page-info {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
}

.page-btns {
  display: flex;
  gap: 0.4rem;
}

@media (max-width: 1080px) {
  .charts-grid-2 {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .kpi-strip {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
