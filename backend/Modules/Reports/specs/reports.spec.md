# Reports backend

Contrato compartido: [spec raíz de reportes](../../../../docs/specs/reports.spec.md).

## Responsabilidades
- `ReportsController`: Sábana de Seguimiento en vivo (`GET /api/reports/tracking` con checklist `CP, CA, SOL, CC, R1..R3, EV1..EV3, CT` y `%` de avance), Estadístico Semestral y por Año Escolar (`GET /api/reports/statistics`: En condiciones, Solicitantes, Liberados y PNC por carrera y sexo), Formato SEP/TecNM 911.10 (`GET /api/reports/911`) e Indicador PNC ISO 9001.
