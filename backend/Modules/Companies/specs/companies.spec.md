> Autorización vigente: [Acceso por proceso](../../../../docs/specs/process-access.spec.md). Sus reglas prevalecen sobre permisos anteriores incompatibles.

# Companies backend

Contrato compartido: [spec raíz de empresas](../../../../docs/specs/companies.spec.md).

## 1. Responsabilidades

- `CompaniesController` (`/api/companies`):
  - CRUD de dependencias externas y departamentos internos del TecNM.
  - Filtros: `search` (en name, commercialName, rfc), `sector`, `companySize`, `isTecnmInternal`, `isActive`.
  - Ordenamiento por `name`, `-name`, `createdAt`, `-createdAt`.
  - Endpoint `/options` con metadatos `isTecnmInternal` y `hasValidAgreement`.
  - Campos calculados en respuesta: `activeSupervisorsCount` y `hasValidAgreement`.
  - Validaciones: RFC en mayúsculas (regex `^[A-Z&Ñ]{3,4}\d{6}[A-Z0-9]{3}$`), unicidad lógica de RFC activo (`409 companies.duplicated_rfc`), tamaño obligatorio si sector es `private` (`422 companies.size_required`).
  - `DELETE` con soft delete; error `409 companies.in_use` si tiene supervisores, convenios o trámites vinculados.

- `SupervisorsController` (`/api/companies/{companyId}/supervisors`):
  - CRUD anidado garantizando integridad de pertenencia de supervisores a la empresa.
  - Endpoint `/options` (`label = "{fullName} · {position}"`, `meta.isSignatory`).
  - Toggle de estado activo/inactivo (`PATCH /status`).
  - `DELETE` con soft delete; error `409 company_supervisors.in_use` si tiene evaluaciones o trámites vinculados.

- `AgreementsController` (`/api/agreements`):
  - CRUD de convenios institucionales con cálculo en tiempo real de `validity` (`valid`, `expiring`, `expired`, `inactive`) y `daysToExpiration`.
  - `signedDate` y `expirationDate` con regla `expirationDate >= signedDate` (`422 agreements.invalid_dates`).
  - Unicidad de `agreementNumber` (`409 agreements.duplicated_number`).
  - Endpoint `/options` filtrando por `companyId`, `tramiteType` (`social_service` o `internship`) y fecha `onDate`.
  - Carga y descarga de documento PDF (`PUT /{id}/document`, `GET /{id}/document`), validación de magic bytes PDF (`%PDF`), tamaño máximo 10 MB.
  - `DELETE` con soft delete; error `409 agreements.in_use` si está asociado a trámites.

## 2. Reglas de Autorización

- Lectura y escritura general: `Staff` (`department_head,social_service_officer,internship_coordinator`).
- Endpoint `/options`: disponible para todos los usuarios autenticados (`[Authorize]`, incluidos `student` para registro de trámites).
- Escritura (POST, PUT, PATCH, DELETE, upload PDF): `Staff` (`admin,social_service_officer,internship_coordinator`).
