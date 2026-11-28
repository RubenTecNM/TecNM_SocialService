# Gestión de plantillas PDF

Las diez plantillas del catálogo PDF se versionan en PostgreSQL. Cada versión publicada conserva PDF, SHA-256 y mapa. Las aperturas SS y PP fijan las activas del área en su transacción. R1–R3 comparten formato. Publicar y restaurar afectan aperturas posteriores. Las plantillas asignadas se usan también al descargar bases desde el expediente. Los archivos ya guardados y la emisión de Carta de Presentación SS conservan sus flujos.

Bajo `/api/template-management`, Administración administra ambas áreas y cada encargado administra la suya, incluida publicación y restauración. Las escrituras requieren token antifalsificación. Cada clave tiene un borrador. Las revisiones de concurrencia protegen cambios del borrador y de la versión activa. Publicar y restaurar requieren motivo y registran un evento de auditoría.

La carga acepta PDF estático limpio de hasta 5 MiB, sin cifrado, formularios, anotaciones, acciones ni adjuntos, con páginas sin rotación, origen cero y recorte igual a la página. El mapa conserva los campos y reglas del catálogo. El editor configura caja, renglones, tipografía y alineación en puntos PDF. La publicación valida campos, fuentes y previsualizaciones sintéticas normal y larga con el motor oficial.

El importador toma el manifiesto desplegado y asigna su línea base a los expedientes existentes. Volver a ejecutarlo conserva versiones ya publicadas y asignaciones previas.

Administración gestiona revisiones PNG inmutables en la biblioteca de logotipos. Cada área selecciona una revisión y ajusta su posición dentro de la zona editada de una base limpia. Una versión publicada fija las imágenes. Descargas en blanco, muestras y generación oficial incluyen las capas correspondientes.

Terminación SS y el ejemplo de Aceptación resuelven los destinatarios institucionales vigentes al descargar, al igual que el Acta Individual. Las versiones históricas con nombres impresos se vinculan por hash a una copia compatible que conserva su diseño y sus logotipos. Las asignaciones y evidencias existentes se conservan. Un borrador anterior puede adoptar explícitamente una base limpia compatible conservando sus campos.
