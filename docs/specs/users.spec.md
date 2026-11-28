# Contrato: Usuarios y roles

> **Estado:** Borrador v0.1. Convenciones en [api-conventions.spec.md](./api-conventions.spec.md). Tablas: `users`, `roles`, `user_roles`.

## Acceso

Todo el módulo: rol `department_head`. Excepción: `PUT /api/users/me/password` para cualquier usuario autenticado.

## DTOs

```ts
interface UserListItem {
  id: number;
  username: string;
  email: string;
  fullName: string;        // first_name + first_surname + second_surname
  roles: string[];
  isActive: boolean;
  studentId: number | null; // students.user_id vinculado
}

interface UserDetail extends UserListItem {
  firstName: string;
  firstSurname: string;
  secondSurname: string | null;
  createdAt: string;
  updatedAt: string;
}

interface CreateUserRequest {
  username: string;
  email: string;
  password: string;
  firstName: string;
  firstSurname: string;
  secondSurname: string | null;
  roles: string[];
}

type UpdateUserRequest = Omit<CreateUserRequest, "username" | "password">;

interface ResetPasswordRequest { newPassword: string; }
interface ChangeOwnPasswordRequest { currentPassword: string; newPassword: string; }

interface RoleItem { id: number; name: string; description: string | null; }
```

## Validación

| Campo | Regla |
| --- | --- |
| `username` | Requerido, 3–254, único sin distinguir mayúsculas; alumnos usan correo institucional. Personal conserva su identificador. |
| `email` | Requerido, email, máx. 254, único (`users.duplicated_email`) |
| `password` / `newPassword` | Requerido, 8–128 caracteres. Se guarda con `PasswordHasher<User>` |
| `firstName` | Requerido, 1–100 |
| `firstSurname` | Requerido, 1–70 |
| `secondSurname` | Opcional, máx. 70 |
| `roles` | Mínimo 1; valores de `roles.name` existentes |

## Endpoints

| Método y ruta | Entrada | Éxito | Errores |
| --- | --- | --- | --- |
| `GET /api/users` | `page`, `pageSize`, `search` (username, email, nombre), `role`, `isActive`, `sort` (`username`, `createdAt`) | `200 PagedResponse<UserListItem>` | `403` |
| `GET /api/users/{id}` | — | `200 UserDetail` | `404` |
| `POST /api/users` | `CreateUserRequest` | `201 UserDetail` | `400`, `409` |
| `PUT /api/users/{id}` | `UpdateUserRequest` | `200 UserDetail` | `400`, `404`, `409` |
| `PATCH /api/users/{id}/status` | `{ isActive }` | `200 UserDetail` | `404`, `422 users.cannot_deactivate_self` |
| `PUT /api/users/{id}/password` | `ResetPasswordRequest` | `204` | `400`, `404` |
| `PUT /api/users/me/password` | `ChangeOwnPasswordRequest` | `204` | `400`, `422 users.invalid_current_password` |
| `DELETE /api/users/{id}` | — | `204` | `404`, `422 users.cannot_delete_self` |
| `GET /api/roles` | — | `200 RoleItem[]` | `403` |

## Reglas

- Roles se sincronizan reemplazando el conjunto completo en `user_roles`.
- Cada petición revalida cuenta activa y roles; `session_version` revoca JWT al cambiar contraseña o identidad. Usuarios de alumno usan correo institucional (máximo 254), sin distinción de mayúsculas en unicidad. La migración conserva contraseñas y bloquea vínculos ambiguos o roles incompatibles.
- Un usuario `department_head` no puede desactivarse ni borrarse a sí mismo.
- Borrado lógico de usuario no borra `students`; `students.user_id` queda intacto (FK `SET NULL` solo aplica a borrado físico).
