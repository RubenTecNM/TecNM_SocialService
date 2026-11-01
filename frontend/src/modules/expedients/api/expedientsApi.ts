import { http } from '../../../shared/api/http'
import type { ExpedientDocument, ReviewDocumentRequest, ReviewQueueItem } from '../types/expedients'

export async function getStudentDocuments(studentId: number): Promise<ExpedientDocument[]> {
  return (await http.get<ExpedientDocument[]>(`/students/${studentId}/documents`)).data
}

export async function uploadStudentDocument(
  studentId: number,
  formData: FormData,
): Promise<ExpedientDocument> {
  return (await http.post<ExpedientDocument>(`/students/${studentId}/documents`, formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })).data
}

export async function getDocumentBlob(documentId: number): Promise<Blob> {
  const response = await http.get(`/documents/${documentId}/file`, { responseType: 'blob' })
  return response.data
}

export async function getReviewQueue(status?: string): Promise<ReviewQueueItem[]> {
  return (await http.get<ReviewQueueItem[]>('/documents/review-queue', {
    params: status ? { status } : undefined,
  })).data
}

export async function reviewDocument(
  documentId: number,
  request: ReviewDocumentRequest,
): Promise<ExpedientDocument> {
  return (await http.put<ExpedientDocument>(`/documents/${documentId}/review`, request)).data
}

