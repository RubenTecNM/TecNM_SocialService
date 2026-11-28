> Autorización vigente: [Acceso por proceso](../../../../../docs/specs/process-access.spec.md). Sus reglas prevalecen sobre permisos anteriores incompatibles.

> Aplicar primero [Flujo documental v2](../../../../../docs/specs/document-workflow-v2.spec.md). Sin datos ficticios ante fallos; estados y acciones los decide la API.

# Especificación Frontend: Módulo de Servicio Social (SS)

> **Contrato Backend:** [`docs/specs/social-service.spec.md`](../../../../../docs/specs/social-service.spec.md)  
> **Roles:** `student` (su trámite), `social_service_officer`, `department_head` (gestión completa).

---

## 1. Vistas Requeridas

| Ruta | Nombre | Componente Principal | Rol Permitido | Propósito |
| :--- | :--- | :--- | :--- | :--- |
| `/social-service` | `ss-staff-list` | `SocialServiceStaffListView.vue` | Staff, Dept Head | Listado de trámites, filtros por carrera/periodo, seguimiento. |
| `/social-service/:id` | `ss-staff-detail` | `SocialServiceStaffDetailView.vue` | Staff, Dept Head | Detalle administrativo, dictamen, emisión de folios y liberación. |
| `/my-social-service` | `ss-student-portal` | `SocialServiceStudentPortalView.vue` | `student` | Portal del alumno: checklist, prellenado de formatos y subida de PDFs. |
| `/my-social-service/new` | `ss-student-wizard` | `SocialServiceOpeningWizard.vue` | `student` | Asistente de solicitud de apertura (Anexo XVIII + IMSS). |

---

## 2. Requerimientos de UI y Flujos

### 2.1 Asistente de Apertura (`/my-social-service/new`)
Formulario por pasos (`UiCard` o stepper):
- **Paso 1: Vigencia IMSS:**
  - Comprueba si el alumno tiene `hasValidImss: true` o archivo cargado. Si no, solicita subir el PDF antes de continuar.
- **Paso 2: Datos de la Dependencia:**
  - Selector de Programa de Servicio Social (9 categorías normativas).
  - Selector de Empresa/Dependencia Receptora (`/api/companies/options`).
    - Al seleccionar una dependencia interna (`isTecnmInternal = true`), la modalidad se fija en `internal`.
  - Selector de Supervisor (`/api/companies/{id}/supervisors`).
  - Fechas programadas de inicio y término (mínimo 6 meses de duración).
- **Paso 3: Formato Anexo XVIII (Solicitud):**
  - Campos precargados con datos del estudiante (domicilio, teléfono, semestre, carrera).
  - Resumen de actividades a desarrollar (`activitiesOutline`).
- **Confirmación:**
  - Al enviar (`POST /api/social-services`), el trámite pasa a `requested` y se notifica al alumno que Vinculación revisará su solicitud.

---

### 2.2 Portal del Estudiante (`/my-social-service`)
- **Resumen del Trámite:**
  - Estado del trámite con `UiBadge` (`requested`, `approved`, `accepted`, `active`, `released`).
  - Porcentaje de avance documental (0% a 100%, 10% por cada hito).
  - Datos de la dependencia, supervisor y fechas.
- **Flujo Guiado de Documentos (`ExpedientChecklist.vue`):**
  1. **Solicitud (Anexo XVIII):** Estado del dictamen de la oficina.
  2. **Carta de Presentación (`CP`):** Botón para descargar el PDF generado una vez que la solicitud es aprobada por Vinculación (con su folio oficial `SS_CP...`).
  3. **Carta de Aceptación (`CA`):**
     - Si es **servicio local en el TecNM**: Botón *"Descargar Plantilla de Carta de Aceptación"* (consume `/print-data/acceptance_letter`), para que el alumno la imprima, pida firma/sello al jefe del departamento interno y suba el PDF escaneado.
     - Si es **externo**: Instrucciones para solicitar a la empresa la carta membretada y subir el PDF escaneado.
  4. **Carta Compromiso (Anexo XX):**
     - Botón *"Llenar / Imprimir Carta Compromiso"*: Modal `CommitmentModal.vue` que permite verificar domicilio y teléfono para imprimir el formato lleno.
     - Botón para subir el PDF firmado por el alumno (`CC`).
  5. **Reportes Bimestrales 1, 2 y 3 (Anexo XXII):**
     - Botón *"Llenar / Imprimir Reporte N"*: Modal `ReportModal.vue` donde el alumno captura el resumen de actividades del bimestre (160 horas por defecto).
     - Botón para imprimir el formato prellenado para firma y sello del supervisor.
     - Botón para subir el PDF escaneado (`R1`, `R2`, `R3`).
  6. **Evaluaciones Cualitativas (Anexos XXIII, XXIV y XXV):**
     - Botón *"Prellenar Evaluaciones"*: Modales para evaluar los 7 criterios oficiales y 8 preguntas de impacto social.
     - Botón para imprimir los anexos listos para firma y sello.
     - Botón para subir los PDFs escaneados (`EV1`, `EV2`, `EV3`).
  7. **Carta de Terminación (Anexo XIX):**
     - Botón para subir el PDF escaneado con firma y sello de la dependencia (`CT`).
  8. **Constancia de Terminación Oficial:**
     - Botón para descargar la constancia emitida por el Instituto (`SSC...`) una vez liberado.

---

### 2.3 Detalle Administrativo para Vinculación (`/social-service/:id`)
- **Panel de Acciones Rápidas (según `allowedActions`):**
  - Si `requested`: Botón "Dictaminar Solicitud (Anexo XVIII)" que abre un modal institucional (`UiDialog`) para aprobar (emite folio `SS_CP...` y fija fecha) o rechazar la solicitud capturando las observaciones con validación y estado de carga.
  - Si `approved`: Botón "Registrar Aceptación" (requiere documento `CA` aprobado).
  - Si `accepted`: Botón "Activar Servicio Social" (requiere documento `CC` aprobado).
  - Si `active`: Botón "Marcar Concluido" (verifica documentos `R1..R3`, `EV1..EV3`, `CT` aprobados).
  - Si `completed_pending_review`: Botón "Liberar Servicio Social" (genera folio atómico `SSC...`, fija calificación cualitativa "Excelente" y fecha de liberación).
  - Si `released`: Botón "Enviar Acta a Escolares" (genera folio `DGTV...`).
  - Gestión de **Observaciones Generales del Expediente** y **Dictamen / Observaciones de Documentos** mediante modales (`UiDialog` / `DocumentReviewDialog.vue`), sin usar `window.prompt`.
  - Modal de gestión integral de expediente (`UiDialog` `size="full"`): resumen superior de estatus/folios/avance, barra unificada de formatos y observaciones, pestañas segmentadas (**PDFs Subidos para Dictamen** y **Checklist Normativo TecNM**) y acciones de avance de etapa alineadas en pie de modal.
  - En móvil (`<= 900px`), tanto la bandeja administrativa como el portal estudiantil adaptan KPIs (`2×2`), filtros, formularios de solicitud/reportes, pestañas y modales a pantalla móvil sin alterar escritorio.
  - Buscador con autocompletado dinámico y debounce (250 ms) y paginación estandarizada de **10 registros por página**.
  - Botón "Registrar Producto No Conforme (PNC)": Abre modal para registrar incidencia ISO 9001.
  - Botón "Ver Historial de Estados": Abre línea de tiempo de auditoría (`tramite_status_logs`).


Apertura SS: enviar Solicitud e IMSS juntos mediante POST /api/social-services/submissions multipart (application JSON, imssFile opcional con evidencia vigente pendiente/aprobada). No exige aprobación IMSS previa al envío. Vista previa antes de confirmar; aprobación administrativa sigue exigiendo IMSS aprobado. No aplica a PP.

## Validación uniforme

Los formularios de captura muestran errores locales y del servidor junto al campo con UiField y resumen UiFormErrors dentro del formulario/modal. Las rutas anidadas se conservan; errores sin control visible permanecen en el resumen. Blur valida, input revalida después de la primera visita. Se conserva el contenido al fallar, se enfoca el primer control inválido y se limpia el estado al cambiar de registro/formulario. Backend conserva reglas, estados HTTP y autorización.

## Acta Individual

GET /api/social-services/{id}/pdf/individual_act entrega PDF oficial únicamente a department_head y social_service_officer, para trámites released. Prellena alumno, matrícula, carrera, dependencia, actividades de reportes ordenados sin duplicados (respaldo solicitud), fechas del trámite y 480 horas. Expedición: día de descarga en America/Mexico_City mediante TimeProvider. Autoridades activas outreach_division_head e institute_director, con título opcional, nombre y cargo; solo firmas vacías. Faltantes devuelven 422 documents.data_missing por campo; desbordamiento 422 documents.text_overflow. Plantilla/fuente inválida devuelve 409. No modifica estado, evidencia, folios ni envío a Escolares. Solo PDF individual, sin reverso. Descarga administrativa con carga, prevención de doble petición y errores visibles; alumno sin acceso.
