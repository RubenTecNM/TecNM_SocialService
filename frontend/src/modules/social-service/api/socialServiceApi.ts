import { http } from '../../../shared/api/http'
import type {
  AcceptanceLetterData,
  CreateSocialServicePayload,
  SocialServiceDetail,
  SocialServiceListItem,
} from '../types/socialService'

export async function getSocialServices(params?: any): Promise<{ items: SocialServiceListItem[]; totalCount: number }> {
  return (await http.get('/social-services', { params })).data
}

export async function getMySocialService(): Promise<SocialServiceDetail> {
  return (await http.get<SocialServiceDetail>('/social-services/my')).data
}

export async function getSocialServiceById(id: number): Promise<SocialServiceDetail> {
  return (await http.get<SocialServiceDetail>(`/social-services/${id}`)).data
}

export async function requestSocialService(payload: CreateSocialServicePayload): Promise<SocialServiceDetail> {
  return (await http.post<SocialServiceDetail>('/social-services/request', payload)).data
}

export async function getAcceptanceLetterPrintData(id: number): Promise<AcceptanceLetterData> {
  return (await http.get<AcceptanceLetterData>(`/social-services/${id}/print-data/acceptance_letter`)).data
}

export async function submitBimonthlyReport(
  id: number,
  reportNumber: number,
  data: { hoursReported: number; activitiesSummary: string },
): Promise<any> {
  return (await http.put(`/social-services/${id}/reports/${reportNumber}`, data)).data
}

export async function releaseSocialService(
  id: number,
  data: { finalScore: number; observations?: string },
): Promise<SocialServiceDetail> {
  return (await http.post<SocialServiceDetail>(`/social-services/${id}/release`, data)).data
}

