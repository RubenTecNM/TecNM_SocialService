# Recursos documentales

`manifest.json` contiene hashes, versiones y mapas oficiales. Generación conserva las bases; actualizar hash solo después de verificar cualquier cambio de plantilla.

Cada entrada requiere `pages` y `pageSizes: [{ width, height }]`, en puntos PDF y en orden de páginas. El motor contrasta estos datos con la base y valida campos dentro de la hoja, página existente y tamaños de fuente positivos/ordenados. Metadatos ausentes, JSON inválido o geometría inconsistente devuelven `409 documents.template_unavailable`. Preparación y verificación reproducibles: `tools/pdf/prepare_templates.py` y `tools/pdf/verify_templates.py`; el preparador conserva mapas calibrados y entradas externas del catálogo.

`fontSize` y `minFontSize` se heredan de la plantilla, con valores de 9 y 7 pt cuando se omiten. Para texto sobre renglones impresos, declarar `rows: [{ x, width, baseline }]` en puntos desde arriba e izquierda, dentro del rectángulo del campo y en orden vertical. Un campo con `followingKey` coloca ambos valores al pie de los renglones, con el segundo inmediatamente después del primero. Ese bloque usa renglones del mismo ancho.

Publicación copia el directorio completo a `Resources/Documents`. Configurar `Documents__RootPath` solo para un catálogo externo completo; configurar `Documents__FontsRootPath` con fuentes autorizadas. No incorporar fixtures de tmp/pdf-qa ni habilitar la Carta SS con una base sintética. La prueba de publicación local compara hashes de los 14 recursos actuales; no sustituye la revisión institucional de formatos y estampas.

Runtime Alpine requiere `icu-libs`, `icu-data-full`, `tzdata` y `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false` para fechas en español y año IMSS de México. El paquete ICU limitado a inglés no cumple este requisito.

Estampas opcionales: `institutional: true`, `stamps: [{ file, sha256, authorityRoleKey, x, y, width, height, opacity, page }]`. Coordenadas en puntos desde arriba/izquierda; página inicia en 0. La imagen conserva proporciones y se centra en su rectángulo. Requiere autoridad activa en datos de emisión. Opacidad 0–1. Solo PNG con hash válido.

Los PNG en `signatures/dummy` y `stamps/dummy` son marcas genéricas de PRUEBA. No se aplican automáticamente. La Carta SS continúa bloqueada sin base limpia. Los anexos de firma física no admiten estampas.

El ejemplo de aceptación se exporta una sola vez desde el DOCX original mediante `tools/pdf/export_acceptance.ps1` (Word en Windows). Su contenido ilustrativo se conserva; no constituye carta personalizada ni modifica el original. Conservar su hash al volver a preparar las otras plantillas.

Acta Individual: preparar con `pwsh -File tools/pdf/export_individual_act.ps1 -PythonPath <python autorizado>`. Exporta el DOCX original en modo lectura y elimina los textos variables y subrayados de combinación antes de registrar `ss-individual-act`. El preparador general conserva esta entrada externa. Fuente dinámica Arial; sin estampas. Revisar el PDF generado y ejecutar verify_templates.py antes de publicar.
