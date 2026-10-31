# Contrato: Prácticas Profesionales

> **Estado:** Borrador v0.2 (alineado al Forms oficial y `SEGUIMIENTO VINCULACIÓN1.xlsx`). Convenciones en [api-conventions.spec.md](./api-conventions.spec.md). Tablas: `internships`, `internship_evaluations`. Escribe en `tramite_status_logs`, `non_conformances`, `document_series`.

## Acceso

- `InternshipStaff`: todo.
- `social_service_officer`, `department_head`: lectura.
- `student`: solicita apertura (Carta de Presentación adjuntando IMSS), llena formulario de evaluación Anexo XVII para imprimirlo prellenado y sube sus documentos (`CA`, `EV`, `CT`).

---

## 1. Resumen Operativo del Proceso Real

1. **Solicitud de Carta de Presentación (Automatización del Microsoft Forms en `docs/sources/practicas-profesionales/forms`):**
   - El alumno confirma/llena sus datos personales y escolares del Forms (**Matrícula, Nombre Completo, Carrera, Semestre 1–15, Turno Matutino/Nocturno, Teléfono de contacto y Correo electrónico INSTITUCIONAL**) y adjunta su **Constancia de Vigencia de Derechos del IMSS** (PDF).
   - No es obligatorio indicar empresa ni fechas en este primer paso (en la operación real el alumno solicita primero su Carta de Presentación `CP` para llevarla a la empresa).
   - Vinculación aprueba la solicitud y el sistema genera automáticamente la **Carta de Presentación (`CP`)** mediante plantilla con su folio (`Oficio No. EJ26/PR...`), marcando `C.P. = SI` en los estadísticos.
2. **Oficio de Aceptación (`CA`):**
   - La empresa envía el **Oficio de Aceptación** en hoja membretada con firma y sello al correo `arturo.martinez@monclova.tecnm.mx` y el alumno (o personal de Vinculación) sube el PDF (`CA`) al expediente, registrando/confirmando la empresa, el responsable, el sector y las fechas de inicio y término.
3. **Evaluación (`EV`) y Oficio de Terminación (`CT`):**
   - El alumno tiene un formulario en el sistema para llenar los datos del **Anexo XVII (Formato de Evaluación al Desempeño de la Actividad Complementaria — PRODUCTIVIDAD LABORAL)** e imprimirlo ya lleno para solo pedir firma y sello del evaluador de la empresa.
   - El alumno sube los PDFs escaneados con firma y sello de su **Evaluación de la empresa (`EV`)** y su **Oficio de Terminación (`CT`)** en hoja membretada.
   - Vinculación valida los documentos (`CP`, `CA`, `EV`, `CT`) y emite la **Constancia de Liberación (`CC`)**, reflejando todo en tiempo real en los estadísticos.

---

## 2. DTOs

```ts
interface InternshipListItem {
  id: number;
  studentId: number;
  studentControlNumber: string;
  studentFullName: string;
  careerCode: string;
  periodCode: string;
  companyName: string | null;         // null si solo ha solicitado CP y aún no registra CA
  status: TramiteStatus;
  documentProgressPercentage: number; // 0..100 (20% por hito: CP, CA, EV, CT, CC)
  isExpedientComplete: boolean;
  presentationLetterFolio: string | null;
  startDate: string | null;
  endDate: string | null;
  pendingReviewsCount: number;
  updatedAt: string;
}

interface InternshipDetail {
  id: number;
  student: {
    id: number;
    controlNumber: string;
    fullName: string;
    careerName: string;
    currentSemester: number;
    shift: StudentShift;
    phoneNumber: string | null;
    institutionalEmail: string | null;
    hasValidImss: boolean;
    hasImssFile: boolean;
  };
  period: { id: number; code: string };
  company: { id: number; name: string; sector: string } | null;
  supervisor: { id: number; fullName: string; position: string } | null;
  agreement: { id: number; agreementNumber: string; validity: string } | null;
  semesterEnrolled: number;
  startDate: string | null;
  endDate: string | null;
  scheduleDescription: string | null;
  requiredHours: number;
  completedHours: number;             // informativo
  documentProgressPercentage: number; // 0..100
  status: TramiteStatus;
  creditsAwarded: 2;
  presentationLetterFolio: string | null;
  presentationLetterDate: string | null;
  acceptanceLetterDate: string | null;
  terminationLetterDate: string | null;
  originalTerminationLetterReceived: boolean;
  releaseCertificateFolio: string | null;
  releaseDate: string | null;
  releasedAt: string | null;
  isExpedientComplete: boolean;
  schoolServicesActSent: boolean;
  schoolServicesActSentAt: string | null;
  schoolServicesActFolio: string | null;
  observations: string | null;
  cancellationReason: string | null;
  evaluation: InternshipEvaluation | null;
  allowedActions: InternshipAction[];
  checklist: TramiteChecklistItem[];
  createdAt: string;
  updatedAt: string;
}

type InternshipAction =
  | "edit" | "approve" | "register_presentation_letter" | "register_acceptance" | "activate"
  | "complete" | "register_evaluation" | "update_expedient"
  | "release" | "send_school_act" | "cancel" | "mark_non_compliant";

// Paso 1: Solicitud de Carta de Presentación (reemplazo del Microsoft Forms)
interface CreateInternshipRequest {
  studentId: number | null;           // staff: requerido; student: se ignora (usa el propio)
  periodId: number;
  // Datos que el alumno confirma/actualiza en el Forms:
  currentSemester: number;            // 1..15
  shift: StudentShift;                // "morning" | "night" (o "afternoon")
  phoneNumber: string;                // teléfono de contacto requerido
  institutionalEmail: string;         // correo institucional requerido
  // Datos de empresa opcionales al pedir CP (se pueden llenar aquí o al registrar CA):
  companyId?: number | null;
  supervisorId?: number | null;
  agreementId?: number | null;
  startDate?: string | null;
  endDate?: string | null;
  scheduleDescription?: string | null;
  requiredHours?: number;             // default 200
}

// Paso 2: Registro / validación del Oficio de Aceptación (CA)
interface RegisterInternshipAcceptanceRequest {
  companyId: number;
  supervisorId: number;
  agreementId?: number | null;
  startDate: string;
  endDate: string;
  scheduleDescription?: string | null;
  requiredHours?: number;             // default 200
  acceptanceLetterDate: string;
}

// Formulario para prellenar e imprimir Anexo XVII (lo puede llenar student o staff)
interface InternshipEvaluationRequest {
  evaluationStartDate: string;
  evaluationEndDate: string;
  criteria: [number, number, number, number, number, number, number]; // 7 enteros 0–4 (default UI 4)
  evaluatorSupervisorId: number | null;
  evaluatorSignatureDate: string;
  observations: string | null;
}

interface InternshipEvaluation extends InternshipEvaluationRequest {
  id: number;
  activityName: string;               // "PRODUCTIVIDAD LABORAL"
  numericalAverage: number;           // calculado, 2 decimales
  performanceLevel: QualitativeGrade; // calculado
  isCompetencyAchieved: boolean;      // calculado: promedio ≥ 1.00
}
```

## 3. Validación

| Campo | Regla | Error |
| --- | --- | --- |
| `periodId` | Requerido; activo | `400` |
| `currentSemester` | Entero 1–15 | `400` |
| `shift` | `morning`, `afternoon` o `night` | `400` |
| `phoneNumber` | Requerido en apertura, máx. 20 | `400` |
| `institutionalEmail` | Requerido en apertura, formato email, máx. 255 | `400` |
| `companyId`, `supervisorId` | Opcionales en apertura (`requested`); requeridos al registrar Oficio de Aceptación (`acceptance`) | `400` |
| `supervisorId` | Si se envía, debe pertenecer a `companyId` | `422 internships.supervisor_company_mismatch` |
| `startDate`, `endDate` | Opcionales en apertura; requeridas en `acceptance` (`endDate ≥ startDate`) | `400` |
| `requiredHours` | Entero 200–480 (default 200) | `400` |
| `criteria[i]` | Exactamente 7 enteros 0–4 | `400` |
| Estudiante | Inscrito y sin otro PP activo/liberado; con archivo IMSS cargado | `422 internships.student_not_eligible` |

## 4. Endpoints

| Método y ruta | Rol | Entrada | Transición / Efecto | Éxito | Errores |
| --- | --- | --- | --- | --- | --- |
| `GET /api/internships` | Staff, lectura | `periodId`, `status`, `careerId`, `companyId`, `isExpedientComplete`, `search`, `sort` | — | `200 PagedResponse<InternshipListItem>` | — |
| `GET /api/internships/me` | student | — | — | `200 InternshipListItem[]` | `404` |
| `GET /api/internships/{id}` | Todos (propio) | — | — | `200 InternshipDetail` | `404` |
| `POST /api/internships` | student, staff | `CreateInternshipRequest` | Actualiza contacto/semestre/turno del alumno y crea trámite en `requested` | `201` | `400`, `422` |
| `PUT /api/internships/{id}` | student (`requested`), staff (no terminal) | `CreateInternshipRequest` | Actualiza datos del trámite | `200` | `409`, `422` |
| `POST .../{id}/approve` | staff | `{ observations }` | `requested → approved`, genera folio atómico `internship_presentation_letter` (`Oficio No. EJ26/PR...`) y fija `presentationLetterDate` | `200` | `409` |
| `POST .../{id}/presentation-letter` | staff | `{ presentationLetterDate }` | Emite o actualiza fecha/folio de `CP` | `200` | `409` |
| `POST .../{id}/acceptance` | student, staff | `RegisterInternshipAcceptanceRequest` | Guarda empresa/supervisor/fechas; si lo ejecuta staff (o al aprobar doc `CA`), pasa `approved → active` | `200` | `409`, `422` |
| `PUT .../{id}/evaluation` | **student**, staff | `InternshipEvaluationRequest` | Guarda datos de Anexo XVII para imprimir el formato lleno y pedir firma y sello | `200 InternshipEvaluation` | `400` |
| `PATCH .../{id}/expedient` | staff | `{ isExpedientComplete, originalTerminationLetterReceived, observations }` | — | `200` | `400` |
| `POST .../{id}/complete` | staff | `{ terminationLetterDate }` | `active → completed_pending_review` (exige docs `EV` y `CT` aprobados; **no bloquea por horas**) | `200` | `422 documents_missing` |
| `POST .../{id}/release` | staff | `{ releaseDate }` | `completed_pending_review → released`; marca `isExpedientComplete=true` y consume folio `internship_release_certificate` (`PPC...`) | `200` | `422 documents_missing` |
| `POST .../{id}/school-services-act` | staff | — | En `released`: consume folio `school_services_act` (`DGTV...`) | `200` | `409 act_already_sent` |
| `POST .../{id}/cancel` | staff; student en `requested` | `{ reason }` | no terminal → `cancelled` | `200` | `409` |
| `POST .../{id}/non-compliance` | staff | `{ category, description }` | → `non_compliant` (+ PNC) | `200` | `409` |
| `GET .../{id}/history` | Todos (propio) | — | — | `200 StatusLogItem[]` | `404` |
| `GET .../{id}/print-data/{document}` | staff, student | `presentation_letter`, `evaluation`, `release_certificate` | — | `200` objeto plano con datos + autoridades + folio | `409 not_available` |

Prefijo de `code`: `internships.`.

## 5. Reglas de negocio

1. Máximo un PP no terminal por estudiante; uno `released` impide otro.
2. **Automatización del Forms de apertura:** Al solicitar Prácticas Profesionales, el alumno sube su Constancia de Vigencia IMSS (`PUT /api/students/me/imss`) y confirma su Matrícula, Nombre, Carrera, Semestre, Turno, Teléfono y Correo Institucional. Con ello solicita su **Carta de Presentación (`CP`)**.
3. **Recepción de Oficio de Aceptación (`CA`):** La UI informa al alumno que la empresa debe enviar el Oficio de Aceptación en hoja membretada con firma y sello a `arturo.martinez@monclova.tecnm.mx` y le permite subir su copia en PDF (`CA`) junto con los datos de la empresa, responsable y periodo.
4. **Impresión prellenada de Evaluación (Anexo XVII):** El alumno (o staff) puede llenar los datos del Anexo XVII en cualquier momento para imprimir el formato ya con todos los datos (`print-data/evaluation`), recabar firma y sello de la empresa y subir el PDF escaneado (`EV`). El backend calcula `numericalAverage = round(avg(criteria), 2)` y `performanceLevel` según la Tabla 2 oficial del Anexo XVII (`3.50–4.00 excellent`, `2.50–3.49 notable`, `1.50–2.49 good`, `1.00–1.49 sufficient`, `0.00–0.99 insufficient`).
5. **Progreso basado en documentos:** El avance (`documentProgressPercentage`) suma 20% por cada uno de los 5 hitos de la hoja `PRÁCTICAS` en `SEGUIMIENTO VINCULACIÓN1.xlsx`: `CP` (Carta de Presentación), `CA` (Oficio de Aceptación), `EV` (Evaluación Anexo XVII), `CT` (Oficio de Terminación) y `CC` (Constancia de Liberación / Expediente Completo). Las horas no bloquean el progreso.
6. Toda transición escribe `tramite_status_logs`.

## 6. Checklist documental (alineado a `SEGUIMIENTO VINCULACIÓN1.xlsx`)

| Clave | Documento / Hito | Columna en hoja `PRÁCTICAS` |
| --- | --- | --- |
| `imss_uploaded` | Constancia de Vigencia de Derechos IMSS | Requisito del Forms |
| `presentation_letter` | Carta de Presentación emitida (`Oficio No. EJ26/PR...`) | `C.P.` (`O`) |
| `document_CA` | Oficio de Aceptación (hoja membretada, firma y sello) | `C.A.` (`P`) |
| `document_EV` | Anexo XVII — Evaluación de la empresa firmada y sellada | `Ev.` (`R`) |
| `document_CT` | Oficio de Terminación (hoja membretada, firma y sello) | `C.T.` (`S`) |
| `release_certificate` | Constancia de Liberación / Expediente Completo | `CC` (`T`) |
