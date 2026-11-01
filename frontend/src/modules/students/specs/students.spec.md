# Especificación Frontend: Módulo Estudiantes & Padrón

> **Contrato Backend:** [`docs/specs/students.spec.md`](../../../../../docs/specs/students.spec.md)  
> **Roles:** `student` (perfil propio `/me`), `admin`, `social_service_officer`, `internship_coordinator` (gestión completa), `department_head` (lectura).

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
  - `ProvisionAccountDialog.vue`: Muestra usuario generado (`username`), contraseña temporal generada y botón de copiar al portapapeles.

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

