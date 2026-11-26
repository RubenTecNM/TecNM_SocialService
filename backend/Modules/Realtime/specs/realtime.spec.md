# Especificación Backend: Módulo Realtime (`Modules/Realtime`)

Contrato raíz: [`docs/specs/realtime.spec.md`](../../../../docs/specs/realtime.spec.md).

## Responsabilidades del Slice
- `RealtimeDTOs.cs`: records inmutables `RealtimeEventPayload` y `DocumentReviewLockDto`.
- `DocumentReviewPresenceRegistry.cs`: registro concurrente en memoria (`ConcurrentDictionary`) de documentos abiertos en revisión por `ConnectionId`, con limpieza automática al desconectar o concluir dictamen.
- `SsppHub.cs`: Hub autenticado (`[Authorize]`) mapeado en `/api/hubs/sspp`. Suscribe conexiones a grupos por `user:{id}` y `staff:{tramiteType}` según roles verificados e implementa `StartReviewing` / `StopReviewing`.
- `RealtimeNotifier.cs`: interfaz `IRealtimeNotifier` (`NullRealtimeNotifier` por defecto para pruebas aisladas sin Hub y `SignalRRealtimeNotifier` en producción) que resuelve datos resumidos del alumno/usuario y despacha eventos `SsppEvent` de forma segura (`try/catch` con `ILogger`) sin afectar transacciones EF Core ya confirmadas.
