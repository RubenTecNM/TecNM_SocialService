<script setup lang="ts">
import { socialDocumentLabel } from '../../../shared/utils/socialServiceDocuments'
import { computed, onMounted, reactive, ref } from 'vue'
import { canReadProcess, hasRole } from '../../../shared/composables/useSession'
import { useRealtimeRefresh } from '../../../shared/composables/useRealtime'
import { useRouter } from 'vue-router'
import { getRoleDisplayLabel } from '../../../app/layouts/mainLayout.config'
import { UiAlert, UiBadge, UiBasePage, UiButton, UiCard } from '../../../shared/components'
import { getReviewQueue } from '../../expedients/api/expedientsApi'
import type { ReviewQueueItem } from '../../expedients/types/expedients'
import { getInternships, getMyInternship } from '../../internships/api/internshipsApi'
import type { InternshipDetail, InternshipListItem } from '../../internships/types/internships'
import { getDashboardMetrics } from '../../reports/api/reportsApi'
import type { DashboardMetrics } from '../../reports/types/reports'
import { getMySocialService, getSocialServices } from '../../social-service/api/socialServiceApi'
import type {
  SocialServiceDetail,
  SocialServiceListItem,
} from '../../social-service/types/socialService'
import { getMyEligibility, getMyStudentProfile } from '../../students/api/studentsApi'
import type { StudentDetail, StudentEligibility } from '../../students/types/students'
import { me } from '../api/authApi'
import type { UserResponse } from '../types/auth'

const router = useRouter()
const user = ref<UserResponse | null>(null)
const isLoading = ref(true)
const errorMessage = ref('')

// Datos para vista de Estudiante
const studentProfile = ref<StudentDetail | null>(null)
const studentEligibility = ref<StudentEligibility | null>(null)
const mySocialService = ref<SocialServiceDetail | null>(null)
const myInternship = ref<InternshipDetail | null>(null)

// Datos para vista de Personal / Vinculación
const dashboard = ref<DashboardMetrics | null>(null)
const pendingDocs = ref<ReviewQueueItem[]>([])
const recentSS = ref<SocialServiceListItem[]>([])
const recentPP = ref<InternshipListItem[]>([])
const tramiteTab = ref<'all' | 'social_service' | 'internship'>('all')

const isStudentOnly = computed(() => {
  const roles = user.value?.roles ?? []
  return (
    roles.includes('student') &&
    !roles.some((r) =>
      ['department_head', 'social_service_officer', 'internship_coordinator'].includes(r),
    )
  )
})

const roleTitle = computed(() => getRoleDisplayLabel(user.value?.roles ?? []))

const totalSSCount = computed(() => {
  if (!dashboard.value?.socialService) return 0
  return Object.values(dashboard.value.socialService).reduce(
    (acc, n) => acc + Number(n || 0),
    0,
  )
})

const totalPPCount = computed(() => {
  if (!dashboard.value?.internships) return 0
  return Object.values(dashboard.value.internships).reduce(
    (acc, n) => acc + Number(n || 0),
    0,
  )
})

const animatedKpis = reactive({
  ss: 0,
  pp: 0,
  pendingDocs: 0,
  pnc: 0,
})

function animateStaffKpis(): void {
  const targets = {
    ss: totalSSCount.value,
    pp: totalPPCount.value,
    pendingDocs: dashboard.value?.pendingDocumentReviews ?? pendingDocs.value.length,
    pnc: dashboard.value?.openNonConformances ?? 0,
  }

  const prefersReduced =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches

  if (prefersReduced) {
    Object.assign(animatedKpis, targets)
    return
  }

  const duration = 520
  const start = performance.now()
  function tick(now: number): void {
    const progress = Math.min((now - start) / duration, 1)
    const ease = 1 - Math.pow(1 - progress, 4)
    animatedKpis.ss = Math.round(targets.ss * ease)
    animatedKpis.pp = Math.round(targets.pp * ease)
    animatedKpis.pendingDocs = Math.round(targets.pendingDocs * ease)
    animatedKpis.pnc = Math.round(targets.pnc * ease)
    if (progress < 1) requestAnimationFrame(tick)
  }
  requestAnimationFrame(tick)
}

interface QuickConnector {
  label: string
  sub: string
  to: string
  visible: boolean
}

const quickConnectors = computed<QuickConnector[]>(() => [
  {
    label: 'Servicio Social',
    sub: 'Dictámenes y folios',
    to: '/social-service',
    visible: canReadProcess('social_service'),
  },
  {
    label: 'Prácticas Prof.',
    sub: 'Cartas y empresas',
    to: '/internships',
    visible: canReadProcess('internship'),
  },
  {
    label: 'Revisión PDF',
    sub: 'Cola de validación',
    to: '/expedients/review',
    visible: hasRole('department_head', 'social_service_officer', 'internship_coordinator'),
  },
  {
    label: 'Padrón Alumnos',
    sub: 'Importación y elegibilidad',
    to: '/students',
    visible: true,
  },
  {
    label: 'Matriz Seguimiento',
    sub: 'Sábana en vivo',
    to: '/reports/tracking',
    visible: true,
  },
  {
    label: 'Estadístico & 911',
    sub: 'Reporte oficial SEP',
    to: '/reports/stats',
    visible: true,
  },
  {
    label: 'Calidad (PNC)',
    sub: 'Desvíos ISO 9001',
    to: '/quality/pnc',
    visible: true,
  },
  {
    label: 'Empresas',
    sub: 'Convenios y enlaces',
    to: '/companies',
    visible: true,
  },
  {
    label: 'Carreras',
    sub: 'Retículas vigentes',
    to: '/catalogs/careers',
    visible: true,
  },
].filter((item) => item.visible))

const statusLabels: Record<string, string> = {
  requested: 'Solicitud en Revisión',
  approved: 'Carta Presentación Emitida',
  accepted: 'Carta Aceptación Validada',
  active: 'En Curso (Activo)',
  completed_pending_review: 'Documentos Completos · Por Liberar',
  released: 'Liberado (100%)',
  cancelled: 'Cancelado',
  non_compliant: 'Producto No Conforme (PNC)',
}

function formatStatus(status?: string | null): string {
  if (!status) return 'Sin iniciar'
  return statusLabels[status] || status
}

function getStatusTone(status?: string | null): 'success' | 'warning' | 'danger' | 'info' | 'purple' {
  if (status === 'released') return 'success'
  if (status === 'active' || status === 'approved' || status === 'accepted') return 'info'
  if (status === 'requested' || status === 'completed_pending_review') return 'warning'
  if (status === 'non_compliant' || status === 'cancelled') return 'danger'
  return 'info'
}

const showSSInTracker = computed(
  () =>
    canReadProcess('social_service') &&
    (tramiteTab.value === 'all' || tramiteTab.value === 'social_service'),
)

const showPPInTracker = computed(
  () =>
    canReadProcess('internship') &&
    (tramiteTab.value === 'all' || tramiteTab.value === 'internship'),
)

async function loadData(): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''
  try {
    user.value = await me()
    if (isStudentOnly.value) {
      const [profile, elig, ss, pp] = await Promise.all([
        getMyStudentProfile().catch(() => null),
        getMyEligibility().catch(() => null),
        getMySocialService().catch(() => null),
        getMyInternship().catch(() => null),
      ])
      studentProfile.value = profile
      studentEligibility.value = elig
      mySocialService.value = ss
      myInternship.value = pp
    } else {
      const [dash, docs, ssRes, ppRes] = await Promise.all([
        getDashboardMetrics().catch(() => null),
        hasRole('department_head', 'social_service_officer', 'internship_coordinator')
          ? getReviewQueue('pending', { pageSize: 10 }).catch(() => [])
          : Promise.resolve([]),
        canReadProcess('social_service')
          ? getSocialServices({ pageSize: 6 }).catch(() => ({ items: [] }))
          : Promise.resolve({ items: [] }),
        canReadProcess('internship')
          ? getInternships({ pageSize: 6 }).catch(() => ({ items: [] }))
          : Promise.resolve({ items: [] }),
      ])
      dashboard.value = dash
      pendingDocs.value = docs
      recentSS.value = ssRes.items ?? []
      recentPP.value = ppRes.items ?? []
      animateStaffKpis()
    }
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'No se pudo cargar la información del panel.'
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  void loadData()
})

useRealtimeRefresh(
  () => true,
  () => loadData(),
)
</script>

<template>
  <UiBasePage :fluid="true" class="fullscreen-page">
    <!-- Encabezado Ejecutivo Compacto -->
    <header class="exec-header">
      <div class="exec-header__left">
        <div class="exec-eyebrow">
          <span class="exec-badge">{{ roleTitle }}</span>
          <span v-if="dashboard?.period" class="exec-period">
            · Periodo Activo: <strong>{{ dashboard.period.code }}</strong>
          </span>
          <span v-if="user?.name && !isStudentOnly" class="exec-period">
            · Sesión: <strong>{{ user.name }}</strong>
          </span>
        </div>
        <div class="title-row">
          <h1 class="exec-title">
            {{
              isStudentOnly
                ? `Bienvenido(a), ${user?.name || 'Estudiante'}`
                : 'Panel de Control de Gestión Tecnológica y Vinculación'
            }}
          </h1>
          <div v-if="!isStudentOnly" class="header-actions">
            <UiButton variant="secondary" size="sm" :disabled="isLoading" @click="loadData">
              Actualizar
            </UiButton>
            <UiButton
              v-if="hasRole('department_head', 'social_service_officer', 'internship_coordinator')"
              variant="primary"
              size="sm"
              @click="router.push('/expedients/review')"
            >
              Cola de Revisión ({{ dashboard?.pendingDocumentReviews ?? pendingDocs.length }})
            </UiButton>
          </div>
        </div>
      </div>

      <!-- 4 KPIs resumen interactivos para Personal (superficie neutra, borde dorado al pasar/activar) -->
      <div
        v-if="!isStudentOnly && !isLoading"
        class="exec-kpis"
        aria-label="Indicadores generales de vinculación"
      >
        <button
          v-if="canReadProcess('social_service')"
          type="button"
          class="mini-kpi mini-kpi--navy"
          @click="router.push('/social-service')"
        >
          <span class="mini-kpi__label">Servicio Social</span>
          <strong class="mini-kpi__value">{{ animatedKpis.ss }}</strong>
          <small class="mini-kpi__foot">
            Lib: {{ dashboard?.socialService?.released || 0 }} · Act:
            {{ dashboard?.socialService?.active || 0 }}
          </small>
        </button>

        <button
          v-if="canReadProcess('internship')"
          type="button"
          class="mini-kpi mini-kpi--emerald"
          @click="router.push('/internships')"
        >
          <span class="mini-kpi__label">Prácticas Prof.</span>
          <strong class="mini-kpi__value">{{ animatedKpis.pp }}</strong>
          <small class="mini-kpi__foot">
            Lib: {{ dashboard?.internships?.released || 0 }} · Act:
            {{ dashboard?.internships?.active || 0 }}
          </small>
        </button>

        <button
          type="button"
          class="mini-kpi mini-kpi--gold"
          @click="router.push('/expedients/review')"
        >
          <span class="mini-kpi__label">Docs. Por Revisar</span>
          <strong class="mini-kpi__value">{{ animatedKpis.pendingDocs }}</strong>
          <small class="mini-kpi__foot">Validación en cola</small>
        </button>

        <button
          type="button"
          class="mini-kpi mini-kpi--danger"
          @click="router.push('/quality/pnc')"
        >
          <span class="mini-kpi__label">PNC Abiertos</span>
          <strong class="mini-kpi__value">{{ animatedKpis.pnc }}</strong>
          <small class="mini-kpi__foot">
            Conv. por vencer: {{ dashboard?.agreementsExpiringSoon ?? 0 }}
          </small>
        </button>
      </div>
    </header>

    <UiAlert v-if="errorMessage" tone="danger" class="compact-alert">
      <div class="error-row">
        <span>{{ errorMessage }}</span>
        <UiButton variant="secondary" size="sm" @click="loadData">Reintentar</UiButton>
      </div>
    </UiAlert>

    <div v-if="isLoading" class="loading-box" role="status">
      Cargando información en tiempo real...
    </div>

    <!-- ==================================================================== -->
    <!-- VISTA 1: PORTAL DEL ESTUDIANTE (Rol 'student')                       -->
    <!-- ==================================================================== -->
    <div v-else-if="isStudentOnly" class="student-portal-grid">
      <UiCard v-if="studentProfile" class="student-profile-card">
        <div class="student-summary-grid">
          <div>
            <span class="meta-label">No. de Control</span>
            <strong class="meta-value mono">{{ studentProfile.controlNumber }}</strong>
          </div>
          <div>
            <span class="meta-label">Carrera</span>
            <strong class="meta-value">{{ studentProfile.careerName }}</strong>
          </div>
          <div>
            <span class="meta-label">Semestre / Turno</span>
            <strong class="meta-value">
              {{ studentProfile.currentSemester }}° ·
              {{
                studentProfile.shift === 'morning'
                  ? 'Matutino'
                  : studentProfile.shift === 'night'
                    ? 'Nocturno'
                    : 'Vespertino'
              }}
            </strong>
          </div>
          <div>
            <span class="meta-label">Avance Reticular</span>
            <strong class="meta-value accent-info">
              {{ studentProfile.curriculumProgressPercentage }}% ({{ studentProfile.completedCredits }}
              créd.)
            </strong>
          </div>
          <div>
            <span class="meta-label">Vigencia IMSS</span>
            <div class="imss-badge-wrap">
              <UiBadge
                :tone="
                  studentProfile.hasImssFile || studentProfile.hasValidImss ? 'success' : 'warning'
                "
              >
                {{
                  studentProfile.hasImssFile
                    ? 'Constancia PDF Cargada'
                    : studentProfile.hasValidImss
                      ? 'IMSS Vigente'
                      : 'Falta Subir PDF IMSS'
                }}
              </UiBadge>
            </div>
          </div>
          <div class="profile-cta">
            <UiButton variant="secondary" size="sm" @click="router.push('/students/me')">
              Mi Perfil / IMSS
            </UiButton>
          </div>
        </div>
      </UiCard>

      <div class="tramites-cards-row">
        <!-- Tarjeta Servicio Social -->
        <UiCard title="1. Mi Servicio Social (480 Horas)" class="student-tramite-card">
          <div v-if="mySocialService" class="tramite-card-body">
            <div class="tramite-status-line">
              <UiBadge :tone="getStatusTone(mySocialService.status)">
                {{ formatStatus(mySocialService.status) }}
              </UiBadge>
              <UiBadge :tone="mySocialService.modality === 'internal' ? 'info' : 'purple'">
                {{
                  mySocialService.modality === 'internal'
                    ? 'Local en el TecNM'
                    : 'Dependencia Externa'
                }}
              </UiBadge>
            </div>

            <div class="info-pairs">
              <div>
                <span class="meta-label">Dependencia / Departamento:</span>
                <strong>{{ mySocialService.company.name }}</strong>
              </div>
              <div>
                <span class="meta-label">Programa:</span>
                <strong>{{ mySocialService.program.name }}</strong>
              </div>
              <div>
                <span class="meta-label">Folio Carta Presentación (CP):</span>
                <strong class="mono">
                  {{ mySocialService.presentationLetterFolio || 'En espera de dictamen' }}
                </strong>
              </div>
              <div v-if="mySocialService.releaseCertificateFolio">
                <span class="meta-label">Constancia de Terminación:</span>
                <strong class="folio-released">{{ mySocialService.releaseCertificateFolio }}</strong>
              </div>
            </div>

            <div class="progress-section">
              <div class="progress-label-row">
                <span>Progreso Documental (Validados)</span>
                <strong>{{ mySocialService.documentProgressPercentage }}%</strong>
              </div>
              <div class="progress-track">
                <div
                  class="progress-fill"
                  :style="{ width: `${mySocialService.documentProgressPercentage}%` }"
                />
              </div>
            </div>

            <div class="card-footer-actions">
              <UiButton variant="primary" size="sm" @click="router.push('/social-service/my')">
                Gestionar Formatos y Documentos SS
              </UiButton>
            </div>
          </div>

          <div v-else class="tramite-card-body">
            <p class="empty-text">
              Aún no has iniciado tu trámite de Servicio Social. Llena tus datos del
              <strong>Anexo XVIII</strong> adjuntando tu Constancia de Vigencia del IMSS para que el
              sistema genere tu formato automáticamente y lo envíe a Vinculación.
            </p>
            <div v-if="studentEligibility">
              <UiBadge :tone="studentEligibility.socialService.eligible ? 'success' : 'warning'">
                {{
                  studentEligibility.socialService.eligible
                    ? 'Cumples requisitos para solicitar apertura'
                    : 'Revisa requisitos pendientes'
                }}
              </UiBadge>
            </div>
            <div class="card-footer-actions">
              <UiButton variant="primary" size="sm" @click="router.push('/social-service/my')">
                Solicitar Apertura de Servicio Social
              </UiButton>
            </div>
          </div>
        </UiCard>

        <!-- Tarjeta Prácticas Profesionales -->
        <UiCard title="2. Mis Prácticas Profesionales (Productividad Laboral)" class="student-tramite-card">
          <div v-if="myInternship" class="tramite-card-body">
            <div class="tramite-status-line">
              <UiBadge :tone="getStatusTone(myInternship.status)">
                {{ formatStatus(myInternship.status) }}
              </UiBadge>
            </div>

            <div class="info-pairs">
              <div>
                <span class="meta-label">Empresa Receptora:</span>
                <strong>
                  {{
                    myInternship.company?.name || 'Por registrar con Oficio de Aceptación (CA)'
                  }}
                </strong>
              </div>
              <div>
                <span class="meta-label">Folio Carta Presentación (CP):</span>
                <strong class="mono">
                  {{ myInternship.presentationLetterFolio || 'En espera de emisión' }}
                </strong>
              </div>
              <div v-if="myInternship.releaseCertificateFolio">
                <span class="meta-label">Constancia de Liberación (CC):</span>
                <strong class="folio-released">{{ myInternship.releaseCertificateFolio }}</strong>
              </div>
            </div>

            <div class="progress-section">
              <div class="progress-label-row">
                <span>Progreso Documental (CP, CA, EV, CT, CC)</span>
                <strong>{{ myInternship.documentProgressPercentage }}%</strong>
              </div>
              <div class="progress-track">
                <div
                  class="progress-fill green"
                  :style="{ width: `${myInternship.documentProgressPercentage}%` }"
                />
              </div>
            </div>

            <div class="card-footer-actions">
              <UiButton variant="primary" size="sm" @click="router.push('/internships/my')">
                Gestionar Carta Presentación y Documentos PP
              </UiButton>
            </div>
          </div>

          <div v-else class="tramite-card-body">
            <p class="empty-text">
              Solicita tu <strong>Carta de Presentación</strong> llenando el formulario automatizado
              y adjuntando tu Constancia de Vigencia de Derechos del IMSS. Posteriormente la empresa
              enviará su oficio de aceptación membretado a
              <code>arturo.martinez@monclova.tecnm.mx</code>.
            </p>
            <div class="card-footer-actions">
              <UiButton variant="primary" size="sm" @click="router.push('/internships/my')">
                Solicitar Carta de Presentación de Prácticas
              </UiButton>
            </div>
          </div>
        </UiCard>
      </div>
    </div>

    <!-- ==================================================================== -->
    <!-- VISTA 2: CENTRO DE CONTROL PARA PERSONAL Y DIRECTIVOS                -->
    <!-- ==================================================================== -->
    <div v-else class="staff-portal">
      <!-- Cinta de Conectores Directos a todos los módulos -->
      <section class="connectors-strip" aria-label="Accesos rápidos del sistema">
        <button
          v-for="item in quickConnectors"
          :key="item.to"
          type="button"
          class="connector-chip"
          @click="router.push(item.to)"
        >
          <div class="connector-chip__text">
            <strong>{{ item.label }}</strong>
            <span>{{ item.sub }}</span>
          </div>
          <span class="connector-chip__arrow" aria-hidden="true">→</span>
        </button>
      </section>

      <!-- Área Principal en 2 Columnas con Scroll Interno -->
      <div class="staff-panels-grid">
        <!-- Panel Izquierdo: Cola de Documentos Pendientes -->
        <UiCard class="panel-card">
          <div class="panel-head">
            <div class="panel-head__left">
              <h2 class="panel-title">Documentos Pendientes de Validación</h2>
              <UiBadge tone="warning">{{ pendingDocs.length }} en espera</UiBadge>
            </div>
            <UiButton
              v-if="hasRole('department_head', 'social_service_officer', 'internship_coordinator')"
              variant="secondary"
              size="sm"
              @click="router.push('/expedients/review')"
            >
              Ver bandeja completa →
            </UiButton>
          </div>

          <div class="panel-scroll">
            <div v-if="pendingDocs.length === 0" class="empty-box">
              No hay documentos pendientes de dictamen en la cola de revisión.
            </div>
            <div v-else class="list-stack">
              <article v-for="doc in pendingDocs" :key="doc.id" class="list-item-row">
                <div class="item-main">
                  <div class="item-title">
                    <span class="mono-code">{{ doc.studentControlNumber }}</span>
                    <strong class="student-name">{{ doc.studentFullName }}</strong>
                    <UiBadge tone="info">{{ doc.careerCode }}</UiBadge>
                  </div>
                  <div class="item-sub">
                    <UiBadge :tone="doc.tramiteType === 'social_service' ? 'info' : 'purple'">
                      {{ doc.tramiteType === 'social_service' ? 'Servicio Social' : 'Prácticas' }}
                    </UiBadge>
                    <span class="doc-file">
                      <strong>
                        {{
                          doc.tramiteType === 'social_service'
                            ? socialDocumentLabel(doc.documentCode)
                            : doc.documentCode
                        }}
                      </strong>
                      · {{ doc.originalFileName }}
                    </span>
                  </div>
                </div>
                <UiButton
                  variant="primary"
                  size="sm"
                  @click="router.push('/expedients/review')"
                >
                  Revisar PDF
                </UiButton>
              </article>
            </div>
          </div>
        </UiCard>

        <!-- Panel Derecho: Trámites Recientes en Seguimiento -->
        <UiCard class="panel-card">
          <div class="panel-head">
            <div class="panel-head__left">
              <h2 class="panel-title">Trámites Recientes en Seguimiento</h2>
            </div>

            <div class="segmented-tabs" role="tablist" aria-label="Filtrar trámites recientes">
              <button
                type="button"
                class="seg-tab"
                :class="{ 'seg-tab--active': tramiteTab === 'all' }"
                @click="tramiteTab = 'all'"
              >
                Todos
              </button>
              <button
                v-if="canReadProcess('social_service')"
                type="button"
                class="seg-tab"
                :class="{ 'seg-tab--active': tramiteTab === 'social_service' }"
                @click="tramiteTab = 'social_service'"
              >
                Servicio Social
              </button>
              <button
                v-if="canReadProcess('internship')"
                type="button"
                class="seg-tab"
                :class="{ 'seg-tab--active': tramiteTab === 'internship' }"
                @click="tramiteTab = 'internship'"
              >
                Prácticas
              </button>
            </div>
          </div>

          <div class="panel-scroll">
            <div class="list-stack">
              <template v-if="showSSInTracker">
                <article
                  v-for="ss in recentSS.slice(0, 6)"
                  :key="`ss-${ss.id}`"
                  class="list-item-row"
                >
                  <div class="item-main">
                    <div class="item-title">
                      <UiBadge tone="info">SS</UiBadge>
                      <span class="mono-code">{{ ss.studentControlNumber }}</span>
                      <strong class="student-name">{{ ss.studentFullName }}</strong>
                    </div>
                    <div class="item-sub">
                      <span>{{ ss.companyName }}</span>
                      <span>·</span>
                      <strong>Avance: {{ ss.documentProgressPercentage }}%</strong>
                    </div>
                  </div>
                  <div class="row-right-actions">
                    <UiBadge :tone="getStatusTone(ss.status)">
                      {{ formatStatus(ss.status) }}
                    </UiBadge>
                    <UiButton
                      variant="secondary"
                      size="sm"
                      @click="router.push('/social-service')"
                    >
                      Abrir
                    </UiButton>
                  </div>
                </article>
              </template>

              <template v-if="showPPInTracker">
                <article
                  v-for="pp in recentPP.slice(0, 6)"
                  :key="`pp-${pp.id}`"
                  class="list-item-row"
                >
                  <div class="item-main">
                    <div class="item-title">
                      <UiBadge tone="purple">PP</UiBadge>
                      <span class="mono-code">{{ pp.studentControlNumber }}</span>
                      <strong class="student-name">{{ pp.studentFullName }}</strong>
                    </div>
                    <div class="item-sub">
                      <span>{{ pp.companyName || 'Carta Presentación solicitada' }}</span>
                      <span>·</span>
                      <strong>Avance: {{ pp.documentProgressPercentage }}%</strong>
                    </div>
                  </div>
                  <div class="row-right-actions">
                    <UiBadge :tone="getStatusTone(pp.status)">
                      {{ formatStatus(pp.status) }}
                    </UiBadge>
                    <UiButton
                      variant="secondary"
                      size="sm"
                      @click="router.push('/internships')"
                    >
                      Abrir
                    </UiButton>
                  </div>
                </article>
              </template>

              <div
                v-if="(!showSSInTracker || recentSS.length === 0) && (!showPPInTracker || recentPP.length === 0)"
                class="empty-box"
              >
                No hay trámites recientes registrados en este filtro.
              </div>
            </div>
          </div>
        </UiCard>
      </div>
    </div>
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
  text-transform: uppercase;
  padding: 0.14rem 0.5rem;
  border-radius: 5px;
  background: var(--color-accent-light);
  color: var(--color-accent);
  border: 1px solid color-mix(in srgb, var(--color-accent) 40%, transparent);
}

.exec-period {
  font-size: 0.74rem;
  color: var(--color-text-secondary);
}

.exec-period strong {
  color: var(--color-text-primary);
}

.title-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.exec-title {
  font-size: 1.2rem;
  font-weight: 800;
  margin: 0;
  color: var(--color-text-primary);
  letter-spacing: -0.015em;
  line-height: 1.2;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

/* 4 KPIs interactivos con superficie neutra */
.exec-kpis {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.mini-kpi {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  padding: 0.4rem 0.8rem;
  min-width: 122px;
  border-radius: 8px;
  border: 1px solid var(--color-border-medium);
  background: var(--color-surface-white);
  color: var(--color-text-primary);
  text-align: left;
  cursor: pointer;
  transition:
    background-color 160ms ease,
    border-color 160ms ease,
    box-shadow 160ms ease;
}

.mini-kpi:hover {
  background: var(--secondary-hover);
  border-color: var(--color-accent);
  box-shadow: 0 0 0 1px var(--color-accent);
}

.mini-kpi__label {
  font-size: 0.63rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.mini-kpi__value {
  font-size: 1.18rem;
  font-weight: 800;
  line-height: 1.15;
  font-variant-numeric: tabular-nums;
  color: var(--color-text-primary);
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

.mini-kpi--danger .mini-kpi__value {
  color: var(--color-danger-text);
}

.mini-kpi__foot {
  font-size: 0.67rem;
  color: var(--color-text-secondary);
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

.loading-box,
.empty-box {
  padding: 2rem;
  text-align: center;
  color: var(--color-text-secondary);
  font-size: 0.85rem;
}

/* Cinta de Conectores Rápidos (Staff) */
.staff-portal {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
  flex: 1;
  min-height: 0;
}

.connectors-strip {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(135px, 1fr));
  gap: 0.45rem;
  flex-shrink: 0;
}

.connector-chip {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.4rem;
  padding: 0.45rem 0.65rem;
  border-radius: 8px;
  border: 1px solid var(--color-border);
  background: var(--color-surface-white);
  color: var(--color-text-primary);
  text-align: left;
  cursor: pointer;
  transition: background-color 150ms ease, border-color 150ms ease;
}

.connector-chip:hover {
  background: var(--secondary-hover);
  border-color: var(--color-accent);
}

.connector-chip__text {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.connector-chip__text strong {
  font-size: 0.77rem;
  font-weight: 700;
  color: var(--color-text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.connector-chip__text span {
  font-size: 0.67rem;
  color: var(--color-text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.connector-chip__arrow {
  font-size: 0.8rem;
  font-weight: 700;
  color: var(--color-accent);
  flex-shrink: 0;
}

/* Dos Columnas Principales (con scroll interno) */
.staff-panels-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
  flex: 1;
  min-height: 0;
}

.panel-card {
  display: flex;
  flex-direction: column;
  min-height: 0;
  padding: 12px 16px !important;
  border: 1px solid var(--color-border);
  background: var(--color-surface-white);
}

.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  padding-bottom: 0.55rem;
  margin-bottom: 0.55rem;
  border-bottom: 1px solid var(--color-border-light);
  flex-wrap: wrap;
  flex-shrink: 0;
}

.panel-head__left {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.panel-title {
  margin: 0;
  font-size: 0.92rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.segmented-tabs {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  padding: 2px;
  height: 30px;
  border-radius: 6px;
  background: var(--input-bg);
  border: 1px solid var(--input-border);
}

.seg-tab {
  min-height: 24px !important;
  height: 24px;
  padding: 0 8px;
  border: 1px solid transparent;
  border-radius: 4px;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 0.72rem;
  font-weight: 600;
  cursor: pointer;
  white-space: nowrap;
  transition: background-color 140ms ease, color 140ms ease, border-color 140ms ease;
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

.panel-scroll {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding-right: 2px;
}

.list-stack {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.list-item-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  padding: 0.6rem 0.75rem;
  border-radius: 8px;
  border: 1px solid var(--color-border);
  background: var(--surface-toolbar);
  transition: background-color 140ms ease, border-color 140ms ease;
}

.list-item-row:hover {
  background: var(--color-primary-subtle);
  border-color: var(--color-border-medium);
}

.item-main {
  min-width: 0;
  flex: 1;
}

.item-title {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex-wrap: wrap;
  margin-bottom: 0.2rem;
}

.mono-code {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.76rem;
  font-weight: 700;
  color: var(--color-info-text);
}

.student-name {
  font-size: 0.83rem;
  font-weight: 600;
  color: var(--color-text-primary);
}

.item-sub {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex-wrap: wrap;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.doc-file {
  overflow-wrap: anywhere;
}

.row-right-actions {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  flex-shrink: 0;
}

/* Vista de Estudiante */
.student-portal-grid {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  flex: 1;
  min-height: 0;
  overflow-y: auto;
}

.student-profile-card {
  padding: 12px 16px !important;
  border: 1px solid var(--color-border);
  background: var(--color-surface-white);
  flex-shrink: 0;
}

.student-summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(175px, 1fr));
  gap: 0.75rem;
  align-items: center;
}

.meta-label {
  display: block;
  font-size: 0.72rem;
  font-weight: 600;
  color: var(--color-text-secondary);
  margin-bottom: 0.12rem;
}

.meta-value {
  font-size: 0.9rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
}

.accent-info {
  color: var(--color-info-text);
}

.folio-released {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  color: var(--color-success-text);
}

.imss-badge-wrap {
  margin-top: 0.2rem;
}

.profile-cta {
  display: flex;
  justify-content: flex-end;
}

.tramites-cards-row {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
  flex: 1;
  min-height: 0;
}

.student-tramite-card {
  display: flex;
  flex-direction: column;
  border: 1px solid var(--color-border);
  background: var(--color-surface-white);
}

.tramite-card-body {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
  height: 100%;
}

.tramite-status-line {
  display: flex;
  gap: 0.45rem;
  flex-wrap: wrap;
}

.info-pairs {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  font-size: 0.85rem;
  color: var(--color-text-primary);
}

.progress-section {
  background: var(--surface-toolbar);
  padding: 0.7rem 0.85rem;
  border-radius: 8px;
  border: 1px solid var(--color-border);
}

.progress-label-row {
  display: flex;
  justify-content: space-between;
  font-size: 0.8rem;
  margin-bottom: 0.35rem;
  color: var(--color-text-secondary);
}

.progress-label-row strong {
  color: var(--color-text-primary);
}

.progress-track {
  height: 8px;
  background: var(--color-border);
  border-radius: 999px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: var(--color-info);
  transition: width 0.3s ease;
}

.progress-fill.green {
  background: var(--color-success);
}

.card-footer-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: auto;
  padding-top: 0.4rem;
}

.empty-text {
  font-size: 0.85rem;
  color: var(--color-text-secondary);
  line-height: 1.5;
  margin: 0;
}

@media (max-width: 1020px) {
  .staff-panels-grid,
  .tramites-cards-row {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 900px) {
  .fullscreen-page {
    height: auto !important;
    overflow: visible !important;
    padding: 4px 0 12px !important;
  }

  .student-portal-grid,
  .panel-scroll {
    overflow: visible;
  }

  .panel-card,
  .student-profile-card {
    padding: 10px 12px !important;
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
    flex-direction: column;
    align-items: stretch;
    gap: 0.5rem;
  }

  .exec-title {
    font-size: 1.06rem;
  }

  .header-actions {
    width: 100%;
  }

  .header-actions :deep(.btn) {
    flex: 1;
    min-height: 36px;
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

  .connectors-strip {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 0.4rem;
  }

  .panel-head {
    flex-direction: column;
    align-items: stretch;
    gap: 0.5rem;
  }

  .panel-head > :deep(.btn) {
    width: 100%;
  }

  .segmented-tabs {
    display: flex;
    width: 100%;
    height: auto;
    min-height: 34px;
  }

  .seg-tab {
    flex: 1;
    min-height: 28px !important;
    height: 28px;
    text-align: center;
  }

  .list-item-row {
    flex-direction: column;
    align-items: stretch;
    gap: 0.55rem;
    padding: 0.65rem 0.7rem;
  }

  .list-item-row > :deep(.btn) {
    width: 100%;
  }

  .row-right-actions {
    justify-content: space-between;
    width: 100%;
  }

  .student-summary-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 0.65rem;
  }

  .profile-cta {
    grid-column: 1 / -1;
    justify-content: stretch;
  }

  .profile-cta :deep(.btn),
  .card-footer-actions :deep(.btn) {
    width: 100%;
    white-space: normal;
    text-align: center;
  }
}
</style>
