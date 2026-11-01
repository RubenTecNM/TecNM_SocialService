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
Este componente se incrusta en el detalle de Servicio Social y Prácticas Profesionales:
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
- **Contenedor:** `UiDialog` de gran tamaño (90% del viewport).
- **Barra de Herramientas:**
  - Título con código y nombre original del archivo (ej. `I23050363_CA_Carta_Aceptacion.pdf`).
  - Botón de descarga directa (`/api/documents/{id}/file`).
  - Botón de cerrar modal.
- **Área de visualización:**
  - Elemento `<iframe>` o `<object>` apuntando al endpoint seguro de descarga con `blob: URL` para no filtrar credenciales.
  - Mensaje de fallback si el navegador no soporta visualizador nativo de PDF.

---

### 2.3 Vista `/documents/review-queue` (Bandeja de Vinculación)
- **Contenedor:** `UiBasePage` (`fluid=true`, título: "Bandeja de Revisión Documental").
- **Barra de Filtros:**
  - Selector de Trámite: Todos / Servicio Social / Prácticas Profesionales / Expediente General.
  - Selector de Código de Documento (ej. solo `CA`, solo `R1`, solo `CT`).
  - Selector de Periodo Académico.
  - Buscador libre (matrícula o nombre de estudiante).
- **Tabla de Revisión (`UiTable`):**
  - Columnas: Alumno (matrícula y nombre), Carrera, Trámite, Documento (código y nombre), Fecha de Envío, Acciones.
  - **Acciones Rápidas:**
    - Botón "Inspeccionar PDF": Abre `DocumentViewerModal.vue` en pantalla dividida o modal.
    - Botón "Aprobar": Consume `POST /api/documents/{id}/review` con `{ status: "approved" }`.
    - Botón "Rechazar": Abre modal solicitando el `rejectionReason` obligatorio (máx. 1000 caracteres).

