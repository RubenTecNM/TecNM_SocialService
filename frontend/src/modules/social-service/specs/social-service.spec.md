# Especificación Frontend: Módulo de Servicio Social (SS)

> **Contrato Backend:** [`docs/specs/social-service.spec.md`](../../../../../docs/specs/social-service.spec.md)  
> **Roles:** `student` (su trámite), `social_service_officer`, `admin` (gestión completa), `department_head`, `internship_coordinator` (lectura).

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
  - Si `requested`: Botón "Aprobar Solicitud" (emite folio `SS_CP...` y fija fecha) o "Rechazar Solicitud" (con observaciones).
  - Si `approved`: Botón "Registrar Aceptación" (requiere documento `CA` aprobado).
  - Si `accepted`: Botón "Activar Servicio Social" (requiere documento `CC` aprobado).
  - Si `active`: Botón "Marcar Concluido" (verifica documentos `R1..R3`, `EV1..EV3`, `CT` aprobados).
  - Si `completed_pending_review`: Botón "Liberar Servicio Social" (genera folio atómico `SSC...`, fija calificación cualitativa "Excelente" y fecha de liberación).
  - Si `released`: Botón "Enviar Acta a Escolares" (genera folio `DGTV...`).
  - Botón "Registrar Producto No Conforme (PNC)": Abre modal para registrar incidencia ISO 9001.
  - Botón "Ver Historial de Estados": Abre línea de tiempo de auditoría (`tramite_status_logs`).

