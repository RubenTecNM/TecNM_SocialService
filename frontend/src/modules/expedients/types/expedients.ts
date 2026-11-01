export type ExpedientDocumentStatus = 'pending' | 'approved' | 'rejected'

export interface ExpedientDocument {
  id: number
  studentId: number
  tramiteType: 'social_service' | 'internship'
  tramiteId?: number
  documentCode: string
  originalFileName: string
  fileMimeType: string
  fileSizeBytes: number
  status: ExpedientDocumentStatus
  rejectionReason?: string
  reviewedByName?: string
  reviewedAt?: string
  createdAt: string
}

export interface ReviewQueueItem extends ExpedientDocument {
  studentControlNumber: string
  studentFullName: string
  careerCode: string
  careerName: string
}

export interface ReviewDocumentRequest {
  status: 'approved' | 'rejected'
  rejectionReason?: string
}

