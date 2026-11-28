# TecNM SSPP — Sistema de Servicio Social y Prácticas Profesionales

Plataforma institucional web para la gestión integral, digitalización, seguimiento normativo y acreditación del **Servicio Social** y las **Prácticas Profesionales** (Productividad Laboral) en el Tecnológico Nacional de México (**TecNM**), implementada para el Instituto Tecnológico Superior de Monclova (**ITSM**).

---

Pruebas del flujo documental actualizado: [guía de entorno local](docs/LOCAL_DOCUMENT_TESTING.md). Carta de Presentación SS usa temporalmente carga de PDF firmado y revisión de Vinculación.

## 🎯 Propósito del Proyecto

El proyecto **TecNM SSPP** moderniza y sustituye los flujos operativos manuales, dispersos en hojas de cálculo y expedientes físicos, centralizando la interacción entre estudiantes, dependencias receptoras y el Departamento de Gestión Tecnológica y Vinculación.

### Objetivos Principales

1. **Gestión Integral de Servicio Social (SS):**
   - Validación automática de prerrequisitos (avance reticular $\ge 70\%$ de créditos y vigencia de seguridad médica IMSS).
   - Tramitación digital de formatos normativos del TecNM:
     - **Anexo XVIII:** Solicitud formal de Servicio Social.
     - **Anexo XX:** Carta compromiso institucional.
     - **Anexo XXII:** 3 reportes bimestrales de avance acumulativo hasta completar 480 horas obligatorias (mínimo 6 meses).
     - **Anexos XXIII y XXIV:** Evaluaciones cualitativas (evaluación externa de la empresa y autoevaluación del prestador).
     - **Anexo XXV:** Evaluación de impacto social y actividades.
   - Emisión automatizada de cartas de presentación, recepción de cartas de aceptación y terminación, y expedición de la constancia oficial de liberación.

2. **Gestión de Prácticas Profesionales (PP):**
   - Vinculación formal con empresas de los sectores privado, público y social.
   - Control de convenios de estancia (200 a 480 horas convenidas), horarios y supervisores de área.
   - Evaluación de desempeño laboral (**Anexo XVII**) y acreditación de los 2 créditos complementarios de Productividad Laboral.

3. **Control Documental y Auditoría Institucional:**
   - Expediente digital único por alumno para carga, dictamen y validación de archivos comprobatorios en formato PDF.
   - Semaforización preventiva del padrón de alumnos (avisos de cumplimiento y estatus en tiempo real).
   - Generación atómica y consecutiva de folios y oficios institucionales sin colisiones.
   - Catálogo configurable de autoridades institucionales (directores, jefes de departamento y coordinadores) para membretes y firmas sin datos quemados en plantillas.
   - Bitácora de auditoría completa con borrado lógico (*soft delete*) en todas las tablas transaccionales.

### Actores del Sistema

| Rol | Identificador | Responsabilidades Clave |
| :--- | :--- | :--- |
| **Estudiante** | `student` | Consulta de elegibilidad, registro de solicitudes, descarga de formatos oficiales, carga de constancias y reportes bimestrales. |
| **Oficial de Servicio Social** | `social_service_officer` | Dictamen de solicitudes, revisión de reportes bimestrales, validación de cartas de aceptación/terminación y trámite de liberación. |
| **Coordinador de Prácticas** | `internship_coordinator` | Registro de convenios de estancia profesional, asignación de supervisores de empresa, validación de evaluaciones y acreditación de créditos. |
| **Jefe de Vinculación / Depto.** | `department_head` | Gestión administrativa integral: usuarios, roles, catálogos, autoridades, plantillas, padrón, convenios, dictamen y firma de oficios oficiales. |

---

## 🛠️ Tecnologías Utilizadas

El sistema está diseñado bajo un modelo desacoplado: una API REST modular en el backend y una Single-Page Application (SPA) reactiva en el frontend.

### Backend

- **Runtime & Lenguaje:** [.NET 10](https://dotnet.microsoft.com/) / C# 13.
- **Framework Web:** ASP.NET Core Web API.
- **ORM & Persistencia:** [Entity Framework Core 10](https://learn.microsoft.com/ef/core/) con proveedor PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`).
- **Seguridad & Autenticación:** 
  - Emisión de JWT institucional encapsulado en cookies `HttpOnly`, `SameSite=Lax` y `Secure` (`auth_token`).
  - Cookie preventiva no-sensible `session_exp` para sincronización de sesión con el cliente.
  - Hashing seguro de credenciales y middleware de autorización basado en roles y políticas.
- **Inyección de Dependencias:** Contenedor nativo de .NET (`Microsoft.Extensions.DependencyInjection`).
- **Configuración:** `Microsoft.Extensions.Configuration` con soporte para variables de entorno y `dotnet user-secrets`.

### Frontend

- **Framework:** [Vue 3](https://vuejs.org/) utilizando la Composition API (`<script setup lang="ts">`).
- **Lenguaje:** [TypeScript](https://www.typescriptlang.org/) con tipado estricto.
- **Herramienta de Construcción:** [Vite](https://vitejs.dev/) para desarrollo ultrarrápido y compilación optimizada.
- **Enrutamiento:** [Vue Router 4](https://router.vuejs.org/) con rutas modulares descentralizadas y carga perezosa (*lazy loading*).
- **Comunicación HTTP:** [Axios](https://axios-http.com/) configurado con `withCredentials: true`, interceptores de expiración (401) y manejo transversal de errores.
- **Iconografía:** [Lucide Vue Next](https://lucide.dev/).
- **Visualización de Datos:** Chart.js y `vue-chartjs` para indicadores y semáforos de cumplimiento.
- **Exportación de Documentos:** SheetJS (`xlsx`) para exportación/importación de padrones y `jsPDF` / `jspdf-autotable` para reportes.
- **Estilos & Diseño:** CSS nativo estructurado sobre tokens semánticos globales, compatibilidad Dark/Light Mode y componentes modulares basados en principios de diseño del TecNM (sin acoplamiento a frameworks CSS pesados).

### Base de Datos

- **Motor:** [PostgreSQL 16+](https://www.postgresql.org/).
- **Estándar de Nomenclatura:** Tablas y columnas en inglés técnico bajo `snake_case`.
- **Identificadores:** `id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY`.
- **Auditoría Estándar (6 columnas transversales):** `created_by`, `updated_by`, `deleted_by`, `created_at`, `updated_at`, `deleted_at`.
- **Integridad y Rendimiento:** Constraints de llave foránea explícitas (`fk_{tabla}__{destino}`), índices en todas las FKs (`idx_{tabla}__{columna}`) y bloque transaccional idempotente en scripts DDL.

---

## 📁 Estructura del Proyecto

### Estructura Global

```text
tecnm-sspp/
├── README.md                      # Documentación principal del repositorio
├── AGENTS.md                      # Punto de entrada y contexto maestro para Agentes IA
├── .gitignore                     # Exclusiones de Git
├── backend/                       # API REST modular en .NET 10 + EF Core + PostgreSQL
├── frontend/                      # SPA en Vue 3 + TypeScript + Vite
├── database/
│   ├── README.md                  # Explicación de esquemas y convenciones de BD
│   ├── design/                    # Borrador del esquema relacional (schema.sql, README.md)
│   ├── seed/                      # Datos demo y usuarios de desarrollo (01_dev_users.sql)
│   └── legacy/                    # Referencia histórica de SET_MX (SQL Server) — SOLO CONSULTA
├── docs/
│   ├── architecture/              # Estándares (AUTH.md, BACKEND, FRONTEND)
│   ├── specs/                     # Contratos canónicos y especificaciones funcionales
│   ├── plans/                     # Planes de implementación técnica por fases
│   ├── legacy-db/                 # Documentación del esquema previo (85 tablas)
│   └── sources/                   # Formatos oficiales PDF/Excel y fuentes normativas
└── scripts/                       # Utilidades de automatización y verificación
```

### Arquitectura Backend (`backend/`)

Sigue el patrón **Vertical Slice** (*Screaming Architecture*), organizando el código por capacidades de negocio en lugar de capas técnicas horizontales:

```text
backend/
├── Core/
│   └── Entities/                  # Entidades de dominio compartidas (POCOs / EF Core)
├── Data/
│   └── TecNMDbContext.cs          # DbContext con mapeo relacional a PostgreSQL
├── Modules/                       # Slices verticales de negocio
│   └── Auth/                      # Rebanada vertical de Autenticación
│       ├── specs/                 # Especificaciones locales y contratos del slice
│       ├── AuthController.cs      # Thin Controller (enrutamiento HTTP y códigos de estado)
│       ├── AuthService.cs         # Fat Service (reglas de negocio, JWT y persistencia)
│       └── AuthDTOs.cs            # Contratos de entrada y salida desacoplados
├── Properties/
│   └── launchSettings.json        # Perfiles de ejecución local
├── appsettings.json               # Configuración base de la aplicación
├── TecNM.Api.csproj               # Archivo de proyecto .NET 10
└── Program.cs                     # Registro de servicios, DI y configuración de pipeline HTTP
```

### Arquitectura Frontend (`frontend/`)

Estructura modular con enrutamiento descentralizado y componentes reutilizables:

```text
frontend/
├── public/                        # Recursos estáticos globales (logos, favicon)
├── src/
│   ├── app/                       # Inicialización de la SPA
│   │   ├── router/                # Agregador central de rutas modulares
│   │   ├── App.vue                # Componente raíz
│   │   └── main.ts                # Bootstrap de la aplicación Vue
│   ├── modules/                   # Slices verticales del frontend
│   │   └── auth/                  # Módulo de Autenticación
│   │       ├── api/               # Llamadas Axios al módulo
│   │       ├── specs/             # Especificaciones locales del slice
│   │       ├── types/             # Tipos e interfaces TypeScript del módulo
│   │       ├── views/             # Vistas principales (LoginView.vue)
│   │       ├── routes.ts          # Definición de rutas con lazy loading
│   │       └── index.ts           # Barrel export (API pública del slice)
│   └── shared/                    # Capa transversal y componentes reutilizables
│       ├── api/                   # Cliente Axios central con interceptores y credenciales
│       ├── components/            # Design System (UiBasePage, UiCard, UiButton, etc.)
│       ├── composables/           # Lógica reactiva compartida (useTheme, etc.)
│       ├── layouts/               # Layouts principales (MainLayout, AuthLayout)
│       └── styles/                # Tokens CSS semánticos y temas (Dark/Light)
├── package.json                   # Dependencias y scripts de pnpm
├── tsconfig.json                  # Configuración estricta de TypeScript
└── vite.config.ts                 # Configuración del bundler y alias de imports
```

### Esquema de Base de Datos (`database/`)

El modelo relacional activo en [`database/design/schema.sql`](file:///c:/Dev/tecnm-sspp/database/design/schema.sql) se organiza en 3 capas de dominio:

1. **Núcleo Compartido (Shared Core):**
   - `roles`, `users`, `user_roles`: Autenticación institucional y control de acceso.
   - `careers`, `academic_periods`: Catálogo de carreras y ciclos escolares.
   - `student_import_batches`, `students`: Padrón y expediente estudiantil centralizado.
   - `companies`, `company_supervisors`: Dependencias receptoras y tutores externos.
   - `institutional_authorities`: Catálogo de autoridades para membretes dinámicos.
   - `document_series`: Generador atómico de folios y secuencias institucionales.
   - `expedient_documents`: Metadatos y control de aprobación de documentos digitales.
2. **Dominio Servicio Social:**
   - `social_services`: Registro maestro del trámite (fechas, horas, folios y estado).
   - `social_service_applications`: Detalle de la solicitud (Anexo XVIII).
   - `social_service_commitments`: Formalización de carta compromiso (Anexo XX).
   - `social_service_bi_monthly_reports`: Entregas bimestrales de 160h (Anexo XXII).
   - `social_service_evaluations`: Evaluaciones cualitativas (Anexos XXIII y XXIV).
   - `social_service_activity_assessments`: Evaluación de impacto social (Anexo XXV).
3. **Dominio Prácticas Profesionales:**
   - `internships`: Registro maestro del trámite (200-480h, horario y estado).
   - `internship_evaluations`: Evaluación de productividad laboral (Anexo XVII).

---

## 🤖 Desarrollo Guiado por Especificaciones (Spec-Driven Development - SDD)

El desarrollo en este repositorio se rige estrictamente por **Spec-Driven Development**:

1. **Especificación Primero:** Antes de programar o modificar código en cualquier módulo, se redactan o actualizan los requerimientos en su carpeta local `specs/` o en `docs/specs/`.
2. **Contexto Aislado:** Casos de uso, contratos DTO y reglas de negocio residen dentro de cada rebanada vertical para garantizar máxima claridad a desarrolladores y agentes de IA.
3. **Contrato como Fuente de Verdad:** Cuando un cambio impacta endpoints, DTOs o cookies, el contrato en [`docs/specs/auth.spec.md`](file:///c:/Dev/tecnm-sspp/docs/specs/auth.spec.md) se actualiza antes de modificar backend o frontend.
4. **Regla de Redacción:** Documentación y especificaciones concisas, directas y sin explicaciones superfluas (alineadas a la skill `caveman`).

### Skills Recomendadas para Agentes

- **`caveman`:** Respuestas y redacción técnica directa, sin relleno.
- **`ponytail`:** Solución mínima, limpia y mantenible (evita sobreingeniería).
- **`frontend-design`:** Dirección estética y apego a la identidad visual institucional.
- **`ui-ux-pro-max`:** Patrones de interacción, accesibilidad, semántica y diseño responsivo.

---

## 🏛️ Enlaces a Documentación Clave

- **[Estándar de Autenticación y Sesiones](file:///c:/Dev/tecnm-sspp/docs/architecture/AUTH.md):** Seguridad de cookies `HttpOnly`, ciclo del JWT y estrategia de sesión.
- **[Arquitectura Backend (.NET 10)](file:///c:/Dev/tecnm-sspp/docs/architecture/BACKEND_ARCHITECTURE.md):** Directrices de diseño para slices, servicios y controladores.
- **[Arquitectura Frontend (Vue 3 + TS)](file:///c:/Dev/tecnm-sspp/docs/architecture/FRONTEND_ARCHITECTURE.md):** Directrices de diseño SPA, layouts y componentes compartidos.
- **[Contrato Canónico de Autenticación](file:///c:/Dev/tecnm-sspp/docs/specs/auth.spec.md):** Especificación formal de contratos y códigos de respuesta.
- **[Plan de Implementación de Autenticación](file:///c:/Dev/tecnm-sspp/docs/plans/01-auth-implementation.md):** Hoja de ruta técnica de la fase inicial.
- **[Borrador de Diseño de Base de Datos](file:///c:/Dev/tecnm-sspp/database/design/README.md):** Detalle técnico de tablas, campos y llaves foráneas.
- **[Análisis y Esquema Legacy SET_MX](file:///c:/Dev/tecnm-sspp/docs/legacy-db/ESQUEMA_TABLAS.md):** Referencia histórica del sistema previo de 85 tablas.

---

## ℹ️ Créditos

Basado en la plantilla [TecNM_Project_Template](https://github.com/AngelZurita28/TecNM_Project_Template) de AngelZurita28. Desarrollado para la gestión institucional del Instituto Tecnológico Superior de Monclova (**ITSM**).

