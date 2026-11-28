# Especificación Canónica: Notificaciones y Presencia en Tiempo Real (SignalR)

## 1. Propósito

Proveer actualización reactiva en tiempo real entre estudiantes, personal de Vinculación y jefatura mediante **ASP.NET Core SignalR** sobre WebSockets (con respaldo automático SSE / Long Polling), respetando estrictamente el aislamiento de acceso por proceso ([`process-access.spec.md`](./process-access.spec.md)) y la autenticación institucional por cookie `HttpOnly` ([`auth.spec.md`](./auth.spec.md)).

---

## 2. Endpoint y Seguridad del Hub

- **Ruta del Hub:** `/api/hubs/sspp`
- **Autenticación:** `[Authorize]` obligatorio mediante el esquema JWT existente leyendo la cookie `HttpOnly` `auth_token` (mismo origen vía proxy Nginx / Vite, sin exponer tokens en query string).
- **Suscripción automática a grupos en `OnConnectedAsync`:**
  - `user:{userId}`: todo usuario autenticado recibe únicamente eventos dirigidos a su propia cuenta.
  - `staff:social_service`: roles `department_head` y `social_service_officer`.
  - `staff:internship`: roles `department_head` y `internship_coordinator`.
  - `staff:general`: roles `department_head`, `social_service_officer` e `internship_coordinator`.
- Ningún cliente puede elegir arbitrariamente a qué grupo unirse; el servidor calcula los grupos exclusivamente a partir de los claims verificados de la sesión.

---

## 3. Contratos de Mensajes (Servidor → Cliente)

### 3.1 Evento `SsppEvent`
Emitido únicamente **después** de confirmar la transacción en PostgreSQL (`SaveChangesAsync` / `CommitAsync`). Un fallo de transporte en SignalR nunca revierte ni bloquea una operación de negocio ya confirmada.

```json
{
  "eventType": "document.uploaded | document.reviewed | document.deleted | tramite.updated | quality.pnc_updated",
  "tramiteType": "social_service | internship | general",
  "tramiteId": 12,
  "studentId": 5,
  "documentId": 44,
  "documentCode": "R1",
  "status": "pending",
  "title": "Nuevo documento en cola",
  "message": "I21050890 · Mariana Villarreal Soto subió R1.",
  "tone": "info | success | warning | danger",
  "silent": false,
  "actorUserId": 2,
  "occurredAt": "2026-11-25T18:00:00Z"
}
```

#### Reglas de enrutamiento por evento:
1. **`document.uploaded`:** enviado al grupo `staff:{tramiteType}` cuando un estudiante o coordinador carga un PDF pendiente (`IMSS`, `SOL`, `CA`, `CC`, `R1..R3`, `EV*`, `CT`).
2. **`document.reviewed`:** enviado a `user:{studentUserId}` (notificando aprobación o rechazo con observación) y a `staff:{tramiteType}` para refrescar bandejas, KPIs y matrices; limpia además cualquier bloqueo visual de presencia sobre ese `documentId`.
3. **`document.deleted`:** enviado a `staff:{tramiteType}` (`silent: true`) para retirar de inmediato documentos pendientes eliminados por el alumno.
4. **`tramite.updated`:** enviado al abrir solicitud (`CreateAsync`) hacia `staff:{tramiteType}`, y en cambios administrativos de etapa/folio (`approved`, `accepted`, `active`, `completed_pending_review`, `released`, `cancelled`, `non_compliant`, envío de acta) tanto a `user:{studentUserId}` como a `staff:{tramiteType}`.
5. **`quality.pnc_updated`:** enviado a `staff:{tramiteType}` al registrar, actualizar o cambiar estatus de un Producto No Conforme (PNC).

---

### 3.2 Evento `ReviewPresenceUpdated` y Métodos del Hub (Cliente → Servidor)
Previene colisiones operativas cuando dos integrantes de Vinculación revisan simultáneamente la bandeja documental:

- **Métodos invocables por roles revisores (`department_head`, `social_service_officer`, `internship_coordinator`):**
  - `StartReviewing(long documentId)`
  - `StopReviewing(long documentId)`
- El servidor valida que el revisor tenga permiso sobre el `tramiteType` del documento antes de registrar su presencia en memoria y libera automáticamente cualquier documento asociado a `ConnectionId` en `OnDisconnectedAsync` o al dictaminar el documento.
- **Payload `ReviewPresenceUpdated`:** lista de bloqueos informativos activos `{ documentId, tramiteType, reviewerUserId, reviewerName, startedAt }[]` emitida al grupo `staff:{tramiteType}` correspondiente.
