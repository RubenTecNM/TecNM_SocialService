# Roadmap Frontend — Progresión de Desarrollo

> **Objetivo:** Guía ejecutiva y secuencial para la construcción del frontend SPA en Vue 3 + TypeScript, Composition API y Tailwind/CSS de tokens institucionales, conectado a la API .NET 10 y PostgreSQL 18.
> **Reglas y arquitectura:** Ver [`frontend/AGENTS.md`](../../frontend/AGENTS.md) y [`docs/architecture/FRONTEND_ARCHITECTURE.md`](../architecture/FRONTEND_ARCHITECTURE.md).
> **Contratos de API:** Ver [`docs/specs/api-conventions.spec.md`](../specs/api-conventions.spec.md) y contratos canónicos en [`docs/specs/`](../specs/).

---

## 🧭 Estado General de Slices Frontend

| Fase | Módulo / Slice | Entregables Clave | Estado |
| :---: | :--- | :--- | :---: |
| **0** | **Fundaciones, Layout & Auth** | Login JWT con cookies `HttpOnly`, `MainLayout` persistente, navegación dinámica por rol y tokens semánticos. | **100% Hecho** |
| **1** | **Catálogos Institucionales** | Carreras (`/catalogs/careers`), Periodos, Programas SS, Autoridades, Series de Folio, Empresas y Convenios. | En curso (Carreras lista) |
| **2** | **Estudiantes & Padrón** | Expediente de alumno (`/students`), perfil propio `/me` con carga de Vigencia IMSS y asistente de importación Excel `.xlsx`. | **Siguiente** |
| **3** | **Expediente Documental & Visor** | Componente checklist de documentos (`CP, CA, CC, R1..3, EV, CT`), visor PDF embebido y cola de revisión de Vinculación (`/documents/review-queue`). | Pendiente |
| **4** | **Servicio Social (SS)** | Portal de Alumno (asistente Anexo XVIII, prellenado de formatos Anexos XX–XXV listos para imprimir y firmar/sellar) y Portal de Vinculación (dictamen, folios `SS_CP...`, plantilla `CA` local y liberación `SSC...`). | Pendiente |
| **5** | **Prácticas Profesionales (PP)** | Portal de Alumno (solicitud Forms de `CP` con IMSS, registro posterior de `CA` membretada y prellenado Anexo XVII) y Portal de Vinculación (folios `EJ26/PR...` y `PPC...`). | Pendiente |
| **6** | **Calidad ISO 9001 & Reportes** | Trazabilidad de estados, Producto No Conforme (`/quality/non-conformances`), Sábana de Seguimiento en vivo (`/reports/tracking`), Estadístico Semestral y Formato 911.10. | Pendiente |

---

## 📋 Progresión Detallada por Fases

### Fase 0: Fundaciones, Layout & Autenticación
- [x] Arquitectura Vertical Slice con Vue 3, Vite, TypeScript y Composition API (`<script setup lang="ts">`).
- [x] Biblioteca de componentes compartidos en `src/shared/components/` (`UiBasePage`, `UiTable`, `UiDialog`, `UiField`, `UiBadge`, `UiButton`, `UiAlert`, `UiToast`).
- [x] Layout principal `MainLayout.vue` con sidebar responsivo y soporte para tema claro/oscuro.
- [x] Módulo `auth` con llamada a `/api/auth/login`, `/api/auth/me`, `/api/auth/logout` con `withCredentials: true`.
- [ ] Configuración de menú lateral dinámico según rol institucional (`student` vs `admin`/`staff`).

### Fase 1: Catálogos Institucionales
- [x] Catálogo de Carreras (`/catalogs/careers`): Tabla paginada, alta, edición, toggle activo/inactivo y baja lógica.
- [ ] Catálogo de Periodos Académicos (`/catalogs/periods`): CRUD, activación única transaccional y visualización de periodo activo.
- [ ] Catálogo de Programas de Servicio Social (`/catalogs/social-service-programs`): 9 categorías normativas y flag `is_internal`.
- [ ] Autoridades Institucionales y Series de Folio (`/catalogs/authorities`, `/catalogs/folios`): Mantenimiento de cargos oficiales y monitoreo de secuencias anuales.
- [ ] Directorio de Empresas y Convenios (`/companies`, `/agreements`): CRUD de empresas, supervisores externos y semáforo de vigencia de convenios (vigente, por vencer ≤60 días, vencido) con visor de PDF.

### Fase 2: Estudiantes, Padrón & Importador Excel
- [ ] Directorio administrativo de estudiantes (`/students`): Paginación, filtros por carrera, semestre, modalidad, turno, vigencia IMSS y buscador libre.
- [ ] Modal de alta individual y edición de expediente para casos específicos.
- [ ] Asistente de importación masiva por Excel (`/students/import`): Descarga de plantilla oficial `.xlsx`, carga mediante drag-and-drop, validación previa y reporte de filas insertadas/actualizadas/errores.
- [ ] Vista del estudiante `/me` (`/profile`): Consulta de datos escolares, actualización de contacto (teléfono y correo) y subida de Constancia de Vigencia de Derechos del IMSS con preview PDF.

### Fase 3: Expediente Documental & Bandeja de Revisión
- [ ] Componente visual `ExpedientChecklist.vue`: Lista tipo checklist donde cada documento muestra su estado (`Aprobado`, `En revisión`, `Observado: motivo`, `Pendiente`), porcentaje de avance relativo y acciones claras ("Llenar e Imprimir Formato" y "Subir PDF firmado y sellado").
- [ ] Visor modal de PDF `DocumentViewerModal.vue`: Renderizado seguro mediante stream de `/api/documents/{id}/file` con opciones de descarga y zoom.
- [ ] Bandeja de revisión para Vinculación (`/documents/review-queue`): Tabla paginada de documentos pendientes por carrera, tipo de trámite y buscador por matrícula/alumno, con botones "Aprobar" y "Rechazar" (con motivo obligatorio de rechazo).

### Fase 4: Módulo de Servicio Social (SS)
- [ ] **Flujo del Estudiante:**
  - Asistente de Apertura (Anexo XVIII): Selección de programa, empresa, supervisor, periodo y confirmación de vigencia IMSS.
  - Generador/Descarga de Carta de Presentación (`CP` con folio `SS_CP...`).
  - Cuando el servicio es local en el TecNM (`modality = "internal"`): Botón para descargar la plantilla oficial de Carta de Aceptación generada por el sistema lista para recabar firma/sello y subir el PDF escaneado (`CA`).
  - Formularios de prellenado para impresión directa:
    - Carta Compromiso (Anexo XX).
    - 3 Reportes Bimestrales (Anexo XXII).
    - Evaluaciones Cualitativas y Autoevaluación (Anexos XXIII y XXIV).
    - Evaluación de Actividades (Anexo XXV).
  - Barra de progreso documental en tiempo real (10% por cada documento validado).
- [ ] **Flujo de Vinculación:**
  - Bandeja de trámites de Servicio Social con filtros por carrera, periodo y estado.
  - Revisión y dictamen de solicitud Anexo XVIII.
  - Registro de fechas de hitos y validación de documentos.
  - Emisión de Constancia de Terminación con folio (`SSC...`) y generación de Acta para Servicios Escolares (`DGTV...`).

### Fase 5: Módulo de Prácticas Profesionales (PP)
- [ ] **Flujo del Estudiante:**
  - Asistente de Apertura (reemplazo del Forms oficial): Confirmación de semestre, turno, teléfono y correo institucional + subida de Vigencia IMSS para solicitar Carta de Presentación (`CP`).
  - Descarga de Carta de Presentación emitida con folio (`EJ26/PR...`).
  - Registro posterior del Oficio de Aceptación (`CA`): Confirmación de empresa, supervisor, fechas y horario + subida del oficio membretado.
  - Formulario de prellenado para Evaluación Anexo XVII (`PRODUCTIVIDAD LABORAL`, 7 criterios 0–4) con cálculo instantáneo de promedio y nivel de desempeño para impresión y firma/sello.
  - Subida de Evaluación (`EV`) y Oficio de Terminación (`CT`).
- [ ] **Flujo de Vinculación:**
  - Aprobación de solicitud y emisión de folios `PR...`.
  - Validación de empresa, supervisor y vigencia de convenio.
  - Acreditación con folio (`PPC...`) y acta a Servicios Escolares.

### Fase 6: Calidad ISO 9001 & Reportes Institucionales
- [ ] **Módulo de Calidad (`/quality/non-conformances`):**
  - Listado de Producto No Conforme con códigos automáticos `PNC-SS-...` y `PNC-PP-...`.
  - Modal de transición de estado (`open → under_review → resolved | cancelled`) con captura de plan de acción correctiva.
  - Modal de historial de estados (`tramite_status_logs`) con línea de tiempo visual por trámite.
- [ ] **Sábana de Seguimiento de Vinculación (`/reports/tracking`):**
  - Tabla completa idéntica a `SEGUIMIENTO VINCULACIÓN1.xlsx` (columnas `CP, CA, SOL, CC, R1, R2, R3, EV, CT, COT`) con barra de progreso interactiva (0% a 100%) y exportación a Excel con SheetJS.
- [ ] **Estadístico Semestral y por Ciclo Escolar (`/reports/statistics`):**
  - Tablero de control con conteos automáticos por carrera y sexo (*En condiciones*, *Solicitantes*, *Liberados*, *PNC* y tasa de liberación).
- [ ] **Formato SEP/TecNM 911.10 (`/reports/911`):**
  - Resumen oficial desglosado por sector (`Educativo`, `Público`, `Privado`, `Social`), tamaño de empresa y género.

---

## 🛠️ Comandos de Desarrollo y Verificación

```bash
# 1. Instalar dependencias del frontend (Estrictamente con pnpm)
pnpm --prefix frontend install

# 2. Iniciar servidor local de desarrollo Vite (conectado a backend http://localhost:8080)
pnpm --prefix frontend run dev

# 3. Compilación de producción y chequeo estricto de tipos TypeScript
pnpm --prefix frontend run build
```

