# Expedients backend

Contrato compartido: [spec raíz de documentos](../../../../docs/specs/documents.spec.md).

## Responsabilidades
- `IFileStorage` / `LocalFileStorage` (`Core/Storage/FileStorage.cs`): Abstracción de almacenamiento físico local bajo `Storage:RootPath`, validación de encabezado mágico `%PDF` y tamaño máximo configurable.
- `DocumentsService` (`Modules/Expedients/DocumentsService.cs`):
  - Subida polimórfica de documentos (`social_service`, `internship`, `general`) con ruta relativa `{studentId}/{tramiteType}/{guid}.pdf`.
  - Versionado automático: al subir un nuevo archivo para el mismo `(studentId, tramiteType, tramiteId, documentCode)`, las versiones previas en estado `pending` o `rejected` pasan automáticamente a `superseded`. Si ya existe una versión `approved`, responde `409 documents.already_approved`.
  - Revisión documental (`POST /api/documents/{id}/review`): transición `pending -> approved | rejected`, validación de permisos por tipo de trámite (`SocialServiceStaff` vs `InternshipStaff`) y sincronización automática de fechas de hitos (`acceptance_letter_date`, `termination_letter_date`) y vigencia IMSS.
  - Cola de revisión (`GET /api/documents/review-queue`) con datos del estudiante y carrera para validación rápida de firma y sello.
- `DocumentsController` (`Modules/Expedients/DocumentsController.cs`):
  - Endpoints `/api/students/{studentId}/documents`, `/api/students/me/documents`, `/api/documents/{id}/file`, `/api/documents/{id}/review`, `/api/documents/{id}` y `/api/documents/review-queue`.
