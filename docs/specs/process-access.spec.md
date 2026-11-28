# Acceso por proceso y estadísticas compartidas

Contrato vigente; prevalece sobre permisos contradictorios de contratos anteriores.

Padrón: jefe de Vinculación (`department_head`), encargado SS (`social_service_officer`) y coordinador PP (`internship_coordinator`) pueden registrar alumnos manualmente y descargar la plantilla vacía. La importación masiva por Excel (`POST /api/student-imports` y consulta de lotes) está habilitada para `department_head` y `social_service_officer`. La edición global y administración de cuentas son exclusivas de `department_head`. La exportación del listado usa exclusivamente los datos autorizados por `GET /api/students`, aplicando filtros y todas las páginas.

- SS consulta y aprueba únicamente expedientes y documentos SS; PP únicamente PP. Aplicar alcance a listados, detalles, PDF, historial, seguimiento y PNC antes de paginar.
- Alumno consulta y carga exclusivamente sus propias evidencias. Personal, incluido `department_head`, no carga formatos del alumno. Aceptación PP admite `internship_coordinator` y `department_head`; alumno solo consulta. Eliminación y reemplazo exigen el mismo permiso.
- Requisitos generales de preingreso: revisión compartida con datos mínimos. Después del ingreso, vincular su consulta al proceso propio.
- Padrón presenta IMSS dentro del detalle autorizado; la cola General/IMSS de preingreso permanece en la bandeja documental, sin duplicar tablas en el padrón. No amplía acceso a perfiles completos. Dictamen compartido mediante `/api/documents/{id}/review`; adaptador `/api/students/{id}/imss/validation` aplica el mismo alcance y devuelve documento, sin consultar detalle académico.
- Ambos encargados y `department_head` gestionan empresas, supervisores y convenios. Edición global y administración de cuentas del padrón son exclusivas de `department_head`; la consulta de los encargados se limita a su proceso.
- Estadísticas agregadas SS/PP disponibles para ambos encargados y `department_head`; nunca exponen identificadores de alumnos o documentos. Seguimiento nominal permanece restringido.
- Estadísticas abren en el proceso propio; ambos roles y `department_head` abren SS. Query explícita prevalece. Botones alternan un proceso a la vez, conservan periodo y descartan respuestas anteriores.
- CompanyType obligatorio: public, micro, small, medium, large, social. Conservar dimensiones 911. Clasificar educativos individualmente; bloquear migración final si quedan pendientes.
- GET /api/reports/linkage requiere tramiteType social_service o internship; periodo activo predeterminado; 404 sin periodo. Página 1, tamaño 20, máximo 100. Totales y seis tipos completos; solo empresas se paginan por nombre e ID.
- Agregar en SQL por proceso, periodo, empresa y tipo. Universo con empresa y estado approved, accepted, active o completed_pending_review. Activos: active. Cada trámite cuenta como proyecto. Excluir borrados; conservar empresas desactivadas.
- Revalidar cuenta y roles en cada petición autenticada. Proteger rutas frontend antes de montar vistas; backend conserva autoridad.

Aceptación: matriz por rol y proceso mediante HTTP real, IDOR sin efectos de almacenamiento, preingreso, revocación, seis tipos, migración incompleta, agregados y paginación; navegación directa y cambios rápidos de estadísticas.

## Contratos de interfaz

- Empresas: `CompanyRequest` y `CompanyResponse` agregan `companyType: "public" | "micro" | "small" | "medium" | "large" | "social"`. `GET /api/companies?companyType=...` valida el filtro. Eliminar una empresa con SS/PP vinculado devuelve `409 companies.in_use`, aunque no tenga supervisores ni convenios.
- `StudentEligibility` devuelve `null` en el proceso ajeno para personal; el alumno propietario mantiene ambos resultados. El dashboard operativo devuelve contadores cero para procesos no autorizados y la interfaz oculta esas tarjetas.
- Linkage: `{ tramiteType, period: { id, code, name }, totals: { assignedStudents, activeStudents, projects }, byCompanyType: [{ companyType, assignedStudents, activeStudents, projects }], companies: PagedResponse<{ companyId, companyName, companyType, projects, assignedStudents, activeStudents }> }`. Totales y distribución no dependen de la página. Valores de paginación se ajustan a página mínima 1 y tamaño 1–100.
- Ruta estadística: `/reports/stats?tramiteType=social_service|internship`. La URL selecciona el proceso; el periodo se conserva mientras se alternan los botones. Las métricas carecen de enlaces a expedientes.
- Estado de sesión en memoria compartido, sin JWT ni almacenamiento persistente. Guard espera `/auth/me`; logout y `401` limpian sesión. Fallos de verificación no montan vistas administrativas.
- Catálogo `/companies`: listado, formularios y filtros; supervisores y convenios en detalle, incluyendo estado, PDF y eliminación protegida. `department_head` y ambos encargados gestionan.

Migración y despliegue: [guía incremental](../../database/migrations/README.md).

Seguimiento SS: encargado SS puede consultar candidatos sin trámite únicamente como resumen nominal (matrícula, nombre, carrera, seguimiento). Los otros campos son nulos y el perfil sigue restringido. `department_head` y encargado SS valoran; PP no recibe seguimiento SS.
