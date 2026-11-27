<script setup lang="ts">
import { onBeforeUnmount, ref, shallowRef, watch } from 'vue'
import type { PDFDocumentProxy, RenderTask } from 'pdfjs-dist'
import { UiAlert, UiButton } from '../../../shared/components'
import type { Placement } from '../types/templateManagement'

const props = defineProps<{
  blob: Blob
  pageSizes: { width: number; height: number }[]
  fields: (Placement | null)[]
  selected: number
}>()
const page = defineModel<number>('page', { required: true })
const emit = defineEmits<{ select: [number]; placement: [number, Placement] }>()
const canvas = ref<HTMLCanvasElement>()
const document = shallowRef<PDFDocumentProxy>()
const zoom = ref(1)
const error = ref('')
const ready = ref(false)
let renderTask: RenderTask | undefined
let rendering = Promise.resolve()
let loaded = 0
let drag: { x: number; y: number; field: Placement; index: number; resize: boolean } | undefined

function render() {
  const instance = document.value
  const target = canvas.value
  const pageNumber = page.value
  const scale = zoom.value
  ready.value = false
  renderTask?.cancel()
  rendering = rendering.catch(() => {}).then(async () => {
    if (!instance || !target || instance !== document.value || pageNumber !== page.value || scale !== zoom.value) return
    const pdfPage = await instance.getPage(pageNumber + 1)
    if (instance !== document.value || pageNumber !== page.value || scale !== zoom.value) return
    const viewport = pdfPage.getViewport({ scale })
    const ratio = Math.min(window.devicePixelRatio || 1, Math.sqrt(8_000_000 / (viewport.width * viewport.height)))
    target.width = Math.max(1, Math.round(viewport.width * ratio))
    target.height = Math.max(1, Math.round(viewport.height * ratio))
    renderTask = pdfPage.render({ canvas: target, viewport, transform: [ratio, 0, 0, ratio, 0, 0], annotationMode: 0 })
    await renderTask.promise
    if (instance === document.value && pageNumber === page.value && scale === zoom.value) ready.value = true
  }).catch((cause: unknown) => {
    if (cause instanceof Error && cause.name !== 'RenderingCancelledException') error.value = 'No se pudo dibujar la página.'
  })
}

watch(() => props.blob, async (blob) => {
  const request = ++loaded
  const previous = document.value
  document.value = undefined
  error.value = ''
  renderTask?.cancel()
  try {
    await previous?.destroy()
    const [pdf, worker, data] = await Promise.all([
      import('pdfjs-dist'), import('pdfjs-dist/build/pdf.worker.min.mjs?url'), blob.arrayBuffer(),
    ])
    if (request !== loaded) return
    pdf.GlobalWorkerOptions.workerSrc = worker.default
    const result = await pdf.getDocument({ data, isEvalSupported: false, enableXfa: false }).promise
    if (request !== loaded) await result.destroy()
    else document.value = result
  } catch {
    if (request === loaded) error.value = 'No se pudo abrir el PDF en el editor.'
  }
}, { immediate: true })
watch([document, canvas, page, zoom], render)
onBeforeUnmount(() => { loaded++; renderTask?.cancel(); void document.value?.destroy() })

function start(event: PointerEvent, index: number, field: Placement, resize: boolean) {
  if (event.button !== 0) return
  emit('select', index)
  drag = { x: event.clientX, y: event.clientY, field, index, resize }
  ;(event.currentTarget as HTMLElement).setPointerCapture(event.pointerId)
}
function move(event: PointerEvent) {
  const size = props.pageSizes[page.value]
  if (!drag || !size) return
  const { field, index, resize } = drag
  const dx = (event.clientX - drag.x) / zoom.value
  const dy = (event.clientY - drag.y) / zoom.value
  const clamp = (value: number, min: number, max: number) => Math.round(Math.max(min, Math.min(max, value)) * 100) / 100
  const x = resize ? field.x : clamp(field.x + dx, 0, size.width - field.width)
  const y = resize ? field.y : clamp(field.y + dy, 0, size.height - field.height)
  const width = resize ? clamp(field.width + dx, 5, size.width - x) : field.width
  const height = resize ? clamp(field.height + dy, 5, size.height - y) : field.height
  const rows = field.rows?.map(row => ({
    x: x + (row.x - field.x) * width / field.width,
    width: row.width * width / field.width,
    baseline: y + (row.baseline - field.y) * height / field.height,
  })) ?? null
  emit('placement', index, { ...field, x, y, width, height, rows })
}
</script>

<template>
  <section class="paper-pane" aria-label="Página de la plantilla">
    <div class="toolbar">
      <label>Página
        <select v-model.number="page">
          <option v-for="(_, i) in pageSizes" :key="i" :value="i">{{ i + 1 }}</option>
        </select>
      </label>
      <UiButton variant="secondary" size="sm" aria-label="Alejar" :disabled="zoom <= .5" @click="zoom -= .25">−</UiButton>
      <span aria-live="polite">{{ Math.round(zoom * 100) }} %</span>
      <UiButton variant="secondary" size="sm" aria-label="Acercar" :disabled="zoom >= 2" @click="zoom += .25">+</UiButton>
    </div>
    <UiAlert v-if="error" tone="danger">{{ error }}</UiAlert>
    <div v-else class="paper-scroll" :aria-busy="!ready">
      <p v-if="!ready" role="status" class="loading-page">Cargando página…</p>
      <div v-if="pageSizes[page]" class="paper" :style="{ width: pageSizes[page]!.width * zoom + 'px', height: pageSizes[page]!.height * zoom + 'px' }">
        <canvas ref="canvas" aria-hidden="true" />
        <template v-for="(field, i) in fields" :key="i">
          <div v-if="ready && field && field.page === page" class="field-box" :class="{ selected: selected === i }"
            :style="{ left: field.x * zoom + 'px', top: field.y * zoom + 'px', width: field.width * zoom + 'px', height: field.height * zoom + 'px' }">
            <button type="button" class="field-move" :aria-label="'Seleccionar campo ' + (i + 1)" @click="emit('select', i)"
              @pointerdown="start($event, i, field, false)" @pointermove="move" @pointerup="drag = undefined" @lostpointercapture="drag = undefined">{{ i + 1 }}</button>
            <span v-for="(row, index) in field.rows ?? []" :key="index" class="baseline"
              :style="{ left: (row.x - field.x) * zoom + 'px', top: (row.baseline - field.y) * zoom + 'px', width: row.width * zoom + 'px' }" />
            <button v-if="selected === i" type="button" class="field-resize" aria-label="Redimensionar. También disponible en los controles numéricos"
              @pointerdown="start($event, i, field, true)" @pointermove="move" @pointerup="drag = undefined" @lostpointercapture="drag = undefined" />
          </div>
        </template>
      </div>
    </div>
  </section>
</template>

<style scoped>
.paper-pane { min-width: 0; border: 1px solid var(--color-border-medium); }
.toolbar { display: flex; align-items: center; flex-wrap: wrap; gap: .5rem; padding: .65rem; }
.paper-scroll { overflow: auto; max-height: 70vh; padding: 1rem; background: var(--color-border-medium); }
.paper { position: relative; margin-inline: auto; background: white; color: #10384a; }
.loading-page { margin: 0 0 .5rem; }
canvas { display: block; width: 100%; height: 100%; }
.field-box { position: absolute; outline: 1px solid #18738b; background: #4bcced28; }
.field-box.selected { z-index: 1; outline: 2px solid #00536a; }
.field-move { position: absolute; inset: 0; width: 100%; border: 0; text-align: left; padding: 0 2px; background: transparent; color: #10384a; font-size: .65rem; cursor: move; touch-action: none; }
.field-resize { position: absolute; right: -4px; bottom: -4px; width: 14px; height: 14px; padding: 0; border: 2px solid #fff; background: #00536a; cursor: nwse-resize; touch-action: none; }
.baseline { position: absolute; border-top: 1px dashed #00536a; pointer-events: none; }
</style>
