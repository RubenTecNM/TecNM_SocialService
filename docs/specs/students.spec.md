> Contrato de autorización vigente: [Acceso por proceso](./process-access.spec.md). Prevalece sobre permisos anteriores incompatibles.

> **Contrato vigente:** [Flujo documental v2](./document-workflow-v2.spec.md). Sus reglas sustituyen los comportamientos anteriores que contradigan IMSS, permisos, firmas, generación y evaluaciones. El resto se conserva.

# Contrato: Estudiantes e importación

> **Estado:** Borrador v0.1. Convenciones en [api-conventions.spec.md](./api-conventions.spec.md). Tablas: `students`, `student_import_batches`.

## Acceso

- `department_head`: CRUD global, importación, validación IMSS, creación y reseteo de cuentas.
- `social_service_officer`: lectura de su proceso y seguimiento SS, alta manual, importación Excel y validación IMSS.
- `internship_coordinator`: lectura de su proceso, alta manual y validación IMSS.
- `student`: solo `/api/students/me*`.

## DTOs

```ts
interface StudentListItem {
  id: number;
  controlNumber: string;
  fullName: string;          // first_name first_surname second_surname
  careerId: number;
  careerCode: string;
  currentSemester: number;
  curriculumProgressPercentage: number;
  shift: StudentShift;
  isEnrolled: boolean;
  hasValidImss: boolean;
  hasUserAccount: boolean;
  accountUsername: string | null;
  accountStatus: "missing" | "active" | "inactive" | "inconsistent";
}

interface StudentDetail {
  id: number;
  userId: number | null;
  careerId: number;
  careerName: string;
  importBatchId: number | null;
  controlNumber: string;
  curp: string | null;
  firstName: string;
  firstSurname: string;
  secondSurname: string | null;
  gender: "M" | "F" | null;
  shift: StudentShift;
  hasDisability: boolean;
  disabilityDescription: string | null;
  institutionalEmail: string | null;
  personalEmail: string | null;
  phoneNumber: string | null;
  currentSemester: number;
  completedCredits: number;
  curriculumProgressPercentage: number;
  isEnrolled: boolean;
  hasValidImss: boolean;
  hasImssFile: boolean;
  imssValidatedAt: string | null;
  imssValidatedByName: string | null;
  createdAt: string;
  updatedAt: string;
}

interface StudentRequest {
  careerId: number;
  controlNumber: string;
  curp: string | null;
  firstName: string;
  firstSurname: string;
  secondSurname: string | null;
  gender: "M" | "F" | null;
  shift: StudentShift;
  hasDisability: boolean;
  disabilityDescription: string | null;
  institutionalEmail: string | null;
  personalEmail: string | null;
  phoneNumber: string | null;
  currentSemester: number;
  completedCredits: number;
  isEnrolled: boolean;
}

interface StudentContactRequest {   // autoservicio del estudiante (y pre-paso de solicitud PP/SS)
  personalEmail: string | null;
  institutionalEmail: string | null;
  phoneNumber: string | null;
  currentSemester?: number;
  shift?: StudentShift;
}

interface EligibilityResult {
  eligible: boolean;
  reasons: { code: string; message: string }[]; // vacío si eligible
}

interface StudentEligibility {
  socialService: EligibilityResult;
  internship: EligibilityResult;
}
```

`curriculumProgressPercentage` lo calcula backend: `round(completedCredits / career.totalCredits * 100, 2)`, tope 100. No se acepta en request.

## Validación

| Campo | Regla |
| --- | --- |
| `careerId` | Requerido; carrera activa |
| `controlNumber` | Requerido, 1–32, mayúsculas/dígitos, único (`students.duplicated_control_number`) |
| `curp` | Opcional, 18, regex CURP |
| `firstName` | Requerido, 1–100 |
| `firstSurname` | Requerido, 1–70 |
| `secondSurname` | Opcional, máx. 70 |
| `gender` | `M`, `F` o `null` |
| `shift` | Requerido; default `morning` |
| `hasDisability` | Booleano |
| `disabilityDescription` | Máx. 150. Requerido si `hasDisability=true`; se limpia a `null` si `false` |
| `institutionalEmail`, `personalEmail` | Opcionales, email, máx. 255 |
| `phoneNumber` | Opcional, máx. 20 |
| `currentSemester` | Entero 1–15 |
| `completedCredits` | Entero ≥ 0 y ≤ `career.totalCredits` (`422 students.credits_exceed_total`) |

## Endpoints

| Método y ruta | Entrada | Éxito | Errores |
| --- | --- | --- | --- |
| `GET /api/students` | `search` (matrícula, nombre, CURP), `careerId`, `shift`, `isEnrolled`, `hasValidImss`, `hasDisability`, `sort` (`controlNumber`, `firstSurname`, `curriculumProgressPercentage`) | `200 PagedResponse<StudentListItem>` | — |
| `GET /api/students/options?search` | Máx. 20 resultados | `200 OptionItem[]` (`label` = `"Matrícula · Nombre"`) | — |
| `GET /api/students/{id}` | — | `200 StudentDetail` | `404` |
| `POST /api/students` | `StudentRequest` | `201 StudentDetail` | `400`, `409`, `422` |
| `PUT /api/students/{id}` | `StudentRequest` | `200 StudentDetail` | `400`, `404`, `409`, `422` |
| `DELETE /api/students/{id}` | — | `204` | `409 students.in_use` (tiene trámites) |
| `GET /api/students/{id}/eligibility` | — | `200 StudentEligibility` | `404` |
| `POST /api/students/{id}/account` | — | `201 { userId, username, accountStatus }` | `409 students.account_exists`, `400 validation.failed` |
| `PUT /api/students/{id}/imss` | `multipart/form-data` `file` (PDF/JPG/PNG ≤ 5 MB) | `200 StudentDetail` (reinicia `hasValidImss=false`) | `413`, `415` |
| `GET /api/students/{id}/imss` | — | Stream archivo | `404` |
| `POST /api/students/{id}/imss/validation` | `{ isValid: boolean }` | `200 StudentDetail` | `422 students.imss_file_missing` |
| `GET /api/students/me` | — | `200 StudentDetail` | `404 students.profile_not_linked` |
| `PUT /api/students/me/contact` | `StudentContactRequest` | `200 StudentDetail` | `400` |
| `PUT /api/students/me/imss` | Igual que staff | `200 StudentDetail` | `413`, `415` |
| `GET /api/students/me/eligibility` | — | `200 StudentEligibility` | `404` |

Cuenta de estudiante: `username = email = institutionalEmail`, normalizado a minúsculas, válido y máximo 254 caracteres; rol exclusivo `student`. Contraseña habitual inicial: matrícula en mayúsculas, almacenada mediante hash; sin cambio obligatorio. No fabricar correos ni reutilizar cuentas por coincidencia.

`POST /api/students/{id}/account` devuelve `{ userId, username, accountStatus }`. `POST /api/students/{id}/account/reset-password`, exclusivo `department_head`, restablece a matrícula y devuelve 204; invalida sesiones. Crear cuenta, rol y vínculo es atómico. El padrón añade `accountUsername` y `accountStatus`: `missing`, `active`, `inactive`, `inconsistent`. Solo `department_head` modifica correo institucional; corrección sincroniza identidad y revoca sesiones. Las contraseñas existentes se conservan salvo restablecimiento explícito.

## Elegibilidad

El padrón de estudiantes se alimenta cargando el Excel de alumnos que ya pueden realizar el servicio (`POST /api/student-imports`) o agregando un caso específico manualmente con el botón "Agregar alumno" (`POST /api/students`).

| Trámite | Regla | `code` si falla |
| --- | --- | --- |
| SS | `isEnrolled = true` | `eligibility.not_enrolled` |
| SS | Sin SS en estado no terminal ni `released` | `eligibility.social_service_exists` |
| PP | `isEnrolled = true` | `eligibility.not_enrolled` |
| PP | Sin PP en estado no terminal ni `released` | `eligibility.internship_exists` |

- Si en el Excel o en el alta manual no se indican `completedCredits` (o se envía `0`), el backend asigna por defecto `156` créditos (`60.00%` de avance reticular) cuando el alumno proviene del listado de candidatos a Servicio Social, sin bloquear la apertura del trámite por falta de conteo exacto de kárdex.
- La **Constancia de Vigencia de Derechos del IMSS** se sube directamente dentro del asistente de apertura de Servicio Social o de solicitud de Carta de Presentación de Prácticas Profesionales (`PUT /api/students/me/imss`).

## Importación masiva — `/api/student-imports`

```ts
interface StudentImportBatch {
  id: number;
  periodId: number;
  periodCode: string;
  fileName: string;
  totalRecords: number;
  importedRecords: number;
  createdAccounts: number;
  existingAccounts: number;
  failedRecords: number;
  status: "pending" | "processing" | "completed" | "failed";
  notes: string | null;
  createdAt: string;
  createdByName: string | null;
}

interface StudentImportResult extends StudentImportBatch {
  rowErrors: { row: number; controlNumber: string | null; field: string | null; message: string }[];
}
```

| Método y ruta | Entrada | Éxito | Errores |
| --- | --- | --- | --- |
| `GET /api/student-imports/template` | — | Archivo `.xlsx` plantilla | — |
| `POST /api/student-imports` | `multipart/form-data`: `file` (`.xlsx` ≤ 5 MB), `periodId` | `201 StudentImportResult` | `400`, `413`, `415`, `422 student_imports.invalid_layout` |
| `GET /api/student-imports?periodId&status` | — | `200 PagedResponse<StudentImportBatch>` | — |
| `GET /api/student-imports/{id}` | — | `200 StudentImportResult` | `404` |

Columnas de plantilla: `control_number`*, `first_surname`*, `second_surname`, `first_name`*, `career_code`*, `curp`, `gender`, `shift`, `current_semester`, `completed_credits`, `institutional_email`, `personal_email`, `phone_number`, `has_disability`, `disability_description`. (`*` requeridas; `current_semester` default `7`, `completed_credits` default `156`).

Reglas: procesamiento síncrono. Upsert por matrícula; cada fila guarda alumno y cuenta en una transacción. `institutional_email` es obligatorio. Si ya tiene cuenta, conserva contraseña, roles y actividad; correo actualizado sincroniza identidad. Una colisión no modifica ninguna cuenta ajena. Primera fila guardada por matrícula/correo prevalece; duplicados posteriores fallan. Respuesta y lote agregan `createdAccounts` y `existingAccounts`. Fila inválida no aborta el lote; se reporta en `rowErrors`. Errores generales finalizan lote como `failed` con contadores reales; éxito parcial normal usa `completed`. IMSS e historial se conservan.

## Flujos UI esperados

- Staff: padrón con filtros, botón **"Agregar alumno"** para casos específicos y botón **"Importar Excel"** para carga masiva de candidatos; detalle con pestañas Datos · Trámites · Documentos · IMSS.
- Staff: asistente de importación (seleccionar periodo → subir Excel → resumen con errores por fila exportable).
- Estudiante: "Mi perfil" con datos académicos, datos de contacto editables, carga de Constancia de Vigencia IMSS y acceso directo a solicitar Servicio Social o Prácticas Profesionales.

## Seguimiento semestral SS

El backend calcula `socialServiceFollowUp` independientemente del estado del trámite. Sin PDF aportado por el alumno y aprobado en el expediente SS vigente: periodo inicial = `first_notice`, uno posterior = `second_notice`, dos = `third_notice`, tres o más = `non_compliant`. Contar periodos por fecha; no usar semestre escolar. IMSS, origen system, borrados y sustituidos no acreditan avance. Con evidencia aprobada: `in_progress`, incluso antes de activar. Liberado prevalece; PNC formal se conserva si no existe nuevo trámite vigente. Cancelados no aportan evidencia. PNC del padrón no crea casos de Calidad ni cierra trámites.

Guardar `socialServiceEligiblePeriodId` al crear: periodo del Excel o selección manual (default activo). Reimportar nunca lo modifica. Admin puede corregirlo; registrar autor y fecha. Registros anteriores sin origen comprobado quedan `pending_setup`; no inferir origen del último lote. Sin periodo activo tampoco calcular avisos.

`GET /api/students` agrega `socialServiceFollowUp` (status, eligiblePeriodId/Code, trackingPeriodId/Code, elapsedSemesters, socialServiceId, approvedDocumentsCount, lastApprovedAt, progressAssessment, autoReason). Filtros `followUpStatus` y `followUpProgress` antes de paginar. Valores de valoración: `consistent`, `low_progress`; sin periodo activo queda `pending` (`null`). Cuando `status` es `in_progress`, el backend calcula `progressAssessment` automáticamente: `consistent` si el trámite está `completed_pending_review`, inició hace ≤ 60 días o tuvo evidencia aprobada dentro del periodo activo o en los últimos 75 días; `low_progress` en caso contrario. No existe validación manual del seguimiento. Poco avance no reactiva avisos; dos semestres son referencia, no límite.

El coordinador PP no recibe seguimiento SS. Encargado SS consulta candidatos sin trámite mediante resumen: identidad, carrera y seguimiento; otros campos nulos, sin habilitar perfil completo. `canReadProfile` indica acceso a las acciones existentes.

UI: columna Seguimiento SS, valoración automática (sin sufijo `(automático)` ni opción de validación manual), periodo inicial, criterio, motivo y filtros. Mostrar periodo activo. Conservar pendientes de cohortes anteriores. Textos y colores accesibles. Sin cortes históricos inmutables.

## Padrón compacto y detalle (vigente)

Una sola tabla en `/students`: nombre, matrícula, carrera/semestre, seguimiento SS (según rol) y Ver detalle. La cola IMSS de preingreso permanece en `/expedients/review`; no se monta ni consulta automáticamente en el padrón. El detalle reutiliza UiDialog, carga datos completos solo para perfiles autorizados y concentra datos personales, académicos, contacto, cuenta, IMSS y seguimiento. Candidatos restringidos muestran únicamente el resumen permitido, sin peticiones al perfil. Acciones de editar, cuentas y dictamen IMSS quedan dentro del detalle. Filtros principales: búsqueda, carrera y seguimiento; turno/IMSS/constancia dentro de Más filtros. Tabla sin cortes en escritorio, presentación por filas compactas en móvil. Mostrar carga, error recuperable, vacío, paginación y cierre por Escape con retorno de foco. Mantener datos y permisos existentes.

## Errores por campo

Conservar reglas actuales. Propagar errors del servidor, normalizar rutas JSON y mostrar todos los mensajes junto al campo y dentro del formulario. Conservar valores al fallar; reiniciar errores al cambiar registro. No añadir campos ni endurecer correo/CURP.
