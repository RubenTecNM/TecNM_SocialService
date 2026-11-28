<script setup lang="ts">
import { problemMessage } from '../../shared/api/problemDetails'
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch, watch as watchValidation } from 'vue'
import { useFormValidation } from '../../shared/composables/useFormValidation'
import { validateFields, optionalEmail } from '../../shared/utils/validationRules'
import UiFormErrors from '../../shared/components/UiFormErrors.vue'
import {
  UiAlert,
  UiBadge,
  UiBasePage,
  UiButton,
  UiCard,
  UiDialog,
  UiField,
  UiToast,
} from '../../shared/components'
import { hasRole } from '../../shared/composables/useSession'
import { deleteCompany, getCompanies, saveCompany, setCompanyStatus } from './api'
import { companyTypeLabels, type Company, type CompanyRequest, type CompanyType } from './types'
import CompanyRelations from './CompanyRelations.vue'

const canManage = computed(() => hasRole('department_head', 'social_service_officer', 'internship_coordinator'))

const rows = ref<Company[]>([])
const kpiPool = ref<Company[]>([])
const search = ref('')
const type = ref('')
const status = ref('')
const kpiMode = ref<'all' | 'active' | 'inactive' | 'internal'>('all')

// Paginación estandarizada (10 registros por página)
const page = ref(1)
const pageSize = 10
const totalItems = ref(0)
const pages = ref(1)

const loading = ref(false)
const busy = ref(false)
const error = ref('')
const formError = ref('')
const open = ref(false)
const editingId = ref<number | null>(null)
const deleting = ref<Company | null>(null)
const selected = ref<Company | null>(null)
const toast = ref<InstanceType<typeof UiToast> | null>(null)

// Autocompletado con debounce de 280 ms
const autocompleteSuggestions = ref<Company[]>([])
const isAutocompleteOpen = ref(false)

const blank = (): CompanyRequest => ({
  name: '',
  rfc: null,
  commercialName: null,
  companyType: '',
  sector: 'private',
  companySize: 'not_applicable',
  isTecnmInternal: false,
  address: null,
  city: 'Monclova',
  state: 'Coahuila',
  postalCode: null,
  phoneNumber: null,
  email: null,
})
const form = reactive(blank())

const typeOptions = Object.entries(companyTypeLabels).map(([value, label]) => ({ value, label }))
let controller: AbortController | undefined
let timer: ReturnType<typeof setTimeout> | undefined
let sequence = 0

// KPIs animados
const animatedKpis = reactive({
  total: 0,
  active: 0,
  inactive: 0,
  internal: 0,
})

function updateAnimatedKpis(): void {
  const source = kpiPool.value.length > 0 ? kpiPool.value : rows.value
  const targets = {
    total: kpiPool.value.length > 0 ? kpiPool.value.length : totalItems.value,
    active: source.filter((c) => c.isActive).length,
    inactive: source.filter((c) => !c.isActive).length,
    internal: source.filter((c) => c.isTecnmInternal).length,
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
    animatedKpis.active = Math.round(targets.active * ease)
    animatedKpis.inactive = Math.round(targets.inactive * ease)
    animatedKpis.internal = Math.round(targets.internal * ease)
    if (progress < 1) requestAnimationFrame(tick)
  }
  requestAnimationFrame(tick)
}

async function loadKpiPool(): Promise<void> {
  try {
    const result = await getCompanies({ page: 1, pageSize: 100 })
    kpiPool.value = result.items || []
  } catch {
    kpiPool.value = []
  } finally {
    updateAnimatedKpis()
  }
}

async function load(): Promise<void> {
  controller?.abort()
  controller = new AbortController()
  const request = ++sequence
  loading.value = true
  error.value = ''
  try {
    const result = await getCompanies(
      {
        page: page.value,
        pageSize,
        search: search.value.trim() || undefined,
        companyType: type.value || undefined,
        isActive: status.value ? status.value === 'true' : undefined,
      },
      controller.signal,
    )
    if (request !== sequence) return
    const loadedItems = result.items || []
    rows.value =
      kpiMode.value === 'internal'
        ? loadedItems.filter((item) => item.isTecnmInternal)
        : loadedItems
    totalItems.value =
      kpiMode.value === 'internal' ? rows.value.length : result.totalItems
    pages.value = Math.max(1, result.totalPages)
  } catch (e: any) {
    if (request === sequence && e.code !== 'ERR_CANCELED') {
      rows.value = []
      error.value = problemMessage(e, 'No se pudo cargar el catálogo.')
    }
  } finally {
    if (request === sequence) loading.value = false
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
    const result = await getCompanies({ page: 1, pageSize: 6, search: q })
    autocompleteSuggestions.value = (result.items || []).slice(0, 6)
    isAutocompleteOpen.value = autocompleteSuggestions.value.length > 0
  } catch {
    autocompleteSuggestions.value = []
    isAutocompleteOpen.value = false
  }
}

function handleSearchInput(): void {
  clearTimeout(timer)
  timer = setTimeout(() => {
    page.value = 1
    void fetchSuggestions(search.value)
    void load()
  }, 280)
}

function selectSuggestion(company: Company): void {
  search.value = company.name
  isAutocompleteOpen.value = false
  page.value = 1
  void load()
}

function clearSearch(): void {
  clearTimeout(timer)
  search.value = ''
  autocompleteSuggestions.value = []
  isAutocompleteOpen.value = false
  page.value = 1
  void load()
}

function handleSearchFocus(): void {
  if (search.value.trim().length > 0 && autocompleteSuggestions.value.length > 0) {
    isAutocompleteOpen.value = true
  }
}

function handleSearchBlur(): void {
  setTimeout(() => {
    isAutocompleteOpen.value = false
  }, 180)
}

function selectKpi(mode: 'all' | 'active' | 'inactive' | 'internal'): void {
  kpiMode.value = kpiMode.value === mode && mode !== 'all' ? 'all' : mode
  if (kpiMode.value === 'all') {
    status.value = ''
  } else if (kpiMode.value === 'active') {
    status.value = 'true'
  } else if (kpiMode.value === 'inactive') {
    status.value = 'false'
  } else {
    status.value = ''
  }
  page.value = 1
  void load()
}

function resetFilters(): void {
  clearTimeout(timer)
  search.value = ''
  type.value = ''
  status.value = ''
  kpiMode.value = 'all'
  autocompleteSuggestions.value = []
  isAutocompleteOpen.value = false
  page.value = 1
  void load()
}

const hasActiveFilters = computed(() =>
  Boolean(search.value.trim() || type.value || status.value || kpiMode.value !== 'all'),
)

watch([type, status], () => {
  if (status.value === 'true') kpiMode.value = 'active'
  else if (status.value === 'false') kpiMode.value = 'inactive'
  else if (kpiMode.value !== 'internal') kpiMode.value = 'all'
  page.value = 1
  void load()
})

watch(page, () => {
  void load()
})

onBeforeUnmount(() => {
  sequence++
  clearTimeout(timer)
  controller?.abort()
})

function edit(company?: Company): void {
  editingId.value = company?.id ?? null
  Object.assign(form, company ?? blank())
  formError.value = ''
  open.value = true
}

async function save(): Promise<void> {
  if (!companyValidation.validate()) return
  if (busy.value) return
  busy.value = true
  formError.value = ''
  try {
    const privateType = ['micro', 'small', 'medium', 'large'].includes(form.companyType)
    const payload: CompanyRequest = {
      ...form,
      sector:
        form.sector === 'educational'
          ? 'educational'
          : privateType
            ? 'private'
            : form.companyType,
      companySize: privateType ? form.companyType : form.companySize,
    }
    const wasEditing = editingId.value !== null
    await saveCompany(editingId.value, payload)
    open.value = false
    toast.value?.show(
      wasEditing ? 'Empresa actualizada correctamente.' : 'Empresa registrada en el catálogo.',
    )
    await Promise.all([loadKpiPool(), load()])
  } catch (e: any) {
    companyValidation.apply(e, 'No se pudo guardar el formulario.')
    formError.value =
      Object.values(e.response?.data?.errors ?? {})
        .flat()
        .join(' ') || problemMessage(e, 'No se pudo guardar la empresa.')
  } finally {
    busy.value = false
  }
}

async function change(company: Company, remove = false): Promise<void> {
  if (busy.value) return
  busy.value = true
  error.value = ''
  try {
    if (remove) {
      await deleteCompany(company.id)
      toast.value?.show(`Empresa "${company.name}" eliminada del catálogo.`)
    } else {
      await setCompanyStatus(company.id, !company.isActive)
      toast.value?.show(
        `Empresa "${company.name}" ${!company.isActive ? 'activada' : 'desactivada'}.`,
      )
    }
    deleting.value = null
    await Promise.all([loadKpiPool(), load()])
  } catch (e: any) {
    error.value = problemMessage(e, 'No se pudo actualizar la empresa.')
    deleting.value = null
  } finally {
    busy.value = false
  }
}

function typeLabel(value: string): string {
  return companyTypeLabels[value as CompanyType] || value
}

const companyValidation = useFormValidation(() =>
  validateFields(form, {
    name: { required: true, max: 200 },
    commercialName: { max: 200 },
    companyType: { required: true },
    postalCode: {
      pattern: /^[0-9]{5}$/,
      message: 'El código postal debe contener 5 dígitos.',
    },
    phoneNumber: { max: 30 },
    email: optionalEmail,
  }),
)
watchValidation(open, () => companyValidation.reset())

onMounted(async () => {
  await Promise.all([loadKpiPool(), load()])
})
</script>

<template>
  <UiBasePage :fluid="true" class="fullscreen-page">
    <!-- Encabezado Ejecutivo Compacto con 4 KPIs Superiores (solo borde dorado activo) -->
    <header class="exec-header">
      <div class="exec-header__left">
        <div class="exec-eyebrow">
          <span class="exec-badge">CATÁLOGO INSTITUCIONAL DE VINCULACIÓN</span>
          <span class="exec-period">· Registro de Dependencias, Sectores y Convenios</span>
        </div>
        <div class="title-row">
          <h1 class="exec-title">Empresas y Dependencias Receptoras</h1>
          <UiButton v-if="canManage" variant="primary" size="sm" @click="edit()">
            + Registrar empresa
          </UiButton>
        </div>
      </div>

      <!-- 4 KPIs con superficie neutra y borde dorado únicamente cuando está activo -->
      <div class="exec-kpis" aria-label="Indicadores del catálogo de empresas">
        <button
          type="button"
          class="mini-kpi"
          :class="{ 'mini-kpi--active': kpiMode === 'all' }"
          @click="selectKpi('all')"
        >
          <span class="mini-kpi__label">Total Catálogo</span>
          <strong class="mini-kpi__value">{{ animatedKpis.total }}</strong>
        </button>

        <button
          type="button"
          class="mini-kpi mini-kpi--emerald"
          :class="{ 'mini-kpi--active': kpiMode === 'active' }"
          @click="selectKpi('active')"
        >
          <span class="mini-kpi__label">Activas</span>
          <strong class="mini-kpi__value">{{ animatedKpis.active }}</strong>
        </button>

        <button
          type="button"
          class="mini-kpi mini-kpi--gold"
          :class="{ 'mini-kpi--active': kpiMode === 'inactive' }"
          @click="selectKpi('inactive')"
        >
          <span class="mini-kpi__label">Inactivas</span>
          <strong class="mini-kpi__value">{{ animatedKpis.inactive }}</strong>
        </button>

        <button
          type="button"
          class="mini-kpi mini-kpi--navy"
          :class="{ 'mini-kpi--active': kpiMode === 'internal' }"
          @click="selectKpi('internal')"
        >
          <span class="mini-kpi__label">Internas TecNM</span>
          <strong class="mini-kpi__value">{{ animatedKpis.internal }}</strong>
        </button>
      </div>
    </header>

    <UiAlert v-if="error" tone="danger" class="compact-alert">
      <div class="error-row">
        <span>{{ error }}</span>
        <UiButton variant="secondary" size="sm" @click="load">Reintentar</UiButton>
      </div>
    </UiAlert>

    <UiCard class="companies-card">
      <!-- Barra de Filtros con Buscador Autocomplete -->
      <div class="filters-bar">
        <div class="search-box">
          <label class="filter-label" for="companies-search-input">
            Buscar por Razón Social, Nombre Comercial o RFC
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
                id="companies-search-input"
                v-model="search"
                type="search"
                class="modern-input"
                placeholder="Ej. Gunderson, Presidencia o RFC..."
                aria-label="Buscar empresa por nombre o RFC"
                autocomplete="off"
                @input="handleSearchInput"
                @focus="handleSearchFocus"
                @blur="handleSearchBlur"
                @keyup.enter="isAutocompleteOpen = false; page = 1; load()"
              />
              <button
                v-if="search"
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
                  <span class="suggestion-name">{{ suggestion.name }}</span>
                  <span class="suggestion-sub">
                    {{ suggestion.rfc || 'Sin RFC' }} · {{ suggestion.city }}
                  </span>
                </div>
                <div class="suggestion-right">
                  <span class="suggestion-type">{{ typeLabel(suggestion.companyType) }}</span>
                  <UiBadge :tone="suggestion.isActive ? 'success' : 'warning'">
                    {{ suggestion.isActive ? 'Activa' : 'Inactiva' }}
                  </UiBadge>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="select-box">
          <label class="filter-label" for="companies-type-select">Tipo de empresa</label>
          <div class="select-wrapper">
            <select id="companies-type-select" v-model="type" class="modern-select">
              <option value="">Todos los tipos</option>
              <option v-for="opt in typeOptions" :key="opt.value" :value="opt.value">
                {{ opt.label }}
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
          <label class="filter-label" for="companies-status-select">Estado</label>
          <div class="select-wrapper">
            <select id="companies-status-select" v-model="status" class="modern-select">
              <option value="">Todos</option>
              <option value="true">Activas</option>
              <option value="false">Inactivas</option>
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

      <!-- Tabla con scroll interno y encabezados fijos -->
      <div class="companies-table-wrap" role="region" aria-label="Catálogo de empresas">
        <table class="companies-table">
          <thead>
            <tr>
              <th scope="col">EMPRESA O DEPENDENCIA</th>
              <th scope="col">TIPO</th>
              <th scope="col">CIUDAD</th>
              <th scope="col">ESTADO</th>
              <th scope="col" class="th-actions">ACCIONES</th>
            </tr>
          </thead>
          <TransitionGroup name="company-row" tag="tbody">
            <tr v-if="loading" key="loading">
              <td colspan="5" class="empty-cell">Cargando empresas…</td>
            </tr>
            <tr v-else-if="!rows.length" key="empty">
              <td colspan="5" class="empty-cell">
                <div class="empty-state">
                  <p>No hay empresas con estos filtros.</p>
                  <UiButton
                    v-if="hasActiveFilters"
                    variant="secondary"
                    size="sm"
                    @click="resetFilters"
                  >
                    Restablecer filtros
                  </UiButton>
                </div>
              </td>
            </tr>
            <tr v-for="row in rows" :key="row.id" class="company-tr">
              <td>
                <div class="company-cell">
                  <strong class="company-name">{{ row.name }}</strong>
                  <span class="company-meta">
                    {{ row.rfc ? `RFC: ${row.rfc}` : 'Sin RFC' }}
                    <template v-if="row.isTecnmInternal"> · Interna TecNM</template>
                  </span>
                </div>
              </td>
              <td>
                <span class="type-pill">{{ typeLabel(String(row.companyType)) }}</span>
              </td>
              <td>
                <span class="city-text">{{ row.city }}, {{ row.state }}</span>
              </td>
              <td>
                <UiBadge :tone="row.isActive ? 'success' : 'warning'">
                  {{ row.isActive ? 'Activa' : 'Inactiva' }}
                </UiBadge>
              </td>
              <td class="td-actions">
                <div class="row-actions">
                  <UiButton variant="secondary" size="sm" @click="selected = row">
                    Supervisores y convenios
                  </UiButton>
                  <template v-if="canManage">
                    <UiButton variant="secondary" size="sm" @click="edit(row)">
                      Editar
                    </UiButton>
                    <UiButton
                      variant="secondary"
                      size="sm"
                      :disabled="busy"
                      @click="change(row)"
                    >
                      {{ row.isActive ? 'Desactivar' : 'Activar' }}
                    </UiButton>
                    <UiButton variant="danger" size="sm" @click="deleting = row">
                      Eliminar
                    </UiButton>
                  </template>
                </div>
              </td>
            </tr>
          </TransitionGroup>
        </table>
      </div>

      <!-- Barra de Paginación Estandarizada (10 registros por página) -->
      <footer class="pagination-bar">
        <span class="pagination-info">
          Mostrando <strong>{{ rows.length }}</strong> de
          <strong>{{ totalItems }}</strong> empresas · Página
          <strong>{{ page }}</strong> de <strong>{{ Math.max(pages, 1) }}</strong>
        </span>
        <nav class="pagination-controls" aria-label="Páginas de empresas">
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="page <= 1 || loading"
            @click="page--"
          >
            Anterior
          </UiButton>
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="page >= pages || loading"
            @click="page++"
          >
            Siguiente
          </UiButton>
        </nav>
      </footer>
    </UiCard>

    <!-- Modal: Registrar / Editar Empresa -->
    <UiDialog
      v-model="open"
      :title="editingId ? 'Editar empresa' : 'Registrar empresa'"
      size="lg"
    >
      <form novalidate class="company-form" @submit.prevent="save">
        <UiAlert v-if="formError && !companyValidation.hasErrors.value" tone="danger">
          {{ formError }}
        </UiAlert>
        <UiFormErrors :validation="companyValidation" />
        <div class="form-grid-2">
          <UiField
            v-model="form.name"
            label="Nombre o razón social"
            required
            maxlength="200"
            v-bind="companyValidation.field('name', 'Nombre o razón social')"
          />
          <UiField
            v-model="form.companyType"
            label="Tipo de empresa"
            as="select"
            required
            :options="[{ value: '', label: 'Selecciona un tipo' }, ...typeOptions]"
            v-bind="companyValidation.field('companyType', 'Tipo de empresa')"
          />
        </div>
        <div class="form-grid-2">
          <UiField
            :model-value="form.rfc ?? ''"
            @update:model-value="form.rfc = $event"
            label="RFC"
            maxlength="13"
            v-bind="companyValidation.field('rfc', 'RFC')"
          />
          <UiField
            :model-value="form.commercialName ?? ''"
            @update:model-value="form.commercialName = $event"
            label="Nombre comercial"
            maxlength="200"
            v-bind="companyValidation.field('commercialName', 'Nombre comercial')"
          />
        </div>
        <div class="checkbox-row">
          <label class="check-option">
            <input
              type="checkbox"
              :checked="form.sector === 'educational'"
              @change="
                form.sector = ($event.target as HTMLInputElement).checked
                  ? 'educational'
                  : 'private'
              "
            />
            <span>Institución educativa (reporte 911)</span>
          </label>
          <label class="check-option">
            <input v-model="form.isTecnmInternal" type="checkbox" />
            <span>Dependencia interna TecNM</span>
          </label>
        </div>
        <UiField
          :model-value="form.address ?? ''"
          @update:model-value="form.address = $event"
          label="Domicilio"
          v-bind="companyValidation.field('address', 'Domicilio')"
        />
        <div class="form-grid-3">
          <UiField
            v-model="form.city"
            label="Ciudad"
            required
            maxlength="100"
            v-bind="companyValidation.field('city', 'Ciudad')"
          />
          <UiField
            v-model="form.state"
            label="Estado"
            required
            maxlength="100"
            v-bind="companyValidation.field('state', 'Estado')"
          />
          <UiField
            :model-value="form.postalCode ?? ''"
            @update:model-value="form.postalCode = $event"
            label="Código postal"
            pattern="[0-9]{5}"
            v-bind="companyValidation.field('postalCode', 'Código postal')"
          />
        </div>
        <div class="form-grid-2">
          <UiField
            :model-value="form.phoneNumber ?? ''"
            @update:model-value="form.phoneNumber = $event"
            label="Teléfono"
            type="tel"
            maxlength="30"
            v-bind="companyValidation.field('phoneNumber', 'Teléfono')"
          />
          <UiField
            :model-value="form.email ?? ''"
            @update:model-value="form.email = $event"
            label="Correo electrónico"
            type="email"
            maxlength="255"
            v-bind="companyValidation.field('email', 'Correo electrónico')"
          />
        </div>
        <div class="form-actions">
          <UiButton variant="secondary" size="sm" @click="open = false">Cancelar</UiButton>
          <UiButton type="submit" size="sm" :loading="busy">Guardar empresa</UiButton>
        </div>
      </form>
    </UiDialog>

    <!-- Modal: Confirmar Eliminación -->
    <UiDialog
      :model-value="!!deleting"
      @update:model-value="!$event && (deleting = null)"
      title="Eliminar empresa"
      size="sm"
    >
      <p class="delete-confirm-text">
        ¿Eliminar <strong>{{ deleting?.name }}</strong> del catálogo? Si tiene vínculos activos, el
        sistema impedirá eliminarla.
      </p>
      <template #footer>
        <UiButton variant="secondary" size="sm" @click="deleting = null">Cancelar</UiButton>
        <UiButton
          variant="danger"
          size="sm"
          :loading="busy"
          @click="deleting && change(deleting, true)"
        >
          Confirmar eliminación
        </UiButton>
      </template>
    </UiDialog>

    <!-- Modal: Supervisores y Convenios -->
    <UiDialog
      :model-value="!!selected"
      @update:model-value="!$event && (selected = null)"
      :title="selected?.name ?? 'Empresa'"
      size="xl"
    >
      <CompanyRelations
        v-if="selected"
        :key="selected.id"
        :company-id="selected.id"
        :can-manage="canManage"
      />
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

.mini-kpi--emerald .mini-kpi__value {
  color: var(--color-success);
}

.mini-kpi--gold .mini-kpi__value {
  color: var(--color-warning);
}

.mini-kpi--navy .mini-kpi__value {
  color: var(--color-info);
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

.companies-card {
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
  min-width: 240px;
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.select-box {
  flex: 0.85;
  min-width: 165px;
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

.suggestion-name {
  font-size: 0.81rem;
  font-weight: 600;
  color: var(--color-text-primary);
}

.suggestion-sub {
  font-size: 0.72rem;
  color: var(--color-text-secondary);
}

.suggestion-right {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.suggestion-type {
  font-size: 0.7rem;
  font-weight: 700;
  padding: 0.1rem 0.4rem;
  background: var(--color-primary-subtle);
  border-radius: 4px;
  color: var(--color-info-text);
}

/* Tabla con scroll interno y encabezados fijos */
.companies-table-wrap {
  flex: 1;
  min-height: 0;
  overflow: auto;
  margin: 0 -16px;
  border-top: 1px solid var(--color-border);
}

.companies-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  white-space: nowrap;
}

.companies-table th {
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

.companies-table td {
  padding: 7px 14px;
  font-size: 0.8rem;
  vertical-align: middle;
  border-bottom: 1px solid var(--color-border-light);
}

.company-tr {
  transition: background-color 150ms ease;
}

.company-tr:hover {
  background: var(--color-primary-subtle);
}

.company-cell {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.company-name {
  font-weight: 600;
  color: var(--color-text-primary);
}

.company-meta {
  font-size: 0.73rem;
  color: var(--color-text-secondary);
}

.type-pill {
  display: inline-block;
  padding: 0.14rem 0.5rem;
  border-radius: 5px;
  font-size: 0.73rem;
  font-weight: 600;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
  color: var(--color-text-primary);
}

.city-text {
  font-size: 0.79rem;
  color: var(--color-text-secondary);
}

.th-actions,
.td-actions {
  text-align: center;
}

.row-actions {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.35rem;
  flex-wrap: nowrap;
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

.pagination-controls {
  display: flex;
  gap: 0.4rem;
}

/* Formulario modal */
.company-form {
  display: flex;
  flex-direction: column;
  gap: 0.55rem;
}

.company-form :deep(.field) {
  margin-bottom: 0;
  gap: 4px;
  font-size: 13px;
}

.company-form :deep(input:not([type='checkbox']):not([type='radio'])),
.company-form :deep(select) {
  min-height: 36px;
  padding: 6px 10px;
  font-size: 13px;
}

.form-grid-2 {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
  gap: 0.75rem;
  align-items: start;
}

.form-grid-3 {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 0.75rem;
  align-items: start;
}

.checkbox-row {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
}

.check-option {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.84rem;
  color: var(--color-text-primary);
  cursor: pointer;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  padding-top: 0.5rem;
  border-top: 1px solid var(--color-border-light);
}

.delete-confirm-text {
  margin: 0;
  font-size: 0.9rem;
  line-height: 1.5;
  color: var(--color-text-primary);
}

.company-row-enter-active,
.company-row-leave-active {
  transition: opacity 180ms ease, transform 180ms ease;
}

.company-row-enter-from,
.company-row-leave-to {
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

  .companies-card {
    min-width: 0;
    max-width: 100%;
    overflow: hidden;
    padding: 10px 12px !important;
  }

  .companies-table-wrap {
    margin: 0 -12px;
    max-width: calc(100% + 24px);
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
  }

  .companies-table {
    min-width: 640px;
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
  select.modern-select,
  .company-form :deep(input:not([type='checkbox']):not([type='radio'])),
  .company-form :deep(select) {
    min-height: 40px;
    height: 40px;
    font-size: 16px;
  }

  .form-grid-2,
  .form-grid-3 {
    grid-template-columns: 1fr;
  }

  .form-actions :deep(.btn) {
    flex: 1;
    min-height: 38px;
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
