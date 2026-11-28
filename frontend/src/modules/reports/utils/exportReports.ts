import { getAnalyticsCompanies } from '../api/reportsApi'
import type {
  AnalyticsFilters,
  AnalyticsReportResponse,
  CompanyAnalyticsRow,
  Report911Response,
  StatisticalReportResponse,
  TrackingItem,
} from '../types/reports'

function safeText(value: unknown): string {
  const text = value == null ? '' : String(value)
  return /^\s*[=+@-]/.test(text) ? `'${text}` : text
}

function csvCell(value: unknown): string {
  return `"${safeText(value).replace(/"/g, '""')}"`
}

function escapeHtml(value: unknown): string {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

function downloadBlob(blob: Blob, fileName: string): void {
  const url = window.URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  window.URL.revokeObjectURL(url)
}

async function fetchAllFilteredCompanies(filters: AnalyticsFilters): Promise<CompanyAnalyticsRow[]> {
  const rows: CompanyAnalyticsRow[] = []
  let page = 1
  let totalPages = 1
  do {
    const res = await getAnalyticsCompanies(filters, { page, pageSize: 100, sort: 'students_desc' })
    rows.push(...res.items)
    totalPages = res.totalPages
    page++
  } while (page <= totalPages && page <= 20)
  return rows
}

export interface ExportFilterMeta {
  processLabel: string
  scopeLabel: string
  activeFiltersSummary: string
}

export async function exportAnalyticsExcel(
  analytics: AnalyticsReportResponse,
  statistics: StatisticalReportResponse | null,
  rep911: Report911Response | null,
  filters: AnalyticsFilters,
  meta: ExportFilterMeta,
): Promise<void> {
  const ExcelJS = await import('exceljs')
  const workbook = new ExcelJS.Workbook()
  workbook.creator = 'TecNM Campus Monclova · SSPP'
  workbook.created = new Date()

  const styleHeader = (row: any) => {
    row.font = { bold: true, color: { argb: 'FFFFFFFF' } }
    row.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF1B396A' } }
  }

  // Hoja 1: Resumen y Filtros
  const s1 = workbook.addWorksheet('Resumen_y_Filtros')
  s1.addRow(['TECNOLÓGICO NACIONAL DE MÉXICO · CAMPUS MONCLOVA'])
  s1.addRow(['Reporte Analítico de Servicio Social y Prácticas Profesionales'])
  s1.addRow([])
  s1.addRow(['Proceso', safeText(meta.processLabel)])
  s1.addRow(['Alcance temporal', safeText(meta.scopeLabel)])
  s1.addRow(['Periodos incluidos', safeText(analytics.scope.includedPeriodCodes.join(', ') || 'Todos')])
  s1.addRow(['Filtros aplicados', safeText(meta.activeFiltersSummary || 'Sin filtros adicionales')])
  s1.addRow(['Nota metodológica', 'Conteos deduplicados por estudiante en cada proceso para evitar doble conteo en ciclos escolares.'])
  s1.addRow([])
  styleHeader(s1.addRow(['Indicador', 'Valor']))
  s1.addRow(['Estudiantes únicos', analytics.summary.uniqueStudents])
  s1.addRow(['Registros de trámite', analytics.summary.totalTramites])
  s1.addRow(['En realización', analytics.summary.inProgressCount])
  s1.addRow(['Activos', analytics.summary.activeCount])
  s1.addRow(['Liberados', analytics.summary.releasedCount])
  s1.addRow(['% Liberación', `${analytics.summary.releaseRatePercentage}%`])
  s1.addRow(['Hombres', analytics.summary.byGender.male])
  s1.addRow(['Mujeres', analytics.summary.byGender.female])
  s1.addRow(['Sexo no especificado', analytics.summary.byGender.unspecified])
  s1.addRow(['Empresas receptoras distintas', analytics.summary.distinctCompanies])
  s1.addRow(['Sin empresa asignada (PP)', analytics.summary.unassignedCompanyCount])
  s1.addRow(['Sin tipo de actividad', analytics.summary.unclassifiedActivityCount])
  if (analytics.summary.comparison) {
    s1.addRow(['Comparativa vs', safeText(analytics.summary.comparison.previousLabel)])
    s1.addRow(['Estudiantes previos', analytics.summary.comparison.previousUniqueStudents])
    s1.addRow(['Variación', analytics.summary.comparison.deltaStudents])
  }
  s1.columns = [{ width: 32 }, { width: 52 }]

  // Hoja 2: Por Carrera
  const s2 = workbook.addWorksheet('Por_Carrera')
  styleHeader(s2.addRow(['Clave', 'Carrera', 'Hombres', 'Mujeres', 'No especificado', 'En realización', 'Liberados', 'PNC', 'SS', 'PP', 'Total']))
  for (const r of analytics.byCareer) {
    s2.addRow([safeText(r.careerCode), safeText(r.careerName), r.male, r.female, r.unspecified, r.inProgress, r.released, r.pnc, r.socialServiceCount, r.internshipCount, r.total])
  }
  s2.columns = [{ width: 10 }, { width: 36 }, { width: 10 }, { width: 10 }, { width: 15 }, { width: 14 }, { width: 11 }, { width: 9 }, { width: 9 }, { width: 9 }, { width: 10 }]

  // Hoja 3: Por Semestre
  const s3 = workbook.addWorksheet('Por_Semestre')
  styleHeader(s3.addRow(['Semestre', 'Hombres', 'Mujeres', 'No especificado', 'SS', 'PP', 'Total']))
  for (const r of analytics.bySemester) {
    s3.addRow([`${r.semester}°`, r.male, r.female, r.unspecified, r.socialServiceCount, r.internshipCount, r.total])
  }
  s3.columns = [{ width: 14 }, { width: 11 }, { width: 11 }, { width: 16 }, { width: 10 }, { width: 10 }, { width: 10 }]

  // Hoja 4: Por Tipo de Actividad
  const s4 = workbook.addWorksheet('Por_Tipo_Actividad')
  styleHeader(s4.addRow(['Clave', 'Tipo de Actividad', 'Estudiantes', '% Participación', 'Hombres', 'Mujeres', 'No especificado', 'SS', 'PP']))
  for (const r of analytics.byActivityType) {
    s4.addRow([safeText(r.code), safeText(r.name), r.total, `${r.sharePercentage}%`, r.male, r.female, r.unspecified, r.socialServiceCount, r.internshipCount])
  }
  s4.columns = [{ width: 14 }, { width: 42 }, { width: 12 }, { width: 15 }, { width: 10 }, { width: 10 }, { width: 15 }, { width: 9 }, { width: 9 }]

  // Hoja 5: Por Empresa (completo)
  const allCompanies = await fetchAllFilteredCompanies(filters)
  const s5 = workbook.addWorksheet('Por_Empresa')
  styleHeader(s5.addRow(['Empresa / Dependencia', 'Tipo', 'Sector', 'Actividad Principal', 'Estudiantes', 'Activos', 'Liberados', 'Proyectos', 'Hombres', 'Mujeres']))
  for (const r of allCompanies) {
    s5.addRow([
      safeText(r.companyName),
      safeText(r.companyType ?? 'Pendiente'),
      safeText(r.sector ?? '—'),
      safeText(r.topActivityName ?? 'Sin clasificar'),
      r.total,
      r.activeCount,
      r.releasedCount,
      r.projects,
      r.male,
      r.female,
    ])
  }
  s5.columns = [{ width: 38 }, { width: 14 }, { width: 14 }, { width: 32 }, { width: 12 }, { width: 10 }, { width: 10 }, { width: 10 }, { width: 10 }, { width: 10 }]

  // Hoja 6: Tendencia Histórica
  const s6 = workbook.addWorksheet('Tendencia_Historica')
  styleHeader(s6.addRow(['Periodo / Ciclo', 'Detalle', 'Total', 'SS', 'PP', 'En realización', 'Liberados']))
  for (const r of analytics.trend) {
    s6.addRow([safeText(r.label), safeText(r.subLabel), r.total, r.socialServiceCount, r.internshipCount, r.inProgressCount, r.releasedCount])
  }
  s6.columns = [{ width: 18 }, { width: 30 }, { width: 10 }, { width: 10 }, { width: 10 }, { width: 14 }, { width: 12 }]

  // Hoja 7: Estadístico Oficial (si disponible)
  if (statistics) {
    const s7 = workbook.addWorksheet('Estadistico_Oficial')
    styleHeader(s7.addRow(['Carrera', 'En Condiciones (CP)', 'Solic. H', 'Solic. M', 'Total Solic.', 'Lib. H', 'Lib. M', 'Total Lib.', 'PNC', '% Liberación']))
    for (const r of statistics.byCareer) {
      s7.addRow([
        safeText(`${r.careerCode} · ${r.careerName}`),
        r.eligibleWithPresentationLetter,
        r.applicantsMale,
        r.applicantsFemale,
        r.applicantsTotal,
        r.releasedMale,
        r.releasedFemale,
        r.releasedTotal,
        r.pncTotal,
        `${r.releaseRatePercentage}%`,
      ])
    }
    s7.addRow([
      'TOTAL INSTITUCIONAL',
      statistics.totals.eligibleWithPresentationLetter,
      statistics.totals.applicantsMale,
      statistics.totals.applicantsFemale,
      statistics.totals.applicantsTotal,
      statistics.totals.releasedMale,
      statistics.totals.releasedFemale,
      statistics.totals.releasedTotal,
      statistics.totals.pncTotal,
      `${statistics.totals.releaseRatePercentage}%`,
    ])
    s7.columns = [{ width: 38 }, { width: 18 }, { width: 10 }, { width: 10 }, { width: 12 }, { width: 10 }, { width: 10 }, { width: 12 }, { width: 9 }, { width: 14 }]
  }

  // Hoja 8: Formato 911.10 (si disponible)
  if (rep911) {
    const s8 = workbook.addWorksheet('Formato_911_10')
    styleHeader(s8.addRow(['Sector', 'Hombres', 'Mujeres', 'Total', 'Discapacidad']))
    for (const r of rep911.byCompanySector) {
      s8.addRow([safeText(r.sector), r.male, r.female, r.total, r.withDisability])
    }
    s8.addRow([])
    styleHeader(s8.addRow(['Tamaño Empresa (Sección XIII)', 'Colocados']))
    for (const sz of rep911.byCompanySize) {
      s8.addRow([safeText(sz.companySize), sz.total])
    }
    s8.columns = [{ width: 34 }, { width: 12 }, { width: 12 }, { width: 12 }, { width: 14 }]
  }

  const buffer = await workbook.xlsx.writeBuffer()
  const safeScope = meta.scopeLabel.replace(/[^A-Za-z0-9_-]+/g, '_')
  downloadBlob(
    new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }),
    `ANALISIS_VINCULACION_${filters.tramiteType.toUpperCase()}_${safeScope}.xlsx`,
  )
}

export function openAnalyticsPrintReport(
  analytics: AnalyticsReportResponse,
  chartImages: { title: string; dataUrl: string }[],
  meta: ExportFilterMeta,
): void {
  const win = window.open('', '_blank', 'width=1024,height=840')
  if (!win) {
    window.alert('Habilita las ventanas emergentes para imprimir o guardar el reporte como PDF.')
    return
  }

  const chartsHtml = chartImages
    .map(
      (c) => `
      <div class="chart-box">
        <h3>${escapeHtml(c.title)}</h3>
        <img src="${c.dataUrl}" alt="${escapeHtml(c.title)}" />
      </div>`,
    )
    .join('')

  const careerRows = analytics.byCareer
    .map(
      (r) => `<tr>
        <td>${escapeHtml(r.careerCode)} · ${escapeHtml(r.careerName)}</td>
        <td class="num">${r.male}</td>
        <td class="num">${r.female}</td>
        <td class="num">${r.inProgress}</td>
        <td class="num">${r.released}</td>
        <td class="num"><strong>${r.total}</strong></td>
      </tr>`,
    )
    .join('')

  const activityRows = analytics.byActivityType
    .map(
      (r) => `<tr>
        <td>${escapeHtml(r.name)}</td>
        <td class="num">${r.total}</td>
        <td class="num">${r.sharePercentage}%</td>
      </tr>`,
    )
    .join('')

  win.document.write(`<!DOCTYPE html>
<html lang="es">
<head>
  <meta charset="utf-8" />
  <title>Reporte Analítico TecNM · ${escapeHtml(meta.processLabel)}</title>
  <style>
    body { font-family: Inter, Arial, sans-serif; color: #1E293B; margin: 24px 32px; font-size: 12.5px; }
    .print-bar { display: flex; justify-content: space-between; align-items: center; background: #1B396A; color: #fff; padding: 10px 16px; border-radius: 6px; margin-bottom: 18px; }
    .print-btn { background: #fff; color: #1B396A; border: none; padding: 6px 14px; font-weight: 700; border-radius: 4px; cursor: pointer; }
    .header { display: flex; justify-content: space-between; border-bottom: 2px solid #1B396A; padding-bottom: 10px; margin-bottom: 14px; }
    .inst { font-size: 15px; font-weight: 800; color: #1B396A; }
    .meta { background: #F8FAFC; border: 1px solid #CBD5E1; border-radius: 6px; padding: 10px 12px; margin-bottom: 14px; }
    .kpis { display: grid; grid-template-columns: repeat(5, 1fr); gap: 8px; margin-bottom: 16px; }
    .kpi { border: 1px solid #CBD5E1; border-top: 3px solid #1B396A; border-radius: 6px; padding: 8px 10px; }
    .kpi span { display: block; font-size: 10px; text-transform: uppercase; color: #64748B; font-weight: 700; }
    .kpi strong { font-size: 18px; color: #1E293B; }
    .charts { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; margin-bottom: 16px; }
    .chart-box { border: 1px solid #E2E8F0; border-radius: 6px; padding: 10px; break-inside: avoid; }
    .chart-box h3 { margin: 0 0 8px; font-size: 12.5px; color: #1B396A; }
    .chart-box img { width: 100%; height: auto; display: block; }
    table { width: 100%; border-collapse: collapse; margin-bottom: 14px; break-inside: avoid; }
    th, td { border: 1px solid #CBD5E1; padding: 6px 8px; text-align: left; }
    th { background: #EEF2F7; font-size: 11px; text-transform: uppercase; }
    .num { text-align: center; }
    @media print { .print-bar { display: none; } body { margin: 10px; } }
  </style>
</head>
<body>
  <div class="print-bar">
    <span><strong>Reporte Ejecutivo de Vinculación</strong> · Listo para imprimir o Guardar como PDF</span>
    <button class="print-btn" onclick="window.print()">Imprimir / Guardar PDF</button>
  </div>
  <div class="header">
    <div>
      <div class="inst">TECNOLÓGICO NACIONAL DE MÉXICO · CAMPUS MONCLOVA</div>
      <div>División de Gestión Tecnológica y Vinculación · ${escapeHtml(meta.processLabel)}</div>
    </div>
    <div style="text-align:right;font-size:11px;color:#475569;">
      Alcance: <strong>${escapeHtml(meta.scopeLabel)}</strong><br/>
      Generado: ${new Date().toLocaleDateString('es-MX')}
    </div>
  </div>
  <div class="meta">
    <strong>Filtros aplicados:</strong> ${escapeHtml(meta.activeFiltersSummary || 'Todos los registros del universo seleccionado')}<br/>
    <small>Periodos incluidos: ${escapeHtml(analytics.scope.includedPeriodCodes.join(' + ') || 'Todos')} · Conteo deduplicado por estudiante.</small>
  </div>
  <div class="kpis">
    <div class="kpi"><span>Estudiantes únicos</span><strong>${analytics.summary.uniqueStudents}</strong></div>
    <div class="kpi"><span>En realización</span><strong>${analytics.summary.inProgressCount}</strong></div>
    <div class="kpi"><span>Liberados</span><strong>${analytics.summary.releasedCount} (${analytics.summary.releaseRatePercentage}%)</strong></div>
    <div class="kpi"><span>Hombres / Mujeres</span><strong>${analytics.summary.byGender.male} / ${analytics.summary.byGender.female}</strong></div>
    <div class="kpi"><span>Empresas</span><strong>${analytics.summary.distinctCompanies}</strong></div>
  </div>
  <div class="charts">${chartsHtml}</div>
  <h3>Desglose por Carrera</h3>
  <table>
    <thead><tr><th>Carrera</th><th class="num">H</th><th class="num">M</th><th class="num">En curso</th><th class="num">Liberados</th><th class="num">Total</th></tr></thead>
    <tbody>${careerRows}</tbody>
  </table>
  <h3>Desglose por Tipo de Actividad</h3>
  <table>
    <thead><tr><th>Tipo de Actividad</th><th class="num">Estudiantes</th><th class="num">%</th></tr></thead>
    <tbody>${activityRows}</tbody>
  </table>
</body>
</html>`)
  win.document.close()
}

export function exportTrackingCsvSafe(
  items: TrackingItem[],
  activeDocKeys: readonly { key: string; label: string }[],
  tramiteType: 'social_service' | 'internship',
): void {
  if (!items.length) return
  const headers = [
    'NO_CONTROL',
    'ALUMNO',
    'SEXO',
    'CARRERA',
    'SEMESTRE',
    'CICLO_ESCOLAR',
    'PERIODO',
    'TIPO_ACTIVIDAD',
    'DEPENDENCIA_EMPRESA',
    'ESTATUS',
    'FOLIO_CP',
    ...activeDocKeys.map((d) => d.label),
    'AVANCE_%',
    'FOLIO_CONSTANCIA',
    'ACTA_ESCOLARES',
  ]

  const rows = items.map((r) => {
    const docs = (r.documents || {}) as Record<string, boolean | null | undefined>
    const docVals = activeDocKeys.map((d) =>
      docs[d.key] === null ? 'SIN DICTAMEN INDIVIDUAL' : docs[d.key] ? 'SI' : 'NO',
    )
    return [
      r.controlNumber,
      r.studentFullName,
      r.gender || 'NE',
      r.careerCode,
      r.semesterEnrolled,
      r.schoolYear || '',
      r.periodCode || '',
      r.activityTypeName || 'Sin clasificar',
      r.companyName || 'Pendiente de CA',
      r.status,
      r.presentationLetterFolio || '',
      ...docVals,
      `${r.progressPercentage}%`,
      r.releaseCertificateFolio || '',
      r.schoolServicesActFolio || '',
    ]
      .map(csvCell)
      .join(',')
  })

  const csvContent = '\uFEFF' + [headers.map(csvCell).join(','), ...rows].join('\r\n')
  downloadBlob(
    new Blob([csvContent], { type: 'text/csv;charset=utf-8;' }),
    `SEGUIMIENTO_VINCULACION_${tramiteType.toUpperCase()}.csv`,
  )
}

export async function exportTrackingExcel(
  items: TrackingItem[],
  activeDocKeys: readonly { key: string; label: string }[],
  tramiteType: 'social_service' | 'internship',
): Promise<void> {
  if (!items.length) return
  const ExcelJS = await import('exceljs')
  const workbook = new ExcelJS.Workbook()
  const sheet = workbook.addWorksheet(tramiteType === 'social_service' ? 'Servicio_Social' : 'Practicas')

  const headers = [
    'No. Control',
    'Alumno',
    'Sexo',
    'Carrera',
    'Semestre',
    'Ciclo Escolar',
    'Periodo',
    'Tipo de Actividad',
    'Dependencia / Empresa',
    'Estatus',
    'Folio C.P.',
    ...activeDocKeys.map((d) => d.label),
    'Avance %',
    'Const. Term.',
    'Acta Escolares',
  ]
  const hRow = sheet.addRow(headers)
  hRow.font = { bold: true, color: { argb: 'FFFFFFFF' } }
  hRow.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF1B396A' } }

  for (const r of items) {
    const docs = (r.documents || {}) as Record<string, boolean | null | undefined>
    const docVals = activeDocKeys.map((d) =>
      docs[d.key] === null ? 'SIN DICTAMEN INDIVIDUAL' : docs[d.key] ? 'SI' : 'NO',
    )
    sheet.addRow([
      safeText(r.controlNumber),
      safeText(r.studentFullName),
      safeText(r.gender || 'NE'),
      safeText(r.careerCode),
      r.semesterEnrolled,
      safeText(r.schoolYear || ''),
      safeText(r.periodCode || ''),
      safeText(r.activityTypeName || 'Sin clasificar'),
      safeText(r.companyName || 'Pendiente de CA'),
      safeText(r.status),
      safeText(r.presentationLetterFolio || ''),
      ...docVals,
      `${r.progressPercentage}%`,
      safeText(r.releaseCertificateFolio || ''),
      safeText(r.schoolServicesActFolio || ''),
    ])
  }

  const buffer = await workbook.xlsx.writeBuffer()
  downloadBlob(
    new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }),
    `SEGUIMIENTO_VINCULACION_${tramiteType.toUpperCase()}.xlsx`,
  )
}
