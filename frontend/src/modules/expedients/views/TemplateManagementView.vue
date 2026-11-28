<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { isAxiosError } from 'axios'
import { UiAlert, UiBasePage, UiButton } from '../../../shared/components'
import { http } from '../../../shared/api/http'
import { problemFields, problemMessage } from '../../../shared/api/problemDetails'
import DocumentViewerModal from '../components/DocumentViewerModal.vue'
import TemplateCanvas from '../components/TemplateCanvas.vue'
import TemplateLogoEditor from '../components/TemplateLogoEditor.vue'
import type { LogoPlacement } from '../types/templateManagement'
import type { Placement, TemplateActivation, TemplateSlot, TemplateSummary, TemplateVersion, VersionSummary } from '../types/templateManagement'

const base = '/template-management'
const catalog = ref<TemplateSummary[]>([])
const key = ref('')
const current = computed(() => catalog.value.find(item => item.key === key.value))
const route = computed(() => base + '/' + encodeURIComponent(key.value))
const draft = ref<TemplateVersion>()
const history = ref<VersionSummary[]>([])
const activations = ref<TemplateActivation[]>([])
const source = ref<Blob>()
const selected = ref(0)
const page = ref(0)
const field = computed(() => draft.value?.layout.fields[selected.value])
const busy = ref(false)
const dirty = ref(false)
const editingLogos = ref(false)
const cleanBranding = ref(false)
const message = ref('')
const error = ref('')
const fieldErrors = ref<Record<string, string[]>>({})
const previewBlob = ref<Blob | null>(null)
const previewTitle = ref('')
const viewer = ref(false)
const reviewed = ref({ normal: false, long: false })
const reason = ref('')
const restoreId = ref<number>()
const confirmingDiscard = ref(false)
const labels: Record<string, string> = {
  'ss-application': 'Solicitud de Servicio Social',
  'ss-commitment': 'Carta Compromiso',
  'ss-report': 'Reportes bimestrales R1–R3',
  'ss-supervisor-evaluation': 'Evaluación del supervisor',
  'ss-self-evaluation': 'Autoevaluación',
  'ss-termination': 'Carta de Terminación',
  'ss-activity': 'Evaluación de actividades',
  'ss-acceptance-example': 'Ejemplo de Carta de Aceptación',
  'ss-individual-act': 'Acta Individual',
  'pp-evaluation': 'Evaluación de Prácticas Profesionales',
  studentFullName: 'Nombre completo', studentFirstName: 'Nombre(s)', studentFirstSurname: 'Primer apellido',
  studentSecondSurname: 'Segundo apellido', studentControlNumber: 'Número de control', studentAddress: 'Domicilio del alumno',
  studentPhone: 'Teléfono', studentGender: 'Sexo', studentSemester: 'Semestre', careerName: 'Carrera',
  careerCode: 'Casilla de carrera', programCategory: 'Casilla de tipo de programa', programName: 'Nombre del programa',
  activitiesOutline: 'Programa de actividades', activitiesSummary: 'Resumen de actividades', companyName: 'Dependencia',
  dependencyName: 'Dependencia', dependencyAddress: 'Domicilio de la dependencia', dependencyHeadName: 'Titular de la dependencia',
  dependencyHeadPosition: 'Cargo del titular', supervisorFullName: 'Nombre del supervisor', supervisorPosition: 'Cargo del supervisor',
  periodCode: 'Periodo', modality: 'Modalidad', generatedAt: 'Fecha de generación', startDate: 'Fecha de inicio',
  endDate: 'Fecha de término', periodStartDate: 'Inicio del periodo', periodEndDate: 'Término del periodo',
  reportedHours: 'Horas del reporte', reportAccumulatedHours: 'Horas acumuladas del reporte', accumulatedHours: 'Horas acumuladas',
  reportNumber: 'Número de reporte', realizationPeriod: 'Periodo de realización', finalMark: 'Marca final',
  issueDate: 'Fecha de expedición', commitmentDate: 'Fecha de compromiso', city: 'Ciudad',
  evaluationStartDate: 'Inicio de evaluación', evaluationEndDate: 'Término de evaluación',
  actBody: 'Cuerpo del acta', actDate: 'Fecha del acta', outreachAuthority: 'Autoridad de Vinculación', directorAuthority: 'Dirección General',
}

function slotLabel(slot: TemplateSlot | undefined) {
  if (!slot) return ''
  const detail = slot.matchValue ?? slot.format
  return [labels[slot.key] ?? slot.key, slot.followingKey ? labels[slot.followingKey] : undefined, detail].filter(Boolean).join(' · ')
}
const activeFieldErrors = computed(() => {
  const slot = draft.value?.slots[selected.value]
  return slot ? [...(fieldErrors.value[slot.key] ?? []), ...(fieldErrors.value[slot.followingKey ?? ''] ?? [])] : []
})
const date = (value: string | null) => value ? new Intl.DateTimeFormat('es-MX', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : 'Borrador'

async function action(work: () => Promise<void>) {
  busy.value = true
  error.value = ''
  message.value = ''
  fieldErrors.value = {}
  try { await work() }
  catch (cause) {
    if (isAxiosError(cause) && cause.response?.data instanceof Blob) {
      const response = cause.response
      try { response.data = JSON.parse(await response.data.text()) }
      catch { /* An error without ProblemDetails uses the generic message. */ }
    }
    fieldErrors.value = problemFields(cause)
    error.value = problemMessage(cause, 'La operación falló. Vuelve a intentar.')
  } finally { busy.value = false }
}
async function csrf() {
  const response = await http.get<{ token: string }>(base + '/csrf')
  return { headers: { 'X-CSRF-TOKEN': response.data.token } }
}
async function list() {
  catalog.value = (await http.get<TemplateSummary[]>(base)).data
}
async function selectTemplate(value: string) {
  key.value = value
  draft.value = undefined
  source.value = undefined
  restoreId.value = undefined
  confirmingDiscard.value = false
  reason.value = ''
  dirty.value = false
  editingLogos.value = false
  reviewed.value = { normal: false, long: false }
  const [versions, events] = await Promise.all([
    http.get<VersionSummary[]>(route.value + '/versions'),
    http.get<TemplateActivation[]>(route.value + '/activations'),
  ])
  history.value = versions.data
  activations.value = events.data
  if (current.value?.draftId) {
    const response = await http.get<TemplateVersion>(route.value + '/versions/' + current.value.draftId)
    await loadDraft(response.data)
  }
}
async function loadDraft(value: TemplateVersion) {
  const file = await http.get<Blob>(route.value + '/versions/' + value.id + '/file', { responseType: 'blob', params: { source: true } })
  draft.value = value
  source.value = file.data
  selected.value = 0
  page.value = 0
  cleanBranding.value = false
  if (!value.layout.logoZones?.length) editingLogos.value = false
  dirty.value = false
  reviewed.value = { normal: false, long: false }
}
function openFile(blob: Blob, title: string) {
  previewBlob.value = blob
  previewTitle.value = title
  viewer.value = true
}
async function readVersion(id: number) {
  const response = await http.get<Blob>(route.value + '/versions/' + id + '/file', { responseType: 'blob' })
  openFile(response.data, (labels[key.value] ?? key.value) + ' · versión ' + id)
}
async function createDraft() {
  const response = await http.post<TemplateVersion>(route.value + '/draft', null, await csrf())
  await list()
  await loadDraft(response.data)
}
async function replaceFile(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  await action(async () => {
    if (!draft.value) return
    const body = new FormData()
    body.append('file', file)
    const response = await http.put<TemplateVersion>(route.value + '/draft/file', body, {
      ...await csrf(), params: { revision: draft.value.revision, cleanBranding: cleanBranding.value },
    })
    await loadDraft(response.data)
    message.value = 'PDF cargado. Revisa todas las posiciones antes de publicar.'
  })
}
function change() {
  dirty.value = true
  reviewed.value = { normal: false, long: false }
}
function selectSlot(i: number) {
  selected.value = i
  page.value = draft.value?.layout.fields[i]?.page ?? page.value
}
function putPlacement(i: number, value: Placement) {
  if (!draft.value) return
  draft.value.layout.fields[i] = value
  change()
}
function place() {
  const size = draft.value?.layout.pageSizes[page.value]
  if (!size) return
  const slot = draft.value?.slots[selected.value]
  const x = Math.min(36, size.width / 4)
  const y = Math.min(36, size.height / 4)
  const width = Math.min(160, size.width - x)
  const height = Math.min(28, size.height - y)
  putPlacement(selected.value, {
    x, y, width, height, page: page.value, alignment: slot?.alignment ?? 'left', fontSize: null, minFontSize: null,
    rows: slot?.rows ? [{ x, width, baseline: y + height * .65 }] : null,
  })
}
function addRow() {
  if (!field.value) return
  const rows = field.value.rows ?? []
  const start = rows.at(-1)?.baseline ?? field.value.y
  const bottom = field.value.y + field.value.height
  if (bottom - start < .02) return
  field.value.rows = [...rows, { x: field.value.x, width: field.value.width, baseline: (bottom + start) / 2 }]
  if (field.value.alignment === 'justify') field.value.alignment = 'left'
  change()
}
function removeRow(i: number) {
  if (!field.value?.rows) return
  const rows = field.value.rows.filter((_, index) => index !== i)
  field.value.rows = rows.length ? rows : null
  change()
}
async function saveLayout() {
  if (!draft.value) return
  const { revision, layout } = draft.value
  const fontValue = (value: number | null) => value === null || typeof value === 'number' ? value : null
  const fields = layout.fields.map(placement => placement && ({
    ...placement, fontSize: fontValue(placement.fontSize), minFontSize: fontValue(placement.minFontSize),
  }))
  draft.value = (await http.put<TemplateVersion>(route.value + '/draft/layout', {
    revision, font: layout.font, fontSize: layout.fontSize, minFontSize: layout.minFontSize, fields,
  }, await csrf())).data
  dirty.value = false
  reviewed.value = { normal: false, long: false }
  message.value = 'Configuración guardada.'
}
async function preview(longSample: boolean) {
  if (!draft.value) return
  const result = await http.post<Blob>(route.value + '/draft/preview', null, {
    ...await csrf(), params: { revision: draft.value.revision, longSample }, responseType: 'blob',
  })
  openFile(result.data, longSample ? 'Muestra larga' : 'Muestra normal')
  reviewed.value[longSample ? 'long' : 'normal'] = true
}
async function saveLogos(logos: LogoPlacement[]) {
  if (!draft.value) return
  const { data } = await http.put<TemplateVersion>(route.value + '/draft/logos', {
    revision: draft.value.revision, logos,
  }, await csrf())
  draft.value = data
  dirty.value = false
  reviewed.value = { normal: false, long: false }
  message.value = 'Logotipos guardados.'
}
async function adoptAuthorityBase() {
  if (!draft.value) return
  const { data } = await http.post<TemplateVersion>(route.value + '/draft/adopt-authority-base', null, {
    ...await csrf(), params: { revision: draft.value.revision },
  })
  await loadDraft(data)
  message.value = 'Base actualizada. Los campos del borrador se conservaron.'
}
async function activate(id?: number) {
  if (!current.value) return
  await http.post(id === undefined ? route.value + '/draft/publish' : route.value + '/versions/' + id + '/restore', {
    templateRevision: current.value.revision, draftRevision: draft.value?.revision, reason: reason.value,
  }, await csrf())
  await list()
  await selectTemplate(key.value)
  message.value = id === undefined ? 'Plantilla publicada para nuevas aperturas.' : 'Versión restaurada para nuevas aperturas.'
}
async function discard() {
  if (!draft.value) return
  await http.delete(route.value + '/draft', { ...await csrf(), params: { revision: draft.value.revision } })
  await list()
  await selectTemplate(key.value)
  message.value = 'Borrador descartado.'
}
onMounted(() => action(list))
</script>

<template>
  <UiBasePage title="Plantillas documentales" fluid>
    <div class="templates" :aria-busy="busy">
      <UiAlert v-if="error" tone="danger" role="alert">{{ error }}</UiAlert>
      <p v-if="message" role="status" class="feedback">{{ message }}</p>
      <label class="catalog-select">Formato institucional
        <select :value="key" :disabled="busy || dirty" @change="action(() => selectTemplate(($event.target as HTMLSelectElement).value))">
          <option value="" disabled>Selecciona un formato</option>
          <option v-for="item in catalog" :key="item.key" :value="item.key">{{ labels[item.key] ?? item.key }}</option>
        </select>
      </label>
      <p v-if="!catalog.length && !busy && !error">El catálogo todavía está vacío.</p>
      <template v-if="current">
        <div class="template-actions">
          <span>Versión activa <strong>{{ current.activeVersionId ?? 'Sin publicar' }}</strong></span>
          <UiButton v-if="current.activeVersionId" variant="secondary" size="sm" :disabled="busy" @click="action(() => readVersion(current!.activeVersionId!))">Ver activa</UiButton>
          <UiButton v-if="!draft" size="sm" :disabled="busy || !!current.draftId" @click="action(createDraft)">Crear borrador</UiButton>
          <UiButton variant="secondary" size="sm" :disabled="busy || dirty" @click="action(async () => { await list(); await selectTemplate(key) })">Actualizar estado</UiButton>
        </div>
        <template v-if="draft">
          <div class="template-actions">
            <strong>Borrador {{ draft.id }}</strong>
            <label>PDF sustituto <input type="file" accept="application/pdf,.pdf" :disabled="busy || dirty" @change="replaceFile" /></label>
            <label v-if="draft.layout.logoZones?.length || draft.layout.authorityFields?.length" class="clean-branding">
              <input v-model="cleanBranding" type="checkbox" :disabled="busy || dirty" />
              Conservar zonas de identidad. El PDF sustituto debe traer esas zonas limpias.
            </label>
            <UiButton v-if="dirty" variant="secondary" size="sm" :disabled="busy" @click="action(() => selectTemplate(key))">Deshacer cambios locales</UiButton>
            <UiButton variant="secondary" size="sm" :disabled="busy" @click="confirmingDiscard = true">Descartar borrador</UiButton>
          </div>
          <div v-if="confirmingDiscard" class="template-actions">
            <span>Se eliminará el PDF y el mapa del borrador.</span>
            <UiButton variant="danger" size="sm" :disabled="busy" @click="action(discard)">Confirmar descarte</UiButton>
            <UiButton variant="secondary" size="sm" @click="confirmingDiscard = false">Cancelar</UiButton>
          </div>
          <UiAlert v-if="draft.canAdoptAuthorityBase" tone="warning">
            Este borrador usa destinatarios impresos. Adopta la base con autoridades dinámicas para poder publicarlo.
            <UiButton variant="secondary" size="sm" :disabled="busy || dirty" @click="action(adoptAuthorityBase)">Adoptar base limpia</UiButton>
          </UiAlert>
          <p v-if="draft.layout.authorityFields?.length">Los destinatarios se toman del catálogo de autoridades vigentes.</p>
          <div v-if="draft.layout.logoZones?.length" class="template-actions">
            <UiButton variant="secondary" :aria-pressed="!editingLogos" :disabled="busy || dirty" @click="editingLogos = false">Campos</UiButton>
            <UiButton variant="secondary" :aria-pressed="editingLogos" :disabled="busy || dirty" @click="editingLogos = true">Logotipos</UiButton>
          </div>
          <TemplateLogoEditor v-if="source && editingLogos" :key="draft.revision" :draft="draft" :source="source"
            :busy="busy" @changed="change" @save="logos => action(() => saveLogos(logos))" />
          <div v-else-if="source" class="editor">
            <TemplateCanvas v-model:page="page" :blob="source" :page-sizes="draft.layout.pageSizes"
              :fields="draft.layout.fields" :selected="selected" @select="selectSlot" @placement="putPlacement" />
            <fieldset v-if="current.mode === 'prefill'" class="inspector" :disabled="busy">
              <legend>Configuración</legend>
              <label>Fuente de plantilla
                <select v-model="draft.layout.font" @change="change"><option>Arial</option><option>Century Gothic</option></select>
              </label>
              <div class="numbers">
                <label>Tamaño nominal <input v-model.number="draft.layout.fontSize" type="number" step=".5" @input="change" /></label>
                <label>Tamaño mínimo <input v-model.number="draft.layout.minFontSize" type="number" step=".5" @input="change" /></label>
              </div>
              <label>Campo
                <select :value="selected" @change="selectSlot(Number(($event.target as HTMLSelectElement).value))">
                  <option v-for="(slot, i) in draft.slots" :key="i" :value="i">{{ i + 1 }}. {{ slotLabel(slot) }}{{ draft.layout.fields[i] ? '' : ' · Sin ubicar' }}</option>
                </select>
              </label>
              <p v-for="entry in activeFieldErrors" :key="entry" class="feedback">{{ entry }}</p>
              <UiButton v-if="!field" size="sm" @click="place">Ubicar en esta página</UiButton>
              <template v-else>
                <label>Página del campo
                  <select v-model.number="field.page" @change="page = field.page; change()">
                    <option v-for="(_, i) in draft.layout.pageSizes" :key="i" :value="i">{{ i + 1 }}</option>
                  </select>
                </label>
                <div class="numbers">
                  <label>X (pt) <input v-model.number="field.x" type="number" step=".1" @input="change" /></label>
                  <label>Y (pt) <input v-model.number="field.y" type="number" step=".1" @input="change" /></label>
                  <label>Ancho (pt) <input v-model.number="field.width" type="number" step=".1" @input="change" /></label>
                  <label>Alto (pt) <input v-model.number="field.height" type="number" step=".1" @input="change" /></label>
                  <label>Tamaño del campo <input v-model.number="field.fontSize" type="number" step=".5" placeholder="Heredado" @input="change" /></label>
                  <label>Mínimo del campo <input v-model.number="field.minFontSize" type="number" step=".5" placeholder="Heredado" @input="change" /></label>
                </div>
                <label>Alineación
                  <select v-model="field.alignment" @change="change">
                    <option value="left">Izquierda</option><option value="center">Centro</option><option v-if="!field.rows" value="justify">Justificada</option>
                  </select>
                </label>
                <fieldset v-for="(row, i) in field.rows" :key="i" class="row-settings">
                  <legend>Renglón {{ i + 1 }}</legend>
                  <div class="numbers">
                    <label>X (pt) <input v-model.number="row.x" type="number" step=".1" @input="change" /></label>
                    <label>Ancho (pt) <input v-model.number="row.width" type="number" step=".1" @input="change" /></label>
                    <label>Línea base (pt) <input v-model.number="row.baseline" type="number" step=".1" @input="change" /></label>
                  </div>
                  <UiButton variant="secondary" size="sm" :disabled="field.rows?.length === 1 && !!draft.slots[selected]?.followingKey" @click="removeRow(i)">Quitar renglón</UiButton>
                </fieldset>
                <UiButton variant="secondary" size="sm" @click="addRow">Añadir renglón</UiButton>
              </template>
              <UiButton size="sm" :disabled="!dirty" @click="action(saveLayout)">Guardar configuración</UiButton>
            </fieldset>
          </div>
          <section class="release">
            <h2>Revisar y publicar</h2>
            <p class="feedback">Revisa la alineación y las zonas de llenado manual en ambas muestras.</p>
            <div class="template-actions">
              <UiButton variant="secondary" size="sm" :disabled="busy || dirty" @click="action(() => preview(false))">{{ reviewed.normal ? 'Volver a ver muestra normal' : 'Ver muestra normal' }}</UiButton>
              <UiButton variant="secondary" size="sm" :disabled="busy || dirty" @click="action(() => preview(true))">{{ reviewed.long ? 'Volver a ver muestra larga' : 'Ver muestra larga' }}</UiButton>
            </div>
            <template v-if="restoreId === undefined">
              <label>Motivo de publicación <input v-model="reason" type="text" maxlength="1000" :disabled="busy" /></label>
              <UiButton :disabled="busy || dirty || !reviewed.normal || !reviewed.long || !reason.trim()" @click="action(() => activate())">Publicar para nuevas aperturas</UiButton>
            </template>
          </section>
        </template>
        <section class="release">
          <h2>Historial de versiones</h2>
          <ul class="history">
            <li v-for="version in history.filter(item => item.publishedAt)" :key="version.id">
              <span><strong>Versión {{ version.id }}</strong> · {{ date(version.publishedAt) }}{{ current.activeVersionId === version.id ? ' · Activa' : '' }}</span>
              <UiButton variant="secondary" size="sm" :disabled="busy" @click="action(() => readVersion(version.id))">Ver PDF</UiButton>
              <UiButton v-if="current.activeVersionId !== version.id" variant="secondary" size="sm" :disabled="busy || dirty" @click="restoreId = version.id; reason = ''">Restaurar</UiButton>
            </li>
          </ul>
          <div v-if="restoreId !== undefined" class="release">
            <label>Motivo para restaurar la versión {{ restoreId }} <input v-model="reason" maxlength="1000" /></label>
            <div class="template-actions">
              <UiButton :disabled="busy || dirty || !reason.trim()" @click="action(() => activate(restoreId))">Confirmar restauración</UiButton>
              <UiButton variant="secondary" @click="restoreId = undefined; reason = ''">Cancelar</UiButton>
            </div>
          </div>
          <details>
            <summary>Eventos de activación</summary>
            <ul class="history">
              <li v-for="(event, i) in activations" :key="i">
                {{ { import: 'Importación', publish: 'Publicación', restore: 'Restauración' }[event.action] }}
                · versión {{ event.versionId }} · {{ date(event.createdAt) }}{{ event.actorId ? ' · Usuario ' + event.actorId : '' }} · {{ event.reason }}
              </li>
            </ul>
          </details>
        </section>
      </template>
      <DocumentViewerModal v-model="viewer" :title="previewTitle" :blob="previewBlob" @update:model-value="open => { if (!open) previewBlob = null }" />
    </div>
  </UiBasePage>
</template>

<style scoped>
.templates { display: grid; gap: 1rem; min-width: 0; }
.catalog-select { max-width: 30rem; }
.templates label { display: grid; gap: .25rem; font-size: .9rem; }
.templates input, .templates select { width: 100%; min-width: 0; }
.templates label.clean-branding { display: flex; align-items: center; gap: .4rem; }
.clean-branding input { width: auto; }
.template-actions { display: flex; flex-wrap: wrap; align-items: center; gap: .75rem; }
.editor { display: grid; grid-template-columns: minmax(0, 1fr) minmax(16rem, 21rem); gap: 1rem; align-items: start; }
.inspector, .row-settings { display: grid; gap: .65rem; min-width: 0; margin: 0; border: 1px solid var(--color-border-medium); padding: .85rem; }
.numbers { display: grid; gap: .55rem; grid-template-columns: repeat(2, minmax(0, 1fr)); }
.release { display: grid; gap: .75rem; border-top: 1px solid var(--color-border-medium); padding-top: .85rem; justify-items: start; }
.release h2, .feedback { margin: 0; }
.release label { width: min(100%, 42rem); }
.history { margin: 0; padding: 0; list-style: none; }
.history li { display: flex; align-items: center; gap: .5rem; flex-wrap: wrap; padding-block: .3rem; }
details summary { cursor: pointer; }
@media (max-width: 860px) { .editor { grid-template-columns: minmax(0, 1fr); } }
</style>
