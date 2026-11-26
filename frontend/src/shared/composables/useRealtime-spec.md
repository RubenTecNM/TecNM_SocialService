# Composable `useRealtime`

Contrato compartido: [`docs/specs/realtime.spec.md`](../../../../docs/specs/realtime.spec.md).

## Responsabilidades
- Mantener una única conexión `HubConnection` (`@microsoft/signalr`) hacia `/api/hubs/sspp` con `withCredentials: true` y reconexión automática (`withAutomaticReconnect`).
- Exponer estado reactivo `realtimeConnected` y mapa `activeReviewers` (`Record<number, DocumentReviewLock>`).
- Permitir suscripción desacoplada por vista mediante `onRealtimeEvent(callback)` con limpieza automática en `onBeforeUnmount`.
- Proveer `startReviewPresence(documentId)` y `stopReviewPresence(documentId)` para anunciar en vivo qué documento está siendo dictaminado en `DocumentReviewDialog` / `ExpedientsReviewView`.
- En `MainLayout.vue`, mostrar `UiToast` institucional cuando llega un `SsppEvent` con `silent === false` originado por otro usuario (`actorUserId !== currentUser.id`) e indicador discreto de conexión en vivo.
