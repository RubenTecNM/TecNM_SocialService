> Autorización vigente: [Acceso por proceso](../../../../../docs/specs/process-access.spec.md). Sus reglas prevalecen sobre permisos anteriores incompatibles.

> Aplicar primero [Flujo documental v2](../../../../../docs/specs/document-workflow-v2.spec.md). Sin datos ficticios ante fallos; estados y acciones los decide la API.

# Especificación Frontend: Módulo Estudiantes & Padrón

> **Contrato Backend:** [`docs/specs/students.spec.md`](../../../../../docs/specs/students.spec.md)  
> **Roles:** `student` (perfil propio `/me`), `department_head` (gestión completa, edición global y cuentas), `social_service_officer` (su proceso, seguimiento SS, alta e importación), `internship_coordinator` (su proceso y alta).

---

## 1. Vistas Requeridas

| Ruta | Nombre | Componente Principal | Rol Permitido | Propósito |
| :--- | :--- | :--- | :--- | :--- |
| `/students` | `students-list` | `StudentsListView.vue` | Staff, Dept Head | Listado general, filtros avanzados y acciones de padrón. |
| `/students/import` | `students-import` | `StudentImportView.vue` | Staff | Asistente de importación masiva por Excel (`.xlsx`). |
| `/profile` (o `/students/me`) | `student-profile` | `StudentProfileView.vue` | `student` | Consulta de estatus escolar, edición de contacto y subida de Vigencia IMSS. |

---

## 2. Requerimientos de UI y Componentes

### 2.1 Vista `/students` (Directorio y Padrón Administrativo)
- **Contenedor:** `UiBasePage` (`fluid=true`, título: "Padrón de Estudiantes").
- **Header Actions:**
  - Barra visible para personal: «Exportar alumnos (.csv)» obtiene todas las páginas con los filtros actuales y el alcance autorizado; los errores impiden descargar resultados parciales.
  - Jefe de Vinculación (`department_head`), encargado SS (`social_service_officer`) y coordinador PP (`internship_coordinator`): «Descargar plantilla Excel (.xlsx)» y «Agregar alumno manualmente», conectados a descarga y formulario/POST. Jefe de Vinculación (`department_head`) y encargado SS (`social_service_officer`): «Importar Padrón Excel». Editar/cuentas son exclusivos de `department_head`.
  - Acciones con ajuste de línea; en móvil ocupan el ancho disponible, permiten texto multilínea y mantienen al menos 44 px de altura.
  - Botón secundario con icono: "Importar Excel" -> navega a `/students/import`.
  - Botón principal: "Nuevo Estudiante" -> abre `UiDialog` de alta individual.
- **Barra de Filtros:**
  - `UiField` con buscador por matrícula, nombre o apellidos (con debounce 300 ms).
  - Select de Carrera (`/api/careers/options`).
  - Select de Semestre (1 a 15).
  - Select de Turno (`morning`, `afternoon`, `night`).
  - Toggle o Select de Elegibilidad: Todos / Elegibles SS / Elegibles PP.
  - Toggle o Select de Seguro IMSS: Todos / Vigente / Pendiente.
- **Tabla de Estudiantes (`UiTable`):**
  - Columnas:
    - `controlNumber`: Matrícula en mono/negrita (ej. `I23050363`).
    - `fullName`: Nombre completo del alumno.
    - `career`: Clave de carrera (badge informativo).
    - `semester`: Semestre y turno.
    - `progress`: Porcentaje de créditos reticulares (`curriculumProgressPercentage%`).
    - `imss`: `UiBadge` (`success` "Vigente", `warning` "Subido", `danger` "Sin vigencia").
    - `status`: `UiBadge` (`success` Inscrito / `danger` Baja o Suspendido).
    - `actions`: Botón "Ver Expediente", "Editar Datos" y "Generar/Resetear Cuenta".
- **Modales:**
  - `StudentFormDialog.vue`: Modal con pestañas o secciones claras para Datos Personales (CURP, Nombre, Apellidos, Sexo), Datos Escolares (Carrera, Semestre, Turno, Créditos) y Datos de Contacto (Emails, Teléfono).
  - `ProvisionAccountDialog.vue`: Muestra usuario generado (`username`), estado real y matrícula como contraseña habitual inicial.

---

### 2.2 Vista `/students/import` (Asistente Excel Masivo)
- **Contenedor:** `UiBasePage` con breadcrumb hacia `/students`.
- **Paso 1: Descarga de Plantilla Oficial:**
  - Tarjeta `UiCard` explicativa que incluye botón para descargar plantilla oficial `.xlsx` vía `GET /api/student-imports/template`.
  - Instrucciones claras sobre nombres de columnas exactos (`control_number, curp, first_surname, first_name, career_code, current_semester, completed_credits, shift, institutional_email, personal_email, phone_number`).
- **Paso 2: Carga de Archivo:**
  - Zona de soltar archivo (Drag & Drop) que valida extensión `.xlsx` y tamaño `<= 10 MB`.
  - Selector de Periodo Académico de destino (`/api/academic-periods/options`).
  - Checkbox opcional: "Proveer cuentas de acceso automáticamente a nuevos alumnos".
- **Paso 3: Resultados y Resumen:**
  - Al completar la subida (`POST /api/student-imports`), renderiza métricas (`UiMetric`): Total procesados, Nuevos insertados, Actualizados y Errores.
  - Tabla de incidencias si existieron filas con datos no válidos.

---

### 2.3 Vista `/profile` (Perfil del Estudiante)
- **Contenedor:** `UiBasePage` centrado o fluido con dos columnas:
  - **Columna 1 (Resumen Escolar):** Tarjeta con Matrícula, Carrera, Semestre, Turno, Avance Reticular y estatus de elegibilidad para Servicio Social y Prácticas Profesionales.
  - **Columna 2 (Datos de Contacto):** Formulario editable para Teléfono y Correo personal (`PUT /api/students/me/contact`).
- **Sección de Vigencia IMSS:**
  - Estado actual con `UiAlert` o `UiBadge`.
  - Si no tiene documento o fue rechazado: Zona de carga de PDF (`PUT /api/students/me/imss`, máx. 5 MB).
  - Si ya tiene archivo subido: Botón "Ver Constancia Actual" (abre visor PDF) y botón "Reemplazar Constancia".


## Seguimiento semestral SS

El backend calcula `socialServiceFollowUp` independientemente del estado del trámite. Sin PDF aportado por el alumno y aprobado en el expediente SS vigente: periodo inicial = `first_notice`, uno posterior = `second_notice`, dos = `third_notice`, tres o más = `non_compliant`. Contar periodos por fecha; no usar semestre escolar. IMSS, origen system, borrados y sustituidos no acreditan avance. Con evidencia aprobada: `in_progress`, incluso antes de activar. Liberado prevalece; PNC formal se conserva si no existe nuevo trámite vigente. Cancelados no aportan evidencia. PNC del padrón no crea casos de Calidad ni cierra trámites.

Guardar `socialServiceEligiblePeriodId` al crear: periodo del Excel o selección manual (default activo). Reimportar nunca lo modifica. Admin puede corregirlo; registrar autor y fecha. Registros anteriores sin origen comprobado quedan `pending_setup`; no inferir origen del último lote. Sin periodo activo tampoco calcular avisos.

`GET /api/students` agrega `socialServiceFollowUp` (status, eligiblePeriodId/Code, trackingPeriodId/Code, elapsedSemesters, socialServiceId, approvedDocumentsCount, lastApprovedAt, progressAssessment, autoReason). Filtros `followUpStatus` y `followUpProgress` antes de paginar. Valores de valoración: `consistent`, `low_progress`; sin periodo activo queda `pending` (`null`). Cuando `status` es `in_progress`, el backend calcula `progressAssessment` automáticamente y la UI muestra únicamente la etiqueta de constancia documental (`Constante`, `Con poco avance` o `Pendiente de valoración`, sin `(automático)`) y el criterio correspondiente, sin opción ni diálogo de validación manual. Poco avance no reactiva avisos; dos semestres son referencia, no límite.

El coordinador PP no recibe seguimiento SS. Encargado SS consulta candidatos sin trámite mediante resumen: identidad, carrera y seguimiento; otros campos nulos, sin habilitar perfil completo. `canReadProfile` indica acceso a las acciones existentes.

UI: columna Seguimiento SS, valoración automática, periodo inicial, motivo y filtros (sin diálogo Valorar seguimiento ni validación manual). La insignia principal muestra siempre el estatus administrativo/aviso (`Primer aviso`, `Segundo aviso`, `Tercer aviso`, `Producto no conforme`, `Liberado` o `Periodo de aptitud pendiente` según `elapsedSemesters` cuando `status` es `in_progress`), y cuando el alumno está en proceso (`in_progress`) se muestra `En proceso de realización del servicio` como distinción secundaria junto con la valoración documental. Mostrar periodo activo. Conservar pendientes de cohortes anteriores. Textos y colores accesibles. Sin cortes históricos inmutables.

## Padrón compacto y detalle (vigente)

Una sola tabla en `/students`: nombre, matrícula, carrera/semestre, seguimiento SS (según rol) y Ver detalle. La cola IMSS de preingreso permanece en `/expedients/review`; no se monta ni consulta automáticamente en el padrón. El detalle reutiliza UiDialog, carga datos completos solo para perfiles autorizados y concentra datos personales, académicos, contacto, cuenta, IMSS y seguimiento. Candidatos restringidos muestran únicamente el resumen permitido, sin peticiones al perfil. Acciones de editar, cuentas y dictamen IMSS quedan dentro del detalle. Filtros principales: búsqueda con autocompletado dinámico y debounce (`280 ms` mostrando sugerencias por matrícula, nombre y carrera), carrera y seguimiento; turno/IMSS/constancia dentro de Más filtros. Paginación estandarizada en `10` registros por página (`pageSize = 10`) sincronizada con filtros y buscador, en contenedor a pantalla completa (`100dvh`) con scroll interno de tabla y cabecera fija. El modal de importación masiva por Excel (`isImportModalOpen`) se cierra automáticamente al finalizar la importación sin errores (`failedRecords === 0`), actualizando la tabla del padrón sin recargar la página; si ocurre un error de transporte, validación o filas rechazadas (`failedRecords > 0`), el modal permanece abierto mostrando la alerta y el desglose de errores. Tabla sin cortes en escritorio; en móvil (`<= 900px`) la tabla usa desplazamiento horizontal táctil legible sin comprimir columnas fijas, acciones y filtros a ancho completo, y el perfil estudiantil apila tarjetas y carga de PDF sin desbordar pantalla. Mostrar carga, error recuperable, vacío, paginación y cierre por Escape con retorno de foco. Mantener datos y permisos existentes.

## Validación uniforme

Los formularios de captura muestran errores locales y del servidor junto al campo con UiField y resumen UiFormErrors dentro del formulario/modal. Las rutas anidadas se conservan; errores sin control visible permanecen en el resumen. Blur valida, input revalida después de la primera visita. Se conserva el contenido al fallar, se enfoca el primer control inválido y se limpia el estado al cambiar de registro/formulario. Backend conserva reglas, estados HTTP y autorización.
