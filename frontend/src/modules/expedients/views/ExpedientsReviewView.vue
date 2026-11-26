<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import {
  socialDocumentLabel,
  socialDocumentPeriod,
  socialServiceDocuments,
} from '../../../shared/utils/socialServiceDocuments'
import DocumentReviewDialog from '../components/DocumentReviewDialog.vue'
import { canReadProcess, sessionUser } from '../../../shared/composables/useSession'
import { activeReviewers, useRealtimeRefresh } from '../../../shared/composables/useRealtime'
import {
  UiAlert,
  UiBadge,
  UiBasePage,
  UiButton,
  UiCard,
  UiDialog,
  UiToast,
} from '../../../shared/components'
import { getDocumentBlob, getReviewQueuePage } from '../api/expedientsApi'
import type { ExpedientDocumentStatus, ReviewQueueItem } from '../types/expedients'

// Catálogo descriptivo de códigos normativos de documentos
const documentCatalog: Record<string, { title: string; anexo: string }> = {
  CP: { title: 'Carta de Presentación', anexo: 'Oficio Vinculación' },
  CA: { title: 'Carta de Aceptación Oficial', anexo: 'Membrete Empresa' },
  EV: { title: 'Evaluación Final de Desempeño', anexo: 'Anexo XVII' },
  IMSS: { title: 'Vigencia de Derechos IMSS', anexo: 'Requisito de admisión' },
  CC: { title: 'Carta Compromiso', anexo: 'Anexo XX' },
  CT: { title: 'Carta de Terminación y Liberación', anexo: 'Constancia 480 h' },
}

const queue = ref<ReviewQueueItem[]>([])
const loadError = ref(false)
const isLoading = ref(true)
const toast = ref<InstanceType<typeof UiToast> | null>(null)

// Filtros reactivos y paginación estandarizada (10 por página, sin filtro por ID de periodo)
const searchQuery = ref('')
const modalityFilter = ref<'all' | 'social_service' | 'internship' | 'general'>('all')
const statusFilter = ref<'all' | ExpedientDocumentStatus>('all')
const docCodeFilter = ref<string | null>(null)
const page = ref(1)
const pageSize = 10
const totalPages = ref(1)
const totalItems = ref(0)

// Autocomplete con Debounce (280 ms)
const autocompleteSuggestions = ref<ReviewQueueItem[]>([])
const isAutocompleteOpen = ref(false)
let requestSequence = 0
let filterTimer: ReturnType<typeof setTimeout> | undefined

watch([modalityFilter, statusFilter, docCodeFilter], () => {
  clearTimeout(filterTimer)
  isAutocompleteOpen.value = false
  page.value = 1
  void loadQueue()
})

onBeforeUnmount(() => {
  clearTimeout(filterTimer)
  requestSequence++
})

// Contadores KPI animados
const animatedCounts = reactive({
  pending: 0,
  socialService: 0,
  internship: 0,
  approved: 0,
})

function updateAnimatedKpis(): void {
  const source = queue.value
  const targets = {
    pending: source.filter((d) => d.status === 'pending').length,
    socialService: source.filter((d) => d.tramiteType === 'social_service').length,
    internship: source.filter((d) => d.tramiteType === 'internship').length,
    approved: source.filter((d) => d.status === 'approved').length,
  }

  const prefersReduced =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches

  if (prefersReduced) {
    Object.assign(animatedCounts, targets)
    return
  }

  const duration = 520
  const start = performance.now()

  function tick(now: number): void {
    const elapsed = now - start
    const progress = Math.min(elapsed / duration, 1)
    const ease = 1 - Math.pow(1 - progress, 4)

    animatedCounts.pending = Math.round(targets.pending * ease)
    animatedCounts.socialService = Math.round(targets.socialService * ease)
    animatedCounts.internship = Math.round(targets.internship * ease)
    animatedCounts.approved = Math.round(targets.approved * ease)

    if (progress < 1) {
      requestAnimationFrame(tick)
    }
  }

  requestAnimationFrame(tick)
}

function sortNewestFirst(items: ReviewQueueItem[]): ReviewQueueItem[] {
  return [...items].sort((a, b) => {
    const timeDiff = new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
    if (timeDiff !== 0) return timeDiff
    return b.id - a.id
  })
}

async function loadQueue(): Promise<void> {
  const sequence = ++requestSequence
  isLoading.value = true
  try {
    const data = await getReviewQueuePage({
      page: page.value,
      pageSize,
      status: statusFilter.value,
      tramiteType: modalityFilter.value === 'all' ? undefined : modalityFilter.value,
      documentCode: docCodeFilter.value || undefined,
      search: searchQuery.value.trim() || undefined,
    })
    if (sequence !== requestSequence) return
    totalPages.value = Math.max(1, data.totalPages || Math.ceil((data.totalItems || 0) / pageSize))
    totalItems.value = data.totalItems
    loadError.value = false
    queue.value = sortNewestFirst(data.items || [])
  } catch {
    if (sequence !== requestSequence) return
    totalItems.value = 0
    totalPages.value = 1
    loadError.value = true
    queue.value = []
    toast.value?.show('No fue posible cargar la bandeja. Actualiza para intentar nuevamente.')
  } finally {
    if (sequence === requestSequence) isLoading.value = false
    updateAnimatedKpis()
  }
}

async function fetchSuggestions(query: string): Promise<void> {
  const q = query.trim()
  if (!q) {
    autocompleteSuggestions.value = []
    isAutocompleteOpen.value = false
    return
  }
  try {
    const data = await getReviewQueuePage({
      page: 1,
      pageSize: 6,
      status: statusFilter.value,
      tramiteType: modalityFilter.value === 'all' ? undefined : modalityFilter.value,
      documentCode: docCodeFilter.value || undefined,
      search: q,
    })
    autocompleteSuggestions.value = sortNewestFirst(data.items || []).slice(0, 6)
    isAutocompleteOpen.value = autocompleteSuggestions.value.length > 0
  } catch {
    autocompleteSuggestions.value = []
    isAutocompleteOpen.value = false
  }
}

function handleSearchInput(): void {
  clearTimeout(filterTimer)
  filterTimer = setTimeout(() => {
    page.value = 1
    void fetchSuggestions(searchQuery.value)
    void loadQueue()
  }, 280)
}

function selectSuggestion(item: ReviewQueueItem): void {
  searchQuery.value = item.studentControlNumber
  isAutocompleteOpen.value = false
  page.value = 1
  void loadQueue()
}

function clearSearch(): void {
  searchQuery.value = ''
  autocompleteSuggestions.value = []
  isAutocompleteOpen.value = false
  page.value = 1
  void loadQueue()
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

function resetAllFilters(): void {
  searchQuery.value = ''
  autocompleteSuggestions.value = []
  isAutocompleteOpen.value = false
  modalityFilter.value = 'all'
  statusFilter.value = 'all'
  docCodeFilter.value = null
  page.value = 1
  void loadQueue()
}

const filteredQueue = computed(() => sortNewestFirst(queue.value))

function formatFileSize(bytes: number): string {
  const kb = Math.round(bytes / 1024)
  return kb >= 1024 ? `${(kb / 1024).toFixed(1)} MB` : `${kb} KB`
}

function formatSubmittedDate(isoString: string | undefined): string {
  if (!isoString) return 'Sin fecha'
  try {
    return new Date(isoString).toLocaleString('es-MX', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      timeZone: 'America/Mexico_City',
    })
  } catch {
    return isoString.slice(0, 10)
  }
}

function getDocMeta(code: string): { title: string; anexo: string } {
  return (
    documentCatalog[code] ?? {
      title: `Documento Institucional (${code})`,
      anexo: 'Formato SGC',
    }
  )
}

// Visor / Previsualización de PDF en Modal Amplio (size="full")
const isPdfViewerOpen = ref(false)
const pdfPreviewItem = ref<ReviewQueueItem | null>(null)
const pdfBlobUrl = ref<string | null>(null)

function releasePdfUrl(): void {
  if (pdfBlobUrl.value) window.URL.revokeObjectURL(pdfBlobUrl.value)
  pdfBlobUrl.value = null
}
watch(isPdfViewerOpen, (open) => {
  if (!open) releasePdfUrl()
})
onBeforeUnmount(releasePdfUrl)

async function handleViewPdf(doc: ReviewQueueItem): Promise<void> {
  pdfPreviewItem.value = doc
  if (pdfBlobUrl.value) {
    window.URL.revokeObjectURL(pdfBlobUrl.value)
    pdfBlobUrl.value = null
  }

  try {
    const blob = await getDocumentBlob(doc.id)
    pdfBlobUrl.value = window.URL.createObjectURL(blob)
  } catch {
    pdfBlobUrl.value = null
  }

  isPdfViewerOpen.value = true
}

function proceedFromViewerToReview(): void {
  if (!pdfPreviewItem.value) return
  const target = pdfPreviewItem.value
  isPdfViewerOpen.value = false
  openReview(target)
}

// Modal de Dictamen Técnico
const selectedItem = ref<ReviewQueueItem | null>(null)
const isDialogOpen = ref(false)
function openReview(item: ReviewQueueItem): void {
  selectedItem.value = item
  isDialogOpen.value = true
}

function openNextPending(): void {
  const nextDoc = filteredQueue.value.find((d) => d.status === 'pending')
  if (nextDoc) {
    openReview(nextDoc)
  } else {
    toast.value?.show('No hay documentos pendientes de dictamen en la cola.')
  }
}

// Códigos rápidos en panel lateral
const documentCodeChips = computed(() =>
  [...socialServiceDocuments.flatMap((item) => item.codes), 'EV', 'IMSS'].map((code) => ({
    code,
    period: socialDocumentPeriod(code),
    label: socialServiceDocuments.some((item) => item.codes.includes(code))
      ? socialDocumentLabel(code)
      : documentCatalog[code]?.title || code,
    count: queue.value.filter((d) => d.documentCode === code).length,
  })),
)

onMounted(() => {
  void loadQueue()
})

useRealtimeRefresh(
  (evt) => evt.eventType.startsWith('document.') || evt.eventType === 'tramite.updated',
  () => loadQueue(),
)
</script>

<template>
  <UiBasePage :fluid="true" class="fullscreen-page">
    <!-- Encabezado Ejecutivo Compacto con 4 KPIs de borde dorado activo -->
    <header class="exec-header">
      <div class="exec-header__left">
        <div class="exec-eyebrow">
          <span class="exec-badge">BANDEJA UNIFICADA DE VALIDACIÓN DOCUMENTAL</span>
          <span class="exec-period">· Control SGC ISO 9001:2015 · Ordenado del más reciente al más antiguo</span>
        </div>
        <div class="title-row">
          <h1 class="exec-title">Revisión y Dictamen de Expedientes</h1>
          <div class="header-quick-actions">
            <UiButton variant="secondary" size="sm" :disabled="isLoading" @click="loadQueue">
              Actualizar Cola
            </UiButton>
            <UiButton variant="primary" size="sm" @click="openNextPending">
              Dictaminar Siguiente
            </UiButton>
          </div>
        </div>
      </div>

      <!-- 4 KPIs con fondo neutro y borde dorado al estar activos -->
      <div class="exec-kpis" aria-label="Indicadores de revisión documental">
        <button
          type="button"
          class="mini-kpi mini-kpi--gold"
          :class="{ 'mini-kpi--active': statusFilter === 'pending' }"
          @click="statusFilter = statusFilter === 'pending' ? 'all' : 'pending'"
        >
          <span class="mini-kpi__label">En Espera</span>
          <strong class="mini-kpi__value">{{ animatedCounts.pending }}</strong>
        </button>

        <button
          v-if="canReadProcess('social_service')"
          type="button"
          class="mini-kpi mini-kpi--navy"
          :class="{ 'mini-kpi--active': modalityFilter === 'social_service' }"
          @click="modalityFilter = modalityFilter === 'social_service' ? 'all' : 'social_service'"
        >
          <span class="mini-kpi__label">Servicio Social</span>
          <strong class="mini-kpi__value">{{ animatedCounts.socialService }}</strong>
        </button>

        <button
          v-if="canReadProcess('internship')"
          type="button"
          class="mini-kpi mini-kpi--navy"
          :class="{ 'mini-kpi--active': modalityFilter === 'internship' }"
          @click="modalityFilter = modalityFilter === 'internship' ? 'all' : 'internship'"
        >
          <span class="mini-kpi__label">Prácticas Prof.</span>
          <strong class="mini-kpi__value">{{ animatedCounts.internship }}</strong>
        </button>

        <button
          type="button"
          class="mini-kpi mini-kpi--emerald"
          :class="{ 'mini-kpi--active': statusFilter === 'approved' }"
          @click="statusFilter = statusFilter === 'approved' ? 'all' : 'approved'"
        >
          <span class="mini-kpi__label">Validados</span>
          <strong class="mini-kpi__value">{{ animatedCounts.approved }}</strong>
        </button>
      </div>
    </header>

    <!-- Área Principal en 2 Columnas a Pantalla Completa -->
    <div class="workspace-grid">
      <!-- Columna Izquierda: Tabla Unificada de Revisión -->
      <UiCard class="tray-card">
        <div class="filters-bar">
          <!-- Buscador con Autocompletado y Debounce -->
          <div class="search-box">
            <label class="filter-label" for="review-search-input">
              Buscar por Matrícula, Alumno, Carrera o Clave (CA, R1, CT, IMSS)
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
                  id="review-search-input"
                  v-model="searchQuery"
                  type="search"
                  class="modern-input"
                  placeholder="Ej. I23050363, Mariana, INF o CA..."
                  aria-label="Buscar documento en cola de revisión"
                  autocomplete="off"
                  @input="handleSearchInput"
                  @focus="handleSearchFocus"
                  @blur="handleSearchBlur"
                  @keyup.enter="isAutocompleteOpen = false; page = 1; loadQueue()"
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
              <div
                v-if="isAutocompleteOpen && autocompleteSuggestions.length > 0"
                class="suggestions-dropdown"
                role="listbox"
              >
                <div
                  v-for="item in autocompleteSuggestions"
                  :key="item.id"
                  class="suggestion-item"
                  role="option"
                  @mousedown.prevent="selectSuggestion(item)"
                >
                  <div class="suggestion-left">
                    <span class="suggestion-control">
                      {{ item.studentControlNumber }} · {{ item.documentCode }}
                    </span>
                    <span class="suggestion-name">{{ item.studentFullName }}</span>
                  </div>
                  <div class="suggestion-right">
                    <span class="suggestion-career">{{ item.careerCode }}</span>
                    <span class="suggestion-date">{{ formatSubmittedDate(item.createdAt) }}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Selector de Tipo de Trámite (sin filtro de Id de periodo) -->
          <div class="select-box">
            <label class="filter-label" for="review-tramite-type">Tipo de trámite</label>
            <div class="select-wrapper">
              <select
                id="review-tramite-type"
                v-model="modalityFilter"
                class="modern-select"
              >
                <option value="all">Todos los trámites</option>
                <option value="general">General / IMSS</option>
                <option v-if="canReadProcess('social_service')" value="social_service">
                  Servicio Social
                </option>
                <option v-if="canReadProcess('internship')" value="internship">
                  Prácticas Profesionales
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

          <!-- Pestañas de Estatus -->
          <div class="toolbar-right">
            <span class="filter-label">Estatus de dictamen</span>
            <div class="status-tabs-row">
              <div class="status-tabs" role="tablist" aria-label="Filtrar por estatus">
                <button
                  type="button"
                  class="status-tab"
                  :class="{ 'status-tab--active': statusFilter === 'all' }"
                  @click="statusFilter = 'all'"
                >
                  Todos
                </button>
                <button
                  type="button"
                  class="status-tab"
                  :class="{ 'status-tab--active': statusFilter === 'pending' }"
                  @click="statusFilter = 'pending'"
                >
                  Pendientes
                </button>
                <button
                  type="button"
                  class="status-tab"
                  :class="{ 'status-tab--active': statusFilter === 'approved' }"
                  @click="statusFilter = 'approved'"
                >
                  Aprobados
                </button>
                <button
                  type="button"
                  class="status-tab"
                  :class="{ 'status-tab--active': statusFilter === 'rejected' }"
                  @click="statusFilter = 'rejected'"
                >
                  Observados
                </button>
              </div>

              <UiButton
                v-if="searchQuery || modalityFilter !== 'all' || statusFilter !== 'all' || docCodeFilter"
                variant="secondary"
                size="sm"
                @click="resetAllFilters"
              >
                Limpiar
              </UiButton>
            </div>
          </div>
        </div>

        <!-- Tabla de Revisión con scroll interno y orden más reciente primero -->
        <div class="tray-table-wrap" role="region" aria-label="Cola de Documentos">
          <table class="tray-table">
            <thead>
              <tr>
                <th scope="col">ESTUDIANTE / CARRERA</th>
                <th scope="col">DOCUMENTO &amp; MODALIDAD</th>
                <th scope="col">ARCHIVO &amp; FECHA DE ENVÍO</th>
                <th scope="col">ESTATUS SGC</th>
                <th scope="col" class="th-actions">ACCIÓN</th>
              </tr>
            </thead>
            <TransitionGroup name="tray-row" tag="tbody">
              <tr v-for="row in filteredQueue" :key="row.id" class="tray-tr">
                <td>
                  <div class="cell-student">
                    <strong>{{ row.studentFullName }}</strong>
                    <span>{{ row.studentControlNumber }} · {{ row.careerCode }}</span>
                  </div>
                </td>

                <td>
                  <div class="cell-doc">
                    <div class="doc-head-line">
                      <span class="doc-code-badge">{{ row.documentCode }}</span>
                      <strong>
                        {{
                          row.tramiteType === 'social_service'
                            ? socialDocumentLabel(row.documentCode)
                            : getDocMeta(row.documentCode).title
                        }}
                      </strong>
                      <span
                        v-if="activeReviewers[row.id]"
                        class="reviewer-live-badge"
                        :title="`Abierto en dictamen por ${activeReviewers[row.id]?.reviewerName}`"
                      >
                        <span class="reviewer-live-dot" aria-hidden="true" />
                        {{
                          activeReviewers[row.id]?.reviewerUserId === sessionUser?.id
                            ? 'En revisión por ti'
                            : `Revisando: ${activeReviewers[row.id]?.reviewerName}`
                        }}
                      </span>
                    </div>
                    <span
                      :class="
                        row.tramiteType === 'social_service' ? 'mod-sub--navy' : 'mod-sub--gold'
                      "
                    >
                      {{
                        row.tramiteType === 'social_service'
                          ? 'Servicio Social (480h)'
                          : row.tramiteType === 'general'
                            ? 'General / IMSS'
                            : 'Prácticas Profesionales'
                      }}
                      ·
                      {{
                        row.tramiteType === 'social_service'
                          ? socialDocumentPeriod(row.documentCode)
                          : getDocMeta(row.documentCode).anexo
                      }}
                    </span>
                  </div>
                </td>

                <td>
                  <div class="cell-file">
                    <span class="file-name" :title="row.originalFileName">
                      {{ row.originalFileName }}
                    </span>
                    <small class="file-meta">
                      Enviado: <strong>{{ formatSubmittedDate(row.createdAt) }}</strong> · PDF ({{
                        formatFileSize(row.fileSizeBytes)
                      }})
                    </small>
                  </div>
                </td>

                <td>
                  <UiBadge
                    :tone="
                      row.status === 'approved'
                        ? 'success'
                        : row.status === 'rejected'
                          ? 'danger'
                          : 'warning'
                    "
                  >
                    {{
                      row.status === 'approved'
                        ? 'Aprobado SGC'
                        : row.status === 'rejected'
                          ? 'Observado'
                          : 'Por Dictaminar'
                    }}
                  </UiBadge>
                </td>

                <td class="td-actions">
                  <div class="row-actions">
                    <UiButton variant="secondary" size="sm" @click="handleViewPdf(row)">
                      Ver PDF
                    </UiButton>
                    <UiButton
                      :variant="row.status === 'pending' ? 'primary' : 'secondary'"
                      size="sm"
                      @click="openReview(row)"
                    >
                      Dictaminar
                    </UiButton>
                  </div>
                </td>
              </tr>
            </TransitionGroup>
          </table>

          <div v-if="!isLoading && filteredQueue.length === 0" class="empty-queue">
            <p>No hay documentos que coincidan con los filtros seleccionados.</p>
            <UiButton variant="secondary" size="sm" @click="resetAllFilters">
              Restablecer bandeja
            </UiButton>
          </div>
        </div>

        <!-- Barra de Paginación Estandarizada (10 por página) -->
        <footer class="pagination-bar">
          <span class="pagination-info">
            Mostrando <strong>{{ filteredQueue.length }}</strong> de <strong>{{ totalItems }}</strong> documentos · Página <strong>{{ page }}</strong> de <strong>{{ Math.max(1, totalPages) }}</strong>
            <template v-if="loadError">
              · Error al cargar. Actualiza la bandeja para reintentar.
            </template>
          </span>
          <div class="pagination-controls">
            <UiButton
              variant="secondary"
              size="sm"
              :disabled="page <= 1 || isLoading"
              @click="page--; loadQueue()"
            >
              Anterior
            </UiButton>
            <UiButton
              variant="secondary"
              size="sm"
              :disabled="page >= totalPages || isLoading"
              @click="page++; loadQueue()"
            >
              Siguiente
            </UiButton>
          </div>
        </footer>
      </UiCard>

      <!-- Columna Derecha: Filtro por Código de Anexo + Criterios de Validación -->
      <aside class="right-stack">
        <!-- Tarjeta Superior: Filtro por Código de Formato -->
        <UiCard class="side-card side-card--codes">
          <div class="side-card__head">
            <h2 class="side-card__title">FORMATOS OFICIALES</h2>
            <span class="side-card__meta">Filtrar por clave</span>
          </div>

          <div class="code-filter-list">
            <button
              v-for="item in documentCodeChips"
              :key="item.code"
              type="button"
              class="doc-chip-btn"
              :class="{ 'doc-chip-btn--active': docCodeFilter === item.code }"
              @click="docCodeFilter = docCodeFilter === item.code ? null : item.code"
            >
              <span class="doc-chip-btn__code">{{ item.code }}</span>
              <span class="doc-chip-btn__label">
                {{ item.label }}
                <small v-if="item.period">{{ item.period }}</small>
              </span>
              <span class="doc-chip-btn__count">{{ item.count }}</span>
            </button>
          </div>
        </UiCard>

        <!-- Tarjeta Inferior: Reglas de Dictamen SGC -->
        <UiCard class="side-card side-card--criteria">
          <div class="side-card__head">
            <div class="side-card__title-group">
              <svg viewBox="0 0 24 24" aria-hidden="true" class="side-card__icon">
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                <path d="m9 12 2 2 4-4" />
              </svg>
              <h2 class="side-card__title">CRITERIOS DE DICTAMEN</h2>
            </div>
            <span class="official-tag">SGC</span>
          </div>

          <div class="criteria-list">
            <article class="criteria-box">
              <div class="criteria-box__top">
                <span class="criteria-tag criteria-tag--gold">SELLO Y FIRMA</span>
                <span class="criteria-scope">Obligatorio</span>
              </div>
              <h3 class="criteria-title">Rúbrica autógrafa y sello húmedo</h3>
              <p class="criteria-desc">
                Verificar que la Carta de Aceptación y Reportes Bimestrales cuenten con sello legible de la Unidad Receptora.
              </p>
            </article>

            <article class="criteria-box">
              <div class="criteria-box__top">
                <span class="criteria-tag criteria-tag--blue">INTEGRIDAD PDF</span>
                <span class="criteria-scope">≤ 5.0 MB</span>
              </div>
              <h3 class="criteria-title">Formato completo sin recortes</h3>
              <p class="criteria-desc">
                Confirmar que el número de control, periodo y horas acumuladas coincidan con el expediente.
              </p>
            </article>
          </div>
        </UiCard>
      </aside>
    </div>

    <!-- Modal 1: Previsualizador Institucional de PDF Ampliado (size="full") -->
    <UiDialog
      v-model="isPdfViewerOpen"
      size="full"
      :title="
        pdfPreviewItem
          ? `Inspección PDF · ${pdfPreviewItem.originalFileName}`
          : 'Visor de Documento'
      "
    >
      <div v-if="pdfPreviewItem" class="pdf-preview-sheet">
        <div class="pdf-preview-header">
          <div class="pdf-preview-meta">
            <span class="doc-code-badge">{{ pdfPreviewItem.documentCode }}</span>
            <span class="pdf-preview-student">
              {{ pdfPreviewItem.studentFullName }} ({{ pdfPreviewItem.studentControlNumber }} · {{ pdfPreviewItem.careerCode }})
            </span>
            <span class="pdf-preview-date">
              Enviado: {{ formatSubmittedDate(pdfPreviewItem.createdAt) }} · {{ formatFileSize(pdfPreviewItem.fileSizeBytes) }}
            </span>
          </div>
          <div class="pdf-preview-header-right">
            <a
              v-if="pdfBlobUrl"
              :href="pdfBlobUrl"
              :download="pdfPreviewItem.originalFileName"
              class="pdf-download-link"
            >
              Descargar PDF
            </a>
            <UiBadge
              :tone="
                pdfPreviewItem.status === 'approved'
                  ? 'success'
                  : pdfPreviewItem.status === 'rejected'
                    ? 'danger'
                    : 'warning'
              "
            >
              {{
                pdfPreviewItem.status === 'approved'
                  ? 'Aprobado'
                  : pdfPreviewItem.status === 'rejected'
                    ? 'Observado'
                    : 'En Revisión'
              }}
            </UiBadge>
          </div>
        </div>

        <div v-if="pdfBlobUrl" class="pdf-iframe-container">
          <iframe
            :src="pdfBlobUrl"
            class="pdf-iframe"
            title="Vista previa del documento PDF"
          />
        </div>

        <UiAlert v-else tone="warning">
          No fue posible abrir el PDF. Cierra el visor y vuelve a intentar.
        </UiAlert>
      </div>

      <template #actions>
        <UiButton variant="secondary" size="sm" @click="isPdfViewerOpen = false">
          Cerrar Visor
        </UiButton>
        <UiButton variant="primary" size="sm" @click="proceedFromViewerToReview">
          Ir a Dictaminar →
        </UiButton>
      </template>
    </UiDialog>

    <!-- Modal 2: Dictamen Técnico de Documento -->
    <DocumentReviewDialog
      v-model="isDialogOpen"
      :document="selectedItem"
      :student-name="selectedItem?.studentFullName"
      @reviewed="loadQueue"
    />

    <UiToast ref="toast" />
  </UiBasePage>
</template>

<style scoped>
/* Estructura a pantalla completa sin scroll externo */
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

.header-quick-actions {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

/* 4 KPIs con fondo neutro y borde dorado al estar activo */
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

/* Grid Principal de 2 Columnas a Pantalla Completa */
.workspace-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 295px;
  gap: 0.75rem;
  flex: 1;
  min-height: 0;
}

.tray-card {
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
  gap: 0.7rem;
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
  min-width: 240px;
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.select-box {
  flex: 0.9;
  min-width: 170px;
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.toolbar-right {
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.status-tabs-row {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  flex-wrap: wrap;
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

.suggestion-date {
  font-size: 0.72rem;
  color: var(--color-text-secondary);
}

/* Pestañas de Estatus */
.status-tabs {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  padding: 3px;
  height: 34px;
  border-radius: 7px;
  background: var(--input-bg);
  border: 1px solid var(--input-border);
}

.status-tab {
  min-height: 26px !important;
  height: 26px;
  padding: 0 9px;
  border: 1px solid transparent;
  border-radius: 5px;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 0.75rem;
  font-weight: 600;
  cursor: pointer;
  transition: background-color 150ms ease, color 150ms ease, border-color 150ms ease;
}

.status-tab:hover {
  color: var(--color-text-primary);
  background: var(--secondary-hover);
}

.status-tab--active {
  background: var(--color-primary);
  color: var(--color-white);
  border-color: var(--color-accent);
}

/* Tabla Unificada con scroll interno */
.tray-table-wrap {
  flex: 1;
  min-height: 0;
  overflow: auto;
  margin: 0 -16px;
  border-top: 1px solid var(--color-border);
}

.tray-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  white-space: normal;
}

.tray-table th {
  padding: 7px 12px;
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

.tray-table td {
  padding: 6px 12px;
  font-size: 0.8rem;
  vertical-align: middle;
  border-bottom: 1px solid var(--color-border-light);
}

.tray-tr {
  transition: background-color 150ms ease;
}

.tray-tr:hover {
  background: var(--color-primary-subtle);
}

.th-actions,
.td-actions {
  width: 176px;
  text-align: center;
  white-space: nowrap;
}

.cell-student {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.cell-student strong {
  font-size: 0.81rem;
  font-weight: 600;
  color: var(--color-text-primary);
}

.cell-student span {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.74rem;
  color: var(--color-text-secondary);
}

.cell-doc {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.doc-head-line {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex-wrap: wrap;
}

.doc-code-badge {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.7rem;
  font-weight: 800;
  padding: 0.08rem 0.4rem;
  border-radius: 4px;
  background: var(--color-info-badge);
  color: var(--color-info-text);
  border: 1px solid var(--color-info-border);
}

.doc-head-line strong {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--color-text-primary);
}

.reviewer-live-badge {
  display: inline-flex;
  align-items: center;
  gap: 0.32rem;
  font-size: 0.69rem;
  font-weight: 700;
  padding: 0.08rem 0.44rem;
  border-radius: 999px;
  background: var(--color-warning-badge);
  color: var(--color-warning-text);
  border: 1px solid var(--color-warning-border);
  white-space: nowrap;
}

.reviewer-live-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--color-accent);
  flex-shrink: 0;
}

.mod-sub--navy,
.mod-sub--gold {
  font-size: 0.73rem;
  color: var(--color-text-secondary);
}

.cell-file {
  display: flex;
  flex-direction: column;
  gap: 0.12rem;
}

.file-name {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.76rem;
  font-weight: 600;
  color: var(--color-text-primary);
  max-width: 240px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-meta {
  font-size: 0.72rem;
  color: var(--color-text-secondary);
}

.file-meta strong {
  color: var(--color-text-primary);
  font-weight: 600;
}

.row-actions {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.4rem;
}

.empty-queue {
  padding: 1.5rem;
  text-align: center;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.65rem;
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

/* Columna Derecha Compacta */
.right-stack {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
  min-height: 0;
  overflow: hidden;
}

.side-card {
  padding: 10px 12px !important;
  border: 1px solid var(--color-border);
  background: var(--color-surface-white);
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.side-card--codes {
  flex: 1.15;
}

.side-card--criteria {
  flex: 0.85;
}

.side-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  padding-bottom: 0.45rem;
  margin-bottom: 0.45rem;
  border-bottom: 1px solid var(--color-border-light);
  flex-shrink: 0;
}

.side-card__title-group {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
}

.side-card__icon {
  width: 15px;
  height: 15px;
  fill: none;
  stroke: var(--color-accent);
  stroke-width: 2;
}

.side-card__title {
  margin: 0;
  font-size: 0.72rem;
  font-weight: 800;
  letter-spacing: 0.06em;
  color: var(--color-text-primary);
}

.side-card__meta,
.official-tag {
  font-size: 0.68rem;
  font-weight: 700;
  color: var(--color-text-secondary);
}

.code-filter-list {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  overflow-y: auto;
  min-height: 0;
  padding-right: 2px;
}

.doc-chip-btn {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  width: 100%;
  min-height: 30px !important;
  padding: 0.28rem 0.5rem;
  border-radius: 6px;
  border: 1px solid var(--color-border);
  background: var(--color-bg-light);
  color: var(--color-text-primary);
  text-align: left;
  cursor: pointer;
  transition: background-color 150ms ease, border-color 150ms ease, transform 140ms ease;
}

.doc-chip-btn:hover {
  background: var(--secondary-hover);
  border-color: var(--color-accent);
  transform: translateX(2px);
}

.doc-chip-btn--active {
  background: var(--kpi-active-bg);
  border-color: var(--color-accent) !important;
  box-shadow: 0 0 0 1px var(--color-accent);
}

.doc-chip-btn__code {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.68rem;
  font-weight: 800;
  padding: 0.08rem 0.35rem;
  border-radius: 4px;
  background: var(--color-info-badge);
  color: var(--color-info-text);
  border: 1px solid var(--color-info-border);
  flex-shrink: 0;
}

.doc-chip-btn__label {
  flex: 1;
  min-width: 0;
  font-size: 0.73rem;
  font-weight: 600;
  line-height: 1.2;
  color: var(--color-text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.doc-chip-btn__label small {
  display: block;
  font-size: 0.66rem;
  font-weight: 500;
  color: var(--color-text-secondary);
}

.doc-chip-btn__count {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.72rem;
  font-weight: 700;
  color: var(--color-text-secondary);
  flex-shrink: 0;
}

.criteria-list {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  overflow-y: auto;
  min-height: 0;
}

.criteria-box {
  padding: 0.5rem 0.65rem;
  border-radius: 7px;
  border: 1px solid var(--color-border);
  background: var(--color-bg-light);
}

.criteria-box__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.4rem;
  margin-bottom: 0.2rem;
}

.criteria-tag {
  font-size: 0.64rem;
  font-weight: 800;
  letter-spacing: 0.05em;
  padding: 0.08rem 0.38rem;
  border-radius: 4px;
}

.criteria-tag--gold {
  background: var(--color-warning-badge);
  color: var(--color-warning-text);
  border: 1px solid var(--color-warning-border);
}

.criteria-tag--blue {
  background: var(--color-info-badge);
  color: var(--color-info-text);
  border: 1px solid var(--color-info-border);
}

.criteria-scope {
  font-size: 0.68rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.criteria-title {
  margin: 0 0 0.15rem;
  font-size: 0.76rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.criteria-desc {
  margin: 0;
  font-size: 0.71rem;
  line-height: 1.3;
  color: var(--color-text-secondary);
}

/* Modal Visor de PDF Ampliado y Responsivo */
.pdf-preview-sheet {
  display: flex;
  flex-direction: column;
  width: 100%;
  min-width: 0;
  box-sizing: border-box;
  gap: 0.65rem;
}

.pdf-preview-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  min-width: 0;
  box-sizing: border-box;
  gap: 0.75rem;
  padding: 0.55rem 0.85rem;
  border-radius: 8px;
  background: var(--surface-toolbar);
  border: 1px solid var(--color-border);
  flex-wrap: wrap;
  flex-shrink: 0;
}

.pdf-preview-meta {
  display: flex;
  align-items: center;
  min-width: 0;
  gap: 0.55rem;
  flex-wrap: wrap;
}

.pdf-preview-student {
  font-size: 0.84rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.pdf-preview-date {
  font-size: 0.76rem;
  color: var(--color-text-secondary);
}

.pdf-preview-header-right {
  display: flex;
  align-items: center;
  gap: 0.6rem;
}

.pdf-download-link {
  font-size: 0.76rem;
  font-weight: 700;
  padding: 0.22rem 0.6rem;
  border-radius: 6px;
  background: var(--color-surface-white);
  border: 1px solid var(--color-border-medium);
  color: var(--color-text-primary);
  transition: background-color 150ms ease, border-color 150ms ease;
}

.pdf-download-link:hover {
  background: var(--secondary-hover);
  border-color: var(--color-accent);
}

.pdf-iframe-container {
  width: 100%;
  min-width: 0;
  display: flex;
  flex-direction: column;
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid var(--color-border-medium);
  background: var(--color-bg-light);
}

.pdf-iframe {
  width: 100%;
  height: min(72dvh, 720px);
  border: 0;
}

.tray-row-enter-active,
.tray-row-leave-active {
  transition: opacity 180ms ease, transform 180ms ease;
}

.tray-row-enter-from,
.tray-row-leave-to {
  opacity: 0;
  transform: translateY(4px);
}

@media (max-width: 1100px) {
  .workspace-grid {
    grid-template-columns: minmax(0, 1fr);
  }
  .right-stack {
    display: none;
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

  .workspace-grid {
    min-width: 0;
    max-width: 100%;
  }

  .tray-card {
    min-width: 0;
    max-width: 100%;
    overflow: hidden;
    padding: 10px 12px !important;
  }

  .tray-table-wrap {
    margin: 0 -12px;
    max-width: calc(100% + 24px);
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
  }

  .tray-table {
    min-width: 660px;
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
  .toolbar-right {
    min-width: 0;
    width: 100%;
    flex: 1 1 auto;
  }

  .status-tabs {
    display: flex;
    width: 100%;
    max-width: 100%;
    overflow-x: auto;
    scrollbar-width: none;
    height: auto;
    min-height: 36px;
  }

  .status-tabs::-webkit-scrollbar {
    display: none;
  }

  .status-tab {
    flex: 1 0 auto;
    min-height: 30px !important;
    height: 30px;
    text-align: center;
  }

  input.modern-input,
  select.modern-select {
    min-height: 40px;
    height: 40px;
    font-size: 16px;
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

  .pdf-preview-header {
    flex-direction: column;
    align-items: stretch;
    gap: 0.5rem;
  }

  .pdf-preview-header-right {
    justify-content: space-between;
    width: 100%;
  }

  .pdf-iframe {
    height: min(56dvh, 440px);
  }
}
</style>
