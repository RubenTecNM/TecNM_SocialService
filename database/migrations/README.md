# Migración de tipología y métricas

## Gestor de plantillas PDF

Conservar un respaldo de PostgreSQL y el catálogo desplegado, incluido cualquier directorio configurado en `Documents__RootPath`. Detener las escrituras y aplicar `20261126_document_templates.sql` con `ON_ERROR_STOP=1` en instalaciones existentes. Las bases nuevas ya incluyen sus tablas en `database/design/schema.sql`.

Con la imagen nueva y el mismo catálogo, ejecutar `docker compose run --rm --no-deps backend --import-document-templates`. En una instalación nativa usar `dotnet TecNM.Api.dll --import-document-templates` desde el directorio publicado. El importador comprueba los PDF y guarda versiones y asignaciones en una transacción. Repetirlo conserva las versiones activadas y las asignaciones que ya existan. Al terminar, arrancar API y frontend juntos y comprobar una descarga de un expediente anterior y la vista de Catálogos oficiales. Conservar el respaldo anterior hasta validar el corte.

Las suites aisladas nativa y Docker verifican publicación por área, concurrencia, protección antifalsificación y conservación de versiones. Con Vite activo, `node frontend/scripts/check-template-management-ui.mjs` prueba el editor con Edge y respuestas API simuladas. Puede configurarse `PLAYWRIGHT_MODULE` para indicar la instalación local de Playwright.

Si el catálogo de la nueva imagen difiere del catálogo en servicio, montar una copia del directorio anterior en solo lectura y pasar su ruta con `Documents__BaselineRootPath` al importador. Este fija el catálogo anterior en los expedientes existentes y activa las diferencias del catálogo nuevo para nuevas aperturas.

## Cuentas de alumnos — 7 de noviembre de 2026

Después de las migraciones documental y de tipología, aplicar `20261107_student_accounts.sql` una sola vez en instalaciones existentes. Las bases nuevas creadas con el diseño actual ya incluyen sus columnas e índices.

1. Crear respaldo PostgreSQL en formato custom y verificarlo con `pg_restore --list`. Conservar también las imágenes anteriores de API y frontend.
2. Ensayar el archivo reemplazando `COMMIT;` por `ROLLBACK;`. El preflight bloquea correos ausentes/duplicados, cuentas compartidas, colisiones y roles incompatibles. Corregir los conflictos antes del despliegue.
3. Detener API y frontend para impedir escrituras; ejecutar la migración con `ON_ERROR_STOP=1`. Nunca ejecutar seeds ni `schema.sql` sobre la instalación.
4. Desplegar API y frontend juntos. Las cuentas vinculadas de alumnos usarán su correo institucional; contraseñas existentes conservadas. Personal conserva usuario. Todos deben iniciar sesión de nuevo.
5. Verificar login, padrón e IMSS; comparar huellas de alumnos, credenciales, roles, trámites, documentos y archivos. Restablecer cuentas solo mediante la acción administrativa explícita.

La transacción revierte por completo si falla. Si falla antes de desplegar, reiniciar los contenedores anteriores. Si se requiere restaurar el respaldo, mantener escrituras detenidas y restaurar junto a las imágenes compatibles; no continuar con versiones mezcladas.

Aceptación aislada: suite nativa/Docker incluye preservación de credenciales, rollback de migración y de filas Excel, cuentas por correo, revocación, adaptación IMSS de preingreso y extracción digital. Interfaz: `node frontend/scripts/check-accounts-imss-ui.mjs`, con Vite activo y `PLAYWRIGHT_MODULE` configurado si hace falta.

Aplicar sobre PostgreSQL con respaldo, después de `20261103_document_workflow_v2.sql`.
No ejecutar `database/design/schema.sql` sobre una instalación existente.

1. Ejecutar `20261106_company_type_prepare.sql`. Agrega el campo y clasifica público, social y privados con tamaño válido.
2. Revisar el listado de pendientes, incluidos educativos. Asignar individualmente `company_type` con valores `public`, `micro`, `small`, `medium`, `large` o `social`. Conservar `sector=educational` para el reporte 911. No ejecutar seeds demo en una instalación existente.
3. Ejecutar `20261106_company_type_finalize.sql`. Aborta si alguna empresa, incluso borrada, sigue sin clasificación. Impone `NOT NULL` y crea índices y vistas agregadas.
4. Desplegar backend y frontend coordinadamente. `companyType` es obligatorio en `POST`/`PUT /api/companies`; los clientes anteriores deben actualizarse. Las consultas conservan los demás campos y agregan filtro `companyType`.

Detener escrituras durante clasificación y finalización. La versión nueva de la API requiere ambas migraciones completas. Una falla de finalización revierte su transacción; corregir pendientes y volver a ejecutarla. No elimina empresas, evidencias ni historiales.

Verificación aislada: `pwsh -File backend.Tests/run-isolated.ps1` con Docker, o `pwsh -File backend.Tests/run-native-isolated.ps1` con PostgreSQL 18 local. Ambas crean una base temporal `document_checks`; nunca usan la conexión del proyecto. Prueban migración incompleta, esquema nuevo, permisos HTTP y agregaciones SQL.

## Seguimiento SS — 8 de noviembre de 2026
Aplicar 20261108_student_social_service_follow_up.sql después de cuentas. Desplegar API/frontend juntos. La migración conserva todos los datos y deja periodos de aptitud históricos nulos; admin debe confirmarlos. PNC de seguimiento no crea casos de Calidad. Ensayar en base aislada antes del despliegue.
