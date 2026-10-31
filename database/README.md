# Base de Datos (`database/`)

Este directorio centraliza los recursos y especificaciones de base de datos del proyecto **TecNM SSPP**, organizados según su rol dentro del ciclo de vida del desarrollo.

| Subcarpeta | Estado | Propósito | Reglas de Uso |
| :--- | :--- | :--- | :--- |
| [`design/`](./design/) | **Borrador en curso** | Nuevo diseño relacional para PostgreSQL enfocado en los módulos del TecNM SSPP. | Espacio de trabajo activo para documentar tablas, relaciones y scripts de esquema en evolución. |
| [`seed/`](./seed/) | **Desarrollo local** | Datos iniciales y semillas (`01_dev_users.sql`, `02_dev_catalogs.sql`, `03_dev_companies.sql`, `04_dev_students.sql`) para pruebas de desarrollo. | Solo para entornos locales de prueba. **No ejecutar ni cargar en producción.** |
| [`legacy/`](./legacy/) | **Solo referencia** | Scripts y esquemas históricos del sistema anterior SET_MX (SQL Server). | **NO EJECUTAR.** Únicamente fuente de consulta técnica y análisis de entidades preexistentes. |

---

> [!WARNING]
> La carpeta `database/legacy/` contiene scripts y esquemas del sistema legacy bajo SQL Server. No deben ejecutarse contra el motor PostgreSQL del proyecto ni intentar migrarse 1:1 sin rediseño previo.
