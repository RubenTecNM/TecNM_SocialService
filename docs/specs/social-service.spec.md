# Contrato: Servicio Social

> **Estado:** Borrador v0.2 (alineado al flujo documental real y `SEGUIMIENTO VINCULACIÓN1.xlsx`). Convenciones en [api-conventions.spec.md](./api-conventions.spec.md). Tablas: `social_services`, `social_service_applications`, `social_service_commitments`, `social_service_bi_monthly_reports`, `social_service_evaluations`, `social_service_activity_assessments`. Escribe en `tramite_status_logs`, `non_conformances`, `document_series`.

## Acceso

- `SocialServiceStaff`: todo.
- `internship_coordinator`, `department_head`: lectura.
- `student`: crea su solicitud, llena todos los formularios de sus formatos (Carta Compromiso, Reportes Bimestrales 1–3, Evaluaciones Cualitativas y de Actividades) para imprimirlos ya con todos los datos y recabar firma/sello, y sube sus documentos PDF escaneados.

---

## 1. Resumen Operativo del Proceso Real

1. **Apertura automatizada (Solicitud + IMSS):** El alumno llena sus datos del Anexo XVIII y en el mismo asistente sube su **Constancia de Vigencia de Derechos del IMSS** (`PUT /api/students/me/imss` + `POST /api/social-services`). El sistema genera automáticamente el formato Anexo XVIII (`print-data/application`) y lo envía a Vinculación.
2. **Carta de Presentación (`CP`):** Al validar la apertura, el sistema genera automáticamente la Carta de Presentación mediante plantilla con su folio oficial (`SS_CP...`, ej. `SS_CP0405/17/09/2026`). El alumno la descarga y la entrega en la dependencia.
3. **Carta de Aceptación (`CA`) y Carta Compromiso (`CC`):**
   - Si el servicio se realiza **de manera local en el TecNM** (`modality == "internal"` / `company.isTecnmInternal == true`), el sistema habilita `print-data/acceptance_letter` para **generar automáticamente la Carta de Aceptación** mediante plantilla. El alumno igual la descarga, pide firma y sello del jefe del departamento interno y sube el PDF firmado y sellado (`CA`).
   - Si es **externo**, la dependencia emite su Carta de Aceptación en hoja membretada y el alumno la sube firmada y sellada (`CA`).
   - El alumno llena el formulario de **Carta Compromiso (Anexo XX)** en el sistema, descarga el PDF generado con sus datos, lo firma y lo sube (`CC`).
4. **Reportes Bimestrales (`R1–R3`) y Evaluaciones Cualitativas (`EV1–EV3`):**
   - El alumno cuenta con formularios en el sistema para llenar los datos de sus **Reportes Bimestrales 1, 2 y 3 (Anexo XXII)** y sus **Formatos de Evaluación Cualitativa y de Actividades (Anexos XXIII, XXIV y XXV)** de modo que **se impriman ya con todos los datos llenos** y el alumno **solo pida la firma y el sello**.
   - Una vez firmados y sellados, el alumno sube los PDFs escaneados (`R1`, `R2`, `R3`, `EV1`, `EV2`, `EV3`).
5. **Carta de Terminación (`CT`) y Liberación:**
   - El alumno sube su **Carta de Terminación (Anexo XIX)** firmada y sellada (`CT`).
   - **Progreso guiado por documentos:** Las horas acumuladas y las calificaciones numéricas **no bloquean el progreso**; solo se comprueba que el alumno vaya subiendo los documentos llenos, firmados y sellados y que Vinculación los valide (`approved`), reflejándose en tiempo real en los estadísticos (`SEGUIMIENTO VINCULACIÓN`).

---

## 2. DTOs principales

```ts
interface SocialServiceListItem {
  id: number;
  studentId: number;
  studentControlNumber: string;
  studentFullName: string;
  careerCode: string;
  periodCode: string;
  programName: string;
  companyName: string;
  modality: "internal" | "external";
  status: TramiteStatus;
  documentProgressPercentage: number; // 0..100 (10% por cada documento validado en matriz de seguimiento)
  accumulatedHours: number;           // informativo (tomado de los formularios de reportes)
  requiredHours: number;              // 480
  startDate: string;
  endDate: string;
  pendingReviewsCount: number;        // documentos por revisar
  updatedAt: string;
}

interface SocialServiceDetail {
  id: number;
  student: { id: number; controlNumber: string; fullName: string; careerName: string; hasValidImss: boolean; hasImssFile: boolean };
  period: { id: number; code: string };
  program: { id: number; code: string; name: string; category: string };
  company: { id: number; name: string; isTecnmInternal: boolean };
  supervisor: { id: number; fullName: string; position: string };
  agreement: { id: number; agreementNumber: string; validity: string } | null;
  semesterEnrolled: number;
  modality: "internal" | "external";
  startDate: string;
  endDate: string;
  requiredHours: number;
  accumulatedHours: number;           // informativo
  documentProgressPercentage: number; // 0..100
  status: TramiteStatus;
  presentationLetterFolio: string | null;
  presentationLetterDate: string | null;
  acceptanceLetterDate: string | null;
  terminationLetterDate: string | null;
  originalTerminationLetterReceived: boolean;
  releaseCertificateFolio: string | null;
  releasedAt: string | null;
  finalGradeLevel: QualitativeGrade | null; // default "excellent" al liberar
  finalScore: number | null;                // opcional
  schoolServicesActSent: boolean;
  schoolServicesActSentAt: string | null;
  schoolServicesActFolio: string | null;
  beneficiariesCount: number;
  hasInclusionActivities: boolean;
  targetCommunity: string | null;
  observations: string | null;
  cancellationReason: string | null;
  application: SocialServiceApplication;
  commitment: SocialServiceCommitment | null;
  reports: BiMonthlyReport[];
  evaluations: SocialServiceEvaluation[];
  activityAssessment: ActivityAssessment | null;
  allowedActions: SocialServiceAction[];
  checklist: TramiteChecklistItem[];
  createdAt: string;
  updatedAt: string;
}

type SocialServiceAction =
  | "edit" | "review_application" | "register_presentation_letter" | "register_acceptance"
  | "register_commitment" | "activate" | "submit_report" | "review_report"
  | "register_evaluation" | "register_self_evaluation" | "register_activity_assessment"
  | "update_impact" | "complete" | "release" | "send_school_act" | "cancel" | "mark_non_compliant";
```

## 3. Crear solicitud de apertura (Anexo XVIII)

`POST /api/social-services` — crea `social_services` (`status=requested`) + `social_service_applications` en una transacción. En el asistente UI, el alumno sube primero (o en el mismo flujo) su Constancia de Vigencia de Derechos del IMSS (`PUT /api/students/me/imss`).

```ts
interface CreateSocialServiceRequest {
  studentId: number | null;     // staff: requerido; student: se ignora (usa el propio)
  periodId: number;
  programId: number;
  companyId: number;
  supervisorId: number;
  agreementId: number | null;
  modality: "internal" | "external";
  startDate: string;
  endDate: string;
  targetCommunity: string | null;
  application: {
    studentAddress: string;
    studentPhone: string;
    periodType: "august_january" | "february_july";
    dependencyName: string;          // UI precarga desde empresa
    dependencyHeadName: string;      // UI precarga desde supervisor
    dependencyHeadPosition: string;  // UI precarga desde supervisor
    activitiesOutline: string;
  };
}
```

| Campo | Regla | Error |
| --- | --- | --- |
| `periodId`, `programId`, `companyId`, `supervisorId` | Requeridos; existentes y activos | `400` |
| `supervisorId` | Debe pertenecer a `companyId` | `422 social_services.supervisor_company_mismatch` |
| `agreementId` | Opcional; misma empresa, tipo compatible y vigente en `startDate` | `422 social_services.agreement_invalid` |
| `modality` | `internal` exige `company.isTecnmInternal=true`; `external` exige `false` | `422 social_services.modality_mismatch` |
| `startDate`/`endDate` | `endDate ≥ startDate` | `400` |
| `targetCommunity` | Opcional, máx. 200 | `400` |
| `application.studentAddress` | Requerido | `400` |
| `application.studentPhone` | Requerido, máx. 20 | `400` |
| `application.dependencyName` | Requerido, 1–200 | `400` |
| `application.dependencyHeadName` | Requerido, 1–180 | `400` |
| `application.dependencyHeadPosition` | Requerido, 1–150 | `400` |
| `application.activitiesOutline` | Requerido | `400` |
| Estudiante | Inscrito y sin otro SS activo/liberado | `422 social_services.student_not_eligible` |

Backend fija: `semesterEnrolled` y `application.currentSemester` = `student.currentSemester`; `requiredHours=480`; `accumulatedHours=0`. Respuesta: `201 SocialServiceDetail`.

## 4. Endpoints

| Método y ruta | Rol | Entrada | Transición / Efecto | Éxito | Errores |
| --- | --- | --- | --- | --- | --- |
| `GET /api/social-services` | Staff, lectura | `periodId`, `status`, `careerId`, `companyId`, `programId`, `search`, `sort` | — | `200 PagedResponse<SocialServiceListItem>` | — |
| `GET /api/social-services/me` | student | — | — | `200 SocialServiceListItem[]` | `404 students.profile_not_linked` |
| `GET /api/social-services/{id}` | Todos (propio) | — | — | `200 SocialServiceDetail` | `404` |
| `POST /api/social-services` | student, staff | `CreateSocialServiceRequest` | → `requested` | `201` | `400`, `422` |
| `PUT /api/social-services/{id}` | student (`requested`), staff (no terminal) | Igual a crear sin `studentId` | Reinicia dictamen a `null` si estaba rechazado | `200` | `409`, `422` |
| `POST .../{id}/application/review` | staff | `{ isAccepted, rejectionReason, officeObservations }` | Si `isAccepted`: `requested → approved`, genera folio `social_service_presentation_letter` (`presentationLetterFolio`) y fija `presentationLetterDate` | `200` | `409`, `400` |
| `POST .../{id}/presentation-letter` | staff | `{ presentationLetterDate }` | Emite o actualiza fecha/folio de `CP` | `200` | `409` |
| `POST .../{id}/acceptance` | staff | `{ acceptanceLetterDate }` | `approved → accepted` (exige documento `CA` aprobado) | `200` | `409`, `422 documents_missing` |
| `PUT .../{id}/commitment` | **student**, staff | `CommitmentRequest` | Guarda datos de Anexo XX para impresión prellenada | `200` | `400` |
| `POST .../{id}/activate` | staff | — | `accepted → active` (exige documento `CC` aprobado) | `200` | `409`, `422 documents_missing` |
| `PUT .../{id}/reports/{reportNumber}` | **student**, staff | `ReportRequest` (`reportNumber`: `1`\|`2`\|`3`) | Crea o actualiza los datos del Anexo XXII (para imprimirlo lleno y pedir firma/sello) | `200 BiMonthlyReport` | `400` |
| `POST .../{id}/reports/{reportId}/review` | staff | `{ status: "approved"\|"rejected", officeObservations }` | Opcional (también se aprueba automáticamente al aprobar el documento `R1`/`R2`/`R3`) | `200 BiMonthlyReport` | `400` |
| `PUT .../{id}/evaluations/{evaluatorType}/{bimonthlyPeriod}` | **student**, staff | `EvaluationRequest` | Guarda datos de Anexo XXIII (`external_supervisor`) o Anexo XXIV (`student_self`) para imprimirlos llenos y pedir firma/sello | `200 SocialServiceEvaluation` | `400` |
| `PUT .../{id}/activity-assessment` | **student**, staff | `ActivityAssessmentRequest` | Guarda datos de Anexo XXV para imprimirlo lleno y firmarlo | `200` | `400` |
| `PATCH .../{id}/impact` | staff | `{ beneficiariesCount, hasInclusionActivities, targetCommunity, observations }` | — | `200` | `400` |
| `POST .../{id}/complete` | staff | `{ terminationLetterDate }` | `active → completed_pending_review` (verifica documentos `R1..R3`, `EV1..EV3`, `CT` aprobados; **no bloquea por horas**) | `200` | `409`, `422 documents_missing` |
| `POST .../{id}/release` | staff | `{ finalGradeLevel?, finalScore?, originalTerminationLetterReceived }` | `completed_pending_review → released`; consume folio `social_service_release_certificate` (`SSC...`) | `200` | `409`, `422 documents_missing` |
| `POST .../{id}/school-services-act` | staff | — | En `released`: consume folio `school_services_act` (`DGTV...`) y marca acta enviada | `200` | `409 act_already_sent` |
| `POST .../{id}/cancel` | staff; student solo en `requested` | `{ reason }` | no terminal → `cancelled` | `200` | `409`, `400` |
| `POST .../{id}/non-compliance` | staff | `{ category, description }` | no terminal → `non_compliant` (+ PNC creado) | `200` | `409`, `400` |
| `GET .../{id}/history` | Todos (propio) | — | — | `200 StatusLogItem[]` | `404` |
| `GET .../{id}/print-data/{document}` | staff, student (propio) | `document`: `application`, `presentation_letter`, `acceptance_letter` (solo si `modality="internal"`), `commitment`, `report_1`, `report_2`, `report_3`, `evaluation_1`, `evaluation_2`, `evaluation_3`, `self_evaluation_1`, `self_evaluation_2`, `self_evaluation_3`, `activity_assessment`, `release_certificate` | — | `200` objeto plano con todos los datos llenos + autoridades activas + folio | `404`, `409 not_available` |

Prefijo de `code` de error: `social_services.`.

## 5. Sub-DTOs (Formularios de Prellenado para Impresión)

```ts
interface SocialServiceApplication {
  studentAddress: string; studentPhone: string; periodType: string; currentSemester: number;
  dependencyName: string; dependencyHeadName: string; dependencyHeadPosition: string;
  activitiesOutline: string;
  isAcceptedByOffice: boolean | null;   // null = sin dictamen
  rejectionReason: string | null; officeObservations: string | null;
  reviewedByName: string | null; reviewedAt: string | null;
}

interface CommitmentRequest {           // Anexo XX
  studentAddress: string;               // requerido (precargado de solicitud)
  studentPhone: string;                 // requerido, máx. 20
  dependencyAddress: string;            // requerido (precargado de empresa)
  commitmentDate: string;               // requerida
  city: string;                         // requerido, 1–100, default "Monclova"
}
interface SocialServiceCommitment extends CommitmentRequest {
  studentControlNumber: string;         // snapshot por backend
  isSignedByStudent: boolean;
  signedAt: string;
}

interface ReportRequest {               // Anexo XXII (lo llena el alumno para imprimirlo)
  periodStartDate: string;
  periodEndDate: string;
  activitiesSummary: string;
  reportedHours: number;                // ej. 160 por bimestre (solo para impresión del formato)
  evaluatorSupervisorId: number | null;
}
interface BiMonthlyReport extends ReportRequest {
  id: number;
  reportNumber: 1 | 2 | 3;
  accumulatedHours: number;             // suma automática de reportedHours
  status: "submitted" | "approved" | "rejected";
  officeObservations: string | null;
  approvedByName: string | null;
  approvedAt: string | null;
}

interface EvaluationRequest {           // Anexos XXIII (supervisor) y XXIV (autoevaluación)
  criteria: [QualitativeGrade, QualitativeGrade, QualitativeGrade, QualitativeGrade,
             QualitativeGrade, QualitativeGrade, QualitativeGrade]; // 7 criterios (default UI "excellent")
  evaluatorSupervisorId: number | null;
  observations: string | null;
}
interface SocialServiceEvaluation extends EvaluationRequest {
  id: number;
  evaluatorType: "external_supervisor" | "student_self";
  bimonthlyPeriod: "1" | "2" | "3" | "final";
}

interface ActivityAssessmentRequest {   // Anexo XXV
  questions: QualitativeGrade[];        // 8 reactivos (default UI "excellent")
  observations: string | null;
}
type ActivityAssessment = ActivityAssessmentRequest & { id: number; createdAt: string };
```

Textos oficiales de los 7 criterios y 8 reactivos: tomar literal de los PDF fuente en `docs/sources/servicio-social/formatos/` (Anexos XXIII, XXIV, XXV).

## 6. Reglas de negocio (Simplificadas al Proceso Real)

1. Un estudiante tiene máximo un SS no terminal; un SS `released` impide crear otro.
2. **Formularios para impresión libre de fricción:** El estudiante puede llenar o editar en cualquier momento del trámite activo los formularios de Carta Compromiso (`commitment`), Reportes 1–3 (`reports/{1..3}`), Evaluaciones (`evaluations`) y Evaluación de Actividades (`activity-assessment`) para descargar sus PDFs listos para firma y sello.
3. **Carta de Aceptación Local (`modality == "internal"`):** Cuando la dependencia es un departamento interno del TecNM (`company.isTecnmInternal = true`), `print-data/acceptance_letter` genera automáticamente la Carta de Aceptación usando la plantilla oficial (`Ejemplo Carta de Aceptación 1.docx`). El alumno igual la descarga, pide firma y sello del responsable interno y sube el PDF escaneado (`CA`).
4. **Progreso basado en documentos (sin bloqueo por horas ni calificaciones):**
   - El avance (`documentProgressPercentage`) se calcula sobre los 10 hitos de la hoja `PRINCIPAL CONSULTA` de `SEGUIMIENTO VINCULACIÓN1.xlsx`: `CP`, `CA`, `SOL`, `CC`, `R1`, `R2`, `R3`, `EV` (`EV1`–`EV3`), `CT` y `COT` (Constancia de liberación), sumando 10% por cada uno aprobado.
   - Aprobar un documento `R1`, `R2` o `R3` en el módulo de Documentos marca automáticamente como `approved` el registro correspondiente en `social_service_bi_monthly_reports` (si existe).
   - `complete` y `release` verifican únicamente que los documentos PDF requeridos estén subidos y aprobados (`approved`); **nunca bloquean por `accumulatedHours` ni por promedios numéricos**. En `release`, `finalGradeLevel` toma por defecto `"excellent"` si no se especifica, y `finalScore` es opcional.
5. `release` consume de forma atómica un folio de `social_service_release_certificate` (`SSC...`) y fija `releasedAt`.
6. `school-services-act` exige `released`; consume folio `school_services_act` (`DGTV...`); fija `schoolServicesActSent=true` y `schoolServicesActSentAt`.
7. Toda transición inserta `tramite_status_logs`.

## 7. Checklist documental (claves estables alineadas al Excel)

| Clave | Documento / Hito | Columna en `SEGUIMIENTO VINCULACIÓN1.xlsx` |
| --- | --- | --- |
| `imss_uploaded` | Constancia de Vigencia de Derechos IMSS | Requisito de apertura |
| `document_SOL` | Anexo XVIII — Solicitud de Servicio Social | `SOL.` |
| `presentation_letter` | Carta de Presentación generada (`CP`) | `C.P.` |
| `document_CA` | Carta de Aceptación firmada y sellada (generada por sistema si es local) | `C.A.` |
| `document_CC` | Anexo XX — Carta Compromiso firmada | `C.C.` |
| `document_R1` | Anexo XXII — Reporte Bimestral 1 firmado y sellado | `REP. 1` |
| `document_EV1` | Anexos XXIII/XXIV — Evaluación Cualitativa 1 firmada y sellada | `EV1` |
| `document_R2` | Anexo XXII — Reporte Bimestral 2 firmado y sellado | `REP. 2` |
| `document_EV2` | Anexos XXIII/XXIV — Evaluación Cualitativa 2 firmada y sellada | `EV2` |
| `document_R3` | Anexo XXII — Reporte Bimestral 3 firmado y sellado | `REP. 3` |
| `document_EV3` | Anexos XXIII/XXIV/XXV — Evaluación Cualitativa 3 y Actividades firmadas y selladas | `EV3` |
| `document_CT` | Anexo XIX — Carta de Terminación firmada y sellada | `CTA. TERM.` |
| `release_certificate` | Constancia de Terminación emitida por Vinculación (`SSC...`) | `CONST.TERM` |
| `school_services_act` | Acta enviada a Servicios Escolares (`DGTV...`) | `ACTA ENVIADA` |
