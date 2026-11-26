<script setup lang="ts">
import { problemMessage } from '../../../shared/api/problemDetails'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { defaultProcess } from '../../../shared/composables/useSession'
import { useRealtimeRefresh } from '../../../shared/composables/useRealtime'
import LinkageMetrics from '../components/LinkageMetrics.vue'
import { UiAlert, UiBadge, UiBasePage, UiButton, UiCard } from '../../../shared/components'
import { getPeriodOptions } from '../../catalogs/api/careersApi'
import type { OptionItem } from '../../catalogs/types/careers'
import { get911Report, getSemesterStatistics } from '../api/reportsApi'

const stats = ref<any>(null)
const rep911 = ref<any>(null)
const periodOptions = ref<OptionItem[]>([])
const selectedPeriodId = ref<number | undefined>(undefined)
const activeTab = ref<'estadistico' | 'formato911' | 'vinculacion'>('estadistico')

const route = useRoute()
const router = useRouter()

const tramiteType = computed<'social_service' | 'internship'>(() =>
  route.query.tramiteType === 'internship' || route.query.tramiteType === 'social_service'
    ? route.query.tramiteType
    : defaultProcess.value,
)

function selectProcess(value: 'social_service' | 'internship'): void {
  void router.replace({ query: { ...route.query, tramiteType: value } })
}

let sequence = 0
let controller: AbortController | undefined

watch(tramiteType, () => {
  void loadData()
})

onBeforeUnmount(() => {
  sequence++
  controller?.abort()
})

const isLoading = ref(true)
const errorMessage = ref('')

const sectorLabels: Record<string, string> = {
  educational: 'Educativo (Propia Institución TecNM)',
  public: 'Sector Público',
  private: 'Sector Privado',
  social: 'Sector Social',
}

async function loadData(): Promise<void> {
  controller?.abort()
  controller = new AbortController()
  const request = ++sequence
  isLoading.value = true
  errorMessage.value = ''
  stats.value = null
  rep911.value = null
  try {
    const [sData, rData] = await Promise.all([
      getSemesterStatistics(selectedPeriodId.value, tramiteType.value, undefined, controller.signal),
      get911Report(selectedPeriodId.value, tramiteType.value, undefined, controller.signal),
    ])
    if (request !== sequence) return
    stats.value = sData
    rep911.value = rData
  } catch (err: any) {
    if (request !== sequence || err.code === 'ERR_CANCELED') return
    errorMessage.value = problemMessage(err, 'Error al cargar estadísticas institucionales.')
  } finally {
    if (request === sequence) isLoading.value = false
  }
}

onMounted(async () => {
  periodOptions.value = await getPeriodOptions().catch(() => [])
  await loadData()
})

useRealtimeRefresh(
  (evt) => evt.tramiteType === tramiteType.value || evt.eventType === 'tramite.updated',
  () => loadData(),
)
</script>

<template>
  <UiBasePage :fluid="true" class="fullscreen-page">
    <!-- Encabezado Ejecutivo Compacto con 4 KPIs Superiores -->
    <header class="exec-header">
      <div class="exec-header__left">
        <div class="exec-eyebrow">
          <span class="exec-badge">ESTADÍSTICA INSTITUCIONAL Y FORMATO SEP/TecNM 911.10</span>
          <span class="exec-period">
            · {{
              tramiteType === 'social_service' ? 'Servicio Social (480 h)' : 'Prácticas Profesionales'
            }}
          </span>
        </div>
        <div class="title-row">
          <h1 class="exec-title">Estadístico Semestral y Reporte 911.10</h1>
          <UiButton variant="secondary" size="sm" :disabled="isLoading" @click="loadData">
            Actualizar
          </UiButton>
        </div>
      </div>

      <!-- 4 KPIs resumen del periodo -->
      <div class="exec-kpis" aria-label="Indicadores estadísticos del periodo">
        <div class="mini-kpi">
          <span class="mini-kpi__label">En Condiciones (C.P.)</span>
          <strong class="mini-kpi__value">
            {{ stats?.totals?.eligibleWithPresentationLetter ?? 0 }}
          </strong>
        </div>

        <div class="mini-kpi mini-kpi--navy">
          <span class="mini-kpi__label">Solicitantes (C.A.)</span>
          <strong class="mini-kpi__value">{{ stats?.totals?.applicantsTotal ?? 0 }}</strong>
        </div>

        <div class="mini-kpi mini-kpi--emerald">
          <span class="mini-kpi__label">Liberados</span>
          <strong class="mini-kpi__value">{{ stats?.totals?.releasedTotal ?? 0 }}</strong>
        </div>

        <div class="mini-kpi mini-kpi--gold">
          <span class="mini-kpi__label">% Liberación</span>
          <strong class="mini-kpi__value">
            {{ stats?.totals?.releaseRatePercentage ?? 0 }}%
          </strong>
        </div>
      </div>
    </header>

    <UiAlert v-if="errorMessage" tone="danger" class="compact-alert">
      <div class="error-row">
        <span>{{ errorMessage }}</span>
        <UiButton variant="secondary" size="sm" @click="loadData">Reintentar</UiButton>
      </div>
    </UiAlert>

    <UiCard class="stats-card">
      <!-- Barra de Herramientas -->
      <div class="filters-bar">
        <div class="filter-group">
          <span class="filter-label">Proceso académico</span>
          <div class="segmented-tabs" role="tablist" aria-label="Seleccionar proceso">
            <button
              type="button"
              class="seg-tab"
              :class="{ 'seg-tab--active': tramiteType === 'social_service' }"
              @click="selectProcess('social_service')"
            >
              Servicio Social
            </button>
            <button
              type="button"
              class="seg-tab"
              :class="{ 'seg-tab--active': tramiteType === 'internship' }"
              @click="selectProcess('internship')"
            >
              Prácticas Profesionales
            </button>
          </div>
        </div>

        <div class="select-box">
          <label class="filter-label" for="stats-period-select">Periodo Escolar</label>
          <div class="select-wrapper">
            <select
              id="stats-period-select"
              v-model="selectedPeriodId"
              class="modern-select"
              @change="loadData"
            >
              <option :value="undefined">Periodo Activo</option>
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

        <div class="filter-group filter-group--right">
          <span class="filter-label">Vista de reporte</span>
          <div class="segmented-tabs" role="tablist" aria-label="Seleccionar sección de reporte">
            <button
              type="button"
              class="seg-tab"
              :class="{ 'seg-tab--active': activeTab === 'estadistico' }"
              @click="activeTab = 'estadistico'"
            >
              Hoja ESTADÍSTICO
            </button>
            <button
              type="button"
              class="seg-tab"
              :class="{ 'seg-tab--active': activeTab === 'formato911' }"
              @click="activeTab = 'formato911'"
            >
              Formato 911.10 &amp; Inclusión
            </button>
            <button
              type="button"
              class="seg-tab"
              :class="{ 'seg-tab--active': activeTab === 'vinculacion' }"
              @click="activeTab = 'vinculacion'"
            >
              Vinculación por Empresa
            </button>
          </div>
        </div>
      </div>

      <!-- Contenido Principal con Scroll Interno -->
      <div v-if="isLoading" class="loading-state" role="status">
        Calculando indicadores en tiempo real...
      </div>

      <template v-else-if="stats && rep911">
        <!-- Tab 1: Hoja ESTADÍSTICO por Carrera -->
        <div
          v-if="activeTab === 'estadistico'"
          class="table-scroll-wrap"
          role="region"
          aria-label="Hoja Estadístico por Carrera"
        >
          <table class="stats-table">
            <thead>
              <tr>
                <th>CARRERA</th>
                <th class="th-center">EN CONDICIONES (C.P. = SI)</th>
                <th class="th-center">SOLIC. H</th>
                <th class="th-center">SOLIC. M</th>
                <th class="th-center">TOTAL SOLICITANTES</th>
                <th class="th-center">LIB. H</th>
                <th class="th-center">LIB. M</th>
                <th class="th-center">TOTAL LIBERADOS</th>
                <th class="th-center">PNC (SOLIC. - LIB.)</th>
                <th class="th-center">% LIBERACIÓN</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in stats?.byCareer || []" :key="row.careerCode" class="stats-tr">
                <td>
                  <div class="career-cell">
                    <span class="code-pill">{{ row.careerCode }}</span>
                    <span class="career-name">{{ row.careerName }}</span>
                  </div>
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
              <tr v-if="stats?.totals" class="totals-row">
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

        <!-- Tab 2: Formato Oficial 911.10 e Inclusión -->
        <div v-else-if="activeTab === 'formato911'" class="grid-911">
          <section class="panel-911">
            <div class="panel-head">
              <h2 class="panel-title">Formato 911.10 — Distribución por Sector</h2>
              <span class="panel-sub">Desglose oficial SEP / TecNM por género y sector</span>
            </div>
            <div class="panel-table-wrap">
              <table class="stats-table">
                <thead>
                  <tr>
                    <th>SECTOR</th>
                    <th class="th-center">HOMBRES</th>
                    <th class="th-center">MUJERES</th>
                    <th class="th-center">TOTAL</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="sec in rep911?.byCompanySector || []" :key="sec.sector" class="stats-tr">
                    <td class="sector-name">{{ sectorLabels[sec.sector] || sec.sector }}</td>
                    <td class="td-center num">{{ sec.male }}</td>
                    <td class="td-center num">{{ sec.female }}</td>
                    <td class="td-center num strong">{{ sec.total }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>

          <section class="panel-911">
            <div class="panel-head">
              <h2 class="panel-title">Indicadores de Impacto e Inclusión (911.10)</h2>
              <span class="panel-sub">Resumen de cobertura comunitaria y atención inclusiva</span>
            </div>
            <div class="impact-list">
              <div class="impact-row">
                <span class="impact-label">Estudiantes con Discapacidad Atendidos</span>
                <strong class="impact-value">{{ rep911?.withDisability?.total || 0 }}</strong>
              </div>
              <div v-if="tramiteType === 'social_service'" class="impact-row">
                <span class="impact-label">Trámites con Actividades de Inclusión</span>
                <strong class="impact-value">{{ rep911?.withInclusionActivities || 0 }}</strong>
              </div>
              <div v-if="tramiteType === 'social_service'" class="impact-row impact-row--highlight">
                <span class="impact-label">Total de Ciudadanos Beneficiados</span>
                <strong class="impact-value impact-value--accent">
                  {{ rep911?.totalBeneficiaries || 0 }} personas
                </strong>
              </div>
            </div>
          </section>
        </div>

        <!-- Tab 3: Vinculación por Empresa -->
        <LinkageMetrics
          v-else
          :tramite-type="tramiteType"
          :period-id="selectedPeriodId"
        />
      </template>
    </UiCard>
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
  font-size: 1.22rem;
  font-weight: 800;
  margin: 0;
  color: var(--color-text-primary);
  letter-spacing: -0.015em;
  line-height: 1.2;
}

/* 4 KPIs con superficie neutra */
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

.mini-kpi--navy .mini-kpi__value {
  color: var(--color-info);
}

.mini-kpi--emerald .mini-kpi__value {
  color: var(--color-success);
}

.mini-kpi--gold .mini-kpi__value {
  color: var(--color-warning);
}

.compact-alert {
  margin-bottom: 0.55rem;
  flex-shrink: 0;
}

.error-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.stats-card {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  padding: 12px 16px !important;
  border: 1px solid var(--color-border);
  background: var(--color-surface-white);
}

/* Barra de herramientas */
.filters-bar {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
  align-items: flex-end;
  margin-bottom: 0.75rem;
  padding: 0.55rem 0.75rem;
  border-radius: 8px;
  background: var(--surface-toolbar);
  border: 1px solid var(--color-border);
  flex-shrink: 0;
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
}

.seg-tab {
  min-height: 26px !important;
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
  transition: background-color 150ms ease, color 150ms ease, border-color 150ms ease;
}

.seg-tab:hover {
  color: var(--color-text-primary);
  background: var(--secondary-hover);
}

.seg-tab--active {
  background: var(--color-primary);
  color: var(--color-white);
  border-color: var(--color-accent);
}

.select-box {
  min-width: 190px;
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.select-wrapper {
  position: relative;
  display: flex;
  align-items: center;
  width: 100%;
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
  transition: border-color 160ms ease, box-shadow 160ms ease;
}

select.modern-select:hover {
  border-color: var(--color-gray-400) !important;
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

.loading-state {
  padding: 2.5rem 1rem;
  text-align: center;
  color: var(--color-text-secondary);
}

/* Tablas con scroll interno y paleta mate */
.table-scroll-wrap {
  flex: 1;
  min-height: 0;
  overflow: auto;
  margin: 0 -16px;
  border-top: 1px solid var(--color-border);
}

.stats-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  font-size: 0.8rem;
}

.stats-table th {
  padding: 7px 8px;
  font-size: 0.66rem;
  font-weight: 700;
  letter-spacing: 0.03em;
  text-transform: uppercase;
  background: var(--surface-table-head);
  color: var(--color-text-secondary);
  border-bottom: 1px solid var(--color-border-medium);
  position: sticky;
  top: 0;
  z-index: 1;
  white-space: normal;
  line-height: 1.25;
}

.stats-table td {
  padding: 7px 8px;
  border-bottom: 1px solid var(--color-border-light);
  vertical-align: middle;
  white-space: normal;
}

.stats-tr {
  transition: background-color 140ms ease;
}

.stats-tr:hover {
  background: var(--color-primary-subtle);
}

.career-cell {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.code-pill {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.72rem;
  font-weight: 800;
  padding: 0.1rem 0.42rem;
  border-radius: 4px;
  background: var(--color-info-badge);
  color: var(--color-info-text);
  border: 1px solid var(--color-info-border);
  flex-shrink: 0;
}

.career-name {
  font-weight: 600;
  color: var(--color-text-primary);
}

.th-center,
.td-center {
  text-align: center !important;
}

.num {
  font-variant-numeric: tabular-nums;
  color: var(--color-text-primary);
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
  color: var(--color-text-primary);
}

/* Sección Formato 911.10 */
.grid-911 {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(360px, 1fr));
  gap: 0.9rem;
  flex: 1;
  min-height: 0;
  overflow-y: auto;
}

.panel-911 {
  display: flex;
  flex-direction: column;
  padding: 0.85rem 1rem;
  border-radius: 9px;
  border: 1px solid var(--color-border);
  background: var(--surface-toolbar);
  min-height: 0;
}

.panel-head {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  padding-bottom: 0.6rem;
  margin-bottom: 0.7rem;
  border-bottom: 1px solid var(--color-border-light);
}

.panel-title {
  margin: 0;
  font-size: 0.92rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.panel-sub {
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.panel-table-wrap {
  border-radius: 7px;
  overflow: hidden;
  border: 1px solid var(--color-border);
  background: var(--color-surface-white);
}

.sector-name {
  font-weight: 600;
  color: var(--color-text-primary);
}

.impact-list {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
}

.impact-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 0.9rem;
  border-radius: 8px;
  background: var(--color-surface-white);
  border: 1px solid var(--color-border);
}

.impact-row--highlight {
  border-color: var(--color-accent);
}

.impact-label {
  font-size: 0.84rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.impact-value {
  font-size: 1rem;
  font-weight: 800;
  color: var(--color-text-primary);
  font-variant-numeric: tabular-nums;
}

.impact-value--accent {
  color: var(--color-info-text);
}

@media (max-width: 1100px) {
  .filter-group--right {
    margin-left: 0;
  }
}

@media (max-width: 900px) {
  .fullscreen-page {
    height: auto !important;
    overflow: visible !important;
    padding: 4px 0 12px !important;
    min-width: 0;
    max-width: 100%;
  }

  .stats-card {
    min-width: 0;
    max-width: 100%;
    overflow: hidden;
    padding: 10px 12px !important;
  }

  .table-scroll-wrap {
    margin: 0 -12px;
    max-width: calc(100% + 24px);
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
  }

  .table-scroll-wrap .stats-table {
    min-width: 680px;
  }

  .grid-911 {
    grid-template-columns: minmax(0, 1fr);
    overflow: visible;
  }

  .panel-table-wrap {
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

  .filter-group,
  .select-box {
    width: 100%;
    min-width: 0;
  }

  .segmented-tabs {
    display: flex;
    flex-wrap: wrap;
    width: 100%;
    max-width: 100%;
    height: auto;
    min-height: 36px;
    box-sizing: border-box;
  }

  .seg-tab {
    flex: 1 1 auto;
    min-width: 0;
    min-height: 30px !important;
    height: auto;
    padding: 4px 8px;
    white-space: normal;
    text-align: center;
  }

  select.modern-select {
    min-height: 40px;
    height: 40px;
    font-size: 16px;
  }

  .impact-row {
    flex-wrap: wrap;
    padding: 0.65rem 0.75rem;
  }
}
</style>
