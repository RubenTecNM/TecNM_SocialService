import { http } from '../../../shared/api/http'
import type { CreateInternshipPayload, InternshipDetail, InternshipListItem } from '../types/internships'

export async function getInternships(params?: any): Promise<{ items: InternshipListItem[]; totalCount: number }> {
  return (await http.get('/internships', { params })).data
}

export async function getMyInternship(): Promise<InternshipDetail> {
  return (await http.get<InternshipDetail>('/internships/my')).data
}

export async function getInternshipById(id: number): Promise<InternshipDetail> {
  return (await http.get<InternshipDetail>(`/internships/${id}`)).data
}

export async function requestInternship(payload: CreateInternshipPayload): Promise<InternshipDetail> {
  return (await http.post<InternshipDetail>('/internships/request', payload)).data
}

export async function releaseInternship(id: number): Promise<InternshipDetail> {
  return (await http.post<InternshipDetail>(`/internships/${id}/release`, {})).data
}

