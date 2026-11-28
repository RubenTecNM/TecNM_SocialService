<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { UiAlert, UiButton } from '../../../shared/components'
import { http } from '../../../shared/api/http'
import TemplateCanvas from './TemplateCanvas.vue'
import type { LogoPlacement, Placement, TemplateVersion } from '../types/templateManagement'

const props = defineProps<{ draft: TemplateVersion; source: Blob; busy: boolean }>()
const emit = defineEmits<{ changed: []; save: [LogoPlacement[]] }>()
const placements = ref(props.draft.logos.map(item => ({ ...item })))
const library = ref<{ id: number; name: string }[]>([])
const images = ref<Record<number, string>>({})
const selected = ref(0)
const page = ref(0)
const error = ref('')
const dirty = ref(false)
let disposed = false
const zones = computed(() => props.draft.layout.logoZones ?? [])
const logo = computed(() => placements.value[selected.value])
const boxes = computed<Placement[]>(() => placements.value.map(item => ({
  ...item, page: zones.value[item.slot]?.page ?? 0, alignment: 'center',
  fontSize: null, minFontSize: null, rows: null,
})))
const imageSources = computed(() => placements.value.map(item => images.value[item.logoId]))
function change() { dirty.value = true; emit('changed') }
function select(index: number) {
  selected.value = index
  page.value = boxes.value[index]?.page ?? page.value
}
function setPlacement(index: number, value: Placement) {
  const item = placements.value[index]
  if (!item || props.busy) return
  Object.assign(item, { x: value.x, y: value.y, width: value.width, height: value.height })
  change()
}
watch(() => placements.value.map(item => item.logoId), async ids => {
  try {
    await Promise.all([...new Set(ids)].filter(id => !images.value[id]).map(async id => {
      const { data } = await http.get<Blob>(`/template-management/logos/${id}/image`, { responseType: 'blob' })
      if (!disposed && !images.value[id]) images.value[id] = URL.createObjectURL(data)
    }))
  } catch { if (!disposed) error.value = 'No se pudo abrir un logotipo.' }
}, { immediate: true })
onMounted(async () => {
  try { library.value = (await http.get('/template-management/logos')).data }
  catch { error.value = 'No se pudo cargar la biblioteca.' }
})
onBeforeUnmount(() => { disposed = true; Object.values(images.value).forEach(URL.revokeObjectURL) })
</script>

<template>
  <div class="logo-editor">
    <UiAlert v-if="error" tone="danger">{{ error }}</UiAlert>
    <TemplateCanvas v-model:page="page" :blob="source" :page-sizes="draft.layout.pageSizes" :fields="boxes"
      :selected="selected" :image-sources="imageSources" :bounds="zones" @select="select" @placement="setPlacement" />
    <fieldset class="logo-controls" :disabled="busy">
      <legend>Logotipos del formato</legend>
      <label>Zona
        <select :value="selected" @change="select(Number(($event.target as HTMLSelectElement).value))">
          <option v-for="(zone, index) in zones" :key="index" :value="index">{{ zone.name }} · página {{ zone.page + 1 }}</option>
        </select>
      </label>
      <template v-if="logo">
        <label>Revisión del logotipo
          <select v-model.number="logo.logoId" @change="change">
            <option v-for="item in library" :key="item.id" :value="item.id">{{ item.name }} · revisión {{ item.id }}</option>
          </select>
        </label>
        <div class="logo-coordinates">
          <label>X (pt) <input v-model.number="logo.x" type="number" step=".1" @input="change" /></label>
          <label>Y (pt) <input v-model.number="logo.y" type="number" step=".1" @input="change" /></label>
          <label>Ancho (pt) <input v-model.number="logo.width" type="number" step=".1" @input="change" /></label>
          <label>Alto (pt) <input v-model.number="logo.height" type="number" step=".1" @input="change" /></label>
        </div>
      </template>
      <UiButton size="sm" :disabled="!dirty" @click="emit('save', placements)">Guardar logotipos</UiButton>
    </fieldset>
  </div>
</template>

<style scoped>
.logo-editor { display: grid; grid-template-columns: minmax(0, 1fr) minmax(16rem, 21rem); gap: 1rem; align-items: start; }
.logo-controls { display: grid; gap: .75rem; min-width: 0; margin: 0; border: 1px solid var(--color-border-medium); padding: .85rem; }
.logo-controls label { display: grid; gap: .25rem; }
.logo-controls input, .logo-controls select { width: 100%; min-width: 0; }
.logo-coordinates { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .5rem; }
@media (max-width: 860px) { .logo-editor { grid-template-columns: minmax(0, 1fr); } }
</style>
