# Especificación Frontend: Módulo de Calidad ISO 9001 & PNC

> **Contrato Backend:** [`docs/specs/quality.spec.md`](../../../../../docs/specs/quality.spec.md)  
> **Roles:** `admin`, `social_service_officer`, `internship_coordinator` (gestión completa), `department_head` (lectura).

---

## 1. Vistas Requeridas

| Ruta | Nombre | Componente Principal | Rol Permitido | Propósito |
| :--- | :--- | :--- | :--- | :--- |
| `/quality/non-conformances` | `quality-pnc-list` | `NonConformancesListView.vue` | Staff, Dept Head | Bitácora y seguimiento formal de Producto No Conforme (PNC). |
| `/quality/non-conformances/:id` | `quality-pnc-detail` | `NonConformanceDetailView.vue` | Staff, Dept Head | Expediente detallado del PNC y plan de acción correctiva. |

---

## 2. Requerimientos de UI y Componentes

### 2.1 Vista `/quality/non-conformances`
- **Contenedor:** `UiBasePage` (`fluid=true`, título: "Gestión de Calidad — Producto No Conforme (PNC)").
- **Barra de Métricas Superiores (`UiMetric`):**
  - Total Abiertas (`open`).
  - En Revisión (`under_review`).
  - Resueltas (`resolved`).
  - Canceladas (`cancelled`).
- **Barra de Filtros:**
  - Selector de Periodo Académico.
  - Selector de Trámite: Todos / Servicio Social / Prácticas Profesionales.
  - Selector de Estado del PNC.
  - Selector de Categoría (`desertion`, `delayed_reports`, `company_dismissal`, `insufficient_grade`, `invalid_documents`, `other`).
  - Buscador por código de incidencia (`PNC-SS-...`), matrícula o nombre del alumno.
- **Tabla de Incidencias (`UiTable`):**
  - Columnas: Código de Incidencia, Trámite, Alumno (matrícula y nombre), Categoría (etiqueta legible), Estado (`UiBadge`), Fecha de Registro, Acciones.
  - Botón "Nuevo PNC" en encabezado que abre `CreatePncModal.vue`.
  - Acción de fila: Botón "Gestionar Incidencia" -> abre detalle o modal de seguimiento.

---

### 2.2 Modal / Vista de Seguimiento de Incidencia
- **Información del Trámite Asociado:**
  - Enlace directo al expediente de Servicio Social o Prácticas Profesionales.
  - Nombre del alumno, carrera, empresa y supervisor.
- **Formulario de Plan de Acción Correctiva:**
  - Si el estado es `open` o `under_review`, permite capturar o editar el `actionPlan` (análisis de causa raíz y compromisos acordados con el alumno o empresa).
- **Botones de Transición de Estado (`UiButton`):**
  - "Pasar a Revisión" (`under_review`).
  - "Resolver Incidencia" (`resolved`): Exige que el `actionPlan` esté documentado. Muestra badge de éxito y registra el usuario auditor que resolvió la incidencia.
  - "Cancelar Incidencia" (`cancelled`).

---

### 2.3 Componente Reutilizable `TramiteTimelineModal.vue`
- Modal que consume `GET /api/tramite-status-logs?tramiteType=...&tramiteId=...`.
- Renderiza una línea de tiempo vertical visual:
  - Fecha y hora legible (zona horaria `America/Monterrey`).
  - Estado anterior -> Nuevo estado (`UiBadge`).
  - Motivo u observaciones de la transición.
  - Nombre del usuario que autorizó el cambio de estado.

