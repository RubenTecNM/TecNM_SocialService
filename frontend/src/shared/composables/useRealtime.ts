import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { getCurrentInstance, onBeforeUnmount, ref } from 'vue'

export type RealtimeEventType =
  | 'document.uploaded'
  | 'document.reviewed'
  | 'document.deleted'
  | 'tramite.updated'
  | 'quality.pnc_updated'

export type RealtimeTramiteType = 'social_service' | 'internship' | 'general'
export type RealtimeTone = 'info' | 'success' | 'warning'

export interface RealtimeEventPayload {
  eventType: RealtimeEventType
  tramiteType: RealtimeTramiteType
  tramiteId: number | null
  studentId: number
  documentId: number | null
  documentCode: string | null
  status: string | null
  title: string
  message: string
  tone: RealtimeTone
  silent: boolean
  actorUserId: number | null
  occurredAt: string
}

export interface DocumentReviewLock {
  documentId: number
  tramiteType: RealtimeTramiteType
  reviewerUserId: number
  reviewerName: string
  startedAt: string
}

type RealtimeListener = (event: RealtimeEventPayload) => void

export const realtimeConnected = ref(false)
export const activeReviewers = ref<Record<number, DocumentReviewLock>>({})

const listeners = new Set<RealtimeListener>()
const locksByType: Record<string, DocumentReviewLock[]> = {
  social_service: [],
  internship: [],
  general: [],
}

let connection: HubConnection | null = null
let startPromise: Promise<void> | null = null
let activeLocalReviewDocId: number | null = null

function rebuildActiveReviewers(): void {
  const next: Record<number, DocumentReviewLock> = {}
  for (const group of Object.values(locksByType)) {
    for (const lock of group) {
      next[lock.documentId] = lock
    }
  }
  activeReviewers.value = next
}

function resetPresenceState(): void {
  locksByType.social_service = []
  locksByType.internship = []
  locksByType.general = []
  activeReviewers.value = {}
}

function ensureConnection(): HubConnection {
  if (connection) return connection

  const hub = new HubConnectionBuilder()
    .withUrl('/api/hubs/sspp', { withCredentials: true })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(LogLevel.Warning)
    .build()

  hub.on('SsppEvent', (payload: RealtimeEventPayload) => {
    for (const listener of listeners) {
      try {
        listener(payload)
      } catch {
        // Prevent individual view listener errors from breaking event fan-out.
      }
    }
  })

  hub.on('ReviewPresenceSnapshot', (snapshot: DocumentReviewLock[]) => {
    locksByType.social_service = []
    locksByType.internship = []
    locksByType.general = []
    for (const lock of snapshot || []) {
      const bucket = locksByType[lock.tramiteType] ?? (locksByType[lock.tramiteType] = [])
      bucket.push(lock)
    }
    rebuildActiveReviewers()
  })

  hub.on('ReviewPresenceUpdated', (tramiteType: string, locks: DocumentReviewLock[]) => {
    locksByType[tramiteType] = Array.isArray(locks) ? locks : []
    rebuildActiveReviewers()
  })

  hub.onreconnecting(() => {
    realtimeConnected.value = false
  })

  hub.onreconnected(() => {
    realtimeConnected.value = true
    if (activeLocalReviewDocId !== null) {
      void hub.invoke('StartReviewing', activeLocalReviewDocId).catch(() => {})
    }
  })

  hub.onclose(() => {
    realtimeConnected.value = false
    resetPresenceState()
  })

  connection = hub
  return hub
}

export async function startRealtime(): Promise<void> {
  if (typeof window === 'undefined') return
  const hub = ensureConnection()

  if (hub.state === HubConnectionState.Connected) {
    realtimeConnected.value = true
    return
  }

  if (startPromise) return startPromise

  if (hub.state === HubConnectionState.Disconnected) {
    startPromise = hub
      .start()
      .then(() => {
        realtimeConnected.value = true
        if (activeLocalReviewDocId !== null) {
          void hub.invoke('StartReviewing', activeLocalReviewDocId).catch(() => {})
        }
      })
      .catch(() => {
        realtimeConnected.value = false
      })
      .finally(() => {
        startPromise = null
      })

    return startPromise
  }
}

export async function stopRealtime(): Promise<void> {
  activeLocalReviewDocId = null
  resetPresenceState()
  realtimeConnected.value = false

  if (!connection) return
  const hub = connection
  if (startPromise) {
    await startPromise.catch(() => {})
  }
  if (hub.state !== HubConnectionState.Disconnected) {
    await hub.stop().catch(() => {})
  }
}

export async function startReviewPresence(documentId: number): Promise<void> {
  activeLocalReviewDocId = documentId
  if (connection?.state === HubConnectionState.Connected) {
    await connection.invoke('StartReviewing', documentId).catch(() => {})
  }
}

export async function stopReviewPresence(documentId: number): Promise<void> {
  if (activeLocalReviewDocId === documentId) {
    activeLocalReviewDocId = null
  }
  if (connection?.state === HubConnectionState.Connected) {
    await connection.invoke('StopReviewing', documentId).catch(() => {})
  }
}

export function onRealtimeEvent(listener: RealtimeListener): () => void {
  listeners.add(listener)
  const unsubscribe = () => {
    listeners.delete(listener)
  }
  if (getCurrentInstance()) {
    onBeforeUnmount(unsubscribe)
  }
  return unsubscribe
}

export function useRealtimeRefresh(
  predicate: (event: RealtimeEventPayload) => boolean,
  refresh: () => void | Promise<unknown>,
  debounceMs = 260,
): void {
  let timer: ReturnType<typeof setTimeout> | undefined

  onRealtimeEvent((event) => {
    if (!predicate(event)) return
    clearTimeout(timer)
    timer = setTimeout(() => {
      void refresh()
    }, debounceMs)
  })

  if (getCurrentInstance()) {
    onBeforeUnmount(() => {
      clearTimeout(timer)
    })
  }
}
