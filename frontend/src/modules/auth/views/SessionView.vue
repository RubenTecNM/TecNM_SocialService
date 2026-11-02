<script setup lang="ts">
import { isAxiosError } from 'axios'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  UiBadge,
  UiBasePage,
  UiButton,
  UiCard,
  UiDialog,
  UiToast,
} from '../../../shared/components'
import { me } from '../api/authApi'
import type { UserResponse } from '../types/auth'

const router = useRouter()
const user = ref<UserResponse | null>(null)
const barsReady = ref(false)
const toast = ref<InstanceType<typeof UiToast> | null>(null)

const animatedKpis = reactive({
  socialService: 0,
  internships: 0,
  pendingReports: 0,
  companies: 0,
})

function animateCounters(): void {
  const prefersReduced =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches

  const targets = {
    socialService: 142,
    internships: 58,
    pendingReports: 19,
    companies: 34,
  }

  if (prefersReduced) {
    Object.assign(animatedKpis, targets)
    barsReady.value = true
    return
  }

  const duration = 760
  const start = performance.now()

  function tick(now: number): void {
    const elapsed = now - start
    const progress = Math.min(elapsed / duration, 1)
    const ease = 1 - Math.pow(1 - progress, 4)

    animatedKpis.socialService = Math.round(targets.socialService * ease)
    animatedKpis.internships = Math.round(targets.internships * ease)
    animatedKpis.pendingReports = Math.round(targets.pendingReports * ease)
    animatedKpis.companies = Math.round(targets.companies * ease)

    if (progress < 1) {
      requestAnimationFrame(tick)
    }
  }

  requestAnimationFrame(tick)
  setTimeout(() => {
    barsReady.value = true
  }, 80)
}

const displayUser = computed<UserResponse>(() => {
  if (user.value) return user.value
  return {
    id: 1,
    username: 'admin',
    email: 'admin@monclova.tecnm.mx',
    name: 'Coordinación de Servicio Social y Prácticas',
    roles: ['social_service_officer'],
  }
})

async function loadSession(): Promise<void> {
  try {
    user.value = await me()
  } catch (error) {
    if (isAxiosError(error) && error.response?.status === 401) {
      return
    }
  }
}

onMounted(() => {
  void loadSession()
  animateCounters()
})

function notify(message: string): void {
  toast.value?.show(message)
}

function goToStudents(): void {
  router.push('/students')
}

function goToExpedients(): void {
  router.push('/expedients/review')
}

// Distribución por Carrera (Columna Derecha Superior)
interface CareerItem {
  code: string
  shortName: string
  count: number
  percentage: number
  tone: 'navy' | 'sky' | 'gold' | 'slate' | 'cyan' | 'emerald'
}

const careerStats: CareerItem[] = [
  { code: 'IND', shortName: 'Ing. Industrial', count: 48, percentage: 34, tone: 'navy' },
  { code: 'ISC', shortName: 'Ing. Informática / ISC', count: 36, percentage: 25, tone: 'sky' },
  { code: 'IGE', shortName: 'Ing. Gestión Empresarial', count: 28, percentage: 20, tone: 'gold' },
  { code: 'IEM', shortName: 'Ing. Electromecánica', count: 16, percentage: 11, tone: 'slate' },
  { code: 'IEL', shortName: 'Ing. Electrónica', count: 9, percentage: 6, tone: 'cyan' },
  { code: 'IER', shortName: 'Ing. Energías Renovables', count: 5, percentage: 4, tone: 'emerald' },
]

// Bandeja de Expedientes y Dictámenes Prioritarios (Columna Izquierda)
type ModalityFilter = 'all' | 'Servicio Social' | 'Prácticas Profesionales'

interface PriorityTramite {
  id: number
  studentName: string
  control: string
  careerCode: string
  careerLabel: string
  docTitle: string
  modality: 'Servicio Social' | 'Prácticas Profesionales'
  modalitySub: string
  company: string
  advisor: string
  projectTitle: string
  hoursCompleted: number
  hoursTotal: number
  statusText: string
  statusTone: 'warning' | 'success' | 'info' | 'purple' | 'danger'
  actionLabel: 'Dictaminar' | 'Liberar' | 'Revisar'
  actionVariant: 'primary' | 'outline'
}

const activeModality = ref<ModalityFilter>('all')
const activeCareer = ref<string | null>(null)
const selectedRow = ref<PriorityTramite | null>(null)
const isDialogOpen = ref(false)

const priorityTramites = ref<PriorityTramite[]>([
  {
    id: 1,
    studentName: 'Jorge Ramos Valdez',
    control: '20120812',
    careerCode: 'IGE',
    careerLabel: 'Ing. Gestión Empresarial',
    docTitle: 'Plan de Trabajo Prácticas',
    modality: 'Prácticas Profesionales',
    modalitySub: 'Prácticas Prof. (360h)',
    company: 'Trinity Rail de México',
    advisor: 'M.A. Claudia Domínguez Pérez',
    projectTitle: 'Estandarización de Indicadores Logísticos ISO 9001',
    hoursCompleted: 180,
    hoursTotal: 360,
    statusText: 'Por Dictaminar',
    statusTone: 'warning',
    actionLabel: 'Dictaminar',
    actionVariant: 'primary',
  },
  {
    id: 2,
    studentName: 'Mariana Ibarra Soto',
    control: '19120431',
    careerCode: 'IND',
    careerLabel: 'Ing. Industrial',
    docTitle: 'Carta Término (480h)',
    modality: 'Servicio Social',
    modalitySub: 'Servicio Social',
    company: 'DIF Municipal Monclova',
    advisor: 'Ing. Patricia Villarreal Soto',
    projectTitle: 'Optimización de Almacén y Distribución de Apoyos',
    hoursCompleted: 480,
    hoursTotal: 480,
    statusText: 'Horas Cubiertas',
    statusTone: 'success',
    actionLabel: 'Liberar',
    actionVariant: 'outline',
  },
  {
    id: 3,
    studentName: 'Ana Sofía Garza',
    control: '21120094',
    careerCode: 'ISC',
    careerLabel: 'Ing. Informática / ISC',
    docTitle: '2do Reporte Bimestral',
    modality: 'Servicio Social',
    modalitySub: 'Servicio Social',
    company: 'Altos Hornos de México (AHMSA)',
    advisor: 'M.C. Roberto Hernández Garza',
    projectTitle: 'Modernización de Red Institucional y Servidores',
    hoursCompleted: 320,
    hoursTotal: 480,
    statusText: 'Revisión Técnica',
    statusTone: 'info',
    actionLabel: 'Revisar',
    actionVariant: 'outline',
  },
  {
    id: 4,
    studentName: 'Karla Berenice Zapata',
    control: '20120409',
    careerCode: 'IEM',
    careerLabel: 'Ing. Electromecánica',
    docTitle: 'Informe Final Prácticas',
    modality: 'Prácticas Profesionales',
    modalitySub: 'Prácticas Prof. (360h)',
    company: 'Gunderson - GIMSA',
    advisor: 'Dr. Fernando Méndez Lozano',
    projectTitle: 'Mantenimiento Predictivo en Subestación de Planta',
    hoursCompleted: 360,
    hoursTotal: 360,
    statusText: 'Rúbrica Final',
    statusTone: 'warning',
    actionLabel: 'Dictaminar',
    actionVariant: 'primary',
  },
  {
    id: 5,
    studentName: 'Héctor Manuel Cantú',
    control: '20120199',
    careerCode: 'IEL',
    careerLabel: 'Ing. Electrónica',
    docTitle: '2do Reporte Bimestral',
    modality: 'Servicio Social',
    modalitySub: 'Servicio Social',
    company: 'Comisión Federal de Electricidad',
    advisor: 'Ing. Héctor Saldaña Ríos',
    projectTitle: 'Diagnóstico de Instrumentación y Telemetría Regional',
    hoursCompleted: 310,
    hoursTotal: 480,
    statusText: 'Observación',
    statusTone: 'warning',
    actionLabel: 'Revisar',
    actionVariant: 'outline',
  },
])

const visibleTramites = computed(() => {
  return priorityTramites.value.filter((item) => {
    const matchModality =
      activeModality.value === 'all' || item.modality === activeModality.value
    const matchCareer = !activeCareer.value || item.careerCode === activeCareer.value
    return matchModality && matchCareer
  })
})

function toggleModality(modality: ModalityFilter): void {
  activeModality.value = activeModality.value === modality ? 'all' : modality
}

function toggleCareer(code: string): void {
  activeCareer.value = activeCareer.value === code ? null : code
}

function openExpedienteDialog(row: PriorityTramite): void {
  selectedRow.value = row
  isDialogOpen.value = true
}

function confirmExpedienteAction(): void {
  if (!selectedRow.value) return
  const target = selectedRow.value
  target.statusText = 'Dictamen Validado'
  target.statusTone = 'success'
  target.actionLabel = 'Revisar'
  target.actionVariant = 'outline'
  isDialogOpen.value = false
  notify(
    `Expediente ${target.control} (${target.studentName}) dictaminado y sellado en el SGC.`,
  )
}

// Vencimientos Normativos (Columna Derecha Inferior)
const deadlines = [
  {
    date: '15 NOV 2026',
    scope: 'Servicio Social',
    title: 'Cierre 2do Reporte Bimestral',
    detail: 'Plataforma abierta hasta las 23:59 hrs para firmas de asesores.',
    tone: 'gold' as const,
  },
  {
    date: '28 NOV 2026',
    scope: 'SEP / TecNM',
    title: 'Captura de Cartas de Término',
    detail: 'Carga central de expedientes con horas acreditadas y liberadas.',
    tone: 'blue' as const,
  },
  {
    date: '05 DIC 2026',
    scope: 'Vinculación',
    title: 'Dictamen Final de Prácticas Prof.',
    detail: 'Cierre de actas de acreditación de proyectos con empresas.',
    tone: 'slate' as const,
  },
]
</script>

<template>
  <UiBasePage fluid>
    <!-- Encabezado Compacto de Control Administrativo -->
    <template #header>
      <header class="ctrl-header reveal-step reveal-1">
        <div class="ctrl-header__left">
          <div class="ctrl-eyebrow">
            <span class="ctrl-badge">PLATAFORMA DE CONTROL ADMINISTRATIVO</span>
            <span class="ctrl-period">· Periodo AD26</span>
            <span class="ctrl-user-pill">
              <span class="ctrl-live-dot" aria-hidden="true" />
              {{ displayUser.name }} (<code>05DIT0001V</code>)
            </span>
          </div>
          <h1 class="ctrl-title">Gestión Integral de Servicio Social y Prácticas</h1>
        </div>

        <div class="ctrl-header__actions">
          <UiButton variant="secondary" class="ctrl-btn" @click="goToStudents">
            <svg viewBox="0 0 24 24" aria-hidden="true" class="ctrl-btn__icon">
              <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
              <circle cx="9" cy="7" r="4" />
              <path d="M22 21v-2a4 4 0 0 0-3-3.87" />
              <path d="M16 3.13a4 4 0 0 1 0 7.75" />
            </svg>
            Padrón General
          </UiButton>

          <UiButton variant="primary" class="ctrl-btn" @click="goToExpedients">
            <svg viewBox="0 0 24 24" aria-hidden="true" class="ctrl-btn__icon">
              <path d="M12 20h9" />
              <path d="M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4Z" />
            </svg>
            Dictaminar Pendiente
          </UiButton>
        </div>
      </header>
    </template>

    <!-- 1. Fila de 4 Recuadros KPI Compactos -->
    <section class="compact-kpis reveal-step reveal-2" aria-label="Indicadores operativos">
      <!-- KPI 1: Servicio Social -->
      <article
        class="kpi-tile"
        :class="{ 'kpi-tile--selected': activeModality === 'Servicio Social' }"
        @click="toggleModality('Servicio Social')"
      >
        <div class="kpi-tile__top">
          <span class="kpi-tile__label">
            <span class="kpi-dot kpi-dot--navy" aria-hidden="true" />
            Servicio Social
          </span>
          <span class="kpi-meta kpi-meta--emerald">480 Horas</span>
        </div>

        <div class="kpi-tile__main">
          <strong class="kpi-tile__value">{{ animatedKpis.socialService }}</strong>
          <span class="kpi-tile__unit">prestadores</span>
        </div>

        <div class="kpi-tile__foot">
          <span><strong>118</strong> al corriente</span>
          <span class="kpi-highlight--gold">24 por entregar</span>
        </div>
      </article>

      <!-- KPI 2: Prácticas Profesionales -->
      <article
        class="kpi-tile"
        :class="{ 'kpi-tile--selected': activeModality === 'Prácticas Profesionales' }"
        @click="toggleModality('Prácticas Profesionales')"
      >
        <div class="kpi-tile__top">
          <span class="kpi-tile__label">
            <span class="kpi-dot kpi-dot--sky" aria-hidden="true" />
            Prácticas Prof.
          </span>
          <span class="kpi-meta kpi-meta--sky">360 Horas</span>
        </div>

        <div class="kpi-tile__main">
          <strong class="kpi-tile__value">{{ animatedKpis.internships }}</strong>
          <span class="kpi-tile__unit">practicantes</span>
        </div>

        <div class="kpi-tile__foot">
          <span><strong>42</strong> en industria</span>
          <span>16 internos</span>
        </div>
      </article>

      <!-- KPI 3: Dictámenes Urgentes -->
      <article class="kpi-tile kpi-tile--gold" @click="goToExpedients">
        <div class="kpi-tile__top">
          <span class="kpi-tile__label">
            <span class="kpi-dot kpi-dot--gold" aria-hidden="true" />
            Dictámenes Urgentes
          </span>
          <span class="kpi-tag-gold">Revisión</span>
        </div>

        <div class="kpi-tile__main">
          <strong class="kpi-tile__value kpi-tile__value--gold">
            {{ animatedKpis.pendingReports }}
          </strong>
          <span class="kpi-tile__unit">por validar</span>
        </div>

        <div class="kpi-tile__foot">
          <span>11 bimestrales</span>
          <span>8 planes</span>
        </div>
      </article>

      <!-- KPI 4: Convenios Marco -->
      <article
        class="kpi-tile"
        @click="notify('34 Empresas y Dependencias con convenio activo en la Región Centro.')"
      >
        <div class="kpi-tile__top">
          <span class="kpi-tile__label">
            <span class="kpi-dot kpi-dot--emerald" aria-hidden="true" />
            Convenios Marco
          </span>
          <span class="kpi-meta kpi-meta--emerald">• Enlace Activo</span>
        </div>

        <div class="kpi-tile__main">
          <strong class="kpi-tile__value">{{ animatedKpis.companies }}</strong>
          <span class="kpi-tile__unit">empresas activas</span>
        </div>

        <div class="kpi-tile__foot">
          <span>Región Centro Coahuila</span>
          <span class="kpi-highlight--emerald">100% validadas</span>
        </div>
      </article>
    </section>

    <!-- 2. Área Principal en 2 Columnas (Sin necesidad de scroll vertical) -->
    <div class="workspace-grid reveal-step reveal-3">
      <!-- Columna Izquierda: Bandeja de Expedientes y Dictámenes Prioritarios -->
      <UiCard class="tray-card">
        <div class="tray-header">
          <div class="tray-header__title">
            <svg viewBox="0 0 24 24" aria-hidden="true" class="tray-icon">
              <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
            </svg>
            <h2>Bandeja de Expedientes y Dictámenes Prioritarios</h2>
          </div>

          <div class="tray-header__meta">
            <button
              v-if="activeModality !== 'all' || activeCareer"
              type="button"
              class="clear-chip"
              @click="
                activeModality = 'all';
                activeCareer = null
              "
            >
              Limpiar filtro ✕
            </button>
            <span class="tray-count">
              Mostrando <strong>{{ visibleTramites.length }}</strong> trámites en espera
            </span>
          </div>
        </div>

        <div class="tray-table-wrap" role="region" aria-label="Bandeja de expedientes prioritarios">
          <table class="tray-table">
            <thead>
              <tr>
                <th scope="col">ESTUDIANTE / CARRERA</th>
                <th scope="col">TRÁMITE &amp; MÓDULO</th>
                <th scope="col">UNIDAD RECEPTORA</th>
                <th scope="col">ESTATUS</th>
                <th scope="col" class="th-right">ACCIÓN</th>
              </tr>
            </thead>
            <TransitionGroup name="tray-row" tag="tbody">
              <tr v-for="row in visibleTramites" :key="row.id" class="tray-tr">
                <td>
                  <div class="cell-student">
                    <strong>{{ row.studentName }}</strong>
                    <span>{{ row.control }} · {{ row.careerLabel }}</span>
                  </div>
                </td>

                <td>
                  <div class="cell-doc">
                    <strong>{{ row.docTitle }}</strong>
                    <span
                      :class="
                        row.modality === 'Servicio Social'
                          ? 'mod-sub--navy'
                          : 'mod-sub--muted'
                      "
                    >
                      {{ row.modalitySub }}
                    </span>
                  </div>
                </td>

                <td>
                  <span class="cell-company">{{ row.company }}</span>
                </td>

                <td>
                  <span class="status-box" :class="`status-box--${row.statusTone}`">
                    <span class="status-box__dot" aria-hidden="true" />
                    {{ row.statusText }}
                  </span>
                </td>

                <td class="td-right">
                  <button
                    type="button"
                    class="row-action-btn"
                    :class="
                      row.actionVariant === 'primary'
                        ? 'row-action-btn--primary'
                        : 'row-action-btn--outline'
                    "
                    @click="openExpedienteDialog(row)"
                  >
                    {{ row.actionLabel }}
                  </button>
                </td>
              </tr>
            </TransitionGroup>
          </table>
        </div>

        <footer class="tray-footer">
          <span>Plataforma central TecNM con validación SGC ISO 9001:2015</span>
          <button type="button" class="tray-footer__link" @click="goToExpedients">
            Ver todos los registros →
          </button>
        </footer>
      </UiCard>

      <!-- Columna Derecha Apilada: Distribución por Carrera + Vencimientos Normativos -->
      <aside class="right-stack">
        <!-- Panel Superior Derecho: Distribución por Carrera -->
        <UiCard class="side-card">
          <div class="side-card__head">
            <h2 class="side-card__title">DISTRIBUCIÓN POR CARRERA</h2>
            <span class="side-card__meta">142 Alumnos</span>
          </div>

          <div class="compact-careers">
            <button
              v-for="item in careerStats"
              :key="item.code"
              type="button"
              class="compact-career-row"
              :class="{ 'compact-career-row--active': activeCareer === item.code }"
              @click="toggleCareer(item.code)"
            >
              <div class="compact-career-row__labels">
                <span class="compact-career-name">{{ item.shortName }}</span>
                <span class="compact-career-nums">
                  <strong>{{ item.count }}</strong>
                  <em :class="`pct-tone--${item.tone}`">({{ item.percentage }}%)</em>
                </span>
              </div>
              <div class="compact-track">
                <span
                  class="compact-fill"
                  :class="`compact-fill--${item.tone}`"
                  :style="{ width: barsReady ? `${item.percentage}%` : '0%' }"
                />
              </div>
            </button>
          </div>
        </UiCard>

        <!-- Panel Inferior Derecho: Vencimientos Normativos -->
        <UiCard class="side-card">
          <div class="side-card__head">
            <div class="side-card__title-group">
              <svg viewBox="0 0 24 24" aria-hidden="true" class="side-card__icon">
                <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
                <line x1="16" y1="2" x2="16" y2="6" />
                <line x1="8" y1="2" x2="8" y2="6" />
                <line x1="3" y1="10" x2="21" y2="10" />
              </svg>
              <h2 class="side-card__title">VENCIMIENTOS NORMATIVOS</h2>
            </div>
            <span class="official-tag">Oficial</span>
          </div>

          <div class="deadlines-list">
            <article
              v-for="d in deadlines"
              :key="d.title"
              class="deadline-box"
              :class="`deadline-box--${d.tone}`"
            >
              <div class="deadline-box__top">
                <span class="deadline-date">{{ d.date }}</span>
                <span class="deadline-scope">{{ d.scope }}</span>
              </div>
              <h3 class="deadline-title">{{ d.title }}</h3>
              <p class="deadline-detail">{{ d.detail }}</p>
            </article>
          </div>
        </UiCard>
      </aside>
    </div>

    <!-- Modal de Dictamen Rápido -->
    <UiDialog
      v-model="isDialogOpen"
      :title="selectedRow ? `${selectedRow.docTitle} · ${selectedRow.studentName}` : 'Expediente'"
    >
      <div v-if="selectedRow" class="dialog-body">
        <div class="dialog-top">
          <span class="dialog-control">{{ selectedRow.control }}</span>
          <UiBadge :tone="selectedRow.statusTone">{{ selectedRow.statusText }}</UiBadge>
        </div>

        <dl class="dialog-grid">
          <div>
            <dt>Estudiante</dt>
            <dd>{{ selectedRow.studentName }}</dd>
          </div>
          <div>
            <dt>Carrera</dt>
            <dd>{{ selectedRow.careerLabel }}</dd>
          </div>
          <div>
            <dt>Modalidad</dt>
            <dd>{{ selectedRow.modality }}</dd>
          </div>
          <div>
            <dt>Unidad Receptora</dt>
            <dd>{{ selectedRow.company }}</dd>
          </div>
          <div>
            <dt>Asesor(a) Interno</dt>
            <dd>{{ selectedRow.advisor }}</dd>
          </div>
          <div>
            <dt>Avance de Horas</dt>
            <dd>{{ selectedRow.hoursCompleted }} / {{ selectedRow.hoursTotal }} h</dd>
          </div>
        </dl>

        <div class="dialog-project">
          <span>PROYECTO VINCULADO</span>
          <strong>{{ selectedRow.projectTitle }}</strong>
        </div>
      </div>

      <template #actions>
        <UiButton variant="secondary" @click="isDialogOpen = false">
          Cerrar
        </UiButton>
        <UiButton variant="primary" @click="confirmExpedienteAction">
          Confirmar Dictamen SGC
        </UiButton>
      </template>
    </UiDialog>

    <UiToast ref="toast" />
  </UiBasePage>
</template>

<style scoped>
/* ==========================================================================
   Animaciones de Entrada Escalonada y Transición de Filas
   ========================================================================== */
@keyframes slideInPanel {
  from {
    opacity: 0;
    transform: translateY(8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.reveal-step {
  animation: slideInPanel 380ms cubic-bezier(0.22, 1, 0.36, 1) both;
}

.reveal-1 {
  animation-delay: 15ms;
}
.reveal-2 {
  animation-delay: 55ms;
}
.reveal-3 {
  animation-delay: 95ms;
}

.tray-row-enter-active,
.tray-row-leave-active {
  transition:
    opacity 200ms ease,
    transform 200ms ease;
}

.tray-row-enter-from,
.tray-row-leave-to {
  opacity: 0;
  transform: translateY(4px);
}

/* ==========================================================================
   Encabezado Compacto (Fiel a la referencia y con identidad TecNM)
   ========================================================================== */
.ctrl-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 14px;
  flex-wrap: wrap;
}

.ctrl-eyebrow {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
  flex-wrap: wrap;
}

.ctrl-badge {
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.07em;
  text-transform: uppercase;
  color: var(--color-accent);
  background: var(--color-accent-light);
  border: 1px solid color-mix(in srgb, var(--color-accent) 45%, transparent);
  padding: 2px 8px;
  border-radius: 4px;
}

.ctrl-period {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11px;
  color: var(--color-text-secondary);
}

.ctrl-user-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 11px;
  color: var(--color-text-secondary);
  background: var(--color-surface-white);
  border: 1px solid var(--color-border);
  padding: 2px 8px;
  border-radius: 99px;
}

.ctrl-user-pill code {
  color: var(--color-accent);
  font-weight: 700;
}

.ctrl-live-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--color-success);
}

.ctrl-title {
  font-size: clamp(20px, 2vw, 25px);
  font-weight: 700;
  letter-spacing: -0.02em;
  color: var(--color-text-primary);
  line-height: 1.15;
}

.ctrl-header__actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.ctrl-btn {
  min-height: 36px !important;
  padding: 6px 14px !important;
  font-size: 12.5px !important;
  display: inline-flex;
  align-items: center;
  gap: 7px;
}

.ctrl-btn__icon {
  width: 14px;
  height: 14px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.9;
  stroke-linecap: round;
  stroke-linejoin: round;
}

/* ==========================================================================
   1. Fila de 4 Recuadros KPI Compactos
   ========================================================================== */
.compact-kpis {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 14px;
  margin-bottom: 14px;
}

.kpi-tile {
  padding: 13px 16px;
  border-radius: 10px;
  background: var(--color-surface-white);
  border: 1px solid var(--color-border);
  box-shadow: var(--card-shadow);
  cursor: pointer;
  transition:
    transform 180ms cubic-bezier(0.22, 1, 0.36, 1),
    border-color 180ms ease,
    box-shadow 180ms ease;
}

.kpi-tile:hover {
  transform: translateY(-2px);
  border-color: var(--color-primary);
  box-shadow: var(--floating-shadow);
}

.kpi-tile--selected {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-primary) 22%, transparent);
}

.kpi-tile--gold:hover {
  border-color: var(--color-accent);
}

.kpi-tile__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.kpi-tile__label {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  font-size: 11.5px;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.kpi-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
}

.kpi-dot--navy {
  background: var(--color-primary);
}
.kpi-dot--sky {
  background: var(--color-info);
}
.kpi-dot--gold {
  background: var(--color-accent);
}
.kpi-dot--emerald {
  background: var(--color-success);
}

.kpi-meta {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10.5px;
  font-weight: 600;
}

.kpi-meta--emerald {
  color: var(--color-success);
}

.kpi-meta--sky {
  color: var(--color-info);
}

.kpi-tag-gold {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10px;
  font-weight: 700;
  color: var(--color-accent);
  background: var(--color-accent-light);
  border: 1px solid color-mix(in srgb, var(--color-accent) 40%, transparent);
  padding: 1px 6px;
  border-radius: 4px;
}

.kpi-tile__main {
  display: flex;
  align-items: baseline;
  gap: 7px;
  margin: 8px 0;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--color-border-light);
}

.kpi-tile__value {
  font-size: 27px;
  font-weight: 700;
  letter-spacing: -0.03em;
  line-height: 1;
  color: var(--color-text-primary);
  font-variant-numeric: tabular-nums;
}

.kpi-tile__value--gold {
  color: var(--color-accent);
}

.kpi-tile__unit {
  font-size: 12px;
  color: var(--color-text-secondary);
}

.kpi-tile__foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  font-size: 11px;
  color: var(--color-text-secondary);
}

.kpi-tile__foot strong {
  color: var(--color-text-primary);
}

.kpi-highlight--gold {
  color: var(--color-accent);
  font-weight: 600;
}

.kpi-highlight--emerald {
  color: var(--color-success);
  font-weight: 600;
}

/* ==========================================================================
   2. Área Principal en 2 Columnas (Bandeja + Stack Derecho)
   ========================================================================== */
.workspace-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.95fr) minmax(0, 1fr);
  gap: 14px;
  align-items: stretch;
}

.tray-card {
  padding: 0;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  overflow: hidden;
}

.tray-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 14px 18px;
  border-bottom: 1px solid var(--color-border);
  flex-wrap: wrap;
}

.tray-header__title {
  display: flex;
  align-items: center;
  gap: 9px;
}

.tray-header__title h2 {
  font-size: 14px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.tray-icon {
  width: 16px;
  height: 16px;
  stroke: var(--color-accent);
  fill: none;
  stroke-width: 1.9;
}

.tray-header__meta {
  display: flex;
  align-items: center;
  gap: 10px;
}

.clear-chip {
  font-size: 11px;
  font-weight: 600;
  color: var(--color-accent);
  background: var(--color-accent-light);
  border: 1px solid color-mix(in srgb, var(--color-accent) 40%, transparent);
  padding: 2px 8px;
  border-radius: 4px;
}

.tray-count {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11px;
  color: var(--color-text-secondary);
}

.tray-count strong {
  color: var(--color-text-primary);
}

.tray-table-wrap {
  margin: 0;
  overflow-x: auto;
  flex: 1;
}

.tray-table {
  width: 100%;
  border-collapse: collapse;
  white-space: normal;
}

.tray-table th {
  padding: 9px 18px;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.06em;
  color: var(--color-text-muted);
  background: var(--color-bg-light);
  border-bottom: 1px solid var(--color-border);
}

.th-right,
.td-right {
  text-align: right;
}

.tray-tr {
  transition: background 150ms ease;
}

.tray-tr:hover {
  background: var(--color-primary-subtle);
}

.tray-tr td {
  padding: 11px 18px;
  vertical-align: middle;
  border-bottom: 1px solid var(--color-border-light);
}

.tray-tr:last-child td {
  border-bottom: 0;
}

.cell-student,
.cell-doc {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.cell-student strong,
.cell-doc strong {
  font-size: 12.5px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.cell-student span,
.cell-doc span {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11px;
  color: var(--color-text-secondary);
}

.mod-sub--navy {
  color: var(--color-info) !important;
}

.mod-sub--muted {
  color: var(--color-text-secondary);
}

.cell-company {
  font-size: 12px;
  color: var(--color-text-secondary);
}

.status-box {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 3px 9px;
  border-radius: 6px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10.5px;
  font-weight: 600;
  line-height: 1.25;
}

.status-box__dot {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: currentColor;
}

.status-box--warning {
  background: var(--color-warning-badge);
  color: var(--color-warning-text);
  border: 1px solid var(--color-warning-border);
}

.status-box--success {
  background: var(--color-success-badge);
  color: var(--color-success-text);
  border: 1px solid var(--color-success-border);
}

.status-box--info {
  background: var(--color-info-badge);
  color: var(--color-info-text);
  border: 1px solid var(--color-info-border);
}

.row-action-btn {
  min-height: 28px;
  padding: 4px 12px;
  border-radius: 6px;
  font-size: 11.5px;
  font-weight: 600;
  transition:
    transform 140ms ease,
    background 160ms ease,
    border-color 160ms ease;
}

.row-action-btn:active {
  transform: scale(0.97);
}

.row-action-btn--primary {
  background: var(--color-primary);
  color: var(--color-white);
  border: 1px solid transparent;
}

.row-action-btn--primary:hover {
  background: var(--color-primary-hover);
}

.row-action-btn--outline {
  background: var(--color-bg-light);
  color: var(--color-text-primary);
  border: 1px solid var(--color-border-medium);
}

.row-action-btn--outline:hover {
  background: var(--secondary-hover);
  border-color: var(--color-accent);
}

.tray-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 18px;
  background: var(--color-bg-light);
  border-top: 1px solid var(--color-border);
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11px;
  color: var(--color-text-secondary);
}

.tray-footer__link {
  background: transparent;
  border: 0;
  padding: 0;
  font-size: 11.5px;
  font-weight: 600;
  color: var(--color-info);
  transition: transform 150ms ease;
}

.tray-footer__link:hover {
  transform: translateX(3px);
  color: var(--color-accent);
}

/* ==========================================================================
   Columna Derecha: Distribución por Carrera + Vencimientos Normativos
   ========================================================================== */
.right-stack {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.side-card {
  padding: 14px 16px;
}

.side-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 10px;
}

.side-card__title-group {
  display: flex;
  align-items: center;
  gap: 7px;
}

.side-card__icon {
  width: 14px;
  height: 14px;
  stroke: var(--color-accent);
  fill: none;
  stroke-width: 1.9;
}

.side-card__title {
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--color-text-primary);
}

.side-card__meta {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10.5px;
  color: var(--color-text-secondary);
}

.official-tag {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10px;
  font-weight: 700;
  color: var(--color-accent);
  background: var(--color-accent-light);
  border: 1px solid color-mix(in srgb, var(--color-accent) 40%, transparent);
  padding: 1px 6px;
  border-radius: 4px;
}

/* Barras de Carrera Compactas */
.compact-careers {
  display: grid;
  gap: 7px;
}

.compact-career-row {
  appearance: none;
  background: transparent;
  border: 1px solid transparent;
  border-radius: 6px;
  padding: 3px 6px;
  margin: -3px -6px;
  text-align: left;
  display: grid;
  gap: 4px;
  transition:
    background 150ms ease,
    border-color 150ms ease;
}

.compact-career-row:hover {
  background: var(--color-bg-light);
  border-color: var(--color-border);
}

.compact-career-row--active {
  background: var(--color-primary-subtle);
  border-color: var(--color-primary);
}

.compact-career-row__labels {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 11.5px;
}

.compact-career-name {
  font-weight: 500;
  color: var(--color-text-primary);
}

.compact-career-nums {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11px;
  display: inline-flex;
  gap: 4px;
  font-variant-numeric: tabular-nums;
}

.compact-career-nums strong {
  color: var(--color-text-secondary);
}

.compact-career-nums em {
  font-style: normal;
  font-weight: 600;
}

.pct-tone--navy,
.pct-tone--sky,
.pct-tone--cyan {
  color: var(--color-info);
}

.pct-tone--gold {
  color: var(--color-accent);
}

.pct-tone--slate {
  color: var(--color-text-secondary);
}

.pct-tone--emerald {
  color: var(--color-success);
}

.compact-track {
  height: 4px;
  width: 100%;
  background: var(--color-gray-100);
  border-radius: 99px;
  overflow: hidden;
}

.compact-fill {
  display: block;
  height: 100%;
  border-radius: 99px;
  transition: width 700ms cubic-bezier(0.16, 1, 0.3, 1);
}

.compact-fill--navy {
  background: var(--color-primary);
}
.compact-fill--sky {
  background: var(--color-info);
}
.compact-fill--gold {
  background: var(--color-accent);
}
.compact-fill--slate {
  background: var(--color-gray-500);
}
.compact-fill--cyan {
  background: var(--color-info);
}
.compact-fill--emerald {
  background: var(--color-success);
}

/* Vencimientos Normativos Compactos */
.deadlines-list {
  display: grid;
  gap: 8px;
}

.deadline-box {
  padding: 9px 12px;
  border-radius: 8px;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
  border-left-width: 3px;
  transition: transform 160ms ease;
}

.deadline-box:hover {
  transform: translateX(2px);
}

.deadline-box--gold {
  border-left-color: var(--color-accent);
}

.deadline-box--blue {
  border-left-color: var(--color-info);
}

.deadline-box--slate {
  border-left-color: var(--color-gray-400);
}

.deadline-box__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 2px;
}

.deadline-date {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10px;
  font-weight: 700;
}

.deadline-box--gold .deadline-date {
  color: var(--color-accent);
}
.deadline-box--blue .deadline-date {
  color: var(--color-info);
}
.deadline-box--slate .deadline-date {
  color: var(--color-text-secondary);
}

.deadline-scope {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10px;
  color: var(--color-text-muted);
}

.deadline-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.deadline-detail {
  font-size: 11px;
  line-height: 1.35;
  color: var(--color-text-secondary);
  margin-top: 2px;
}

/* Modal de Dictamen */
.dialog-body {
  display: grid;
  gap: 12px;
  margin: 12px 0 18px;
}

.dialog-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.dialog-control {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 12px;
  font-weight: 700;
  color: var(--color-primary);
  background: var(--color-primary-subtle);
  padding: 2px 8px;
  border-radius: 4px;
}

.dialog-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
  margin: 0;
  padding: 12px;
  border-radius: 8px;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
}

.dialog-grid dt {
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.dialog-grid dd {
  margin: 2px 0 0;
  font-size: 12.5px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.dialog-project {
  padding: 10px 12px;
  border-radius: 8px;
  background: var(--color-accent-light);
  border-left: 3px solid var(--color-accent);
  display: grid;
  gap: 2px;
}

.dialog-project span {
  font-size: 9.5px;
  font-weight: 700;
  letter-spacing: 0.06em;
  color: var(--color-accent);
}

.dialog-project strong {
  font-size: 12.5px;
  color: var(--color-text-primary);
}

@media (max-width: 1150px) {
  .compact-kpis {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .workspace-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .compact-kpis {
    grid-template-columns: 1fr;
  }
  .ctrl-header__actions {
    width: 100%;
    flex-direction: column;
    align-items: stretch;
  }
  .dialog-grid {
    grid-template-columns: 1fr;
  }
}
</style>
