# Prueba documental local

Acceso actual: http://localhost:6070 (API en http://127.0.0.1:6080). Frontend, API y PostgreSQL se ejecutan en Docker.

## Gestor de plantillas — 26 de noviembre de 2026

- Aplicada la migración documental e importadas 10 plantillas, 12 versiones y 68 asignaciones para los 12 expedientes existentes. Solicitud v1 y Reporte v3 quedaron asignados a los expedientes anteriores. Solicitud v2 y Reporte v4 quedaron activas para nuevas aperturas.
- Coinciden las huellas anteriores y posteriores de los 12 expedientes, los 52 registros de evidencia y los 13 archivos del volumen. Respaldo previo en `tmp/pdf-qa/backups/tecnm-pre-plantillas-20261126-203635.dump`. Las imágenes previas tienen la etiqueta `pre-template-management-20261126`.
- Verificados el gestor en Edge, el catálogo operativo de Administración y de ambas áreas, y la descarga de Solicitud, Reporte, Compromiso y evaluación PP. El editor completo se probó en la compilación que sirve Nginx con respuestas API simuladas. La contraseña histórica de la cuenta demo de alumno fue rechazada. Los permisos de estudiante se comprobaron en la suite aislada.

Cuentas demo verificadas: `i23050363@monclova.tecnm.mx` (alumno) y `jefa.vinculacion` (`department_head`). Contraseña demo: `TecNM-Demo-2026!`.

## Entorno preparado

- PostgreSQL Docker, base `tecnm_sspp`, puerto local **15432**; volumen existente conservado.
- Migración incremental documental v2 aplicada. Expedientes existentes permanecen en flujo 1; nuevos usan flujo 2.
- Respaldo previo: `tmp/pdf-qa/backups/tecnm-before-documents-v2-20261104.dump`.
- Cinco archivos conservados en `tecnm_storage_data`, incluidos los cargados durante las pruebas nativas. Copia y hashes verificados; `local-data/documents` conserva la copia previa al cambio.
- API Docker expuesta en http://127.0.0.1:5055; frontend Nginx en http://localhost:3000. La conexión interna de API usa `db:5432`. Configuración en `.env` ignorado por Git.
- Fuentes locales de Windows montadas en solo lectura para pruebas; no se copian al repositorio ni a imágenes. Runtime Linux incluye ICU completo y zona horaria de México.
- Descargas desde mcr.microsoft.com y reconstrucción verificadas. El fallo DNS anterior ya no se reprodujo; no se modificó la configuración global de red. API nativa y Vite detenidos.
- Respaldo adicional antes del cambio: `tmp/pdf-qa/backups/tecnm-before-docker-switch.dump`. No se recrearon tablas ni eliminaron volúmenes.

## Recorrido

Verificación real de RBAC, empresas y métricas: `node frontend/scripts/check-live-process-access.mjs`. Inicia sesión con los cinco roles demo y comprueba estadísticas de ambas áreas, valores iniciales por rol, catálogo tipificado y bloqueo de expedientes cruzados. No carga archivos ni modifica empresas o dictámenes. `PLAYWRIGHT_MODULE` puede apuntar al runtime local; `UI_TEST_URL` y `UI_TEST_PASSWORD` permiten ajustar el entorno. `LINKAGE_SQL_SNAPSHOT` permite indicar un snapshot SQL reciente para comparar las métricas HTTP con los agregados de la base; no usa automáticamente capturas anteriores.

1. Entrar como alumno y abrir Mi perfil. Cargar constancia IMSS PDF real con fecha de expedición.
2. Entrar como admin y revisar IMSS: consultar PDF, confirmar fecha del año actual y aprobar, o rechazar con motivo. Los datos demo anteriores no acreditan vigencia para nuevas solicitudes.
3. Alumno: crear SS o PP, guardar datos y descargar prellenados; firmar físicamente y subir el PDF.
4. SS: cargar Solicitud firmada; staff aprueba su PDF y después la solicitud. Esta aprobación no genera Carta de Presentación ni folio.
5. SS: alumno carga Carta de Presentación ya firmada en el expediente. Staff revisa y aprueba/rechaza. Corregir rechazo mediante nueva carga. No se altera ni estampa el archivo recibido.
6. Completar anexos del expediente. SS nuevo exige las siete evaluaciones separadas. En PP, aceptación se carga únicamente desde administración; alumno carga evaluación y terminación.
7. Staff: concluir y liberar cuando todos los documentos requeridos estén aprobados.

La plantilla limpia de Carta SS queda pendiente para habilitar emisión institucional futura; no bloquea el flujo temporal de carga firmada. La revisión institucional de los formatos sigue siendo necesaria antes de publicar en producción.

## Reinicio

Desde la raíz: `docker compose up -d --build`. Para iniciar las imágenes ya construidas sin descargar: `docker compose up -d --no-build --pull never`.

Estado: `docker compose ps`. Detener conservando datos: `docker compose stop`.

No ejecutar nuevamente la migración ya aplicada ni el archivo de diseño sobre esta base. Prueba HTTP aislada reproducible: `pwsh -File backend.Tests/run-isolated.ps1`. Comprobación del entorno Docker activo sin modificar evidencias: `node frontend/scripts/check-live-documents.mjs` (requiere Playwright y Edge; `UI_TEST_URL` permite otro origen). Verificados login real de alumno/admin, bandeja, PDFs SS/PP y mes en español del Compromiso.

## RBAC y empresas desplegados — 6 de noviembre de 2026

- Aplicadas `20261106_company_type_prepare.sql` y `20261106_company_type_finalize.sql` en una sola transacción, con API y frontend detenidos. No se ejecutaron schema de diseño ni seeds sobre esta instalación.
- Clasificación individual: TecNM interno `public`, conservando `sector=educational`; Presidencia Municipal `public`; Gunderson `large`; SIMSA `medium`. No quedan empresas pendientes y `company_type` es `NOT NULL`.
- Respaldo verificado: `tmp/pdf-qa/backups/tecnm-before-rbac-20261107T031301Z.dump` (nombre en UTC; operación realizada la noche del 6 de noviembre en México).
- Huellas anteriores y posteriores verificadas para 11 alumnos, 6 expedientes SS, 4 expedientes PP, 47 documentos, campos previos de empresas y archivos del volumen. Evidencias y dictámenes conservados.
- Imágenes backend/frontend reconstruidas y servicios actualizados activos en `http://localhost:3000`, API en puerto 5055 y PostgreSQL en 15432. Volúmenes existentes conservados.
- Pasan ambos recorridos reales: acceso con cinco roles, tipología, área inicial, alternancia estadística, bloqueo de expedientes cruzados, comparación de métricas SQL/HTTP y descargas documentales. Capturas en `tmp/pdf-qa/live-process-access/`.

Estas migraciones ya están aplicadas en esta base local. No volver a ejecutarlas; para otras instalaciones seguir `database/migrations/README.md` y clasificar sus registros individualmente.

## Cuentas e IMSS desplegados — 7 de noviembre de 2026

- Aplicada `20261107_student_accounts.sql` tras ensayo con rollback y respaldo verificado `tmp/pdf-qa/backups/tecnm-before-student-accounts-20261107-190923.dump`.
- Nueve cuentas vinculadas migradas a correo institucional. Huellas de credenciales, alumnos, roles, lotes, SS/PP y 47 documentos conservadas antes del restablecimiento explícito; cinco archivos del volumen conservan sus hashes.
- Restablecida exclusivamente la cuenta de `I23050348` mediante endpoint admin. Usuario `i23050348@monclova.tecnm.mx`; contraseña su matrícula en mayúsculas. Login y perfil correctos. Aún no hay constancia IMSS para este alumno: debe cargar su PDF real.
- API y frontend actualizados en http://localhost:3000. Sesiones anteriores requieren nuevo login. No se ejecutaron seeds ni scripts de diseño/legacy en esta base.
- Pasan suite aislada y tres recorridos de UI: cuentas/IMSS, bandeja y expedientes. Comprobaciones reales verifican cinco roles, aislamiento de procesos, catálogo y descargas oficiales sin cambiar evidencias.
- Imágenes de reversión reconstruidas desde HEAD anterior: `tecnm-sspp-backend:before-accounts-20261107` y `tecnm-sspp-frontend:before-accounts-20261107`. Docker no conservaba los manifiestos originales; no se alteraron volúmenes para resolverlo.
- Para recorridos reales, definir `UI_TEST_STUDENT_USERNAME=i23050363@monclova.tecnm.mx`; las bases nuevas con seeds usan `l23050363@monclova.tecnm.mx`. Ambas conservan su contraseña demo. Capturas en `tmp/pdf-qa/accounts-imss/`.
- IMSS: carga con fecha opcional, revisión de preingreso desde padrón y bandeja, sugerencia digital con confirmación. No hay OCR ni dictamen automático.

## Ajuste: fecha IMSS manual

La revisión del documento 49 confirmó que el PDF es digital y usa «Fecha de Constancia», mientras el extractor reconocía expedición/emisión; la API devolvía `not_found`. El PDF contiene otras fechas que no deben confundirse con la documental. La revisión ahora captura/confirma manualmente esa fecha y no consulta sugerencias. Se conserva fecha declarada como referencia, validación anual y rechazo con motivo. No se modificó el documento ni su dictamen.

## Criterio final IMSS: confirmación visual de VIGENTE

Por decisión del usuario se retiraron captura de fecha y restricción del año actual. Alumno carga PDF sin fechas; encargado comprueba titular y estado VIGENTE mediante confirmación explícita en el visor antes de aprobar. Un documento aprobado sigue habilitando ingreso al cambiar de año. Reemplazo requiere nuevo dictamen; rechazo bloquea ingreso. Fechas históricas se conservan como metadatos. No se aprobaron documentos reales durante este ajuste.
