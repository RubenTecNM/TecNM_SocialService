import { http } from '../../../shared/api/http'
import type { CloseNonConformanceRequest, CreateNonConformanceRequest, NonConformance } from '../types/quality'

export async function getNonConformances(params?: any): Promise<NonConformance[]> {
  return (await http.get<NonConformance[]>('/non-conformances', { params })).data
}

export async function getNonConformanceById(id: number): Promise<NonConformance> {
  return (await http.get<NonConformance>(`/non-conformances/${id}`)).data
}

export async function createNonConformance(request: CreateNonConformanceRequest): Promise<NonConformance> {
  return (await http.post<NonConformance>('/non-conformances', request)).data
}

export async function closeNonConformance(id: number, request: CloseNonConformanceRequest): Promise<NonConformance> {
  return (await http.put<NonConformance>(`/non-conformances/${id}/close`, request)).data
}

