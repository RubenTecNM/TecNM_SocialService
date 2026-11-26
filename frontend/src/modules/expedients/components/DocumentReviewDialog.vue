<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { UiAlert, UiButton, UiDialog, UiField } from '../../../shared/components'
import { socialDocumentLabel } from '../../../shared/utils/socialServiceDocuments'
import { problemMessage } from '../../../shared/api/problemDetails'
import { useFormValidation } from '../../../shared/composables/useFormValidation'
import { startReviewPresence, stopReviewPresence } from '../../../shared/composables/useRealtime'
import UiFormErrors from '../../../shared/components/UiFormErrors.vue'
import { getDocumentBlob, reviewDocument } from '../api/expedientsApi'
import type { ExpedientDocument } from '../types/expedients'

const open = defineModel<boolean>({ default: false })
const props = defineProps<{ document: ExpedientDocument | null; studentName?: string }>()
const emit = defineEmits<{ reviewed: [document: ExpedientDocument] }>()
const imssVerified = ref(false)
const reason = ref(''), error = ref('')
const decision = ref('approved')
const validation = useFormValidation(() => {
  const errors: Record<string, string[]> = {}
  if (decision.value === 'rejected' && !reason.value.trim()) errors.rejectionReason = ['Indica el motivo de rechazo.']
  if (decision.value === 'rejected' && reason.value.trim().length > 1000) errors.rejectionReason = ['El motivo admite máximo 1000 caracteres.']
  if (decision.value === 'approved' && props.document?.documentCode === 'IMSS' && !imssVerified.value) errors.imssVerified = ['Confirma que el PDF corresponde al alumno y muestra estado VIGENTE.']
  return errors
})
const url = ref(''), loading = ref(false), submitting = ref(false)
let sequence = 0
let presenceDocumentId: number | null = null
function releasePresence() {
  if (presenceDocumentId !== null) {
    const prevId = presenceDocumentId
    presenceDocumentId = null
    void stopReviewPresence(prevId)
  }
}
function release() { if (url.value) URL.revokeObjectURL(url.value); url.value = '' }
watch([open, () => props.document?.id], async () => {
  const request = ++sequence
  release()
  validation.reset()
  imssVerified.value = false; reason.value = ''; error.value = ''
  if (!open.value || !props.document) {
    releasePresence()
    return
  }
  const document = props.document
  if (document.status === 'pending') {
    if (presenceDocumentId !== document.id) {
      releasePresence()
      presenceDocumentId = document.id
    }
    void startReviewPresence(document.id)
  } else {
    releasePresence()
  }
  reason.value = document.rejectionReason || ''
  loading.value = true
  try {
    const blob = await getDocumentBlob(document.id)
    if (request !== sequence) return
    url.value = URL.createObjectURL(blob)
  } catch (failure) {
    if (request !== sequence) return
    error.value = problemMessage(failure, 'No fue posible abrir el PDF. Cierra y vuelve a intentar.')
  }
  loading.value = false
}, { immediate: true })
onBeforeUnmount(() => { sequence++; releasePresence(); release() })

async function submit(status: 'approved' | 'rejected') {
  const document = props.document
  if (!document || document.status !== 'pending' || submitting.value || loading.value) return
  error.value = ''
  decision.value = status
  if (!validation.validate()) return
  submitting.value = true
  try {
    const reviewed = await reviewDocument(document.id, { status,
      rejectionReason: status === 'rejected' ? reason.value.trim() : undefined })
    emit('reviewed', reviewed)
    open.value = false
  } catch (failure: any) {
    validation.apply(failure, 'No fue posible registrar el dictamen.')
  } finally { submitting.value = false }
}
</script>

<template>
  <UiDialog v-model="open" :title="`Revisar ${document?.tramiteType === 'social_service' ? socialDocumentLabel(document.documentCode) : document?.documentCode || 'documento'}${studentName ? ` · ${studentName}` : ''}`" size="full">
    <div class="review-content" :aria-busy="loading || submitting">
      <UiFormErrors :validation="validation" />
      <UiAlert v-if="error" tone="danger" role="alert">{{ error }}</UiAlert>
      <p v-if="loading" role="status">Cargando documento…</p>
      <iframe v-if="url" :src="url" title="PDF que se está revisando" class="review-pdf" />
      <a v-if="url" :href="url" :download="document?.originalFileName">Descargar PDF</a>
      <p v-if="document?.status !== 'pending'">Dictamen: {{ document?.status === 'approved' ? 'Aprobado' : document?.status === 'rejected' ? 'Rechazado' : 'Sustituido' }}. {{ document?.rejectionReason }}</p>
      <template v-if="document?.documentCode === 'IMSS'">
        <template v-if="document.status === 'pending'">
          <p>Verifica los datos del alumno y que la constancia muestre estado VIGENTE.</p>
          <UiField v-bind="validation.field('imssVerified', 'Confirmo que el PDF corresponde al alumno y muestra estado VIGENTE.')" label="Confirmo que el PDF corresponde al alumno y muestra estado VIGENTE." v-slot="{ controlAttrs }"><input v-bind="controlAttrs" v-model="imssVerified" type="checkbox" :disabled="loading || submitting" /></UiField>
        </template>
      </template>
      <UiField v-if="document?.status === 'pending'" v-model="reason" as="textarea" label="Motivo de rechazo (obligatorio para rechazar)" v-bind="validation.field('rejectionReason', 'Motivo de rechazo')" maxlength="1000" :disabled="submitting" />
    </div>
    <template #footer>
      <div class="review-actions">
      <UiButton variant="secondary" :disabled="submitting" @click="open = false">Cerrar</UiButton>
      <template v-if="document?.status === 'pending'">
        <UiButton variant="danger" :disabled="loading || submitting || !url" @click="submit('rejected')">Rechazar documento</UiButton>
        <UiButton :disabled="loading || submitting || !url" :loading="submitting" @click="submit('approved')">{{ 'Aprobar documento' }}</UiButton>
      </template>
      </div>
    </template>
  </UiDialog>
</template>

<style scoped>
.review-content { display: grid; gap: 1rem; }
.review-pdf { width: 100%; height: min(62dvh, 42rem); border: 1px solid var(--color-border); border-radius: 8px; }
.review-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: .75rem; width: 100%; }
@media (max-width: 600px) {
  .review-pdf { height: min(48dvh, 360px); }
  .review-actions { flex-direction: column-reverse; align-items: stretch; gap: .5rem; }
}
</style>
