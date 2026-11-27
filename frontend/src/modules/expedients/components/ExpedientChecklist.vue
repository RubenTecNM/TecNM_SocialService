<script setup lang="ts">
import { problemMessage } from '../../../shared/api/problemDetails'
import { computed, ref, watch } from 'vue'
import { UiBadge, UiButton, UiAlert } from '../../../shared/components'
import { http } from '../../../shared/api/http'
import { getDocumentBlob, uploadMyDocument, uploadStudentDocument } from '../api/expedientsApi'
import type { ExpedientDocument, DocumentRequirement } from '../types/expedients'
import {
  socialServiceDocuments,
  socialDocumentLabel,
  socialDocumentOrder,
  socialDocumentPeriod,
} from '../../../shared/utils/socialServiceDocuments'
import DocumentViewerModal from './DocumentViewerModal.vue'

const props = defineProps<{
  requirements: DocumentRequirement[]
  documents: ExpedientDocument[]
  tramiteType: 'social_service' | 'internship'
  tramiteId: number
  studentId?: number
  canEdit?: boolean
  applicationAccepted?: boolean | null
}>()

const orderedRequirements = computed(() =>
  props.tramiteType === 'social_service'
    ? [...props.requirements].sort((a, b) => socialDocumentOrder(a.code) - socialDocumentOrder(b.code))
    : props.requirements,
)

const emit = defineEmits<{ changed: []; edit: [key: string] }>()
const error = ref('')
const busy = ref('')
const title = ref('')
const blob = ref<Blob | null>(null)
const open = ref(false)
const pending = ref<{ item: DocumentRequirement; file: File } | null>(null)

watch(open, (value) => {
  if (!value && !busy.value) {
    pending.value = null
    blob.value = null
  }
})

function current(code: string): ExpedientDocument | undefined {
  return props.documents.find((d) => d.documentCode === code && d.status !== 'superseded')
}

function resolveStatusLabel(item: DocumentRequirement): string {
  if (item.code === 'SOL' && props.tramiteType === 'social_service') {
    if (props.applicationAccepted === true) return 'Aprobado'
    if (props.applicationAccepted === false) return 'Rechazado'
    return 'En revisión'
  }
  const st = current(item.code)?.status
  if (st === 'approved') return 'Aprobado'
  if (st === 'rejected') return 'Rechazado'
  if (st) return 'En revisión'
  return 'Pendiente'
}

function resolveStatusTone(item: DocumentRequirement): 'success' | 'danger' | 'warning' | 'info' {
  if (item.code === 'SOL' && props.tramiteType === 'social_service') {
    if (props.applicationAccepted === true) return 'success'
    if (props.applicationAccepted === false) return 'danger'
    return 'warning'
  }
  const st = current(item.code)?.status
  if (st === 'approved') return 'success'
  if (st === 'rejected') return 'danger'
  if (st) return 'warning'
  return 'info'
}

async function view(item: DocumentRequirement, uploaded = false): Promise<void> {
  pending.value = null
  error.value = ''
  busy.value = item.code
  try {
    const doc = current(item.code)
    blob.value =
      uploaded && doc
        ? await getDocumentBlob(doc.id)
        : (
            await http.get(
              item.templateKey
                ? `/document-templates/${item.templateKey}/file?tramiteId=${props.tramiteId}`
                : `/${props.tramiteType === 'internship' ? 'internships' : 'social-services'}/${props.tramiteId}/pdf/${item.pdfKey}`,
              { responseType: 'blob' },
            )
          ).data
    title.value =
      props.tramiteType === 'social_service' ? socialDocumentLabel(item.code) : item.title
    open.value = true
  } catch {
    error.value = 'El PDF no está disponible. Revisa los datos guardados y vuelve a intentar.'
  } finally {
    busy.value = ''
  }
}

async function upload(item: DocumentRequirement, event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file || busy.value) return
  error.value = ''
  if (!file.name.toLowerCase().endsWith('.pdf') || file.size > 5 * 1024 * 1024) {
    error.value = 'Adjunta un PDF de hasta 5 MiB.'
    return
  }
  if ((await file.slice(0, 5).text()) !== '%PDF-') {
    error.value = 'El archivo no es un PDF válido.'
    return
  }
  pending.value = { item, file }
  blob.value = file
  title.value = `Verifica el archivo: ${file.name}`
  open.value = true
}

async function confirmUpload(): Promise<void> {
  if (!pending.value || busy.value) return
  const { item, file } = pending.value
  busy.value = item.code
  error.value = ''
  try {
    if (props.studentId) {
      const data = new FormData()
      data.append('file', file)
      data.append('tramiteType', props.tramiteType)
      data.append('tramiteId', String(props.tramiteId))
      data.append('documentCode', item.code)
      await uploadStudentDocument(props.studentId, data)
    } else {
      await uploadMyDocument({
        file,
        tramiteType: props.tramiteType,
        tramiteId: props.tramiteId,
        documentCode: item.code,
      })
    }
    pending.value = null
    open.value = false
    blob.value = null
    emit('changed')
  } catch (e: any) {
    error.value = problemMessage(e, 'No se pudo cargar el PDF.')
  } finally {
    busy.value = ''
  }
}
</script>

<template>
  <section class="checklist-shell" aria-label="Expediente documental">
    <UiAlert v-if="error" tone="danger" class="compact-alert">{{ error }}</UiAlert>

    <div class="checklist-grid">
      <template v-for="item in orderedRequirements" :key="item.code">
        <div
          v-if="tramiteType === 'social_service' && item.code === 'R1'"
          class="group-divider"
        >
          <span>4. {{ socialServiceDocuments.find((doc) => doc.number === '4')?.title }}</span>
        </div>

        <article class="doc-card">
          <div class="doc-card__top">
            <div class="doc-card__title-wrap">
              <span class="doc-code">{{ item.code }}</span>
              <strong class="doc-title">
                {{ tramiteType === 'social_service' ? socialDocumentLabel(item.code) : item.title }}
              </strong>
            </div>
            <UiBadge :tone="resolveStatusTone(item)">
              {{ resolveStatusLabel(item) }}
            </UiBadge>
          </div>

          <p
            v-if="tramiteType === 'social_service' && socialDocumentPeriod(item.code)"
            class="doc-hint"
          >
            {{ socialDocumentPeriod(item.code) }}
          </p>
          <p v-if="current(item.code)?.rejectionReason" class="doc-error">
            Observación: {{ current(item.code)?.rejectionReason }}
          </p>
          <p v-if="item.blockedReason" class="doc-blocked">
            {{ item.blockedReason }}
          </p>

          <div class="doc-actions">
            <UiButton
              v-if="
                canEdit !== false &&
                current(item.code)?.status !== 'approved' &&
                (item.pdfKey?.startsWith('report_') || item.pdfKey === 'evaluation')
              "
              variant="secondary"
              size="sm"
              @click="emit('edit', item.pdfKey!)"
            >
              Editar datos
            </UiButton>
            <UiButton
              v-if="
                (item.pdfKey || item.templateKey) &&
                !(
                  tramiteType === 'social_service' &&
                  item.code === 'CC' &&
                  !studentId &&
                  current(item.code)?.status === 'approved'
                )
              "
              variant="secondary"
              size="sm"
              :disabled="!!busy"
              @click="view(item)"
            >
              {{ item.templateKey ? 'Descargar plantilla' : 'Descargar prellenado' }}
            </UiButton>
            <UiButton
              v-if="current(item.code)"
              variant="secondary"
              size="sm"
              @click="view(item, true)"
            >
              Consultar PDF
            </UiButton>
            <label
              v-if="item.canUpload && current(item.code)?.status !== 'approved'"
              class="upload-pill"
            >
              Subir PDF firmado
              <input
                type="file"
                accept="application/pdf"
                class="hidden-file-input"
                :disabled="!!busy"
                @change="upload(item, $event)"
              />
            </label>
          </div>
        </article>
      </template>
    </div>

    <DocumentViewerModal
      v-model="open"
      :title="title"
      :blob="blob"
      :confirm-upload="!!pending"
      :busy="!!busy"
      :error="pending ? error : undefined"
      @confirm="confirmUpload"
    />
  </section>
</template>

<style scoped>
.checklist-shell {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
}

.compact-alert {
  margin-bottom: 0.25rem;
}

.checklist-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.6rem;
}

.group-divider {
  grid-column: 1 / -1;
  padding: 0.35rem 0.65rem;
  border-radius: 6px;
  background: var(--color-accent-light);
  border: 1px solid color-mix(in srgb, var(--color-accent) 35%, transparent);
  color: var(--color-accent);
  font-size: 0.76rem;
  font-weight: 800;
  letter-spacing: 0.03em;
  text-transform: uppercase;
}

.doc-card {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  padding: 0.7rem 0.85rem;
  border-radius: 8px;
  border: 1px solid var(--color-border);
  background: var(--surface-toolbar);
  transition: border-color 140ms ease;
}

.doc-card:hover {
  border-color: var(--color-border-medium);
}

.doc-card__top {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.5rem;
}

.doc-card__title-wrap {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  min-width: 0;
  flex-wrap: wrap;
}

.doc-code {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.68rem;
  font-weight: 800;
  padding: 0.08rem 0.38rem;
  border-radius: 4px;
  background: var(--color-info-badge);
  color: var(--color-info-text);
  border: 1px solid var(--color-info-border);
  flex-shrink: 0;
}

.doc-title {
  font-size: 0.82rem;
  font-weight: 700;
  color: var(--color-text-primary);
  line-height: 1.25;
}

.doc-hint {
  margin: 0;
  font-size: 0.74rem;
  color: var(--color-text-secondary);
  line-height: 1.35;
}

.doc-error {
  margin: 0;
  font-size: 0.74rem;
  color: var(--color-danger-text);
  line-height: 1.35;
}

.doc-blocked {
  margin: 0;
  font-size: 0.74rem;
  color: var(--color-warning-text);
  line-height: 1.35;
}

.doc-actions {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.4rem;
  margin-top: auto;
  padding-top: 0.25rem;
}

.upload-pill {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 30px;
  padding: 0 0.65rem;
  border-radius: 6px;
  border: 1px dashed var(--color-border-medium);
  background: var(--color-surface-white);
  color: var(--color-text-primary);
  font-size: 0.74rem;
  font-weight: 600;
  cursor: pointer;
  transition: background-color 140ms ease, border-color 140ms ease;
}

.upload-pill:hover {
  background: var(--secondary-hover);
  border-color: var(--color-accent);
}

.hidden-file-input {
  display: none;
}

@media (max-width: 780px) {
  .checklist-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .doc-card {
    padding: 0.65rem 0.75rem;
  }

  .doc-actions {
    gap: 0.45rem;
  }

  .doc-actions :deep(.btn),
  .upload-pill {
    flex: 1 1 auto;
    min-height: 36px;
    justify-content: center;
    text-align: center;
  }
}
</style>
