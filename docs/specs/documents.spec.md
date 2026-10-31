# Contrato: Expediente documental

> **Estado:** Borrador v0.1. Convenciones en [api-conventions.spec.md](./api-conventions.spec.md). Tabla: `expedient_documents`.

## Acceso

- `student`: sube, lista y descarga sus documentos; borra solo `pending` propios.
- `SocialServiceStaff`: revisa `tramiteType` `social_service` y `general`.
- `InternshipStaff`: revisa `tramiteType` `internship` y `general`.
- `admin`: todo.

## DTOs

```ts
interface ExpedientDocument {
  id: number;
  studentId: number;
  tramiteType: "social_service" | "internship" | "general";
  tramiteId: number | null;
  documentCode: DocumentCode;
  originalFileName: string;
  fileMimeType: string;
  fileSizeBytes: number;
  status: "pending" | "approved" | "rejected" | "superseded";
  rejectionReason: string | null;
  reviewedByName: string | null;
  reviewedAt: string | null;
  createdAt: string;
}

interface ReviewDocumentRequest {
  status: "approved" | "rejected";
  rejectionReason: string | null;
}
```

`storage_path` nunca se expone. Descarga solo vía endpoint autenticado.

## Endpoints

| Método y ruta | Entrada | Éxito | Errores |
| --- | --- | --- | --- |
| `GET /api/students/{studentId}/documents` | `tramiteType`, `tramiteId`, `status`, `includeSuperseded` (default `false`) | `200 ExpedientDocument[]` | `404` |
| `POST /api/students/{studentId}/documents` | `multipart/form-data`: `file`, `tramiteType`, `tramiteId?`, `documentCode` | `201 ExpedientDocument` | `400`, `404`, `413`, `415`, `422 documents.tramite_mismatch`, `409 documents.already_approved` |
| `GET /api/documents/{id}/file` | — | Stream con `Content-Disposition` | `404` |
| `POST /api/documents/{id}/review` | `ReviewDocumentRequest` | `200 ExpedientDocument` | `400`, `404`, `409 documents.invalid_transition` |
| `DELETE /api/documents/{id}` | — | `204` | `409 documents.not_pending` |
| `GET /api/documents/review-queue` | `tramiteType`, `documentCode`, `periodId`, `page`, `pageSize` | `200 PagedResponse<ExpedientDocument & { studentControlNumber: string; studentFullName: string }>` | `403` |

Alias para estudiante: `studentId` puede ser `me` (`/api/students/me/documents`).

## Reglas

- Archivos: PDF (`application/pdf`) ≤ 5 MB. Se valida tipo real por firma, no solo extensión.
- `tramiteType ≠ general` exige `tramiteId` de un trámite existente del mismo `studentId`.
- `tramiteType = general` exige `tramiteId = null`.
- Nueva carga con mismo (`studentId`, `tramiteType`, `tramiteId`, `documentCode`) marca la anterior `pending`/`rejected` como `superseded`. Si existe una `approved`, responde `409 documents.already_approved`.
- `rejected` exige `rejectionReason` (1–1000).
- Transiciones: `pending → approved | rejected`. `approved`, `rejected`, `superseded` no cambian por revisión.
- Almacenamiento v1: sistema de archivos local bajo `Storage__RootPath`; `storage_path` relativo `{studentId}/{tramiteType}/{guid}.pdf`.

## Documentos esperados por trámite (alineados con `SEGUIMIENTO VINCULACIÓN1.xlsx`)

| Trámite | Código | Formato / Descripción | Cómo se genera y entrega |
| --- | --- | --- | --- |
| **SS** | `SOL` | Anexo XVIII — Solicitud de Servicio Social | El alumno llena el formulario de apertura en el sistema; descarga el PDF generado, o se registra su solicitud, y Vinculación la valida (`SI`). |
| **SS** | `CP` | Carta de Presentación (`SS_CP...`) | Se genera automáticamente mediante plantilla al aprobar la apertura. El alumno la descarga y la entrega en la dependencia. |
| **SS** | `CA` | Carta de Aceptación | Si es **externo**, la emite la dependencia en hoja membretada. Si es **local en el TecNM** (`isTecnmInternal=true`), el sistema la genera automáticamente con plantilla; el alumno igual la descarga, pide firma y sello del jefe del depto interno y sube el PDF firmado y sellado. |
| **SS** | `CC` | Anexo XX — Carta Compromiso | El alumno llena sus datos en el formulario del sistema, imprime el PDF prellenado, lo firma y sube el PDF escaneado. |
| **SS** | `R1`, `R2`, `R3` | Anexo XXII — Reportes Bimestrales 1, 2 y 3 | El alumno llena el formulario de cada reporte en el sistema, imprime el PDF ya con todos los datos, recaba firma y sello del supervisor y sube el PDF escaneado. |
| **SS** | `EV1`, `EV2`, `EV3` | Anexos XXIII, XXIV y XXV — Evaluaciones Cualitativas | El alumno llena los datos en el formulario del sistema, imprime los formatos ya llenos, recaba firma y sello y sube el PDF escaneado por bimestre/final. |
| **SS** | `CT` | Anexo XIX — Carta de Terminación | Emitida por la dependencia (con firma y sello). El alumno la sube escaneada para liberación. |
| **PP** | `CP` | Carta de Presentación (`EJ26/PR...`) | Se genera automáticamente con plantilla cuando el alumno solicita apertura adjuntando su Vigencia IMSS. |
| **PP** | `CA` | Oficio de Aceptación | En hoja membretada con firma y sello (la empresa la envía a `arturo.martinez@monclova.tecnm.mx` y/o el alumno la sube al sistema). |
| **PP** | `EV` | Anexo XVII — Evaluación de Actividad Complementaria | El alumno llena/descarga la plantilla prellenada en el sistema, recaba calificación, firma y sello de la empresa, y sube el PDF escaneado. |
| **PP** | `CT` | Carta / Oficio de Terminación de Prácticas | En hoja membretada de la empresa con firma y sello; el alumno la sube escaneada. |

> **Impacto estadístico directo:** Cada vez que un documento cambia a `approved` (o se emite `CP`), los contadores de la sábana `SEGUIMIENTO VINCULACIÓN` y el tablero `ESTADÍSTICO` (`En condiciones`, `Solicitantes`, `Liberados`, `% de avance`) se actualizan automáticamente en tiempo real.

## Flujos UI esperados

- Estudiante: lista tipo checklist por trámite donde cada documento tiene dos acciones claras: **"1. Llenar e Imprimir Formato"** (si tiene plantilla del sistema) y **"2. Subir PDF firmado y sellado"**.
- Staff: bandeja de revisión con visor PDF embebido para comprobar que el documento esté lleno, firmado y sellado, y botón Aprobar / Rechazar con motivo.
