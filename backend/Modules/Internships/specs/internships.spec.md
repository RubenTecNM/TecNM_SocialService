# Internships backend

Contrato compartido: [spec raíz de prácticas profesionales](../../../../docs/specs/internships.spec.md).

## Responsabilidades
- `InternshipsController`: Solicitud de Carta de Presentación automatizando el Forms oficial (confirmación de datos personales/escolares + subida de Vigencia IMSS, sin exigir empresa inicial), emisión de Carta de Presentación con folio (`EJ26/PR...`) y registro posterior del Oficio de Aceptación (`CA`).
- `InternshipEvaluationsController`: Formulario Anexo XVII (7 criterios 0–4, actividad `PRODUCTIVIDAD LABORAL`) para que el alumno llene e imprima su formato listo para firma y sello de la empresa.
- `InternshipReleaseController`: Avance documental (20% por hito: `CP`, `CA`, `EV`, `CT`, `CC`) y acreditación con asignación atómica de folio (`PPC...`).
