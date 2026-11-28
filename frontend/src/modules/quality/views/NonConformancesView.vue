<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { problemMessage } from '../../../shared/api/problemDetails'
import {
  UiAlert,
  UiBadge,
  UiBasePage,
  UiButton,
  UiCard,
  UiDialog,
  UiField,
  UiFormErrors,
  UiToast,
} from '../../../shared/components'
import { hasRole } from '../../../shared/composables/useSession'
import { useRealtimeRefresh } from '../../../shared/composables/useRealtime'
import { useFormValidation } from '../../../shared/composables/useFormValidation'
import { validateFields } from '../../../shared/utils/validationRules'
import { getPeriodOptions } from '../../catalogs/api/careersApi'
import type { OptionItem } from '../../catalogs/types/careers'
import { getInternships } from '../../internships/api/internshipsApi'
import { getSocialServices } from '../../social-service/api/socialServiceApi'
import {
  createNonConformance,
  getNonConformanceById,
  getNonConformances,
  updateNonConformanceStatus,
} from '../api/qualityApi'
import type { NonConformance, NonConformanceStatus } from '../types/quality'

interface TramiteOption {
  id: number
  label: string
}

const CATEGORY_LABELS: Record<string, string> = {
  desertion: 'Deserción',
  delayed_reports: 'Reportes extemporáneos',
  company_dismissal: 'Baja por empresa',
  insufficient_grade: 'Calificación insuficiente',
  invalid_documents: 'Documentación inválida',
  other: 'Otro',
}

const STATUS_META: Record<
  NonConformanceStatus,
  { label: string; tone?: 'danger' | 'warning' | 'success' | 'info' }
> = {
  open: { label: 'Abierto', tone: 'danger' },
  under_review: { label: 'En revisión', tone: 'warning' },
  resolved: { label: 'Resuelto', tone: 'success' },
  cancelled: { label: 'Cancelado' },
}

const canWriteSocialService = computed(() => hasRole('department_head', 'social_service_officer'))
const canWriteInternship = computed(() => hasRole('department_head', 'internship_coordinator'))
const canCreateAny = computed(() => canWriteSocialService.value || canWriteInternship.value)

const items = ref<NonConformance[]>([])
const kpiPool = ref<NonConformance[]>([])
const periodOptions = ref<OptionItem[]>([])
const isLoading = ref(true)
const errorMessage = ref('')
const toast = ref<InstanceType<typeof UiToast> | null>(null)

// Paginación estandarizada (10 registros por página)
const page = ref(1)
const pageSize = 10
const totalItems = ref(0)
const totalPages = ref(1)

// Filtros y autocompletado con debounce (280 ms)
const searchQuery = ref('')
const selectedPeriodId = ref<string>('')
const selectedTramiteType = ref<string>('')
const selectedStatus = ref<string>('')
const selectedCategory = ref<string>('')
const autocompleteSuggestions = ref<NonConformance[]>([])
const isAutocompleteOpen = ref(false)

let debounceTimer: ReturnType<typeof setTimeout> | null = null
let loadRequestId = 0

// KPIs animados superiores
const animatedKpis = reactive({
  total: 0,
  open: 0,
  underReview: 0,
  resolved: 0,
})

function updateAnimatedKpis(): void {
  const source = kpiPool.value.length > 0 ? kpiPool.value : items.value
  const targets = {
    total: kpiPool.value.length > 0 ? kpiPool.value.length : totalItems.value,
    open: source.filter((item) => item.status === 'open').length,
    underReview: source.filter((item) => item.status === 'under_review').length,
    resolved: source.filter((item) => item.status === 'resolved').length,
  }

  const prefersReduced =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches

  if (prefersReduced) {
    Object.assign(animatedKpis, targets)
    return
  }

  const duration = 500
  const start = performance.now()
  function tick(now: number): void {
    const progress = Math.min((now - start) / duration, 1)
    const ease = 1 - Math.pow(1 - progress, 4)
    animatedKpis.total = Math.round(targets.total * ease)
    animatedKpis.open = Math.round(targets.open * ease)
    animatedKpis.underReview = Math.round(targets.underReview * ease)
    animatedKpis.resolved = Math.round(targets.resolved * ease)
    if (progress < 1) requestAnimationFrame(tick)
  }
  requestAnimationFrame(tick)
}

// Modal: Registrar PNC
const isCreateOpen = ref(false)
const isSubmittingCreate = ref(false)
const tramiteOptions = ref<TramiteOption[]>([])
const isLoadingTramites = ref(false)

const defaultCreateTramiteType = (): 'social_service' | 'internship' =>
  canWriteSocialService.value ? 'social_service' : 'internship'

const createForm = reactive({
  tramiteType: defaultCreateTramiteType() as 'social_service' | 'internship',
  tramiteId: 0,
  category: 'invalid_documents',
  description: '',
  actionPlan: '',
})

const createValidation = useFormValidation(() =>
  validateFields(createForm, {
    tramiteType: { required: true, choices: ['social_service', 'internship'] },
    tramiteId: { required: true, min: 1, integer: true, message: 'Selecciona un trámite válido.' },
    category: { required: true, choices: Object.keys(CATEGORY_LABELS) },
    description: { required: true, max: 4000 },
    actionPlan: { max: 4000 },
  }),
)

// Modal: Detalle y seguimiento
const isDetailOpen = ref(false)
const selectedPnc = ref<NonConformance | null>(null)
const isLoadingDetail = ref(false)
const isUpdatingStatus = ref(false)
const detailActionPlan = ref('')
const pendingTargetStatus = ref<'under_review' | 'resolved' | 'cancelled'>('under_review')

const statusValidation = useFormValidation(() =>
  validateFields(
    { actionPlan: detailActionPlan.value },
    {
      actionPlan: {
        required: pendingTargetStatus.value === 'resolved',
        max: 4000,
      },
    },
  ),
)

const canManageSelected = computed(() => {
  const pnc = selectedPnc.value
  if (!pnc) return false
  if (pnc.status !== 'open' && pnc.status !== 'under_review') return false
  return pnc.tramiteType === 'social_service'
    ? canWriteSocialService.value
    : canWriteInternship.value
})

const hasActiveFilters = computed(() =>
  Boolean(
    searchQuery.value.trim() ||
      selectedPeriodId.value ||
      selectedTramiteType.value ||
      selectedStatus.value ||
      selectedCategory.value,
  ),
)

function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—'
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return iso
  return date.toLocaleDateString('es-MX', {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
    timeZone: 'America/Monterrey',
  })
}

function tramiteLabel(type: string): string {
  return type === 'social_service' ? 'Servicio Social' : 'Prácticas Prof.'
}

function canManageRow(row: NonConformance): boolean {
  if (row.status !== 'open' && row.status !== 'under_review') return false
  return row.tramiteType === 'social_service'
    ? canWriteSocialService.value
    : canWriteInternship.value
}

async function loadKpiPool(): Promise<void> {
  try {
    const res = await getNonConformances({ page: 1, pageSize: 100 })
    kpiPool.value = res.items || []
  } catch {
    kpiPool.value = []
  } finally {
    updateAnimatedKpis()
  }
}

async function loadData(): Promise<void> {
  const reqId = ++loadRequestId
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await getNonConformances({
      page: page.value,
      pageSize,
      periodId: selectedPeriodId.value ? Number(selectedPeriodId.value) : undefined,
      tramiteType: selectedTramiteType.value || undefined,
      status: selectedStatus.value || undefined,
      category: selectedCategory.value || undefined,
      search: searchQuery.value.trim() || undefined,
    })
    if (reqId !== loadRequestId) return
    items.value = res.items || []
    totalItems.value = res.totalItems
    totalPages.value = Math.max(1, res.totalPages)
  } catch (err: any) {
    if (reqId !== loadRequestId) return
    errorMessage.value = problemMessage(
      err,
      'No se pudieron cargar los incidentes de Producto No Conforme.',
    )
  } finally {
    if (reqId === loadRequestId) {
      isLoading.value = false
    }
    updateAnimatedKpis()
  }
}

useRealtimeRefresh(
  (evt) =>
    evt.eventType === 'quality.pnc_updated' ||
    (evt.eventType === 'tramite.updated' && evt.status === 'non_compliant'),
  () => Promise.all([loadKpiPool(), loadData()]),
)

async function fetchSuggestions(query: string): Promise<void> {
  const q = query.trim()
  if (!q) {
    autocompleteSuggestions.value = []
    isAutocompleteOpen.value = false
    return
  }
  try {
    const res = await getNonConformances({
      page: 1,
      pageSize: 6,
      search: q,
    })
    autocompleteSuggestions.value = (res.items || []).slice(0, 6)
    isAutocompleteOpen.value = autocompleteSuggestions.value.length > 0
  } catch {
    autocompleteSuggestions.value = []
    isAutocompleteOpen.value = false
  }
}

async function loadTramiteOptions(type: 'social_service' | 'internship'): Promise<void> {
  isLoadingTramites.value = true
  tramiteOptions.value = []
  try {
    if (type === 'social_service') {
      const res = await getSocialServices({ page: 1, pageSize: 50 })
      tramiteOptions.value = res.items.map((item) => ({
        id: item.id,
        label: `#${item.id} · ${item.studentControlNumber} — ${item.studentFullName} (${item.periodCode})`,
      }))
    } else {
      const res = await getInternships({ page: 1, pageSize: 50 })
      tramiteOptions.value = res.items.map((item) => ({
        id: item.id,
        label: `#${item.id} · ${item.studentControlNumber} — ${item.studentFullName} (${item.periodCode})`,
      }))
    }
  } catch {
    tramiteOptions.value = []
  } finally {
    isLoadingTramites.value = false
  }
}

async function openCreateModal(): Promise<void> {
  createForm.tramiteType = defaultCreateTramiteType()
  createForm.tramiteId = 0
  createForm.category = 'invalid_documents'
  createForm.description = ''
  createForm.actionPlan = ''
  createValidation.reset()
  isCreateOpen.value = true
  await loadTramiteOptions(createForm.tramiteType)
}

async function handleCreateSubmit(): Promise<void> {
  if (isSubmittingCreate.value || !createValidation.validate()) return
  isSubmittingCreate.value = true
  try {
    const created = await createNonConformance({
      tramiteType: createForm.tramiteType,
      tramiteId: createForm.tramiteId,
      category: createForm.category,
      description: createForm.description.trim(),
      actionPlan: createForm.actionPlan.trim() || null,
    })
    isCreateOpen.value = false
    toast.value?.show(`Incidencia ${created.incidentCode} registrada correctamente.`)
    page.value = 1
    await Promise.all([loadKpiPool(), loadData()])
  } catch (err: any) {
    createValidation.apply(err, 'No se pudo registrar la incidencia PNC.')
  } finally {
    isSubmittingCreate.value = false
  }
}

async function openDetailModal(row: NonConformance): Promise<void> {
  selectedPnc.value = row
  detailActionPlan.value = row.actionPlan ?? ''
  statusValidation.reset()
  isDetailOpen.value = true
  isLoadingDetail.value = true
  try {
    const fresh = await getNonConformanceById(row.id)
    selectedPnc.value = fresh
    detailActionPlan.value = fresh.actionPlan ?? ''
  } catch (err: any) {
    errorMessage.value = problemMessage(err, 'No se pudo actualizar el detalle del PNC.')
  } finally {
    isLoadingDetail.value = false
  }
}

async function handleStatusTransition(
  targetStatus: 'under_review' | 'resolved' | 'cancelled',
): Promise<void> {
  const pnc = selectedPnc.value
  if (!pnc || isUpdatingStatus.value) return
  pendingTargetStatus.value = targetStatus
  if (!statusValidation.validate()) return
  isUpdatingStatus.value = true
  try {
    const updated = await updateNonConformanceStatus(pnc.id, {
      status: targetStatus,
      actionPlan: detailActionPlan.value.trim() || null,
    })
    selectedPnc.value = updated
    detailActionPlan.value = updated.actionPlan ?? ''
    toast.value?.show(
      `Incidencia ${updated.incidentCode} actualizada a ${STATUS_META[updated.status].label}.`,
    )
    if (updated.status === 'resolved' || updated.status === 'cancelled') {
      isDetailOpen.value = false
    }
    await Promise.all([loadKpiPool(), loadData()])
  } catch (err: any) {
    statusValidation.apply(err, 'No se pudo actualizar el estado del PNC.')
  } finally {
    isUpdatingStatus.value = false
  }
}

function handleSearchInput(): void {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    page.value = 1
    void fetchSuggestions(searchQuery.value)
    void loadData()
  }, 280)
}

function selectSuggestion(item: NonConformance): void {
  searchQuery.value = item.incidentCode
  isAutocompleteOpen.value = false
  page.value = 1
  void loadData()
}

function clearSearch(): void {
  if (debounceTimer) clearTimeout(debounceTimer)
  searchQuery.value = ''
  autocompleteSuggestions.value = []
  isAutocompleteOpen.value = false
  page.value = 1
  void loadData()
}

function handleSearchFocus(): void {
  if (searchQuery.value.trim().length > 0 && autocompleteSuggestions.value.length > 0) {
    isAutocompleteOpen.value = true
  }
}

function handleSearchBlur(): void {
  setTimeout(() => {
    isAutocompleteOpen.value = false
  }, 180)
}

function handleFilterChange(): void {
  page.value = 1
  void loadData()
}

function toggleKpiStatus(status: string): void {
  selectedStatus.value = selectedStatus.value === status ? '' : status
  page.value = 1
  void loadData()
}

function resetFilters(): void {
  if (debounceTimer) clearTimeout(debounceTimer)
  searchQuery.value = ''
  autocompleteSuggestions.value = []
  isAutocompleteOpen.value = false
  selectedPeriodId.value = ''
  selectedTramiteType.value = ''
  selectedStatus.value = ''
  selectedCategory.value = ''
  page.value = 1
  void loadData()
}

function goToPage(nextPage: number): void {
  if (nextPage < 1 || nextPage > totalPages.value || nextPage === page.value) return
  page.value = nextPage
  void loadData()
}

watch(
  () => createForm.tramiteType,
  async (nextType) => {
    if (!isCreateOpen.value) return
    createForm.tramiteId = 0
    await loadTramiteOptions(nextType)
  },
)

onBeforeUnmount(() => {
  if (debounceTimer) clearTimeout(debounceTimer)
  loadRequestId++
})

onMounted(async () => {
  try {
    periodOptions.value = await getPeriodOptions()
  } catch {
    periodOptions.value = []
  }
  await Promise.all([loadKpiPool(), loadData()])
})
</script>

<template>
  <UiBasePage :fluid="true" class="fullscreen-page">
    <!-- Encabezado Ejecutivo Compacto con 4 KPIs Superiores (solo borde dorado activo) -->
    <header class="exec-header">
      <div class="exec-header__left">
        <div class="exec-eyebrow">
          <span class="exec-badge">GESTIÓN DE CALIDAD · ISO 9001:2015</span>
          <span class="exec-period">· Bitácora de Desvíos y Acciones Correctivas</span>
        </div>
        <div class="title-row">
          <h1 class="exec-title">Producto No Conforme (PNC)</h1>
          <UiButton v-if="canCreateAny" variant="primary" size="sm" @click="openCreateModal">
            + Registrar PNC
          </UiButton>
        </div>
      </div>

      <!-- 4 KPIs con superficie neutra y borde dorado únicamente cuando está activo -->
      <div class="exec-kpis" aria-label="Indicadores de Producto No Conforme">
        <button
          type="button"
          class="mini-kpi"
          :class="{ 'mini-kpi--active': selectedStatus === '' }"
          @click="toggleKpiStatus('')"
        >
          <span class="mini-kpi__label">Total PNC</span>
          <strong class="mini-kpi__value">{{ animatedKpis.total }}</strong>
        </button>

        <button
          type="button"
          class="mini-kpi mini-kpi--danger"
          :class="{ 'mini-kpi--active': selectedStatus === 'open' }"
          @click="toggleKpiStatus('open')"
        >
          <span class="mini-kpi__label">Abiertos</span>
          <strong class="mini-kpi__value">{{ animatedKpis.open }}</strong>
        </button>

        <button
          type="button"
          class="mini-kpi mini-kpi--gold"
          :class="{ 'mini-kpi--active': selectedStatus === 'under_review' }"
          @click="toggleKpiStatus('under_review')"
        >
          <span class="mini-kpi__label">En Revisión</span>
          <strong class="mini-kpi__value">{{ animatedKpis.underReview }}</strong>
        </button>

        <button
          type="button"
          class="mini-kpi mini-kpi--emerald"
          :class="{ 'mini-kpi--active': selectedStatus === 'resolved' }"
          @click="toggleKpiStatus('resolved')"
        >
          <span class="mini-kpi__label">Resueltos</span>
          <strong class="mini-kpi__value">{{ animatedKpis.resolved }}</strong>
        </button>
      </div>
    </header>

    <UiAlert v-if="errorMessage" tone="danger" class="compact-alert">
      <div class="error-row">
        <span>{{ errorMessage }}</span>
        <UiButton variant="secondary" size="sm" @click="loadData">Reintentar</UiButton>
      </div>
    </UiAlert>

    <UiCard class="pnc-card">
      <!-- Barra de Filtros con Buscador Autocomplete -->
      <div class="filters-bar">
        <div class="search-box">
          <label class="filter-label" for="pnc-search-input">
            Buscar por Código PNC, Matrícula o Alumno
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
                id="pnc-search-input"
                v-model="searchQuery"
                type="search"
                class="modern-input"
                placeholder="Ej. PNC-SS-2026, I23050363 o Mariana..."
                aria-label="Buscar incidencia PNC"
                autocomplete="off"
                @input="handleSearchInput"
                @focus="handleSearchFocus"
                @blur="handleSearchBlur"
                @keyup.enter="isAutocompleteOpen = false; page = 1; loadData()"
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

            <!-- Lista desplegable de sugerencias -->
            <div
              v-if="isAutocompleteOpen && autocompleteSuggestions.length > 0"
              class="suggestions-dropdown"
              role="listbox"
            >
              <div
                v-for="suggestion in autocompleteSuggestions"
                :key="suggestion.id"
                class="suggestion-item"
                role="option"
                @mousedown.prevent="selectSuggestion(suggestion)"
              >
                <div class="suggestion-left">
                  <span class="suggestion-control">
                    {{ suggestion.incidentCode }} · {{ suggestion.studentControlNumber }}
                  </span>
                  <span class="suggestion-name">{{ suggestion.studentFullName }}</span>
                </div>
                <div class="suggestion-right">
                  <span class="suggestion-period">{{ suggestion.periodCode }}</span>
                  <UiBadge :tone="STATUS_META[suggestion.status]?.tone">
                    {{ STATUS_META[suggestion.status]?.label ?? suggestion.status }}
                  </UiBadge>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="select-box">
          <label class="filter-label" for="pnc-period-select">Periodo</label>
          <div class="select-wrapper">
            <select
              id="pnc-period-select"
              v-model="selectedPeriodId"
              class="modern-select"
              @change="handleFilterChange"
            >
              <option value="">Todos los periodos</option>
              <option v-for="p in periodOptions" :key="p.id" :value="String(p.id)">
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

        <div class="select-box">
          <label class="filter-label" for="pnc-tramite-select">Trámite</label>
          <div class="select-wrapper">
            <select
              id="pnc-tramite-select"
              v-model="selectedTramiteType"
              class="modern-select"
              @change="handleFilterChange"
            >
              <option value="">Todos</option>
              <option value="social_service">Servicio Social</option>
              <option value="internship">Prácticas Profesionales</option>
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

        <div class="select-box">
          <label class="filter-label" for="pnc-status-select">Estatus</label>
          <div class="select-wrapper">
            <select
              id="pnc-status-select"
              v-model="selectedStatus"
              class="modern-select"
              @change="handleFilterChange"
            >
              <option value="">Todos</option>
              <option value="open">Abierto</option>
              <option value="under_review">En revisión</option>
              <option value="resolved">Resuelto</option>
              <option value="cancelled">Cancelado</option>
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

        <div class="select-box">
          <label class="filter-label" for="pnc-category-select">Categoría</label>
          <div class="select-wrapper">
            <select
              id="pnc-category-select"
              v-model="selectedCategory"
              class="modern-select"
              @change="handleFilterChange"
            >
              <option value="">Todas</option>
              <option v-for="(label, key) in CATEGORY_LABELS" :key="key" :value="key">
                {{ label }}
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

        <div v-if="hasActiveFilters" class="filter-reset">
          <UiButton variant="secondary" size="sm" @click="resetFilters">
            Limpiar
          </UiButton>
        </div>
      </div>

      <!-- Tabla de Incidencias con scroll interno y encabezados fijos -->
      <div class="pnc-table-wrap" role="region" aria-label="Incidencias PNC">
        <table class="pnc-table">
          <thead>
            <tr>
              <th scope="col">CÓDIGO PNC</th>
              <th scope="col">PERIODO</th>
              <th scope="col">TRÁMITE</th>
              <th scope="col">ALUMNO / MATRÍCULA</th>
              <th scope="col">CATEGORÍA</th>
              <th scope="col">ESTATUS</th>
              <th scope="col">REGISTRO</th>
              <th scope="col" class="th-actions">ACCIONES</th>
            </tr>
          </thead>
          <TransitionGroup name="pnc-row" tag="tbody">
            <tr v-if="isLoading" key="loading">
              <td colspan="8" class="empty-cell">
                Cargando incidencias de Producto No Conforme…
              </td>
            </tr>
            <tr v-else-if="items.length === 0" key="empty">
              <td colspan="8" class="empty-cell">
                <div class="empty-state">
                  <p>No se encontraron incidencias PNC con los filtros actuales.</p>
                  <UiButton v-if="hasActiveFilters" variant="secondary" size="sm" @click="resetFilters">
                    Restablecer filtros
                  </UiButton>
                </div>
              </td>
            </tr>
            <tr v-for="row in items" :key="row.id" class="pnc-tr">
              <td>
                <span class="mono-code">{{ row.incidentCode }}</span>
              </td>
              <td>
                <span class="period-pill">{{ row.periodCode }}</span>
              </td>
              <td>
                <span class="tramite-text">{{ tramiteLabel(row.tramiteType) }}</span>
              </td>
              <td>
                <div class="student-cell">
                  <span class="student-name">{{ row.studentFullName }}</span>
                  <span class="student-control">{{ row.studentControlNumber }}</span>
                </div>
              </td>
              <td>
                <span class="category-text">{{ CATEGORY_LABELS[row.category] ?? row.category }}</span>
              </td>
              <td>
                <UiBadge :tone="STATUS_META[row.status]?.tone">
                  {{ STATUS_META[row.status]?.label ?? row.status }}
                </UiBadge>
              </td>
              <td>
                <span class="date-text">{{ formatDate(row.createdAt) }}</span>
              </td>
              <td class="td-actions">
                <UiButton
                  :variant="canManageRow(row) ? 'primary' : 'secondary'"
                  size="sm"
                  @click="openDetailModal(row)"
                >
                  {{ canManageRow(row) ? 'Gestionar' : 'Ver detalle' }}
                </UiButton>
              </td>
            </tr>
          </TransitionGroup>
        </table>
      </div>

      <!-- Barra de Paginación Estandarizada (10 por página) -->
      <footer class="pagination-bar">
        <span class="pagination-info">
          Mostrando <strong>{{ items.length }}</strong> de
          <strong>{{ totalItems }}</strong> incidencias · Página
          <strong>{{ page }}</strong> de <strong>{{ totalPages }}</strong>
        </span>
        <div class="pagination-actions">
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="page <= 1 || isLoading"
            @click="goToPage(page - 1)"
          >
            Anterior
          </UiButton>
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="page >= totalPages || isLoading"
            @click="goToPage(page + 1)"
          >
            Siguiente
          </UiButton>
        </div>
      </footer>
    </UiCard>

    <!-- Modal: Registrar PNC -->
    <UiDialog v-model="isCreateOpen" size="md" title="Registrar Producto No Conforme (PNC)">
      <div class="dialog-body">
        <UiFormErrors :validation="createValidation" />

        <UiField
          v-bind="createValidation.field('tramiteType', 'Tipo de trámite')"
          label="Proceso / Trámite *"
          v-slot="{ controlAttrs }"
        >
          <select
            v-bind="controlAttrs"
            v-model="createForm.tramiteType"
            class="modern-select"
            :disabled="!(canWriteSocialService && canWriteInternship)"
          >
            <option v-if="canWriteSocialService" value="social_service">Servicio Social</option>
            <option v-if="canWriteInternship" value="internship">Prácticas Profesionales</option>
          </select>
        </UiField>

        <UiField
          v-bind="createValidation.field('tramiteId', 'Trámite')"
          label="Expediente asociado *"
          v-slot="{ controlAttrs }"
        >
          <select
            v-bind="controlAttrs"
            v-model.number="createForm.tramiteId"
            class="modern-select"
            :disabled="isLoadingTramites"
          >
            <option :value="0">
              {{ isLoadingTramites ? 'Cargando trámites…' : 'Selecciona un trámite…' }}
            </option>
            <option v-for="t in tramiteOptions" :key="t.id" :value="t.id">{{ t.label }}</option>
          </select>
        </UiField>

        <UiField
          v-bind="createValidation.field('category', 'Categoría')"
          label="Categoría *"
          v-slot="{ controlAttrs }"
        >
          <select v-bind="controlAttrs" v-model="createForm.category" class="modern-select">
            <option v-for="(label, key) in CATEGORY_LABELS" :key="key" :value="key">
              {{ label }}
            </option>
          </select>
        </UiField>

        <UiField
          v-bind="createValidation.field('description', 'Descripción')"
          label="Descripción de la no conformidad *"
          v-slot="{ controlAttrs }"
        >
          <textarea
            v-bind="controlAttrs"
            v-model="createForm.description"
            rows="3"
            class="modern-textarea"
            placeholder="Describe el desvío detectado conforme a ISO 9001"
          />
        </UiField>

        <UiField
          v-bind="createValidation.field('actionPlan', 'Plan de acción')"
          label="Plan de acción correctiva (opcional)"
          v-slot="{ controlAttrs }"
        >
          <textarea
            v-bind="controlAttrs"
            v-model="createForm.actionPlan"
            rows="2"
            class="modern-textarea"
            placeholder="Acciones acordadas para corregir la incidencia"
          />
        </UiField>
      </div>

      <template #footer>
        <UiButton variant="secondary" size="sm" @click="isCreateOpen = false">
          Cancelar
        </UiButton>
        <UiButton
          variant="primary"
          size="sm"
          :loading="isSubmittingCreate"
          @click="handleCreateSubmit"
        >
          Registrar PNC
        </UiButton>
      </template>
    </UiDialog>

    <!-- Modal: Detalle y Seguimiento -->
    <UiDialog
      v-model="isDetailOpen"
      size="lg"
      :title="selectedPnc ? `Incidencia ${selectedPnc.incidentCode}` : 'Detalle PNC'"
    >
      <div v-if="selectedPnc" class="dialog-body">
        <div v-if="isLoadingDetail" class="detail-loading">Actualizando información…</div>

        <div class="detail-grid">
          <div class="detail-item">
            <span class="detail-label">Estatus</span>
            <div class="detail-badge-wrap">
              <UiBadge :tone="STATUS_META[selectedPnc.status]?.tone">
                {{ STATUS_META[selectedPnc.status]?.label ?? selectedPnc.status }}
              </UiBadge>
            </div>
          </div>
          <div class="detail-item">
            <span class="detail-label">Proceso</span>
            <p class="detail-value">
              {{ tramiteLabel(selectedPnc.tramiteType) }} #{{ selectedPnc.tramiteId }}
            </p>
          </div>
          <div class="detail-item">
            <span class="detail-label">Alumno</span>
            <p class="detail-value">
              {{ selectedPnc.studentFullName }} ({{ selectedPnc.studentControlNumber }})
            </p>
          </div>
          <div class="detail-item">
            <span class="detail-label">Periodo</span>
            <p class="detail-value">{{ selectedPnc.periodCode }}</p>
          </div>
          <div class="detail-item">
            <span class="detail-label">Categoría</span>
            <p class="detail-value">
              {{ CATEGORY_LABELS[selectedPnc.category] ?? selectedPnc.category }}
            </p>
          </div>
          <div class="detail-item">
            <span class="detail-label">Fecha de registro</span>
            <p class="detail-value">{{ formatDate(selectedPnc.createdAt) }}</p>
          </div>
        </div>

        <div class="detail-block">
          <span class="detail-label">Descripción del desvío</span>
          <p class="detail-box">{{ selectedPnc.description }}</p>
        </div>

        <template v-if="canManageSelected">
          <UiFormErrors :validation="statusValidation" />
          <UiField
            v-bind="statusValidation.field('actionPlan', 'Plan de acción correctiva')"
            label="Plan de acción correctiva"
            hint="Obligatorio para resolver la incidencia."
            v-slot="{ controlAttrs }"
          >
            <textarea
              v-bind="controlAttrs"
              v-model="detailActionPlan"
              rows="3"
              class="modern-textarea"
              placeholder="Documenta la causa raíz y acciones correctivas realizadas"
            />
          </UiField>
        </template>

        <template v-else>
          <div class="detail-block">
            <span class="detail-label">Plan de acción correctiva</span>
            <p class="detail-box">
              {{ selectedPnc.actionPlan || 'Sin plan de acción documentado.' }}
            </p>
          </div>
          <div v-if="selectedPnc.resolvedAt" class="detail-grid">
            <div class="detail-item">
              <span class="detail-label">Resuelto el</span>
              <p class="detail-value">{{ formatDate(selectedPnc.resolvedAt) }}</p>
            </div>
            <div class="detail-item">
              <span class="detail-label">Resuelto por</span>
              <p class="detail-value">{{ selectedPnc.resolvedByName || '—' }}</p>
            </div>
          </div>
        </template>
      </div>

      <template #footer>
        <UiButton variant="secondary" size="sm" @click="isDetailOpen = false">Cerrar</UiButton>
        <template v-if="selectedPnc && canManageSelected">
          <UiButton
            v-if="selectedPnc.status === 'open'"
            variant="secondary"
            size="sm"
            :disabled="isUpdatingStatus"
            @click="handleStatusTransition('under_review')"
          >
            Pasar a revisión
          </UiButton>
          <UiButton
            variant="danger"
            size="sm"
            :disabled="isUpdatingStatus"
            @click="handleStatusTransition('cancelled')"
          >
            Cancelar PNC
          </UiButton>
          <UiButton
            variant="primary"
            size="sm"
            :loading="isUpdatingStatus"
            @click="handleStatusTransition('resolved')"
          >
            Resolver PNC
          </UiButton>
        </template>
      </template>
    </UiDialog>

    <UiToast ref="toast" />
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
  min-width: 108px;
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

.mini-kpi--danger .mini-kpi__value {
  color: var(--color-danger-text);
}

.mini-kpi--gold .mini-kpi__value {
  color: var(--color-warning);
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

.error-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.pnc-card {
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

.search-box {
  flex: 1.6;
  min-width: 230px;
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.select-box {
  flex: 0.75;
  min-width: 140px;
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.filter-reset {
  display: flex;
  align-items: center;
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
select.modern-select:hover,
textarea.modern-textarea:hover {
  border-color: var(--color-gray-400) !important;
}

input.modern-input:focus,
select.modern-select:focus,
textarea.modern-textarea:focus {
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

textarea.modern-textarea {
  width: 100%;
  padding: 0.55rem 0.75rem;
  border-radius: 7px;
  border: 1px solid var(--input-border);
  background: var(--input-bg);
  color: var(--color-text-primary);
  font: inherit;
  font-size: 0.84rem;
  resize: vertical;
  outline: none;
  box-sizing: border-box;
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

.suggestion-period {
  font-size: 0.7rem;
  font-weight: 700;
  padding: 0.1rem 0.4rem;
  background: var(--color-primary-subtle);
  border-radius: 4px;
  color: var(--color-info-text);
}

/* Tabla con scroll interno y encabezados fijos */
.pnc-table-wrap {
  flex: 1;
  min-height: 0;
  overflow: auto;
  margin: 0 -16px;
  border-top: 1px solid var(--color-border);
}

.pnc-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  white-space: nowrap;
}

.pnc-table th {
  padding: 7px 14px;
  font-size: 0.68rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  background: var(--surface-table-head);
  color: var(--color-text-secondary);
  border-bottom: 1px solid var(--color-border-medium);
  position: sticky;
  top: 0;
  z-index: 1;
}

.pnc-table td {
  padding: 7px 14px;
  font-size: 0.8rem;
  vertical-align: middle;
  border-bottom: 1px solid var(--color-border-light);
}

.pnc-tr {
  transition: background-color 150ms ease;
}

.pnc-tr:hover {
  background: var(--color-primary-subtle);
}

.th-actions,
.td-actions {
  width: 132px;
  text-align: center;
}

.mono-code {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.78rem;
  font-weight: 700;
  color: var(--color-info-text);
}

.period-pill {
  display: inline-block;
  padding: 0.12rem 0.45rem;
  border-radius: 5px;
  font-size: 0.72rem;
  font-weight: 700;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
  color: var(--color-text-primary);
}

.tramite-text,
.category-text,
.date-text {
  color: var(--color-text-secondary);
  font-size: 0.79rem;
}

.student-cell {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.student-name {
  font-weight: 600;
  color: var(--color-text-primary);
}

.student-control {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.73rem;
  color: var(--color-text-secondary);
}

.empty-cell {
  padding: 2rem 1rem !important;
  text-align: center;
  color: var(--color-text-secondary);
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.6rem;
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

.pagination-actions {
  display: flex;
  gap: 0.4rem;
}

/* Modales */
.dialog-body {
  display: flex;
  flex-direction: column;
  gap: 0.9rem;
}

.detail-loading {
  font-size: 0.82rem;
  color: var(--color-text-secondary);
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 0.75rem;
  padding: 0.75rem;
  border-radius: 8px;
  background: var(--surface-toolbar);
  border: 1px solid var(--color-border);
}

.detail-item {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.detail-badge-wrap {
  margin-top: 0.1rem;
}

.detail-block {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.detail-label {
  display: block;
  font-size: 0.72rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.detail-value {
  margin: 0;
  font-size: 0.84rem;
  font-weight: 600;
  color: var(--color-text-primary);
}

.detail-box {
  margin: 0;
  padding: 0.7rem 0.85rem;
  border-radius: 8px;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
  color: var(--color-text-primary);
  font-size: 0.84rem;
  line-height: 1.45;
  white-space: pre-wrap;
}

.pnc-row-enter-active,
.pnc-row-leave-active {
  transition: opacity 180ms ease, transform 180ms ease;
}

.pnc-row-enter-from,
.pnc-row-leave-to {
  opacity: 0;
  transform: translateY(4px);
}

@media (max-width: 900px) {
  .fullscreen-page {
    height: auto !important;
    overflow: visible !important;
    padding: 4px 0 12px !important;
    min-width: 0;
    max-width: 100%;
  }

  .pnc-card {
    min-width: 0;
    max-width: 100%;
    overflow: hidden;
    padding: 10px 12px !important;
  }

  .pnc-table-wrap {
    margin: 0 -12px;
    max-width: calc(100% + 24px);
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
  }

  .pnc-table {
    min-width: 680px;
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

  .search-box,
  .select-box,
  .filter-reset {
    min-width: 0;
    width: 100%;
    flex: 1 1 auto;
  }

  .filter-reset :deep(.btn) {
    width: 100%;
  }

  input.modern-input,
  select.modern-select {
    min-height: 40px;
    height: 40px;
    font-size: 16px;
  }

  textarea.modern-textarea {
    font-size: 16px;
  }

  .detail-grid {
    grid-template-columns: 1fr;
  }

  .pagination-bar {
    flex-direction: column;
    align-items: stretch;
    gap: 0.5rem;
  }

  .pagination-info {
    text-align: center;
  }

  .pagination-actions {
    width: 100%;
  }

  .pagination-actions :deep(.btn) {
    flex: 1;
  }
}
</style>
