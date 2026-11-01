# Especificación Frontend: Módulo de Prácticas Profesionales (PP)

> **Contrato Backend:** [`docs/specs/internships.spec.md`](../../../../../docs/specs/internships.spec.md)  
> **Roles:** `student` (su trámite), `internship_coordinator`, `admin` (gestión completa), `department_head`, `social_service_officer` (lectura).

---

## 1. Vistas Requeridas

| Ruta | Nombre | Componente Principal | Rol Permitido | Propósito |
| :--- | :--- | :--- | :--- | :--- |
| `/internships` | `internships-staff-list` | `InternshipsStaffListView.vue` | Staff, Dept Head | Listado de trámites, filtros por carrera/periodo, seguimiento. |
| `/internships/:id` | `internships-staff-detail` | `InternshipsStaffDetailView.vue` | Staff, Dept Head | Detalle administrativo, emisión de folios `PR...` y `PPC...`, actas. |
| `/my-internship` | `internships-student-portal` | `InternshipsStudentPortalView.vue` | `student` | Portal del alumno: checklist, formatos y subida de documentos. |
| `/my-internship/new` | `internships-student-wizard` | `InternshipOpeningWizard.vue` | `student` | Solicitud de Carta de Presentación (automatización del Forms + IMSS). |

---

## 2. Requerimientos de UI y Flujos

### 2.1 Asistente de Solicitud de Carta de Presentación (`/my-internship/new`)
Automatiza el formulario oficial de Microsoft Forms:
- **Campos del Alumno:**
  - Matrícula y Nombre Completo (solo lectura, obtenidos de la sesión).
  - Carrera (solo lectura).
  - Semestre actual (selector 1 a 15).
  - Turno (selector: `morning`, `night`, `afternoon`).
  - Teléfono celular de contacto (campo editable obligatorio).
  - Correo electrónico institucional (`@monclova.tecnm.mx`, obligatorio).
- **Vigencia IMSS:**
  - Comprobación de que el alumno tiene su PDF de Vigencia IMSS cargado en el sistema; si no, permite subirlo directamente en este paso.
- **Nota operativa en UI:**
  - Se explica claramente al alumno que en este paso inicial **no es necesario capturar empresa ni fechas**, ya que la Carta de Presentación se emite primero para que pueda postularse en las empresas.
- **Acción:**
  - Botón "Solicitar Carta de Presentación" (`POST /api/internships`). El trámite queda en estado `requested`.

---

### 2.2 Portal del Estudiante (`/my-internship`)
- **Resumen del Trámite:**
  - Estado (`requested`, `approved`, `accepted`, `active`, `released`).
  - Barra de avance documental (0% a 100%, 20% por cada uno de los 5 hitos: `CP, CA, EV, CT, CC`).
  - Folios emitidos (`presentationLetterFolio`, `releaseCertificateFolio`).
- **Checklist de Documentos (`ExpedientChecklist.vue`):**
  1. **Carta de Presentación (`CP`):**
     - Botón para descargar el PDF generado con folio oficial (`Oficio No. EJ26/PR...`) en cuanto el coordinador apruebe la solicitud.
  2. **Oficio de Aceptación (`CA`):**
     - Alerta informativa: *"La empresa debe enviar el Oficio de Aceptación en hoja membretada, con firma y sello al correo: arturo.martinez@monclova.tecnm.mx"*.
     - Botón *"Registrar Empresa y Subir Oficio"*: Modal `RegisterAcceptanceModal.vue` donde el alumno captura el nombre de la empresa, supervisor, periodo (fecha inicio y fin, horario) y sube el PDF escaneado del oficio membretado.
  3. **Evaluación de la Empresa (Anexo XVII — `PRODUCTIVIDAD LABORAL`):**
     - Botón *"Prellenar Formato Anexo XVII"*: Modal `InternshipEvaluationModal.vue` que permite capturar los 7 criterios oficiales (escala 0 a 4) y fechas de evaluación.
     - Botón *"Imprimir Formato Prellenado"*: Genera el formato con todos los campos llenos para recabar calificación, firma y sello de la empresa.
     - Botón para subir el PDF escaneado (`EV`).
  4. **Oficio de Terminación (`CT`):**
     - Botón para subir el PDF escaneado del oficio de terminación en hoja membretada de la empresa (`CT`).
  5. **Constancia de Liberación / Acreditación (`CC`):**
     - Botón para descargar la constancia institucional emitida con folio (`PPC...`) una vez que el coordinador libere el trámite.

---

### 2.3 Detalle Administrativo para Vinculación (`/internships/:id`)
- **Acciones Rápidas del Coordinador:**
  - Si `requested`: Botón "Aprobar y Emitir Carta de Presentación" (genera automáticamente el folio `EJ26/PR...` y fecha de oficio).
  - Si `approved`: Botón "Registrar / Validar Aceptación" (asigna empresa, supervisor y activa las prácticas al verificar el PDF `CA`).
  - Si `active`: Botón "Completar Prácticas" (verifica documentos `EV` y `CT` aprobados).
  - Si `completed_pending_review`: Botón "Liberar y Acreditar (PPC)" (asigna folio `PPC...`, marca expediente completo y acredita los 2 créditos).
  - Si `released`: Botón "Enviar Acta a Escolares" (genera folio `DGTV...`).
  - Botón "Registrar Producto No Conforme (PNC)".
  - Botón "Ver Historial de Auditoría".

