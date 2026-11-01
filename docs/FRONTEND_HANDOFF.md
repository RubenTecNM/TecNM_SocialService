# Guía de Traspaso Frontend (Developer Handoff) — TecNM SSPP

Esta guía resume todo lo necesario para comenzar a desarrollar las vistas y funcionalidades del frontend del sistema TecNM SSPP.

---

## 🚀 1. Entorno de Ejecución Local

El backend corre en un contenedor Docker con PostgreSQL 18 y .NET 10 Web API. El frontend es una SPA en Vue 3 + TypeScript servida con Vite en `http://127.0.0.1:5900`.

### Arrancar Backend y Base de Datos
```bash
docker compose up -d
```
> Verifica que los contenedores `tecnm_postgres` (puerto 5432) y `tecnm_backend` (puerto 8080) estén corriendo con `docker ps`.

### Arrancar Frontend
> [!IMPORTANT]
> Usar **únicamente `pnpm`**. Nunca usar `npm` ni `yarn`.

```bash
pnpm --prefix frontend install
pnpm --prefix frontend run dev
```

El servidor Vite correrá en `http://127.0.0.1:5900` con proxy automático configurado en `vite.config.ts`: todas las peticiones a `/api/*` se reenvían transparentemente a `http://localhost:8080/api/*`.

---

## 🔑 2. Cuentas de Prueba Pre-Cargadas

Ambas cuentas están sembradas y listas en la base de datos:

| Rol | Usuario | Contraseña | Perfil / Matrícula |
| :--- | :--- | :--- | :--- |
| **Administrador / Depto. Vinculación** | `admin` | `TecNM-Demo-2026!` | Acceso total a padrón, revisión documental, reportes y catálogo |
| **Estudiante** | `usuario` | `TecNM-Demo-2026!` | No. Control `I23050363`, Ing. Industrial, 7° Semestre, 72% créditos |

---

## 🏛️ 3. Arquitectura y Reglas del Frontend

1. **Vertical Slices:** Cada dominio funcional vive en `frontend/src/modules/<modulo>/` con la siguiente estructura estándar:
   ```
   frontend/src/modules/<modulo>/
   ├── api/          # Métodos Axios tipados que consumen el backend
   ├── specs/        # Especificaciones canónicas (SDD) del módulo
   ├── types/        # Interfaces y tipos TypeScript
   ├── views/        # Vistas Vue (<script setup lang="ts">)
   ├── routes.ts     # Definición de rutas del módulo
   └── index.ts      # Barril de exportación pública
   ```
2. **Componentes Compartidos:** Siempre priorizar la biblioteca existente en `frontend/src/shared/components/` (`UiBasePage`, `UiCard`, `UiTable`, `UiButton`, `UiField`, `UiDialog`, `UiAlert`, `UiBadge`, `UiToast`, etc.).
3. **Páginas:** Toda vista de ruta debe tener como raíz `<UiBasePage :fluid="true">` (o `:fluid="false"` para formularios compactos de perfil).
4. **Autenticación:** Las sesiones se manejan mediante cookies `HttpOnly` (`withCredentials: true` en `src/shared/api/http.ts`). Si el backend responde `401`, el interceptor redirige automáticamente al login.

---

## 🗺️ 4. Mapa de Módulos, Rutas y Especificaciones (SDD)

Todas las especificaciones canónicas de UI ya fueron redactadas y están vinculadas en el código base:

| Módulo | Ruta URL | Spec Canónica | API & Tipos | Vistas Iniciales |
| :--- | :--- | :--- | :--- | :--- |
| **Padrón Estudiantes** | `/students`, `/students/me` | [`students.spec.md`](../frontend/src/modules/students/specs/students.spec.md) | `studentsApi.ts`, `students.ts` | `StudentsListView.vue`, `StudentProfileView.vue` |
| **Revisión Documental** | `/expedients/review` | [`expedients.spec.md`](../frontend/src/modules/expedients/specs/expedients.spec.md) | `expedientsApi.ts`, `expedients.ts` | `ExpedientsReviewView.vue` |
| **Servicio Social** | `/social-service`, `/social-service/my` | [`social-service.spec.md`](../frontend/src/modules/social-service/specs/social-service.spec.md) | `socialServiceApi.ts`, `socialService.ts` | `SocialServiceListView.vue`, `StudentSocialServiceView.vue` |
| **Prácticas Profesionales**| `/internships`, `/internships/my` | [`internships.spec.md`](../frontend/src/modules/internships/specs/internships.spec.md) | `internshipsApi.ts`, `internships.ts` | `InternshipsListView.vue`, `StudentInternshipsView.vue` |
| **Calidad PNC (ISO 9001)**| `/quality/pnc` | [`quality.spec.md`](../frontend/src/modules/quality/specs/quality.spec.md) | `qualityApi.ts`, `quality.ts` | `NonConformancesView.vue` |
| **Reportes & Dashboard** | `/reports/dashboard`, `/reports/tracking`, `/reports/stats` | [`reports.spec.md`](../frontend/src/modules/reports/specs/reports.spec.md) | `reportsApi.ts`, `reports.ts` | `DashboardView.vue`, `TrackingSheetView.vue`, `StatisticsView.vue` |
| **Catálogo Carreras** | `/catalogs/careers` | [`careers.spec.md`](../frontend/src/modules/catalogs/specs/careers.spec.md) | `careersApi.ts`, `careers.ts` | `CareersView.vue` |

---

## 📋 5. Prioridades de Trabajo Sugeridas

Consulta el plan detallado en [03-frontend-roadmap.md](./plans/03-frontend-roadmap.md). Se recomienda avanzar en el siguiente orden:

1. **Portal Estudiante (Fase 4 & 5):**
   - Completar formulario de actualización de NSS IMSS y subida de constancia médica en `/students/me`.
   - Implementar el componente de carga de archivos PDF (`uploadStudentDocument`) con validación de tipo MIME y tamaño (&le; 10 MB).
2. **Servicio Social Alumno (Fase 6):**
   - Implementar formulario de apertura (Anexo XVIII) y descarga directa de Carta de Aceptación Interna con los datos pre-rellenados.
   - Implementar subida de reportes bimestrales (R1..R3) y evaluaciones (EV1..EV3).
3. **Prácticas Profesionales Alumno (Fase 7):**
   - Implementar formulario simplificado que emula Microsoft Forms para generar la carta de presentación.
4. **Portal Administrativo / Vinculación:**
   - Detalle de expediente de alumno para revisar y dictaminar documentos (Aprobar / Rechazar con motivo).
   - Matriz de Seguimiento (`/reports/tracking`): vista réplica de Excel con filtros por carrera y periodo.

