import { http } from '../../../shared/api/http'
import type {
  AcademicPeriod,
  ActivityType,
  ActivityTypeRequest,
  Career,
  CareerFilterParams,
  CareerRequest,
  OptionItem,
  PagedResponse,
  SchoolYearOption,
  UpdateStatusRequest,
} from '../types/careers'

export async function getCareers(params?: CareerFilterParams): Promise<PagedResponse<Career>> {
  const queryParams: Record<string, string | number | boolean> = {}

  if (params?.page) queryParams.page = params.page
  if (params?.pageSize) queryParams.pageSize = params.pageSize
  if (params?.search) queryParams.search = params.search
  if (params?.isActive !== undefined && params?.isActive !== null) queryParams.isActive = params.isActive
  if (params?.sort) queryParams.sort = params.sort

  const response = await http.get<PagedResponse<Career>>('/careers', { params: queryParams })
  return response.data
}

export async function getCareerOptions(): Promise<OptionItem[]> {
  return (await http.get<OptionItem[]>('/careers/options')).data
}

export async function getPeriodOptions(): Promise<OptionItem[]> {
  return (await http.get<OptionItem[]>('/academic-periods/options')).data
}

export async function getSchoolYearOptions(): Promise<SchoolYearOption[]> {
  return (await http.get<SchoolYearOption[]>('/academic-periods/school-years')).data
}

export async function getActivityTypeOptions(
  tramiteType?: 'social_service' | 'internship' | 'both',
): Promise<OptionItem[]> {
  const params = tramiteType && tramiteType !== 'both' ? { tramiteType } : undefined
  return (await http.get<OptionItem[]>('/activity-types/options', { params })).data
}

export async function getActivityTypes(params?: {
  page?: number
  pageSize?: number
  search?: string
  appliesTo?: string
  isActive?: boolean | null
}): Promise<PagedResponse<ActivityType>> {
  return (await http.get<PagedResponse<ActivityType>>('/activity-types', { params })).data
}

export async function createActivityType(request: ActivityTypeRequest): Promise<ActivityType> {
  return (await http.post<ActivityType>('/activity-types', request)).data
}

export async function updateActivityType(id: number, request: ActivityTypeRequest): Promise<ActivityType> {
  return (await http.put<ActivityType>(`/activity-types/${id}`, request)).data
}

export async function updateActivityTypeStatus(id: number, isActive: boolean): Promise<ActivityType> {
  return (await http.patch<ActivityType>(`/activity-types/${id}/status`, { isActive })).data
}

export async function deleteActivityType(id: number): Promise<void> {
  await http.delete(`/activity-types/${id}`)
}

export async function getCurrentPeriod(): Promise<AcademicPeriod> {
  return (await http.get<AcademicPeriod>('/academic-periods/current')).data
}

export async function getProgramOptions(): Promise<OptionItem[]> {
  return (await http.get<OptionItem[]>('/social-service-programs/options')).data
}

export async function getCompanyOptions(isTecnmInternal?: boolean): Promise<OptionItem[]> {
  const params = isTecnmInternal !== undefined ? { isTecnmInternal } : undefined
  return (await http.get<OptionItem[]>('/companies/options', { params })).data
}

export async function getSupervisorOptions(companyId: number): Promise<OptionItem[]> {
  return (await http.get<OptionItem[]>(`/companies/${companyId}/supervisors/options`)).data
}

export async function getAgreementOptions(companyId?: number): Promise<OptionItem[]> {
  const params = companyId ? { companyId } : undefined
  return (await http.get<OptionItem[]>('/agreements/options', { params })).data
}

export async function getCareerById(id: number): Promise<Career> {
  const response = await http.get<Career>(`/careers/${id}`)
  return response.data
}

export async function createCareer(request: CareerRequest): Promise<Career> {
  const response = await http.post<Career>('/careers', request)
  return response.data
}

export async function updateCareer(id: number, request: CareerRequest): Promise<Career> {
  const response = await http.put<Career>(`/careers/${id}`, request)
  return response.data
}

export async function updateCareerStatus(id: number, isActive: boolean): Promise<Career> {
  const payload: UpdateStatusRequest = { isActive }
  const response = await http.patch<Career>(`/careers/${id}/status`, payload)
  return response.data
}

export async function deleteCareer(id: number): Promise<void> {
  await http.delete(`/careers/${id}`)
}
