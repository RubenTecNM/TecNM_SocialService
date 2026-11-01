# Roadmap Backend — Progresión de Desarrollo

> **Objetivo:** Guía ejecutiva de avance fase por fase para la construcción del backend .NET 10 y su integración con PostgreSQL y Frontend.
> **Convenciones y contratos:** Ver [`docs/specs/api-conventions.spec.md`](../specs/api-conventions.spec.md).

---

## 🧭 Estado General del Proyecto

| Fase | Módulo / Componente | Entregables Clave | Estado |
| :---: | :--- | :--- | :---: |
| **0** | **Infraestructura & Auth** | Docker Compose (3 servicios), PostgreSQL 18 (24 tablas), Auth JWT con cookies HttpOnly. | **100% Hecho** |
| **1** | **Fundaciones & Catálogos Base** | `AuditableEntity`, Soft Delete global, RFC 7807 ProblemDetails, Carreras y Periodos. | **100% Hecho** |
| **2** | **Catálogos Restantes & Folios** | Programas SS, Autoridades Institucionales, Series de Folio atómicas. | **100% Hecho** |
| **3** | **Empresas & Convenios** | Directorio de empresas, supervisores externos y convenios de colaboración. | **100% Hecho** |
| **4** | **Estudiantes & Padrón** | Expediente de alumno, validación IMSS, importador Excel masivo. | **100% Hecho** |
| **5** | **Expediente Documental** | Almacenamiento local, subida de PDFs, estados de revisión y versionado. | **100% Hecho** |
| **6** | **Servicio Social (SS)** | Trámite completo (480 h), Anexos XVIII–XXV, 3 reportes bimestrales y liberación. | **100% Hecho** |
| **7** | **Prácticas Profesionales (PP)** | Trámite completo (200–480 h), evaluación Anexo XVII y acreditación. | **100% Hecho** |
| **8** | **Calidad & Reportes** | Trazabilidad de estados, Producto No Conforme (PNC ISO 9001) y Formato 911.10. | **100% Hecho** |

---

## 📋 Progresión Detallada por Fases

### Fase 0: Infraestructura & Autenticación
- [x] Docker Compose configurado (`tecnm_postgres`, `tecnm_backend`, `tecnm_frontend`).
- [x] Base de datos PostgreSQL con las 24 tablas de [`schema.sql`](../../database/design/schema.sql).
- [x] Semilla oficial de usuarios y roles cargada ([`01_dev_users.sql`](../../database/seed/01_dev_users.sql)).
- [x] Login JWT con cookies seguras `auth_token` (`HttpOnly`, `SameSite=Lax`) y endpoints `/login`, `/me`, `/logout`.

### Fase 1: Fundaciones y Primeros Catálogos
- [x] Entidad base `AuditableEntity` (6 campos) e interceptor `AuditInterceptor` para soft delete automático.
- [x] Manejo global de excepciones (`GlobalExceptionHandler`) con RFC 7807 y códigos PostgreSQL (`23505`, `23503`).
- [x] Utilidades de paginación transversal `Paging.cs` (`PagedRequest`, `PagedResponse<T>`, `OptionItem`).
- [x] Catálogo **Carreras** (`/api/careers`): CRUD, regex de clave, toggle de estado y `/options`.
- [x] Catálogo **Periodos Académicos** (`/api/academic-periods`): CRUD, activación transaccional única y `/current`.

### Fase 2: Catálogos Restantes & Motor de Folios
- [x] **Programas de Servicio Social** (`/api/social-service-programs`): 9 categorías oficiales, flag `is_internal`.
- [x] **Autoridades Institucionales** (`/api/institutional-authorities`): cargos oficiales, regla de una autoridad activa por puesto y endpoint `/active` para generador de constancias.
- [x] **Series de Folio** (`/api/document-series`): incremento atómico (`SELECT FOR UPDATE`), prevención de rollback y vista previa de folios con tokens (`{PREFIX}{SEQ:4}/{DD}/{MM}/{YYYY}`).
- [x] **Semilla oficial de catálogos** ([`database/seed/02_dev_catalogs.sql`](../../database/seed/02_dev_catalogs.sql)) con datos reales del ITSM (6 carreras, 3 periodos, 9 programas normativos, 4 autoridades, 3 series de folio).

### Fase 3: Empresas, Supervisores y Convenios
- [x] Módulo **Empresas** (`/api/companies`): sectores (público, privado, social, educativo), tamaños y receptores TecNM internos.
- [x] Módulo **Supervisores** (`/api/companies/{id}/supervisors`): vinculados a su empresa receptora.
- [x] Módulo **Convenios** (`/api/agreements`): tipos de convenio, control de vigencia dinámico y fechas de inicio/fin, subida y descarga de PDF.
- [x] Semilla oficial de empresas, supervisores y convenios ([`database/seed/03_dev_companies.sql`](../../database/seed/03_dev_companies.sql)).
- [x] Regla de integridad: supervisor y convenio del trámite deben pertenecer a la misma empresa.

### Fase 4: Estudiantes, IMSS e Importación Masiva
- [x] Módulo **Estudiantes** (`/api/students`): padrón, alta individual con botón para casos específicos, perfil `/me` y actualización de datos de contacto.
- [x] Carga y validación de **Constancia de Vigencia de Derechos del IMSS** (`/api/students/me/imss`).
- [x] Asistente de **importación masiva por Excel** (`/api/student-imports`) para cargar el listado de alumnos en condiciones de realizar el servicio.
- [x] Semilla oficial de estudiantes ([`database/seed/04_dev_students.sql`](../../database/seed/04_dev_students.sql)).

### Fase 5: Expediente Documental & Archivos
- [x] Servicio de almacenamiento físico local en volumen Docker (`IFileStorage` / `LocalFileStorage`).
- [x] Subida y descarga de archivos PDF firmados y sellados (`/api/students/{id}/documents`, `/api/students/me/documents`, `/api/documents/{id}/file`).
- [x] Flujo de revisión por Vinculación (`/api/documents/{id}/review`, `/api/documents/review-queue`): `approve`, `reject` con motivo y actualización en tiempo real del progreso documental, hitos y vigencia IMSS.
- [x] Versionado automático `superseded` al re-subir documentos observados.

### Fase 6: Servicio Social Operativo (Anexos XVIII–XXV)
- [x] Apertura automatizada (datos Anexo XVIII + Vigencia IMSS) y generación de Carta de Presentación (`CP`) con folio (`SS_CP...`).
- [x] Generación automática de Carta de Aceptación (`CA`) mediante plantilla cuando el servicio es local en el TecNM (`modality = "internal"`), para que el alumno la descargue, recabe firma/sello y la suba.
- [x] Formularios de prellenado para que el alumno llene todos los datos e imprima sus formatos listos para firma y sello: Carta Compromiso (Anexo XX), 3 Reportes Bimestrales (Anexo XXII) y Evaluaciones Cualitativas/Actividades (Anexos XXIII, XXIV y XXV).
- [x] Progreso documental (10% por documento validado: `CP, CA, SOL, CC, R1, R2, R3, EV1..3, CT, COT`) sin bloqueos por horas ni promedios numéricos, y liberación con folio (`SSC...`).

### Fase 7: Prácticas Profesionales (Anexo XVII)
- [x] Solicitud de Carta de Presentación automatizando el Forms oficial (confirmación de datos personales/escolares + subida de Vigencia IMSS) y emisión de `CP` con folio (`EJ26/PR...`).
- [x] Registro de empresa, responsable y fechas al recibir/subir el Oficio de Aceptación (`CA`, en hoja membretada con firma y sello).
- [x] Formulario de prellenado para Evaluación Anexo XVII (`PRODUCTIVIDAD LABORAL`, 7 criterios 0–4) para impresión y firma/sello, subida de documentos (`EV`, `CT`) y liberación con folio (`PPC...`).

### Fase 8: Calidad ISO 9001 & Estadísticos en Tiempo Real
- [x] Bitácora append-only de cambios de estado (`tramite_status_logs`) y Producto No Conforme (`/api/non-conformances`).
- [x] Sábana de Seguimiento en vivo (`/api/reports/tracking`) con porcentaje de avance documental idéntico a `SEGUIMIENTO VINCULACIÓN1.xlsx`.
- [x] Estadístico Semestral y por Año Escolar (`/api/reports/statistics`: En condiciones, Solicitantes, Liberados y PNC por carrera y sexo) y Formato SEP/TecNM 911.10 (`/api/reports/911`).
