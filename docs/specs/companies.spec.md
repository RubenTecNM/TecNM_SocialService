> Contrato de autorización vigente: [Acceso por proceso](./process-access.spec.md). Prevalece sobre permisos anteriores incompatibles.

# Contrato: Empresas, supervisores y convenios

> **Estado:** Borrador v0.1. Convenciones en [api-conventions.spec.md](./api-conventions.spec.md). Tablas: `companies`, `company_supervisors`, `agreements`.

## Acceso

- Lectura y escritura completa: `Staff` (`department_head`, `social_service_officer`, `internship_coordinator`).
- `student`: solo `/options` de empresas, supervisores y convenios vigentes (para llenar su solicitud).

---

## 1. Empresas — `/api/companies`

```ts
interface Company {
  id: number;
  rfc: string | null;
  name: string;
  commercialName: string | null;
  sector: CompanySector;
  companySize: CompanySize;
  isTecnmInternal: boolean;
  address: string | null;
  city: string;
  state: string;
  postalCode: string | null;
  phoneNumber: string | null;
  email: string | null;
  isActive: boolean;
  activeSupervisorsCount: number;   // calculado
  hasValidAgreement: boolean;       // calculado: algún convenio vigente hoy
  createdAt: string;
  updatedAt: string;
}

type CompanyRequest = Omit<Company,
  "id" | "isActive" | "activeSupervisorsCount" | "hasValidAgreement" | "createdAt" | "updatedAt">;
```

| Campo | Regla |
| --- | --- |
| `rfc` | Opcional, 12–13, mayúsculas, regex RFC |
| `name` | Requerido, 1–200 |
| `commercialName` | Opcional, máx. 200 |
| `sector` | Requerido; default UI `private` |
| `companySize` | Requerido; default UI `not_applicable`. Obligatorio distinto de `not_applicable` si `sector=private` (regla 911, `422 companies.size_required`) |
| `isTecnmInternal` | Booleano; si `true`, sugerir `sector=educational` |
| `address` | Opcional |
| `city` / `state` | Requeridos, 1–100; default `Monclova` / `Coahuila` |
| `postalCode` | Opcional, `^\d{5}$` |
| `phoneNumber` | Opcional, máx. 30 |
| `email` | Opcional, email, máx. 255 |

| Método y ruta | Éxito | Errores |
| --- | --- | --- |
| `GET /api/companies?search&sector&companySize&isTecnmInternal&isActive&sort=name` | `200 PagedResponse<Company>` | — |
| `GET /api/companies/options?isTecnmInternal` | `200 OptionItem[]` (`meta.isTecnmInternal`, `meta.hasValidAgreement`) | — |
| `GET /api/companies/{id}` | `200 Company` | `404` |
| `POST /api/companies` | `201 Company` | `400`, `409 companies.duplicated_rfc` (validación de app; RFC no es UNIQUE en BD) |
| `PUT /api/companies/{id}` | `200 Company` | `400`, `404`, `409` |
| `PATCH /api/companies/{id}/status` | `200 Company` | `404` |
| `DELETE /api/companies/{id}` | `204` | `404`, `409 companies.in_use` |

`search` busca en `name`, `commercialName`, `rfc`.

## 2. Supervisores — `/api/companies/{companyId}/supervisors`

Ruta anidada: garantiza que el supervisor pertenece a la empresa (la BD no lo cruza con trámites).

```ts
interface CompanySupervisor {
  id: number;
  companyId: number;
  fullName: string;
  position: string;
  department: string | null;
  email: string | null;
  phoneNumber: string | null;
  isSignatory: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

type CompanySupervisorRequest = Omit<CompanySupervisor,
  "id" | "companyId" | "isActive" | "createdAt" | "updatedAt">;
```

| Campo | Regla |
| --- | --- |
| `fullName` | Requerido, 1–180 |
| `position` | Requerido, 1–150 |
| `department` | Opcional, máx. 150 |
| `email` | Opcional, email, máx. 255 |
| `phoneNumber` | Opcional, máx. 30 |
| `isSignatory` | Booleano; default `true` |

| Método y ruta | Éxito | Errores |
| --- | --- | --- |
| `GET .../supervisors?isActive` | `200 CompanySupervisor[]` | `404` empresa |
| `GET .../supervisors/options` | `200 OptionItem[]` (`label` = `"Nombre · Puesto"`, `meta.isSignatory`) | `404` |
| `POST .../supervisors` | `201 CompanySupervisor` | `400`, `404` |
| `PUT .../supervisors/{id}` | `200 CompanySupervisor` | `400`, `404` |
| `PATCH .../supervisors/{id}/status` | `200 CompanySupervisor` | `404` |
| `DELETE .../supervisors/{id}` | `204` | `404`, `409 company_supervisors.in_use` |

Supervisores no son usuarios del sistema. Sus evaluaciones las captura personal de Vinculación a partir del formato firmado.

## 3. Convenios — `/api/agreements`

```ts
interface Agreement {
  id: number;
  companyId: number;
  companyName: string;
  agreementNumber: string;
  agreementType: AgreementType;
  description: string | null;
  signedDate: string;
  expirationDate: string;
  isActive: boolean;
  hasDocument: boolean;
  validity: "valid" | "expiring" | "expired" | "inactive"; // calculado
  daysToExpiration: number;                                 // negativo si venció
  createdAt: string;
  updatedAt: string;
}

interface AgreementRequest {
  companyId: number;
  agreementNumber: string;
  agreementType: AgreementType;
  description: string | null;
  signedDate: string;
  expirationDate: string;
}
```

`validity`: `inactive` si `isActive=false`; `expired` si hoy > `expirationDate`; `expiring` si vence en ≤ 60 días; si no, `valid`.

| Campo | Regla |
| --- | --- |
| `companyId` | Requerido; empresa activa |
| `agreementNumber` | Requerido, 1–64, único (`agreements.duplicated_number`) |
| `agreementType` | Requerido; default UI `general_framework` |
| `signedDate`, `expirationDate` | Requeridas; `expirationDate ≥ signedDate` |

| Método y ruta | Éxito | Errores |
| --- | --- | --- |
| `GET /api/agreements?companyId&agreementType&validity&search&sort=expirationDate` | `200 PagedResponse<Agreement>` | — |
| `GET /api/agreements/options?companyId&tramiteType&onDate` | `200 OptionItem[]` vigentes en `onDate` y compatibles con trámite | `400` |
| `GET /api/agreements/{id}` | `200 Agreement` | `404` |
| `POST /api/agreements` | `201 Agreement` | `400`, `409` |
| `PUT /api/agreements/{id}` | `200 Agreement` | `400`, `404`, `409` |
| `PATCH /api/agreements/{id}/status` | `200 Agreement` | `404` |
| `PUT /api/agreements/{id}/document` | `multipart/form-data` `file` (PDF ≤ 10 MB) → `200 Agreement` | `413`, `415` |
| `GET /api/agreements/{id}/document` | Stream PDF | `404` |
| `DELETE /api/agreements/{id}` | `204` | `409 agreements.in_use` |

Compatibilidad trámite–convenio: `social_service` acepta `general_framework`, `social_service`, `collaboration`. `internship` acepta `general_framework`, `internship`, `dual_education`, `collaboration`.

## Flujos UI esperados

- Directorio de empresas con filtros (sector, tamaño, interna, vigencia de convenio).
- Detalle de empresa con pestañas: Datos · Supervisores · Convenios · Trámites (lectura).
- Tablero de convenios con badge de `validity` y alerta de próximos a vencer.
