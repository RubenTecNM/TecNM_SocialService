<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { UiAlert, UiBadge, UiBasePage, UiButton, UiCard, UiDialog, UiField, UiTable } from '../../../shared/components'
import { getDocumentBlob, getReviewQueue, reviewDocument } from '../api/expedientsApi'
import type { ReviewQueueItem } from '../types/expedients'

interface ReviewQueueRow extends ReviewQueueItem {
  actions?: string
}

const queue = ref<ReviewQueueRow[]>([])
const isLoading = ref(true)
const errorMessage = ref('')
const selectedItem = ref<ReviewQueueRow | null>(null)
const isDialogOpen = ref(false)
const rejectionReason = ref('')
const isSubmitting = ref(false)

const columns: { key: keyof ReviewQueueRow & string; label: string }[] = [
  { key: 'studentControlNumber', label: 'Control' },
  { key: 'studentFullName', label: 'Alumno' },
  { key: 'careerCode', label: 'Carrera' },
  { key: 'tramiteType', label: 'Trámite' },
  { key: 'documentCode', label: 'Código Doc' },
  { key: 'originalFileName', label: 'Archivo' },
  { key: 'status', label: 'Estatus' },
  { key: 'actions', label: 'Acción' },
]

async function loadQueue() {
  isLoading.value = true
  errorMessage.value = ''
  try {
    queue.value = await getReviewQueue('pending')
  } catch (err: any) {
    errorMessage.value = err.response?.data?.message || 'Error al cargar la cola de revisión documental'
  } finally {
    isLoading.value = false
  }
}

async function handleViewPdf(doc: ReviewQueueRow) {
  try {
    const blob = await getDocumentBlob(doc.id)
    const url = window.URL.createObjectURL(blob)
    window.open(url, '_blank')
  } catch {
    alert('No se pudo visualizar el archivo')
  }
}

function openReview(item: ReviewQueueRow) {
  selectedItem.value = item
  rejectionReason.value = ''
  isDialogOpen.value = true
}

async function submitReview(status: 'approved' | 'rejected') {
  if (!selectedItem.value) return
  if (status === 'rejected' && !rejectionReason.value.trim()) {
    alert('Debe especificar el motivo de rechazo')
    return
  }

  isSubmitting.value = true
  try {
    await reviewDocument(selectedItem.value.id, {
      status,
      rejectionReason: status === 'rejected' ? rejectionReason.value : undefined,
    })
    isDialogOpen.value = false
    await loadQueue()
  } catch (err: any) {
    alert(err.response?.data?.message || 'Error al procesar dictamen del documento')
  } finally {
    isSubmitting.value = false
  }
}

onMounted(() => {
  loadQueue()
})
</script>

<template>
  <UiBasePage :fluid="true">
    <div style="margin-bottom: 1.5rem;">
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Cola de Revisión Documental</h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">Validación y dictamen de evidencias, cartas y reportes firmados y sellados</p>
    </div>

    <UiAlert v-if="errorMessage" tone="danger" style="margin-bottom: 1rem;">{{ errorMessage }}</UiAlert>

    <UiCard>
      <UiTable label="Cola de Documentos" :columns="columns" :rows="queue">
        <template #cell-status="{ row }">
          <UiBadge :tone="row.status === 'approved' ? 'success' : row.status === 'rejected' ? 'danger' : 'warning'">
            {{ row.status === 'approved' ? 'Aprobado' : row.status === 'rejected' ? 'Rechazado' : 'Pendiente' }}
          </UiBadge>
        </template>
        <template #cell-actions="{ row }">
          <div style="display: flex; gap: 0.5rem;">
            <UiButton class="btn-sm" variant="secondary" @click="handleViewPdf(row)">Ver PDF</UiButton>
            <UiButton class="btn-sm" variant="primary" @click="openReview(row)">Dictaminar</UiButton>
          </div>
        </template>
      </UiTable>
    </UiCard>

    <UiDialog v-model="isDialogOpen" title="Dictaminar Documento">
      <div v-if="selectedItem" style="display: flex; flex-direction: column; gap: 1rem;">
        <p>
          Documento <strong>{{ selectedItem.documentCode }}</strong> ({{ selectedItem.originalFileName }}) presentado por
          <strong>{{ selectedItem.studentFullName }}</strong> ({{ selectedItem.studentControlNumber }}).
        </p>

        <UiField label="Motivo de rechazo (requerido si se rechaza)">
          <textarea v-model="rejectionReason" rows="3" placeholder="Ej. Falta firma del asesor externo o sello ilegible..."></textarea>
        </UiField>
      </div>

      <template #actions>
        <UiButton variant="danger" :disabled="isSubmitting" @click="submitReview('rejected')">Rechazar</UiButton>
        <UiButton variant="primary" :disabled="isSubmitting" @click="submitReview('approved')">Aprobar</UiButton>
      </template>
    </UiDialog>
  </UiBasePage>
</template>

