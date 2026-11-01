export interface Student {
  id: number
  userId: number
  controlNumber: string
  fullName: string
  curp: string
  careerId: number
  careerName?: string
  careerCode?: string
  currentSemester: number
  percentageApprovedCredits: number
  imssNumber?: string
  imssClinic?: string
  imssRegistrationDate?: string
  isVigente: boolean
  hasSocialService: boolean
  hasInternship: boolean
  createdAt: string
  updatedAt?: string
}

export interface StudentFilterParams {
  page?: number
  pageSize?: number
  search?: string
  careerId?: number
  isVigente?: boolean
  sort?: string
}

export interface PagedResponse<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export interface CreateStudentRequest {
  controlNumber: string
  fullName: string
  curp: string
  careerId: number
  currentSemester: number
  percentageApprovedCredits: number
  imssNumber?: string
  imssClinic?: string
}

export interface UpdateImssRequest {
  imssNumber: string
  imssClinic: string
  imssRegistrationDate?: string
}

export interface StudentImportResult {
  totalRows: number
  importedCount: number
  updatedCount: number
  errorCount: number
  errors: string[]
}

