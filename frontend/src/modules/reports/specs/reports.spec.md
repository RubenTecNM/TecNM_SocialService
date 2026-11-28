> Autorización vigente: [Acceso por proceso](../../../../../docs/specs/process-access.spec.md). Sus reglas prevalecen sobre permisos anteriores incompatibles.

> Aplicar primero [Flujo documental v2](../../../../../docs/specs/document-workflow-v2.spec.md). Sin datos ficticios ante fallos; estados y acciones los decide la API.

# Especificación Frontend: Módulo de Reportes & Estadísticos

> **Contrato Backend:** [`docs/specs/reports.spec.md`](../../../../../docs/specs/reports.spec.md)  
> **Roles:** `department_head`, `social_service_officer`, `internship_coordinator` (acceso completo a consulta y exportación).

---

## 1. Vistas Requeridas

| Ruta | Nombre | Componente Principal | Propósito |
| :--- | :--- | :--- | :--- |
| `/reports/dashboard` | `reports-dashboard` | Redirección → `/reports/stats` | Consolida el tablero analítico en un solo módulo. |
| `/reports/tracking` | `reports-tracking` | `TrackingSheetView.vue` | Sábana de seguimiento en vivo con filtros combinables (periodo, ciclo escolar, carrera, semestre, sexo, empresa, actividad, estatus), modal expediente y exportación `.xlsx`/`.csv`. |
| `/reports/stats` | `reports-stats` | `StatisticsView.vue` | Centro Analítico y Reportes (Tab 1: Resumen Analítico con `UiChart` interactivo y cross-filtering; Tab 2: Estadístico Oficial y Ciclo Escolar; Tab 3: Formato 911.10 completo; Exportación Excel `.xlsx` multi-hoja y PDF imprimible). |

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
- **Diseño a Pantalla Completa (`100dvh`):**
  - Contenedor `UiBasePage` (`fluid=true`, `class="fullscreen-page"`) sin scroll exterior de ventana y con scroll interno tanto horizontal como vertical dentro de la tabla.
  - Encabezado ejecutivo con 4 KPIs de resumen (Total en Matriz, En Proceso, Avance 100%, Con Constancia) en superficie neutra donde únicamente el KPI activo presenta borde dorado (`var(--color-accent)`).
- **Barra Superior de Herramientas:**
  - Selector de Trámite: Pestaña "Servicio Social" / Pestaña "Prácticas Profesionales".
  - Selector de Periodo Académico y Selector de Carrera.
  - Buscador rápido con autocompletado interactivo, debounce de `280 ms` (por matrícula, nombre, carrera o dependencia/empresa) y botón único azul (`✕`) para limpiar.
  - Botón principal: **"Exportar Sábana (.CSV)"**.
- **Tabla Dinámica de Seguimiento (con scroll interno y encabezados fijos):**
  - Paginación estandarizada de **`10` registros por página** (`pageSize = 10`) con controles `Anterior`, `Página X de Y` y `Siguiente` sincronizados con búsqueda y filtros.
  - Columnas del Estudiante: No. Control, Nombre del Alumno (con botón compacto `Ver expediente`), Carrera.
  - Columnas de la Empresa y Trámite: Dependencia / Empresa, Folio C.P.
  - **Matriz de Documentos (Pills semánticos mate "SI" / "—"):**
    - En Servicio Social (10 columnas): `C.P.`, `C.A.`, `SOL.`, `C.C.`, `REP. 1`, `REP. 2`, `REP. 3`, `EV1..3`, `CTA. TERM.`, `CONST.TERM`.
    - En Prácticas Profesionales (5 columnas): `C.P.`, `C.A.`, `EV.`, `C.T.`, `CC`.
  - **Columna de Avance General:** Barra de progreso visual con porcentaje numérico (`progressPercentage%`, de 0% a 100%).
  - Columnas de Folios de Cierre: Const. Term. y Acta Escolares.

#### Consulta de expediente desde la matriz actual
- Incorporar únicamente un botón compacto con ícono de ver, tooltip «Ver expediente» y nombre accesible que identifique al alumno, junto al nombre en la celda existente. Mantener estructura, columnas, estilos y disposición de la matriz implementada.
- Abrir `UiDialog` de consulta sobre la matriz: datos personales, académicos, contacto, IMSS, detalle del trámite y documentos autorizados con estado, observaciones y consulta/descarga de PDF mediante el visor existente.
- Resolver `student.id` mediante el detalle de SS/PP de la fila; consultar perfil y documentos del trámite exacto más documentos generales autorizados. No cambiar contratos ni permisos.
- Mantener filtros, búsqueda, filas y scroll al abrir/cerrar; restaurar foco al botón mediante `UiDialog`. Descartar respuestas pendientes cuando se cierra o cambia el expediente.
- Deshabilitar los botones mientras se actualiza la matriz o falla su carga y descartar respuestas de filtros anteriores para no abrir un trámite de otro proceso.
- Mostrar carga, error con reintento y estado sin documentos. No conservar información de otro alumno ante fallos.
- Verificar SS y PP, selección de alumnos distintos, PDF, errores, cierre durante carga, teclado y conservación de columnas/filtros/scroll.

---

### 2.3 Vista `/reports/statistics` (Estadístico Semestral y Formato 911.10)
Reemplaza las hojas `ESTADÍSTICO`, `PNC` y `911.10` de `SEGUIMIENTO VINCULACIÓN1.xlsx`:
- **Diseño a Pantalla Completa (`100dvh`):**
  - Encabezado ejecutivo con 4 KPIs institucionales en superficie neutra (**En Condiciones C.P.**, **Solicitantes C.A.**, **Liberados**, **% Liberación**).
  - Barra de herramientas con selector de proceso (**Servicio Social** / **Prácticas Profesionales**), selector de periodo escolar y pestañas de sección (**Hoja ESTADÍSTICO**, **Formato 911.10 & Inclusión**, **Vinculación por Empresa**).
- **Tabla de Resultados por Carrera (`Hoja ESTADÍSTICO`):**
  - Columnas oficiales: Carrera (clave y nombre), En condiciones (`CP = SI`), Solicitantes (`H`, `M`, `Total`), Liberados (`H`, `M`, `Total`), PNC (`Solic. - Lib.`), `% Liberación` y fila de **TOTAL INSTITUCIONAL** con tokens mate claro/oscuro.
- **Sección Formato 911.10 & Vinculación (`LinkageMetrics.vue`):**
  - Distribución por sector (`Educativo`, `Público`, `Privado`, `Social`), indicadores de inclusión y beneficiarios, y tabla de proyectos por empresa con paginación estandarizada de `10` registros por página (`pageSize = 10`).
  - En móvil (`<= 900px`), Matriz de Seguimiento, Estadístico 911 y Tablero liberan `fullscreen-page` a altura natural, agrupan KPIs en `2×2`, permiten deslizar pestañas horizontalmente y adaptan paneles/tablas sin desbordamiento de pantalla ni cambios en escritorio.

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

