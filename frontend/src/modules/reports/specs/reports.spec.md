# Especificación Frontend: Módulo de Reportes & Estadísticos

> **Contrato Backend:** [`docs/specs/reports.spec.md`](../../../../../docs/specs/reports.spec.md)  
> **Roles:** `admin`, `social_service_officer`, `internship_coordinator`, `department_head` (acceso completo a consulta y exportación).

---

## 1. Vistas Requeridas

| Ruta | Nombre | Componente Principal | Propósito |
| :--- | :--- | :--- | :--- |
| `/reports/dashboard` | `reports-dashboard` | `ExecutiveDashboardView.vue` | Tablero ejecutivo con indicadores clave, tarjetas y accesos rápidos. |
| `/reports/tracking` | `reports-tracking` | `VinculacionTrackingView.vue` | Sábana de seguimiento en vivo con avance 0–100% y matriz de documentos. |
| `/reports/statistics` | `reports-statistics` | `StatisticalReportView.vue` | Estadístico semestral y por ciclo escolar con fórmulas de `SEGUIMIENTO VINCULACIÓN1.xlsx`. |
| `/reports/911` | `reports-911` | `Report911View.vue` | Formato oficial SEP/TecNM 911.10 (Secciones XII y XIII). |

---

## 2. Requerimientos de UI y Componentes

### 2.1 Vista `/reports/dashboard` (Tablero Ejecutivo)
- **Contenedor:** `UiBasePage` (`fluid=true`, título: "Tablero de Gestión y Vinculación").
- **Selector Superior:** Periodo Académico actual (por defecto el activo).
- **Tarjetas de Métricas (`UiMetric` / `UiCard`):**
  - **Servicio Social:** Distribución por estado (Solicitados, Activos, Concluidos, Liberados).
  - **Prácticas Profesionales:** Distribución por estado.
  - **Revisión Pendiente:** Contador de documentos en cola esperando validación de Vinculación.
  - **Convenios por Vencer:** Alerta si existen convenios con empresas a vencer en menos de 60 días.
  - **Calidad (PNC):** Incidencias no conformes abiertas.
- **Gráficas (Chart.js / Vue-Chartjs):**
  - Gráfica de dona o barras de avance de trámites por carrera.

---

### 2.2 Vista `/reports/tracking` (Sábana de Seguimiento de Vinculación)
Reemplaza las hojas `SERVICIO SOCIAL`, `PRÁCTICAS` y `PRINCIPAL CONSULTA` de `SEGUIMIENTO VINCULACIÓN1.xlsx`:
- **Barra Superior de Herramientas:**
  - Selector de Trámite: Pestaña "Servicio Social" / Pestaña "Prácticas Profesionales".
  - Selector de Periodo Académico.
  - Selector de Carrera y Estado.
  - Buscador rápido con debounce por matrícula o nombre.
  - Botón principal: **"Exportar a Excel (.xlsx)"** (usa SheetJS co-localizado en frontend).
- **Tabla Dinámica de Seguimiento (`UiTable` con scroll horizontal):**
  - Columnas del Estudiante: No., Matrícula, Nombre Completo, Carrera, Semestre, Sexo, Turno, Teléfono.
  - Columnas de la Empresa: Dependencia / Empresa, Sector, Titular / Responsable, Convenio.
  - **Columna de Avance General:** Barra de progreso visual con porcentaje numérico (`progressPercentage%`, de 0% a 100%).
  - **Matriz de Documentos (Pills verdes "SI" / grises "—"):**
    - En Servicio Social (10 columnas): `C.P.`, `C.A.`, `SOL.`, `C.C.`, `REP. 1`, `REP. 2`, `REP. 3`, `EV1..3`, `CTA. TERM.`, `CONST.TERM`.
    - En Prácticas Profesionales (5 columnas): `C.P.`, `C.A.`, `EV.`, `C.T.`, `CC`.
  - Columnas de Folios y Cierre: Folio CP, Folio Liberación, Fecha Término, Calificación, PNC (`SI`/`NO`), Acta Enviada.

---

### 2.3 Vista `/reports/statistics` (Estadístico Semestral y por Ciclo Escolar)
Reemplaza las hojas `ESTADÍSTICO` y `PNC` de `SEGUIMIENTO VINCULACIÓN1.xlsx`:
- **Selector de Comparativa:**
  - Selector de Semestre Principal (ej. `FJ26`).
  - Checkbox "Consolidar con semestre anterior para Ciclo Escolar" + selector de semestre previo (ej. `AD25`).
- **Tabla de Resultados por Carrera:**
  - Columnas oficiales:
    - **Carrera:** Clave (`MEC, IND, ELE, INF, GE, ER`) y Nombre Oficial.
    - **En condiciones (`CP = SI`):** Total de alumnos con Carta de Presentación.
    - **Solicitantes (`CA = SI`):** Desglose Hombres (`M`), Mujeres (`F`) y Total.
    - **Liberados:** Desglose Hombres (`M`), Mujeres (`F`) y Total.
    - **PNC (`Solicitantes - Liberados`):** Diferencia calculada automáticamente.
    - **% Eficiencia / Liberación:** `(Liberados / Solicitantes) * 100`.
  - Fila de **Totales Generales Institucionales**.
- **Tarjeta de Resumen de Ciclo Escolar:** Si se seleccionó comparar periodos, muestra los totales consolidados del año escolar.
- **Botón "Descargar Informe Estadístico (.xlsx)":** Genera un libro de Excel con formato formal de Vinculación.

---

### 2.4 Vista `/reports/911` (Estadística SEP/TecNM 911.10)
Reemplaza las hojas `911.10 XII` y `911.10 XIII`:
- **Sección XII — Servicio Social y Prácticas Profesionales Liberados:**
  - Tabla cruzada: Carrera vs Sector de la Dependencia (`Educativo (propia Institución)`, `Público`, `Privado`, `Social`).
  - Columnas: Hombres, Mujeres, No especificado, Total, Alumnos con Discapacidad.
- **Sección XIII — Vinculación con Empresas por Tamaño y Sector:**
  - Conteos por tamaño de empresa (`Micro`, `Pequeña`, `Mediana`, `Grande`).
  - Distribución por turnos (`Matutino`, `Vespertino`, `Nocturno`).
  - Actividades de inclusión y beneficiarios comunitarios acumulados.
- **Botón "Exportar Formato 911.10 (.xlsx)":** Descarga directa lista para el informe anual a Dirección General del TecNM.

