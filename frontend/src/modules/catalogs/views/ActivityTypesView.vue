<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { problemMessage } from '../../../shared/api/problemDetails'
import { hasRole } from '../../../shared/composables/useSession'
import {
  UiAlert,
  UiBadge,
  UiBasePage,
  UiButton,
  UiCard,
  UiDialog,
  UiField,
  UiTable,
} from '../../../shared/components'
import {
  createActivityType,
  deleteActivityType,
  getActivityTypes,
  updateActivityType,
  updateActivityTypeStatus,
} from '../api/careersApi'
import type { ActivityAppliesTo, ActivityType } from '../types/careers'

const canWrite = computed(() =>
  hasRole('department_head', 'social_service_officer', 'internship_coordinator'),
)

interface ActivityTypeRow extends ActivityType {
  actions?: string
}

const items = ref<ActivityTypeRow[]>([])
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const feedback = ref('')
const search = ref('')
const appliesFilter = ref('')

const dialogOpen = ref(false)
const editingId = ref<number | null>(null)
const form = reactive<{
  code: string
  name: string
  appliesTo: ActivityAppliesTo
  description: string
}>({
  code: '',
  name: '',
  appliesTo: 'both',
  description: '',
})

const appliesLabels: Record<ActivityAppliesTo, string> = {
  both: 'Servicio Social y Prácticas',
  social_service: 'Solo Servicio Social',
  internship: 'Solo Prácticas Profesionales',
}

const columns: { key: keyof ActivityTypeRow; label: string }[] = [
  { key: 'code', label: 'Clave' },
  { key: 'name', label: 'Tipo de actividad' },
  { key: 'appliesTo', label: 'Aplica a' },
  { key: 'isActive', label: 'Estado' },
  { key: 'actions', label: 'Acciones' },
]

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    const res = await getActivityTypes({
      page: 1,
      pageSize: 100,
      search: search.value.trim() || undefined,
      appliesTo: appliesFilter.value || undefined,
    })
    items.value = res.items
  } catch (e: any) {
    error.value = problemMessage(e, 'No se pudo cargar el catálogo de tipos de actividad.')
  } finally {
    loading.value = false
  }
}

function openCreate(): void {
  editingId.value = null
  form.code = ''
  form.name = ''
  form.appliesTo = 'both'
  form.description = ''
  dialogOpen.value = true
}

function openEdit(item: ActivityType): void {
  editingId.value = item.id
  form.code = item.code
  form.name = item.name
  form.appliesTo = item.appliesTo
  form.description = item.description ?? ''
  dialogOpen.value = true
}

async function save(): Promise<void> {
  saving.value = true
  error.value = ''
  feedback.value = ''
  try {
    const payload = {
      code: form.code.trim().toUpperCase(),
      name: form.name.trim(),
      appliesTo: form.appliesTo,
      description: form.description.trim() || null,
    }
    if (editingId.value) {
      await updateActivityType(editingId.value, payload)
      feedback.value = 'Tipo de actividad actualizado.'
    } else {
      await createActivityType(payload)
      feedback.value = 'Tipo de actividad registrado.'
    }
    dialogOpen.value = false
    await load()
  } catch (e: any) {
    error.value = problemMessage(e, 'No se pudo guardar el tipo de actividad.')
  } finally {
    saving.value = false
  }
}

async function toggleStatus(item: ActivityType): Promise<void> {
  error.value = ''
  try {
    await updateActivityTypeStatus(item.id, !item.isActive)
    await load()
  } catch (e: any) {
    error.value = problemMessage(e, 'No se pudo cambiar el estado.')
  }
}

async function remove(item: ActivityType): Promise<void> {
  if (!window.confirm(`¿Eliminar el tipo de actividad "${item.name}"?`)) return
  error.value = ''
  try {
    await deleteActivityType(item.id)
    feedback.value = 'Tipo de actividad eliminado.'
    await load()
  } catch (e: any) {
    error.value = problemMessage(e, 'No se pudo eliminar el tipo de actividad.')
  }
}

onMounted(() => void load())
</script>

<template>
  <UiBasePage
    fluid
    title="Catálogo de Tipos de Actividad"
    description="Clasificación estándar para Servicio Social y Prácticas Profesionales usada en análisis y reportes."
  >
    <template #actions>
      <UiButton v-if="canWrite" variant="primary" size="sm" @click="openCreate">
        Nuevo tipo de actividad
      </UiButton>
    </template>

    <UiAlert v-if="error" tone="danger">{{ error }}</UiAlert>
    <UiAlert v-if="feedback" tone="success">{{ feedback }}</UiAlert>

    <UiCard>
      <div class="toolbar">
        <UiField v-model="search" label="Buscar" placeholder="Clave o nombre..." @keyup.enter="load" />
        <UiField v-model="appliesFilter" label="Proceso" as="select" @change="load">
          <template #options>
            <option value="">Todos</option>
            <option value="both">Ambos procesos</option>
            <option value="social_service">Servicio Social</option>
            <option value="internship">Prácticas Profesionales</option>
          </template>
        </UiField>
        <div class="toolbar-btn">
          <UiButton variant="secondary" size="sm" :disabled="loading" @click="load">Filtrar</UiButton>
        </div>
      </div>

      <p v-if="loading" class="status-text">Cargando catálogo...</p>
      <UiTable v-else label="Tipos de actividad" :columns="columns" :rows="items">
        <template #cell-code="{ value }">
          <code>{{ value }}</code>
        </template>
        <template #cell-appliesTo="{ value }">
          <UiBadge :tone="value === 'both' ? 'info' : value === 'social_service' ? 'purple' : 'success'">
            {{ appliesLabels[value as ActivityAppliesTo] || value }}
          </UiBadge>
        </template>
        <template #cell-isActive="{ value }">
          <UiBadge :tone="value ? 'success' : 'warning'">
            {{ value ? 'Activo' : 'Inactivo' }}
          </UiBadge>
        </template>
        <template #cell-actions="{ row }">
          <div v-if="canWrite" class="row-actions">
            <UiButton variant="secondary" size="sm" @click="openEdit(row as ActivityType)">Editar</UiButton>
            <UiButton variant="secondary" size="sm" @click="toggleStatus(row as ActivityType)">
              {{ (row as ActivityType).isActive ? 'Desactivar' : 'Activar' }}
            </UiButton>
            <UiButton variant="danger" size="sm" @click="remove(row as ActivityType)">Eliminar</UiButton>
          </div>
          <span v-else class="muted">Solo lectura</span>
        </template>
      </UiTable>
    </UiCard>

    <UiDialog
      v-model="dialogOpen"
      :title="editingId ? 'Editar tipo de actividad' : 'Nuevo tipo de actividad'"
      size="md"
    >
      <div class="dialog-form">
        <UiField v-model="form.code" label="Clave *" placeholder="Ej. ACT-TI" required />
        <UiField v-model="form.name" label="Nombre *" placeholder="Ej. Desarrollo de Software y TI" required />
        <UiField v-model="form.appliesTo" label="Aplica a *" as="select">
          <template #options>
            <option value="both">Servicio Social y Prácticas</option>
            <option value="social_service">Solo Servicio Social</option>
            <option value="internship">Solo Prácticas Profesionales</option>
          </template>
        </UiField>
        <UiField v-model="form.description" label="Descripción" as="textarea" placeholder="Descripción breve (opcional)" />
      </div>
      <template #footer>
        <div class="row-actions">
          <UiButton variant="secondary" size="sm" @click="dialogOpen = false">Cancelar</UiButton>
          <UiButton variant="primary" size="sm" :disabled="saving" @click="save">
            {{ saving ? 'Guardando...' : 'Guardar' }}
          </UiButton>
        </div>
      </template>
    </UiDialog>
  </UiBasePage>
</template>

<style scoped>
.toolbar {
  display: grid;
  grid-template-columns: minmax(220px, 1fr) minmax(200px, 260px) auto;
  gap: 0.75rem;
  align-items: end;
  margin-bottom: 1rem;
}

.toolbar-btn {
  padding-bottom: 16px;
}

.row-actions {
  display: flex;
  gap: 0.4rem;
  flex-wrap: wrap;
}

.dialog-form {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.status-text,
.muted {
  color: var(--color-text-secondary);
  font-size: 0.85rem;
}

@media (max-width: 720px) {
  .toolbar {
    grid-template-columns: 1fr;
  }

  .toolbar-btn {
    padding-bottom: 0;
  }
}
</style>
