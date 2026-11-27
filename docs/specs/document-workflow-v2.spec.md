> Contrato de autorización vigente: [Acceso por proceso](./process-access.spec.md). Prevalece sobre permisos anteriores incompatibles.

# Expediente documental v2

Contrato vigente para documentos, alumnos, SS, PP y estadísticos. Las reglas v1 de los contratos anteriores solo aplican donde aquí se indica compatibilidad histórica.

## Ajuste temporal autorizado: Carta de Presentación SS

Mientras se entrega la plantilla limpia, CP SS se incorpora como PDF firmado cargado por el alumno (o staff SS/admin), pendiente de dictamen. Se permiten rechazo, corrección y aprobación con el historial documental habitual. No se estampa ni altera el archivo recibido. Su aprobación cuenta para progreso y liberación.

Aprobar la solicitud exige SOL aprobado e IMSS vigente, pero no emite CP ni asigna folio institucional. La apertura aprobada no afirma que la carta ya fue emitida. La descarga CP devuelve únicamente evidencia aprobada existente. El endpoint institucional de emisión permanece protegido por plantilla limpia y no se usa en el flujo temporal. CP PP conserva sus reglas actuales. Este ajuste prevalece sobre las referencias a emisión automática y prohibición de carga CP SS de las secciones anteriores.

## Flujo y permisos

- Solicitudes nuevas: `documentWorkflowVersion=2`; expedientes existentes: `1`. No convertir paquetes históricos ni inferir dictámenes individuales.

### Catálogo único de presentación SS

El catálogo JSON compartido por API y frontend define estos nombres y orden en todas las vistas, formularios y exportaciones:
0. Carta de Presentación
1. Solicitud
2. Carta Compromiso
3. Carta de Aceptación
4. Reportes Bimestrales
   - 4.1 Reporte Bimestral 1
   - 4.2 Reporte Bimestral 2
   - 4.3 Reporte Bimestral 3
5. Formato de Evaluación Cualitativa
6. Formato de Autoevaluación Cualitativa
7. Formato de evaluación de las actividades por el prestador de s.s.
8. Carta de Terminación

Los bimestres de evaluaciones son metadatos separados del nombre. IMSS queda fuera de la numeración SS. Los paquetes históricos no acreditan dictámenes individuales. La matriz agrega los tres bimestres por tipo de evaluación sin cambiar las reglas de avance ni los códigos de almacenamiento.
- SS v2: `SOL`, `CP`, `CA`, `CC`, `R1..R3`, `EV_SUP1..EV_SUP3`, `EV_SELF1..EV_SELF3`, `EV_ACT`, `CT`. Bimestre 3 es final.
- SS v1 conserva `EV1..EV3` y sus paquetes históricos; su descarga de formato usa XXIII prellenado. XXIII prellena alumno, programa y nombre/cargo del supervisor del trámite. XXIV prellena alumno y programa. XXV conserva nombre, programa, periodo y marca final. XXIV/XXV no agregan bloques del responsable.
- R1..R3 añaden nombre y puesto del evaluador registrado en cada reporte; sin evaluador usan el supervisor del trámite. Todas las firmas, sellos, criterios y observaciones permanecen vacíos. Descargar no registra calificaciones ni acredita entrega o aprobación.
- Nombre y cargo del supervisor/responsable se colocan juntos, próximos a la línea inferior de su bloque, con el cargo inmediatamente encima. El espacio superior queda libre para la firma manual.
- CT SS (Anexo XIX) ofrece prellenado `termination_letter` en ambas versiones del expediente: alumno y programa del trámite, actividades de reportes ordenados (o solicitud si no hay reportes), fechas del trámite, total fijo de 480 horas, ciudad de la dependencia (o Carta Compromiso) y supervisor del trámite como responsable. Fecha de expedición: fecha documental existente o día actual de México. El total impreso siempre es 480, independientemente de las horas capturadas en reportes; no modifica las horas persistidas. No inventar ciudad/actividades. Datos faltantes y desbordamiento conservan errores 422.
- CT conserva la base oficial y sus destinatarios impresos, firma/sello manuales vacíos y nombre/cargo próximos a la línea. Descargar no registra terminación ni modifica evidencias, dictámenes o estado; se conserva carga y revisión del PDF firmado y el endpoint de plantilla vacía.
- `CA` SS nunca se autogenera, tampoco para modalidad interna. Descargar ejemplo; cargar carta de la dependencia.
- PP: `CA` solo lo carga/revisa `admin` o `internship_coordinator`; alumno solo consulta. UI informa envío empresarial a `arturo.martinez@monclova.tecnm.mx`.
- Alumno no carga documentos institucionales (`CP`, `COT`, liberación PP `CC`). Staff solo modifica su dominio; `department_head` solo consulta.
- Eliminar evidencia pendiente exige los mismos permisos del catálogo que cargarla. Alumno no elimina aceptación PP incorporada por staff; trámites terminales no admiten eliminación. Documentos aprobados/rechazados conservan su protección e historial.
- PDF válido, no cifrado, con páginas, máximo 5 MiB. Rechazo exige motivo de 1–1000 caracteres. Reemplazos conservan archivos históricos; aprobado no se reemplaza excepto IMSS.
- Nueva carga idéntica al documento vigente es idempotente. Una revisión repetida idéntica devuelve el dictamen; un dictamen distinto devuelve 409.
- Guardar datos no acredita firma, evaluación ni entrega. Revisión del reporte delega al documento PDF. `CC` aprobado acredita firma; `signed_at` indica validación documental.
- SS v2 requiere `SOL` aprobado para aprobar apertura. Conclusión/liberación requiere todos los documentos exigibles aprobados, incluida CP emitida. SS v1 conserva sus requisitos históricos.
- Conclusión SS v2 sigue bloqueada con seis evaluaciones aprobadas si falta EV_ACT, o si este último PDF está pendiente. El bloque estadístico solo se completa al aprobar los siete. PP exige CA, EV y CT aprobados. Liberación concurrente debe consumir un solo folio; tras liberar se conservan descargas y se bloquean nuevas evidencias.

## IMSS único

- Fuente única: `expedient_documents`, tipo `general`, código `IMSS`, `tramiteId=null`.
- Carga multipart agrega `documentDate` (fecha declarada); puede omitirse para cargar un documento histórico pendiente. Aprobar exige confirmar `documentDate` del año actual y no futura.
- El año se calcula con `TimeProvider` en `America/Mexico_City`. No usar año de carga ni booleano del perfil como autoridad.
- Crear y aprobar solicitudes SS/PP exige documento IMSS vigente aprobado, incluso para staff. Nuevas solicitudes guardan FK `admission_imss_document_id`.
- Trámites ya admitidos continúan al cambiar el año. Una carga nueva reinicia revisión; archivo y dictamen previo se conservan como versión sustituida.
- Endpoints IMSS actuales delegan al servicio documental. Validación recibe `{ isValid, rejectionReason?, documentDate? }`; rechazar sin motivo o aprobar sin fecha confirmada devuelve 400.
- Perfil expone estado, fecha documental, observaciones e identificador IMSS. `hasValidImss` es una proyección del documento aprobado vigente del año, no autorización persistente independiente.

## PDF oficial

- Carta Compromiso: descarga directa con datos registrados, sin exigir guardar previamente el formulario CC. Domicilio del alumno desde solicitud (respaldo CC), teléfono del perfil (respaldos solicitud/CC), domicilio de la dependencia vinculada (respaldo CC); omitir valores vacíos al resolver respaldos. Ciudad desde CC o dependencia. La fecha impresa se calcula en cada petición de descarga con TimeProvider y zona America/Mexico_City; nunca reutiliza fecha guardada ni inicio del servicio. Solo firma del alumno vacía; conservar los demás campos prellenados. Descargar no persiste datos ni acredita firma. Datos ausentes/desbordamiento mantienen errores 422.

- `GET /api/social-services/{id}/pdf/{document}`: `application`, `commitment`, `report_1..3`, `evaluation_1..3`, `self_evaluation_1..3`, `activity_assessment`, `termination_letter`, `presentation_letter`. Evaluaciones y CT usan `pdfKey` en el checklist; las plantillas vacías siguen disponibles por su endpoint existente.
- `GET /api/internships/{id}/pdf/evaluation`: solo datos del estudiante y fechas; ambas páginas; criterios, resultados, observaciones y firmas vacíos.
- `PUT /api/internships/{id}/evaluation-format`: `{ evaluationStartDate, evaluationEndDate }`; no crea calificaciones. Captura calificada existente queda exclusiva para staff, con siete valores explícitos.
- El alumno propietario puede guardar fechas del formato PP incluso cuando IMSS deje de habilitar nuevas solicitudes; otro alumno recibe 404. La evidencia aprobada y los estados terminales bloquean edición.
- `GET /api/document-templates/{key}/file`: catálogo autenticado de plantillas sin datos personales. Claves permitidas, nunca rutas arbitrarias.
- PDFsharp Core 6.2.4. Superposición en puntos sobre copias oficiales verificadas, sin recrear membretes ni rasterizar páginas vectoriales. Entrega estática.
- Solicitud: retirar AcroForm, anotaciones, acciones, valores y metadatos personales en copia preparada. Área administrativa siempre vacía.
- Solicitud (XVIII) y Reportes (XXII) trazan texto regular negro en Arial 10 pt y Century Gothic 9 pt, respectivamente, con mínimo de 7 pt y excepciones declaradas en el manifiesto. Los campos declaran renglones con ancho y línea base fija en puntos desde arriba e izquierda. Al reducir la fuente, la línea base se conserva. Los bloques de actividades respetan saltos explícitos y aprovechan primero el renglón junto al rótulo. Las casillas centran cada marca en su interior.
- R1–R3 comparten mapa. Número de reporte, segmentos del periodo y horas se centran en sus líneas. Nombre y cargo del supervisor ocupan hasta cuatro renglones contiguos al pie del bloque, con el cargo inmediatamente arriba de la línea de firma.
- Texto fuera de caja: 422 `documents.text_overflow` con campo; datos faltantes: 422 `documents.data_missing`. Sin truncamiento silencioso.
- Fuente requerida ausente: 409 `documents.font_unavailable`. Plantilla/recurso no disponible: 409 `documents.template_unavailable`.
- Manifiesto publicado registra dimensiones por página. Antes de descargar/generar, verificar número/dimensiones de páginas y límites de cada campo: página existente, rectángulo dentro de hoja y tamaños de fuente finitos, positivos y ordenados. Metadatos o mapa inválidos devuelven 409, sin generar archivo ni consumir folio.
- Carta SS original es escaneo ya firmado: emisión bloqueada hasta base limpia configurada. Aprobar/emisión no consume folio ni cambia estado si faltan recursos.
- Carta SS se almacena como `origin=system`, aprobada al emitir, con plantilla y hash. Descarga posterior reutiliza bytes y folio.
- Estampas PNG transparentes solo en documentos institucionales, con coordenadas, dimensiones y opacidad configurables. Dummies identificables; nunca extraer firmas reales del ejemplo.
- Impresión HTML de CP PP y liberaciones SS/PP conserva comportamiento actual. No ofrecer impresión HTML alternativa para documentos de esta matriz.

## Interfaz y estadísticas

- Detalles exponen `documentWorkflowVersion` y `documentRequirements`: código, título, modo de descarga, clave PDF/plantilla, permiso de carga y bloqueo.
- Visor autenticado por blob, descarga y revocación al cerrar/desmontar. Rechazo muestra observaciones y permite corregir.
- Cola paginada con filtros de tipo (incluido `general`), código, periodo, estado y búsqueda. Error de API muestra error/reintento, nunca datos ficticios ni dictámenes locales.
- SS mantiene diez bloques de 10%. En v2, evaluaciones exige los siete documentos separados. EV1/EV2/EV3 estadísticos agregan supervisor+autoevaluación por bimestre; EV3 incluye actividades.
- Comparten cálculo detalle, listado y reportes. V1 conserva cálculo histórico.
- Las marcas CP, CA, SOL y CT de seguimiento usan la misma regla por versión que el porcentaje: en v2, fecha/folio sin PDF aprobado no acredita documento; v1 conserva sus hitos históricos.

## Persistencia y verificación

- Migración PostgreSQL incremental, sin ejecutar `database/design/schema.sql` ni `database/legacy/`. Conserva evidencias, auditoría y versiones.
- Documento agrega fecha, origen, SHA-256 y versión de plantilla. Unicidad vigente incluye `tramiteId=null`. Concurrencia mediante tokens y transacciones; limpiar archivo nuevo si falla BD.
- Probar aprobación IMSS anual y cambio de año, permisos, revisión/reemplazo concurrente, unicidad de folios, firma documental, históricos y errores de almacenamiento.
- Verificar dimensiones, páginas, contenido vacío y ausencia de datos ocultos. Comparar renders fuera de cajas autorizadas; página 2 de PP intacta. Probar acentos, Ñ, texto largo y desbordamiento.
- Publicación incluye plantillas/mapas y fuentes autorizadas configuradas explícitamente. No redistribuir fuentes del sistema operativo automáticamente.

## Mapas SS

Casillas se marcan por valores explícitos de carrera y categoría; valores sin mapeo devuelven 422. Categorías combinadas marcan todas las casillas aplicables. Fechas fragmentadas usan día, mes en español y año. Preparar plantillas conserva mapas calibrados. Campos opcionales no fuerzan datos inexistentes, como segundo apellido.

## Recursos institucionales

Las estampas del manifiesto solo aplican a plantillas marcadas `institutional=true`; requieren autoridad activa en los datos de emisión, PNG y hash, página y rectángulo dentro de la hoja, y opacidad 0–1. Cualquier estampa configurada sobre anexos de firma física se rechaza. Los PNG dummy dicen PRUEBA y son genéricos; no reproducen firmas existentes.

## Inmutabilidad documental

La captura digital de calificaciones SS/PP queda exclusiva para staff. Los siete criterios deben ser explícitos; no sustituir datos ausentes por Excelente. Reporte y Compromiso aprobados no admiten edición de datos. Un trámite terminal no admite nuevas evidencias. IMSS general mantiene su renovación independiente.

## Atomicidad de apertura y folios

Un índice único parcial por alumno impide dos SS o PP activos/liberados (excluye cancelados y no conformes). Apertura y bitácora comparten transacción. Las acciones que consumen folio confirman serie, trámite y bitácora en una transacción; conflictos concurrentes devuelven 409 sin consumir folios extra. La emisión directa SS sigue bloqueada sin base limpia.

## Emisión SS persistida

Carta SS requiere clave `ss-presentation-letter` institucional, sello y firma configurados. Aprobación exige SOL aprobado en v2 e IMSS vigente; emisión guarda PDF, SHA-256, origen system, revisor y versión de plantilla en la misma transacción del folio. Repetir emisión devuelve el archivo existente sin cambiar fecha/folio. Si falla BD, se limpia solo el archivo nuevo. Sin base limpia se mantiene 409.

## Bandeja administrativa

Identificar documentos generales como General / IMSS. Un dictamen aprobado no implica sello digital. Las indicaciones de revisión no deben fijar periodos académicos de ejemplo. Verificar en navegador filtros enviados al servidor, fecha IMSS, rechazo obligatorio y error de API sin datos ficticios.

## Fecha declarada en cargas IMSS

Las cargas documentales y adaptadores IMSS propagan `documentDate` multipart opcional al expediente pendiente. Si se proporciona, no admite fechas futuras. La fecha declarada nunca acredita vigencia ni sustituye la confirmación del encargado. Reenviar los mismos bytes no cambia la fecha ni el dictamen existentes; cambiar solo la fecha exige confirmación administrativa.

## Revisión manual IMSS

- Padrón y bandeja comparten visor y dictamen. El encargado captura o confirma manualmente la fecha que aparece como «Fecha de Constancia» o fecha de expedición del PDF. La fecha declarada por el alumno se muestra como referencia y se precarga si existe.
- No ejecutar extracción automática al abrir el diálogo ni mostrar sugerencias o avisos de extracción fallida. La constancia real contiene otras fechas (publicación del formato, nacimiento e inicio de vigencia); ninguna sustituye la fecha documental.
- Mantener fecha del año actual de México, sin fechas futuras, y motivo obligatorio para rechazar. Reiniciar campos por documento y mostrar errores dentro del diálogo. Sin evidencia no hay dictamen.
- El endpoint de sugerencia existente queda disponible por compatibilidad y diagnóstico, fuera del flujo de revisión. No modifica documentos ni se consulta desde la interfaz.
- SS/PP separan carga opcional de fecha, revisión y apertura. No hay OCR ni aprobación automática.

## Criterio vigente: estado IMSS revisado visualmente

Este criterio sustituye las reglas anteriores de fecha/año IMSS. Para aprobar, el encargado verifica que el PDF corresponda al alumno y muestre estado VIGENTE. No se captura fecha, no se exige año actual y no caduca automáticamente al cambiar el año. El documento aprobado vigente (no sustituido ni borrado) habilita ingreso SS/PP; una nueva carga reinicia revisión. Rechazar exige motivo. Fechas históricas conservadas son metadatos, no autorización. Interfaz muestra confirmación explícita de estado VIGENTE y titular antes de aprobar, tanto en padrón como bandeja; cargas no solicitan fechas.

## Simplificación del flujo del alumno

Solicitud se revisa desde los datos del trámite: no requiere firma, nueva captura ni PDF. Su aprobación administrativa cierra SOL en checklist, progreso, conclusión y liberación. No se permite cargar nuevos PDFs SOL; se conservan evidencias históricas. Carta Compromiso no ofrece edición y oculta prellenado al alumno tras aprobación; conserva consulta del PDF aprobado. Toda carga del checklist muestra el contenido PDF local antes de transmitir bytes; requiere Confirmar y enviar a revisión, permite cancelar sin subir, impide doble envío y mantiene errores para reintento.

La confirmación visual también aplica a cargas IMSS del perfil y de apertura SS/PP.

## Envío conjunto de apertura SS

POST /api/social-services/submissions (student) recibe multipart application (JSON CreateSocialServiceRequest) e imssFile (PDF opcional si existe IMSS pendiente/aprobado). Un solo envío confirmado tras previsualizar adjunto registra solicitud requested e IMSS pendiente en una transacción; no exige IMSS aprobado para enviar. Rechazado/ausente exige nuevo PDF. Conserva endpoint JSON anterior y reglas PP. Aprobar solicitud sigue exigiendo IMSS aprobado. No se exige PDF SOL ni se descarga automáticamente. Ante fallo se revierte BD y se elimina únicamente el archivo nuevo.

## Acta Individual

GET /api/social-services/{id}/pdf/individual_act entrega PDF oficial únicamente a admin, social_service_officer y department_head, para trámites released. Prellena alumno, matrícula, carrera, dependencia, actividades de reportes ordenados sin duplicados (respaldo solicitud), fechas del trámite y 480 horas. Expedición: día de descarga en America/Mexico_City mediante TimeProvider. Autoridades activas outreach_division_head e institute_director, con título opcional, nombre y cargo; solo firmas vacías. Faltantes devuelven 422 documents.data_missing por campo; desbordamiento 422 documents.text_overflow. Plantilla/fuente inválida devuelve 409. No modifica estado, evidencia, folios ni envío a Escolares. Solo PDF individual, sin reverso. Descarga administrativa con carga, prevención de doble petición y errores visibles; alumno sin acceso.
