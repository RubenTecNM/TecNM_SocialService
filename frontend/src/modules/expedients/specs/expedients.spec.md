> Autorización vigente: [Acceso por proceso](../../../../../docs/specs/process-access.spec.md). Sus reglas prevalecen sobre permisos anteriores incompatibles.

> Aplicar primero [Flujo documental v2](../../../../../docs/specs/document-workflow-v2.spec.md). Sin datos ficticios ante fallos; estados y acciones los decide la API.

# Especificación Frontend: Módulo Expediente Documental & Archivos

> **Contrato Backend:** [`docs/specs/documents.spec.md`](../../../../../docs/specs/documents.spec.md)  
> **Roles:** `student` (consulta y subida de propios), `social_service_officer` (revisa SS y general), `internship_coordinator` (revisa PP y general), `admin` (todo).

---

## 1. Vistas y Componentes Requeridos

| Componente / Ruta | Nombre | Tipo | Rol | Propósito |
| :--- | :--- | :--- | :--- | :--- |
| `ExpedientChecklist.vue` | Componente | Compartido en módulo | Todos | Checklist interactivo por trámite (`SOL, CP, CA, CC, R1..3, EV, CT, COT`). |
| `DocumentViewerModal.vue` | Modal | `UiDialog` | Todos | Visor de PDF embebido (`<iframe src="...">`) con descarga y zoom. |
| `/documents/review-queue` | Vista | `ReviewQueueView.vue` | Staff | Bandeja unificada de revisión para Vinculación. |

---

## 2. Requerimientos de UI y Componentes

### 2.1 Componente `ExpedientChecklist.vue`
Este componente se incrusta en el detalle de Servicio Social y Prácticas Profesionales en una cuadrícula responsiva de dos columnas (`checklist-grid`) adaptada al tema claro/oscuro mate:
- **Encabezado del Checklist:**
  - Barra de progreso visual con porcentaje de avance (`documentProgressPercentage%`).
  - Contador de documentos aprobados (ej. `4 de 10 completados`).
- **Filas de Documento:**
  Para cada documento normativo (`CP, CA, CC, R1..R3, EV1..EV3, EV, CT`):
  - **Identificador y Formato:** Código en badge (ej. `CA`, `R1`) y título descriptivo (ej. "Anexo XXII — Reporte Bimestral 1").
  - **Estado:**
    - `approved`: Badge verde ("Aprobado / Válido").
    - `pending`: Badge amarillo ("En revisión por Vinculación").
    - `rejected`: Badge rojo ("Observado") con botón que despliega el motivo de rechazo en `UiCallout`.
    - `missing`: Badge neutral ("Pendiente de entrega").
  - **Acción 1 — "1. Llenar e Imprimir Formato":**
    - Visible si el documento tiene plantilla prellenada por el sistema (`SOL`, `CA` cuando es local, `CC`, `R1..R3`, `EV1..EV3`, `EV`).
    - Al dar clic, abre el modal de datos o descarga/imprime directamente el PDF ya con todos los campos llenos.
  - **Acción 2 — "2. Subir PDF firmado y sellado":**
    - Abre modal de carga que valida que el archivo sea un PDF `<= 5 MB`.
    - Al subir, consume `POST /api/students/me/documents` o `POST /api/students/{id}/documents`.
  - **Acción 3 — "Ver Documento":**
    - Visible si ya hay un PDF cargado; abre `DocumentViewerModal.vue`.

---

### 2.2 Modal `DocumentViewerModal.vue` (Visor de PDF)
- **Contenedor:** `UiDialog` de gran tamaño (`size="full"`, hasta 95vw × 94dvh del viewport) manteniendo visibles los controles de acción y cierre.
- **Barra de Herramientas:**
  - Título con código y nombre original del archivo (ej. `I23050363_CA_Carta_Aceptacion.pdf`).
  - Botón de descarga directa (`/api/documents/{id}/file`).
  - Botón de cerrar modal.
- **Área de visualización:**
  - Elemento `<iframe>` o `<object>` apuntando al endpoint seguro de descarga con `blob: URL` para no filtrar credenciales.
  - Mensaje de fallback si el navegador no soporta visualizador nativo de PDF.

---

### 2.3 Vista `/documents/review-queue` (Bandeja de Vinculación)
- **Contenedor:** `UiBasePage` (`fluid=true`, layout `fullscreen-page` 100dvh sin scroll exterior, encabezado ejecutivo "Revisión y Dictamen de Expedientes" con 4 KPIs de borde dorado activo, respaldo operativo local cuando la API no está disponible, previsualización integrada en `UiDialog` `size="full"` y notificaciones `UiToast` sin usar `window.alert`).
- **Barra de Filtros:**
  - Selector de Trámite: Todos / Servicio Social / Prácticas Profesionales / Expediente General.
  - Selector de Código de Documento (ej. solo `CA`, solo `R1`, solo `CT`).
  - Sin filtro manual de `Id de periodo` en la interfaz.
  - Buscador libre con autocompletado interactivo y debounce de `280 ms` (matrícula, nombre de estudiante, código de formato o carrera) y botón único azul (`✕`) para limpiar.
- **Tabla de Revisión (`UiTable`):**
  - Ordenamiento: Documentos ordenados desde el más reciente hasta el más antiguo (`createdAt` descendente).
  - Paginación estandarizada: `10` registros por página (`pageSize = 10`) con controles `Anterior`, `Página X de Y` y `Siguiente`.
  - En móvil (`<= 900px`), KPIs en cuadrícula `2×2`, barra de filtros y pestañas deslizables sin recorte, tabla con scroll horizontal táctil y visor PDF / checklist adaptados al ancho móvil sin afectar escritorio.
  - Sincronización en tiempo real vía SignalR (`useRealtime`): actualiza automáticamente la cola al recibir `document.uploaded` / `document.reviewed` / `document.deleted` y muestra indicador en vivo cuando otro revisor tiene abierto un PDF (`ReviewPresenceUpdated`).
  - Columnas: Alumno (matrícula y nombre), Carrera, Trámite, Documento (código y nombre), Fecha de Envío, Acciones.
  - **Acciones Rápidas:**
    - Botón "Inspeccionar PDF": Abre `DocumentViewerModal.vue` en pantalla dividida o modal.
    - Botón "Aprobar": Consume `POST /api/documents/{id}/review` con `{ status: "approved" }`.
    - Botón "Rechazar": Abre modal solicitando el `rejectionReason` obligatorio (máx. 1000 caracteres).


## Prellenado SS

Aplicar la matriz de prellenado del contrato documental v2: reportes con evaluador propio o supervisor general; XXIII con alumno, programa y supervisor del trámite; XXIV con alumno y programa; XXV conserva su prellenado. Firmas y sellos manuales vacíos. Evaluaciones se descargan mediante pdfKey, conservando códigos históricos y sin registrar calificaciones.


## Carta de Terminación SS

CT descarga el Anexo XIX prellenado mediante pdfKey termination_letter. Reutiliza alumno, programa, actividades, fechas, horas registradas, ciudad y supervisor según el contrato documental v2. Conserva destinatarios impresos, firmas/sello manuales vacíos y carga/revisión del PDF firmado; descargar no altera el trámite.

Carta Compromiso: descarga directa con domicilio de solicitud, teléfono del perfil y domicilio de la dependencia, usando respaldos registrados cuando falten. La fecha impresa se calcula al descargar en America/Mexico_City, sin reutilizar la fecha guardada. No exige captura previa de CC; firma del alumno vacía y ninguna escritura al descargar.

## Validación uniforme

Los formularios de captura muestran errores locales y del servidor junto al campo con UiField y resumen UiFormErrors dentro del formulario/modal. Las rutas anidadas se conservan; errores sin control visible permanecen en el resumen. Blur valida, input revalida después de la primera visita. Se conserva el contenido al fallar, se enfoca el primer control inválido y se limpia el estado al cambiar de registro/formulario. Backend conserva reglas, estados HTTP y autorización.
