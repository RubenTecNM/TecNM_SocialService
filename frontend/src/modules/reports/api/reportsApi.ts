import { http } from '../../../shared/api/http'
import type {
  AnalyticsFilters,
  AnalyticsReportResponse,
  DashboardMetrics,
  PagedCompanyAnalytics,
  Report911Response,
  StatisticalReportResponse,
  TrackingItem,
  TrackingResponse,
} from '../types/reports'

export async function getDashboardMetrics(periodId?: number): Promise<DashboardMetrics> {
  return (
    await http.get<DashboardMetrics>('/reports/dashboard', {
      params: periodId ? { periodId } : undefined,
    })
  ).data
}

export async function getAnalyticsReport(
  filters: AnalyticsFilters,
  signal?: AbortSignal,
): Promise<AnalyticsReportResponse> {
  return (
    await http.get<AnalyticsReportResponse>('/reports/analytics', {
      signal,
      params: {
        tramiteType: filters.tramiteType,
        timeScope: filters.timeScope,
        periodId: filters.timeScope === 'period' ? filters.periodId : undefined,
        comparePeriodId: filters.timeScope === 'period' ? filters.comparePeriodId : undefined,
        schoolYear: filters.timeScope === 'school_year' ? filters.schoolYear : undefined,
        compareSchoolYear: filters.timeScope === 'school_year' ? filters.compareSchoolYear : undefined,
        statusGroup: filters.statusGroup,
        status: filters.status || undefined,
        careerId: filters.careerId,
        semester: filters.semester,
        companyId: filters.companyId !== undefined ? filters.companyId : undefined,
        companyType: filters.companyType || undefined,
        sector: filters.sector || undefined,
        activityTypeId: filters.activityTypeId !== undefined ? filters.activityTypeId : undefined,
        gender: filters.gender || undefined,
      },
    })
  ).data
}

export async function getAnalyticsCompanies(
  filters: AnalyticsFilters,
  options: { search?: string; sort?: string; page?: number; pageSize?: number } = {},
  signal?: AbortSignal,
): Promise<PagedCompanyAnalytics> {
  return (
    await http.get<PagedCompanyAnalytics>('/reports/analytics/companies', {
      signal,
      params: {
        tramiteType: filters.tramiteType,
        timeScope: filters.timeScope,
        periodId: filters.timeScope === 'period' ? filters.periodId : undefined,
        schoolYear: filters.timeScope === 'school_year' ? filters.schoolYear : undefined,
        statusGroup: filters.statusGroup,
        status: filters.status || undefined,
        careerId: filters.careerId,
        semester: filters.semester,
        companyId: filters.companyId !== undefined ? filters.companyId : undefined,
        companyType: filters.companyType || undefined,
        sector: filters.sector || undefined,
        activityTypeId: filters.activityTypeId !== undefined ? filters.activityTypeId : undefined,
        gender: filters.gender || undefined,
        search: options.search?.trim() || undefined,
        sort: options.sort || 'students_desc',
        page: options.page ?? 1,
        pageSize: options.pageSize ?? 10,
      },
    })
  ).data
}

export interface TrackingFilterParams {
  periodId?: number
  schoolYear?: string
  tramiteType: 'social_service' | 'internship'
  status?: string
  careerId?: number
  semester?: number
  gender?: string
  companyId?: number
  activityTypeId?: number
  search?: string
}

export async function getTrackingData(
  paramsOrPeriodId?: number | TrackingFilterParams,
  tramiteType: 'social_service' | 'internship' = 'social_service',
  careerId?: number,
  search?: string,
): Promise<TrackingResponse> {
  const params =
    typeof paramsOrPeriodId === 'object' && paramsOrPeriodId !== null
      ? {
          periodId: paramsOrPeriodId.schoolYear ? undefined : paramsOrPeriodId.periodId,
          schoolYear: paramsOrPeriodId.schoolYear || undefined,
          tramiteType: paramsOrPeriodId.tramiteType,
          status: paramsOrPeriodId.status || undefined,
          careerId: paramsOrPeriodId.careerId || undefined,
          semester: paramsOrPeriodId.semester || undefined,
          gender: paramsOrPeriodId.gender || undefined,
          companyId: paramsOrPeriodId.companyId !== undefined ? paramsOrPeriodId.companyId : undefined,
          activityTypeId:
            paramsOrPeriodId.activityTypeId !== undefined ? paramsOrPeriodId.activityTypeId : undefined,
          search: paramsOrPeriodId.search || undefined,
        }
      : {
          periodId: paramsOrPeriodId !== undefined && paramsOrPeriodId !== null ? paramsOrPeriodId : undefined,
          tramiteType,
          careerId: careerId || undefined,
          search: search || undefined,
        }

  const res = await http.get<TrackingItem[] | TrackingResponse>('/reports/tracking', { params })
  const items = Array.isArray(res.data) ? res.data : (res.data.items ?? [])
  return { items }
}

export async function getSemesterStatistics(
  periodId?: number,
  tramiteType: 'social_service' | 'internship' = 'social_service',
  comparePeriodId?: number,
  signal?: AbortSignal,
  schoolYear?: string,
  compareSchoolYear?: string,
): Promise<StatisticalReportResponse> {
  return (
    await http.get<StatisticalReportResponse>('/reports/statistics', {
      signal,
      params: {
        periodId: schoolYear ? undefined : periodId || undefined,
        tramiteType,
        comparePeriodId: schoolYear ? undefined : comparePeriodId || undefined,
        schoolYear: schoolYear || undefined,
        compareSchoolYear: compareSchoolYear || undefined,
      },
    })
  ).data
}

export async function get911Report(
  periodId?: number,
  tramiteType: 'social_service' | 'internship' = 'social_service',
  comparePeriodId?: number,
  signal?: AbortSignal,
  schoolYear?: string,
): Promise<Report911Response> {
  return (
    await http.get<Report911Response>('/reports/911', {
      signal,
      params: {
        periodId: schoolYear ? undefined : periodId || undefined,
        tramiteType,
        comparePeriodId: schoolYear ? undefined : comparePeriodId || undefined,
        schoolYear: schoolYear || undefined,
      },
    })
  ).data
}
