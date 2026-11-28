export interface Career {
  id: number
  code: string
  name: string
  totalCredits: number
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface CareerRequest {
  code: string
  name: string
  totalCredits: number
}

export interface UpdateStatusRequest {
  isActive: boolean
}

export interface OptionItem {
  id: number
  label: string
  meta?: Record<string, any>
}

export interface AcademicPeriod {
  id: number
  code: string
  name: string
  schoolYear: string
  startDate: string
  endDate: string
  isActive: boolean
}

export interface SchoolYearPeriodItem {
  id: number
  code: string
  name: string
  startDate: string
  endDate: string
  isActive: boolean
}

export interface SchoolYearOption {
  schoolYear: string
  hasActivePeriod: boolean
  periods: SchoolYearPeriodItem[]
}

export type ActivityAppliesTo = 'social_service' | 'internship' | 'both'

export interface ActivityType {
  id: number
  code: string
  name: string
  appliesTo: ActivityAppliesTo
  description: string | null
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface ActivityTypeRequest {
  code: string
  name: string
  appliesTo: ActivityAppliesTo
  description?: string | null
}

export interface PagedResponse<T> {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface CareerFilterParams {
  page?: number
  pageSize?: number
  search?: string
  isActive?: boolean | null
  sort?: string
}
