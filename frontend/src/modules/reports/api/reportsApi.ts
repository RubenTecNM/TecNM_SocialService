import { http } from '../../../shared/api/http'
import type { DashboardMetrics, TrackingResponse } from '../types/reports'

export async function getDashboardMetrics(periodId?: number): Promise<DashboardMetrics> {
  return (await http.get<DashboardMetrics>('/reports/dashboard', {
    params: periodId ? { periodId } : undefined,
  })).data
}

export async function getTrackingData(
  periodId: number,
  tramiteType: 'social_service' | 'internship',
  careerId?: number,
): Promise<TrackingResponse> {
  return (await http.get<TrackingResponse>('/reports/tracking', {
    params: { periodId, tramiteType, careerId },
  })).data
}

export async function getSemesterStatistics(periodId: number): Promise<any> {
  return (await http.get('/reports/statistics', { params: { periodId } })).data
}

export async function get911Report(periodId: number): Promise<any> {
  return (await http.get('/reports/911', { params: { periodId } })).data
}

