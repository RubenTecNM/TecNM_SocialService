> Autorización vigente: [Acceso por proceso](../../../../docs/specs/process-access.spec.md). Sus reglas prevalecen sobre permisos anteriores incompatibles.

> Aplicar primero [Flujo documental v2](../../../../docs/specs/document-workflow-v2.spec.md). Este archivo conserva contexto previo compatible.

# Reports backend

Contrato compartido: [spec raíz de reportes](../../../../docs/specs/reports.spec.md).

## Responsabilidades
- `ReportsController`: Centro Analítico multidimensional (`GET /api/reports/analytics` y `GET /api/reports/analytics/companies` con consolidación por Periodo o Ciclo Escolar deduplicada por estudiante, filtros por carrera, semestre, empresa, tipo de actividad, sexo y estatus), Sábana de Seguimiento en vivo (`GET /api/reports/tracking`), Estadístico Semestral y por Ciclo Escolar (`GET /api/reports/statistics`), Formato SEP/TecNM 911.10 (`GET /api/reports/911`), Vinculación (`GET /api/reports/linkage`) e Indicador PNC ISO 9001.
