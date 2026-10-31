# Social Service backend

Contrato compartido: [spec raíz de servicio social](../../../../docs/specs/social-service.spec.md).

## Responsabilidades
- `SocialServicesController`: Trámite maestro de Servicio Social, apertura automatizada (Anexo XVIII + Vigencia IMSS), emisión de Carta de Presentación con folio (`SS_CP...`), generación automática de Carta de Aceptación cuando es local en el TecNM (`modality = "internal"`), progreso documental (0–100%) y `allowedActions`.
- `CommitmentsController`, `BiMonthlyReportsController` & `EvaluationsController`: Formularios para que el alumno llene todos los datos de sus formatos (Anexo XX, Reportes Bimestrales 1–3 Anexo XXII, Evaluaciones Cualitativas Anexos XXIII/XXIV y Actividades Anexo XXV) y los imprima listos para pedir firma y sello.
- `ReleaseController`: Validación de documentos firmados/sellados subidos (`SOL`, `CA`, `CC`, `R1–R3`, `EV1–EV3`, `CT`), liberación con folio atómico (`SSC...`) y envío de acta a Servicios Escolares (`DGTV...`) sin bloqueos por horas ni promedios numéricos.
