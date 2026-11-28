<script setup lang="ts">
import { computed } from 'vue'
import type { Report911Response } from '../types/reports'

const props = defineProps<{
  rep911: Report911Response
  tramiteType: 'social_service' | 'internship'
}>()

const sectorLabels: Record<string, string> = {
  educational: 'Educativo (Propia Institución TecNM)',
  public: 'Sector Público',
  private: 'Sector Privado',
  social: 'Sector Social',
}

const sizeLabels: Record<string, string> = {
  micro: 'Micro',
  small: 'Pequeña',
  medium: 'Mediana',
  large: 'Grande',
  not_applicable: 'No aplica (Público / Educativo / Social)',
}

const shiftLabels: Record<string, string> = {
  morning: 'Matutino',
  afternoon: 'Vespertino',
  night: 'Nocturno',
}

const nonZeroCareerSector = computed(() =>
  props.rep911.byCareerAndGender.filter((r) => r.total > 0),
)
</script>

<template>
  <div class="grid-911">
    <!-- Sección XII: Por Sector -->
    <section class="panel-911">
      <div class="panel-head">
        <h2 class="panel-title">Sección XII — Liberados por Sector</h2>
        <span class="panel-sub">Desglose oficial SEP / TecNM por sexo, sector y discapacidad</span>
      </div>
      <div class="panel-table-wrap">
        <table class="stats-table">
          <thead>
            <tr>
              <th>Sector</th>
              <th class="th-center">Hombres</th>
              <th class="th-center">Mujeres</th>
              <th class="th-center">Total</th>
              <th class="th-center">Discapacidad</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="sec in rep911.byCompanySector" :key="sec.sector" class="stats-tr">
              <td class="sector-name">{{ sectorLabels[sec.sector] || sec.sector }}</td>
              <td class="td-center num">{{ sec.male }}</td>
              <td class="td-center num">{{ sec.female }}</td>
              <td class="td-center num strong">{{ sec.total }}</td>
              <td class="td-center num">{{ sec.withDisability }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- Impacto, Inclusión y Turnos -->
    <section class="panel-911">
      <div class="panel-head">
        <h2 class="panel-title">Indicadores de Impacto, Inclusión y Turno (911.10)</h2>
        <span class="panel-sub">Cobertura comunitaria, atención inclusiva y turnos</span>
      </div>
      <div class="impact-list">
        <div class="impact-row">
          <span class="impact-label">Estudiantes con Discapacidad (H / M / Total)</span>
          <strong class="impact-value">
            {{ rep911.withDisability.male }} / {{ rep911.withDisability.female }} / {{ rep911.withDisability.total }}
          </strong>
        </div>
        <div v-if="tramiteType === 'social_service'" class="impact-row">
          <span class="impact-label">Trámites con Actividades de Inclusión</span>
          <strong class="impact-value">{{ rep911.withInclusionActivities }}</strong>
        </div>
        <div v-if="tramiteType === 'social_service'" class="impact-row impact-row--highlight">
          <span class="impact-label">Total de Ciudadanos Beneficiados</span>
          <strong class="impact-value">{{ rep911.totalBeneficiaries }} personas</strong>
        </div>
        <div v-for="sh in rep911.byShift" :key="sh.shift" class="impact-row">
          <span class="impact-label">Turno {{ shiftLabels[sh.shift] || sh.shift }}</span>
          <strong class="impact-value">{{ sh.total }}</strong>
        </div>
      </div>
    </section>

    <!-- Sección XIII: Tamaño de Empresa (Colocados CA = SI) -->
    <section class="panel-911">
      <div class="panel-head">
        <h2 class="panel-title">Sección XIII — Estudiantes Colocados por Tamaño de Empresa</h2>
        <span class="panel-sub">Universo de estudiantes con Carta de Aceptación (CA = SI)</span>
      </div>
      <div class="panel-table-wrap">
        <table class="stats-table">
          <thead>
            <tr>
              <th>Tamaño de Empresa</th>
              <th class="th-center">Colocados</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="rep911.byCompanySize.length === 0">
              <td colspan="2" class="td-center empty-msg">Sin registros colocados en este alcance.</td>
            </tr>
            <tr v-for="sz in rep911.byCompanySize" :key="sz.companySize" class="stats-tr">
              <td class="sector-name">{{ sizeLabels[sz.companySize] || sz.companySize }}</td>
              <td class="td-center num strong">{{ sz.total }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- Matriz Carrera × Sector -->
    <section class="panel-911">
      <div class="panel-head">
        <h2 class="panel-title">Sección XII — Matriz Carrera × Sector</h2>
        <span class="panel-sub">Combinaciones con registros liberados en el periodo o ciclo</span>
      </div>
      <div class="panel-table-wrap">
        <table class="stats-table">
          <thead>
            <tr>
              <th>Carrera</th>
              <th>Sector</th>
              <th class="th-center">H</th>
              <th class="th-center">M</th>
              <th class="th-center">NE</th>
              <th class="th-center">Total</th>
              <th class="th-center">Disc.</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="nonZeroCareerSector.length === 0">
              <td colspan="7" class="td-center empty-msg">Sin registros liberados en este alcance.</td>
            </tr>
            <tr
              v-for="row in nonZeroCareerSector"
              :key="`${row.careerCode}-${row.sector}`"
              class="stats-tr"
            >
              <td class="strong">{{ row.careerCode }}</td>
              <td>{{ sectorLabels[row.sector] || row.sector }}</td>
              <td class="td-center num">{{ row.male }}</td>
              <td class="td-center num">{{ row.female }}</td>
              <td class="td-center num">{{ row.unspecified }}</td>
              <td class="td-center num strong">{{ row.total }}</td>
              <td class="td-center num">{{ row.withDisability }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>
  </div>
</template>

<style scoped>
.grid-911 {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(380px, 1fr));
  gap: 0.9rem;
}

.panel-911 {
  display: flex;
  flex-direction: column;
  padding: 0.9rem 1rem;
  border-radius: 10px;
  border: 1px solid var(--color-border);
  background: var(--surface-toolbar);
}

.panel-head {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  padding-bottom: 0.55rem;
  margin-bottom: 0.65rem;
  border-bottom: 1px solid var(--color-border-light);
}

.panel-title {
  margin: 0;
  font-size: 0.9rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.panel-sub {
  font-size: 0.74rem;
  color: var(--color-text-secondary);
}

.panel-table-wrap {
  border-radius: 7px;
  overflow: auto;
  border: 1px solid var(--color-border);
  background: var(--color-surface-white);
}

.stats-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.8rem;
}

.stats-table th {
  padding: 7px 9px;
  font-size: 0.67rem;
  font-weight: 700;
  text-transform: uppercase;
  background: var(--surface-table-head);
  color: var(--color-text-secondary);
  border-bottom: 1px solid var(--color-border-medium);
  text-align: left;
}

.stats-table td {
  padding: 7px 9px;
  border-bottom: 1px solid var(--color-border-light);
  color: var(--color-text-primary);
}

.th-center,
.td-center {
  text-align: center !important;
}

.num {
  font-variant-numeric: tabular-nums;
}

.strong {
  font-weight: 700;
}

.sector-name {
  font-weight: 600;
}

.impact-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.impact-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  padding: 0.65rem 0.85rem;
  border-radius: 8px;
  background: var(--color-surface-white);
  border: 1px solid var(--color-border);
}

.impact-row--highlight {
  border-color: var(--color-accent);
}

.impact-label {
  font-size: 0.82rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.impact-value {
  font-size: 0.94rem;
  font-weight: 800;
  color: var(--color-text-primary);
  font-variant-numeric: tabular-nums;
}

.empty-msg {
  color: var(--color-text-secondary);
  padding: 1.2rem !important;
}

@media (max-width: 640px) {
  .grid-911 {
    grid-template-columns: 1fr;
  }
}
</style>
