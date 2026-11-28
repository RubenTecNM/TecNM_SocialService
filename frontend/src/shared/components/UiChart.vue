<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import {
  BarController,
  BarElement,
  CategoryScale,
  Chart,
  Filler,
  Legend,
  LinearScale,
  LineController,
  LineElement,
  PointElement,
  Tooltip,
  type ChartConfiguration,
  type ChartData,
  type ChartOptions,
} from 'chart.js'
import { useTheme } from '../composables/useTheme'

Chart.register(
  BarController,
  LineController,
  CategoryScale,
  LinearScale,
  BarElement,
  LineElement,
  PointElement,
  Tooltip,
  Legend,
  Filler,
)

export interface UiChartTableRow {
  label: string
  values: (string | number)[]
}

const props = withDefaults(
  defineProps<{
    type: 'bar' | 'line'
    title: string
    subtitle?: string
    data: ChartData<'bar' | 'line'>
    options?: ChartOptions<'bar' | 'line'>
    height?: number
    emptyText?: string
    tableColumns?: string[]
    tableRows?: UiChartTableRow[]
    interactive?: boolean
  }>(),
  {
    subtitle: '',
    height: 260,
    emptyText: 'Sin datos para los filtros seleccionados.',
    tableColumns: () => [],
    tableRows: () => [],
    interactive: true,
  },
)

const emit = defineEmits<{
  (e: 'select', payload: { datasetIndex: number; index: number; label: string }): void
}>()

const { dark } = useTheme()
const canvasRef = ref<HTMLCanvasElement | null>(null)
const showTable = ref(false)
let chartInstance: Chart | null = null

const hasData = computed(() => {
  const labels = props.data?.labels ?? []
  const datasets = props.data?.datasets ?? []
  if (!labels.length || !datasets.length) return false
  return datasets.some((ds) =>
    Array.isArray(ds.data) && ds.data.some((v) => typeof v === 'number' && v > 0),
  )
})

function readCssToken(name: string, fallback: string): string {
  if (typeof window === 'undefined') return fallback
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim()
  return value || fallback
}

function buildOptions(): ChartOptions<'bar' | 'line'> {
  const textPrimary = readCssToken('--color-text-primary', '#1E293B')
  const textSecondary = readCssToken('--color-text-secondary', '#64748B')
  const gridColor = readCssToken('--color-border-light', '#E2E8F0')
  const tooltipBg = readCssToken('--floating-bg', '#FFFFFF')
  const tooltipBorder = readCssToken('--floating-border', '#CBD5E1')

  const prefersReduced =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches

  const userOpts = props.options ?? {}
  const userScales = (userOpts.scales ?? {}) as Record<string, any>

  return {
    responsive: true,
    maintainAspectRatio: false,
    animation: prefersReduced ? false : { duration: 260 },
    onClick: (_event, elements) => {
      if (!props.interactive || !elements.length) return
      const first = elements[0]
      if (!first) return
      const label = String(props.data.labels?.[first.index] ?? '')
      emit('select', { datasetIndex: first.datasetIndex, index: first.index, label })
    },
    onHover: (event, elements) => {
      const target = event.native?.target as HTMLElement | null
      if (target) {
        target.style.cursor = props.interactive && elements.length > 0 ? 'pointer' : 'default'
      }
    },
    ...userOpts,
    plugins: {
      legend: {
        display: (props.data.datasets?.length ?? 0) > 1,
        position: 'top',
        align: 'end',
        labels: {
          color: textSecondary,
          usePointStyle: true,
          pointStyle: 'rectRounded',
          boxWidth: 10,
          boxHeight: 10,
          font: { size: 11, weight: 600 },
        },
        ...(userOpts.plugins?.legend ?? {}),
      },
      tooltip: {
        backgroundColor: tooltipBg,
        titleColor: textPrimary,
        bodyColor: textSecondary,
        borderColor: tooltipBorder,
        borderWidth: 1,
        padding: 10,
        cornerRadius: 8,
        displayColors: true,
        ...(userOpts.plugins?.tooltip ?? {}),
      },
    },
    scales: {
      x: {
        grid: { color: gridColor },
        ticks: { color: textSecondary, font: { size: 11 } },
        ...(userScales.x ?? {}),
      },
      y: {
        beginAtZero: true,
        grid: { color: gridColor },
        ticks: { color: textSecondary, precision: 0, font: { size: 11 } },
        ...(userScales.y ?? {}),
      },
    },
  }
}

function renderChart(): void {
  if (chartInstance) {
    chartInstance.destroy()
    chartInstance = null
  }
  if (showTable.value || !hasData.value || !canvasRef.value) return

  const config: ChartConfiguration<'bar' | 'line'> = {
    type: props.type,
    data: props.data,
    options: buildOptions(),
  }
  chartInstance = new Chart(canvasRef.value, config)
}

function toDataUrl(): string | null {
  if (!chartInstance) return null
  return chartInstance.toBase64Image('image/png', 1)
}

defineExpose({ toDataUrl })

onMounted(() => renderChart())
onBeforeUnmount(() => {
  chartInstance?.destroy()
  chartInstance = null
})

watch([() => props.data, () => props.type, () => props.options, dark, showTable], () => {
  requestAnimationFrame(() => renderChart())
}, { deep: true })
</script>

<template>
  <section class="ui-chart-card" :aria-label="title">
    <header class="ui-chart-head">
      <div class="ui-chart-head__titles">
        <h3 class="ui-chart-title">{{ title }}</h3>
        <p v-if="subtitle" class="ui-chart-sub">{{ subtitle }}</p>
      </div>
      <div class="ui-chart-head__actions">
        <slot name="actions" />
        <button
          v-if="tableRows.length > 0"
          type="button"
          class="chart-mode-btn"
          :aria-pressed="showTable"
          @click="showTable = !showTable"
        >
          {{ showTable ? 'Gráfica' : 'Tabla' }}
        </button>
      </div>
    </header>

    <div v-if="!hasData" class="ui-chart-empty" role="status">
      {{ emptyText }}
    </div>

    <div v-else-if="showTable && tableRows.length > 0" class="ui-chart-table-wrap">
      <table class="ui-chart-table">
        <thead>
          <tr>
            <th v-for="col in tableColumns" :key="col" scope="col">{{ col }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(row, idx) in tableRows" :key="idx">
            <td class="label-col">{{ row.label }}</td>
            <td v-for="(val, vIdx) in row.values" :key="vIdx" class="num-col">{{ val }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <div v-else class="ui-chart-canvas-wrap" :style="{ height: `${height}px` }">
      <canvas ref="canvasRef" role="img" :aria-label="title" />
    </div>
  </section>
</template>

<style scoped>
.ui-chart-card {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
  padding: 0.95rem 1.05rem;
  border-radius: 10px;
  border: 1px solid var(--color-border);
  background: var(--color-surface-white);
  min-width: 0;
}

.ui-chart-head {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.ui-chart-head__titles {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  min-width: 0;
}

.ui-chart-title {
  margin: 0;
  font-size: 0.92rem;
  font-weight: 700;
  color: var(--color-text-primary);
  line-height: 1.3;
}

.ui-chart-sub {
  margin: 0;
  font-size: 0.75rem;
  color: var(--color-text-secondary);
}

.ui-chart-head__actions {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  flex-wrap: wrap;
}

.chart-mode-btn {
  min-height: 28px;
  padding: 0 0.6rem;
  border-radius: 6px;
  border: 1px solid var(--color-border-medium);
  background: var(--surface-toolbar);
  color: var(--color-text-secondary);
  font-size: 0.72rem;
  font-weight: 600;
  cursor: pointer;
}

.chart-mode-btn:hover,
.chart-mode-btn[aria-pressed='true'] {
  color: var(--color-text-primary);
  border-color: var(--color-accent);
  background: var(--color-surface-white);
}

.ui-chart-canvas-wrap {
  position: relative;
  width: 100%;
  min-width: 0;
}

.ui-chart-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 160px;
  padding: 1rem;
  border-radius: 8px;
  background: var(--surface-toolbar);
  color: var(--color-text-secondary);
  font-size: 0.82rem;
  text-align: center;
}

.ui-chart-table-wrap {
  max-height: 280px;
  overflow: auto;
  border: 1px solid var(--color-border);
  border-radius: 7px;
}

.ui-chart-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.78rem;
}

.ui-chart-table th {
  position: sticky;
  top: 0;
  padding: 6px 10px;
  text-align: left;
  font-size: 0.68rem;
  font-weight: 700;
  text-transform: uppercase;
  background: var(--surface-table-head);
  color: var(--color-text-secondary);
  border-bottom: 1px solid var(--color-border-medium);
}

.ui-chart-table td {
  padding: 6px 10px;
  border-bottom: 1px solid var(--color-border-light);
  color: var(--color-text-primary);
}

.label-col {
  font-weight: 600;
}

.num-col {
  font-variant-numeric: tabular-nums;
}
</style>
