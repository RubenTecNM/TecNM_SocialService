# Contrato: Calidad (bitácora y Producto No Conforme)

> **Estado:** Borrador v0.1. Convenciones en [api-conventions.spec.md](./api-conventions.spec.md). Tablas: `tramite_status_logs`, `non_conformances`.

## Acceso

- Bitácora: lectura para quien puede leer el trámite. Sin escritura vía API (solo la generan las transiciones).
- PNC: `SocialServiceStaff` (SS), `InternshipStaff` (PP), `admin` (todo), `department_head` (lectura).

## 1. Bitácora de estados

```ts
interface StatusLogItem {
  id: number;
  tramiteType: "social_service" | "internship";
  tramiteId: number;
  previousStatus: TramiteStatus | null;
  newStatus: TramiteStatus;
  reason: string | null;
  changedById: number;
  changedByName: string;
  createdAt: string;
}
```

| Método y ruta | Entrada | Éxito |
| --- | --- | --- |
| `GET /api/tramite-status-logs` | `tramiteType`*, `tramiteId`* | `200 StatusLogItem[]` (orden `createdAt` asc) |

Reglas: append-only. Sin `PUT`, `PATCH` ni `DELETE`. `changed_by` = `sub` del JWT. Se inserta en la misma transacción que el cambio de estado.

## 2. Producto No Conforme — `/api/non-conformances`

```ts
interface NonConformance {
  id: number;
  incidentCode: string;          // PNC-SS-2026-001
  tramiteType: "social_service" | "internship";
  tramiteId: number;
  studentId: number;
  studentControlNumber: string;
  studentFullName: string;
  periodId: number;
  periodCode: string;
  category: NonConformanceCategory;
  description: string;
  status: "open" | "under_review" | "resolved" | "cancelled";
  actionPlan: string | null;
  resolvedAt: string | null;
  resolvedByName: string | null;
  createdAt: string;
  updatedAt: string;
}

interface CreateNonConformanceRequest {
  tramiteType: "social_service" | "internship";
  tramiteId: number;
  category: NonConformanceCategory;
  description: string;
  actionPlan: string | null;
}

interface UpdateNonConformanceStatusRequest {
  status: "under_review" | "resolved" | "cancelled";
  actionPlan: string | null;
}
```

| Campo | Regla |
| --- | --- |
| `tramiteType` | `social_service` o `internship`. `residency` → `400` hasta existir módulo |
| `tramiteId` | Trámite existente del tipo indicado |
| `category` | Valor del diccionario |
| `description` | Requerido, 1–4000 |
| `actionPlan` | Requerido al pasar a `resolved` |

| Método y ruta | Éxito | Errores |
| --- | --- | --- |
| `GET /api/non-conformances?periodId&tramiteType&status&category&search` | `200 PagedResponse<NonConformance>` | — |
| `GET /api/non-conformances/{id}` | `200 NonConformance` | `404` |
| `POST /api/non-conformances` | `201 NonConformance` | `400`, `404 tramite` |
| `PUT /api/non-conformances/{id}` | `{ category, description, actionPlan }` → `200` (solo `open`/`under_review`) | `409` |
| `POST /api/non-conformances/{id}/status` | `200 NonConformance` | `409 non_conformances.invalid_transition`, `400` |

Reglas:

- `studentId` y `periodId` se derivan del trámite; no se aceptan en request.
- `incidentCode` = `PNC-{SS|PP}-{YYYY}-{NNN}` generado por backend de forma atómica directo sobre `non_conformances` (`SELECT COALESCE(MAX(consecutive), 0) + 1 ... WHERE tramite_type = @type AND date_part('year', created_at) = @year`). No utiliza ni altera la tabla `document_series` (la cual queda reservada exclusivamente para constancias de acreditación/liberación y actas escolares).
- Contexto operativo: en el sistema legacy (`SEGUIMIENTO VINCULACIÓN1.xlsx`), PNC era una simple resta numérica global (`PNC = Solicitantes - Liberados`). En este sistema, cada PNC es un expediente formal auditable bajo la norma ISO 9001:2015 del TecNM con causa, descripción y plan de acción.
- Transiciones: `open → under_review | resolved | cancelled`; `under_review → resolved | cancelled`. `resolved` fija `resolvedAt` y `resolvedBy`.
- Crear PNC manual no cambia el estado del trámite. La acción `non-compliance` del trámite sí crea PNC automáticamente.

## Flujos UI esperados

- Bitácora como línea de tiempo dentro del detalle del trámite.
- Módulo "Calidad": listado PNC con filtros, detalle con plan de acción y botones de transición.
