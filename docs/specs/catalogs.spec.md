# Contrato: Catálogos base

> **Estado:** Implementado v1.0. Convenciones en [api-conventions.spec.md](./api-conventions.spec.md). Tablas: `careers`, `academic_periods`, `social_service_programs`, `institutional_authorities`, `document_series`.

## Acceso

| Recurso | Lectura (`GET`, `/options`) | Escritura |
| --- | --- | --- |
| Carreras | Autenticado | `Admin` |
| Periodos | Autenticado | `Admin` |
| Programas SS | Autenticado | `SocialServiceStaff` |
| Autoridades | `Staff` | `Admin` |
| Series de folio | `Staff` | `Admin` |

---

## 1. Carreras — `/api/careers`

```ts
interface Career {
  id: number;
  code: string;          // INF, IND, MEC, ELE, GE, ER
  name: string;
  totalCredits: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

interface CareerRequest {
  code: string;
  name: string;
  totalCredits: number;  // default UI: 260
}
```

| Campo | Regla |
| --- | --- |
| `code` | Requerido, 1–16, mayúsculas `^[A-Z0-9_-]+$`, único (`careers.duplicated_code`) |
| `name` | Requerido, 1–150 |
| `totalCredits` | Entero > 0 |

| Método y ruta | Éxito | Errores |
| --- | --- | --- |
| `GET /api/careers?search&isActive&sort=code\|name` | `200 PagedResponse<Career>` | — |
| `GET /api/careers/options` | `200 OptionItem[]` (`label` = `"INF · Ingeniería Informática"`, `meta.code`) | — |
| `GET /api/careers/{id}` | `200 Career` | `404` |
| `POST /api/careers` | `201 Career` | `400`, `409` |
| `PUT /api/careers/{id}` | `200 Career` | `400`, `404`, `409` |
| `PATCH /api/careers/{id}/status` | `200 Career` | `404` |
| `DELETE /api/careers/{id}` | `204` | `404`, `409 careers.in_use` (tiene estudiantes) |

## 2. Periodos académicos — `/api/academic-periods`

```ts
interface AcademicPeriod {
  id: number;
  code: string;      // AD25, FJ26
  name: string;      // "Agosto - Diciembre 2025"
  startDate: string;
  endDate: string;
  isActive: boolean; // periodo en curso
  createdAt: string;
  updatedAt: string;
}

interface AcademicPeriodRequest {
  code: string;
  name: string;
  startDate: string;
  endDate: string;
}
```

| Campo | Regla |
| --- | --- |
| `code` | Requerido, 1–32, mayúsculas, sugerido `^[A-Z]{2}\d{2}$`, único (`academic_periods.duplicated_code`) |
| `name` | Requerido, 1–100 |
| `startDate`, `endDate` | Requeridas; `startDate ≤ endDate` |

| Método y ruta | Éxito | Errores |
| --- | --- | --- |
| `GET /api/academic-periods?search&isActive&sort=-startDate` | `200 PagedResponse<AcademicPeriod>` | — |
| `GET /api/academic-periods/options` | `200 OptionItem[]` (orden `-startDate`, `meta.isActive`) | — |
| `GET /api/academic-periods/current` | `200 AcademicPeriod` | `404 academic_periods.no_current` |
| `GET /api/academic-periods/{id}` | `200 AcademicPeriod` | `404` |
| `POST /api/academic-periods` | `201 AcademicPeriod` (inactivo) | `400`, `409` |
| `PUT /api/academic-periods/{id}` | `200 AcademicPeriod` | `400`, `404`, `409` |
| `POST /api/academic-periods/{id}/activate` | `200 AcademicPeriod` | `404` |
| `DELETE /api/academic-periods/{id}` | `204` | `404`, `409 academic_periods.in_use` |

Regla: solo un periodo con `isActive=true`. `activate` desactiva el resto en la misma transacción. `PATCH /status` no existe para periodos.

## 3. Programas de Servicio Social — `/api/social-service-programs`

```ts
interface SocialServiceProgram {
  id: number;
  code: string;
  name: string;
  category: SocialServiceProgramCategory; // ver diccionario de enums
  isInternal: boolean;
  description: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

interface SocialServiceProgramRequest {
  code: string;
  name: string;
  category: SocialServiceProgramCategory;
  isInternal: boolean;
  description: string | null;
}
```

| Campo | Regla |
| --- | --- |
| `code` | Requerido, 1–32, mayúsculas, único (`social_service_programs.duplicated_code`) |
| `name` | Requerido, 1–200 |
| `category` | Uno de los 9 valores oficiales |
| `description` | Opcional |

Endpoints CRUD estándar + `/options` (`meta.category`, `meta.isInternal`) + `PATCH /status`. `DELETE` → `409 social_service_programs.in_use` si hay trámites.

## 4. Autoridades institucionales — `/api/institutional-authorities`

```ts
interface InstitutionalAuthority {
  id: number;
  roleKey: AuthorityRoleKey;
  title: string | null;        // "M.I.", "Ing."
  fullName: string;
  position: string;
  email: string | null;
  phoneNumber: string | null;
  signatureInitials: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

type InstitutionalAuthorityRequest = Omit<InstitutionalAuthority, "id" | "isActive" | "createdAt" | "updatedAt">;
```

| Campo | Regla |
| --- | --- |
| `roleKey` | Requerido; valor de diccionario `institutional_authorities.role_key` |
| `title` | Opcional, máx. 32 |
| `fullName` | Requerido, 1–180 |
| `position` | Requerido, 1–180 |
| `email` | Opcional, email, máx. 255 |
| `phoneNumber` | Opcional, máx. 30 |
| `signatureInitials` | Opcional, máx. 16 |

Endpoints CRUD estándar + `PATCH /status`. Extra: `GET /api/institutional-authorities/active` → `200 Record<AuthorityRoleKey, InstitutionalAuthority | null>` para armar PDFs.

Regla: un solo registro activo por `roleKey`. Activar uno desactiva el anterior del mismo `roleKey` (sucesión de firmantes sin perder histórico).

## 5. Series de folio — `/api/document-series`

```ts
interface DocumentSeries {
  id: number;
  documentType: DocumentSeriesType;
  prefix: string;            // SSC, PPC, DGTV
  currentYear: number;
  currentSequence: number;   // último emitido
  formatPattern: string;     // "{PREFIX}{SEQ:4}/{DD}/{MM}/{YYYY}"
  description: string | null;
  nextFolioPreview: string;  // calculado, no persiste
  createdAt: string;
  updatedAt: string;
}

interface DocumentSeriesRequest {
  documentType: DocumentSeriesType;
  prefix: string;
  currentYear: number;
  currentSequence: number;
  formatPattern: string;
  description: string | null;
}
```

| Campo | Regla |
| --- | --- |
| `documentType` | Uno de 5 valores (`social_service_presentation_letter`, `internship_presentation_letter`, `social_service_release_certificate`, `internship_release_certificate`, `school_services_act`); único por año (`document_series.duplicated_type_year`) |
| `prefix` | Requerido, 1–20 (`SS_CP`, `PR`, `SSC`, `PPC`, `DGTV`) |
| `currentYear` | Entero ≥ 2020 |
| `currentSequence` | Entero ≥ 0. Solo editable si no reduce el valor actual (`422 document_series.sequence_rollback`) |
| `formatPattern` | Requerido, 1–100. Tokens válidos: `{PREFIX}`, `{SEQ:n}`, `{DD}`, `{MM}`, `{YYYY}` |
| `description` | Opcional, máx. 255 |

| Método y ruta | Éxito | Errores |
| --- | --- | --- |
| `GET /api/document-series?documentType&currentYear` | `200 DocumentSeries[]` | — |
| `GET /api/document-series/{id}` | `200 DocumentSeries` | `404` |
| `POST /api/document-series` | `201 DocumentSeries` | `400`, `409` |
| `PUT /api/document-series/{id}` | `200 DocumentSeries` | `400`, `404`, `422` |

Sin `DELETE`. No existe endpoint público para consumir folio: solo lo consumen acciones `presentation-letter`, `release` y `school-services-act` de los trámites, de forma atómica. Si no existe serie para el año en curso, backend la crea copiando `prefix` y `formatPattern` del año anterior con `currentSequence=0`.

## Flujos UI esperados

- Pantalla "Catálogos" con pestañas: Carreras, Periodos, Programas SS, Autoridades, Folios.
- Tabla paginada + drawer/modal de alta/edición + toggle de estado.
- Periodos: botón "Marcar como vigente"; badge en el periodo activo.
- Folios: mostrar `nextFolioPreview`; advertir antes de editar secuencia.
