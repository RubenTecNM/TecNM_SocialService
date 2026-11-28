import type { PagedResponse } from '../../catalogs/types/careers'

export type AnalyticsTramiteType = 'social_service' | 'internship' | 'both'
export type AnalyticsTimeScope = 'period' | 'school_year' | 'all'
export type AnalyticsStatusGroup = 'in_progress' | 'all_non_cancelled' | 'released' | 'all'

export interface ReportPeriodSummary {
  id: number
  code: string
  name: string
}

export interface DashboardMetrics {
  period: {
    id: number
    code: string
    name?: string
  }
  socialService: Record<string, number>
  internships: Record<string, number>
  pendingDocumentReviews: number
  agreementsExpiringSoon: number
  openNonConformances: number
}

export interface TrackingDocumentsChecklist {
  qualitative?: boolean | null
  selfAssessment?: boolean | null
  activityAssessment?: boolean | null
  cp: boolean
  ca: boolean
  sol?: boolean
  cc?: boolean
  r1?: boolean
  r2?: boolean
  r3?: boolean
  ev1?: boolean
  ev2?: boolean
  ev3?: boolean
  ev?: boolean
  ct: boolean
}

export interface TrackingItem {
  id: number
  tramiteId: number
  controlNumber: string
  studentFullName: string
  careerCode: string
  careerName: string
  semesterEnrolled: number
  gender?: string | null
  shift?: string
  phoneNumber?: string | null
  companyName?: string | null
  companySector?: string | null
  supervisorName?: string | null
  agreementNumber?: string | null
  programName?: string | null
  startDate?: string | null
  endDate?: string | null
  status: string
  progressPercentage: number
  documents?: TrackingDocumentsChecklist
  presentationLetterFolio?: string | null
  releaseCertificateFolio?: string | null
  originalTerminationLetterReceived?: boolean
  finalGradeLevel?: string | null
  isPnc: boolean
  observations?: string | null
  schoolServicesActFolio?: string | null
  beneficiariesCount?: number | null
  hasInclusionActivities?: boolean | null
  targetCommunity?: string | null
  periodCode?: string | null
  schoolYear?: string | null
  activityTypeName?: string | null
}

export interface TrackingResponse {
  items: TrackingItem[]
}

export interface CareerStatisticalBreakdown {
  careerCode: string
  careerName: string
  eligibleWithPresentationLetter: number
  applicantsMale: number
  applicantsFemale: number
  applicantsTotal: number
  releasedMale: number
  releasedFemale: number
  releasedTotal: number
  pncTotal: number
  releaseRatePercentage: number
}

export interface StatisticalTotals {
  eligibleWithPresentationLetter: number
  applicantsMale: number
  applicantsFemale: number
  applicantsTotal: number
  releasedMale: number
  releasedFemale: number
  releasedTotal: number
  pncTotal: number
  releaseRatePercentage: number
}

export interface SchoolYearComparison {
  firstPeriodCode: string
  secondPeriodCode: string
  totalEligible: number
  totalApplicants: number
  totalReleased: number
  totalPnc: number
  schoolYearReleaseRatePercentage: number
}

export interface StatisticalReportResponse {
  period: ReportPeriodSummary
  tramiteType: 'social_service' | 'internship'
  byCareer: CareerStatisticalBreakdown[]
  totals: StatisticalTotals
  schoolYearComparison: SchoolYearComparison | null
}

export interface Report911CareerGenderRow {
  careerCode: string
  sector: string
  male: number
  female: number
  unspecified: number
  total: number
  withDisability: number
}

export interface Report911SectorRow {
  sector: string
  male: number
  female: number
  total: number
  withDisability: number
}

export interface Report911CompanySizeRow {
  companySize: string
  total: number
}

export interface Report911ShiftRow {
  shift: string
  total: number
}

export interface Report911Response {
  period: ReportPeriodSummary
  tramiteType: 'social_service' | 'internship'
  byCareerAndGender: Report911CareerGenderRow[]
  byCompanySector: Report911SectorRow[]
  byCompanySize: Report911CompanySizeRow[]
  withDisability: { male: number; female: number; total: number }
  byShift: Report911ShiftRow[]
  withInclusionActivities: number
  totalBeneficiaries: number
}

export interface AnalyticsFilters {
  tramiteType: AnalyticsTramiteType
  timeScope: AnalyticsTimeScope
  periodId?: number
  comparePeriodId?: number
  schoolYear?: string
  compareSchoolYear?: string
  statusGroup: AnalyticsStatusGroup
  status?: string
  careerId?: number
  semester?: number
  companyId?: number
  companyType?: string
  sector?: string
  activityTypeId?: number
  gender?: 'M' | 'F' | 'unspecified'
}

export interface AnalyticsScopeSummary {
  tramiteType: AnalyticsTramiteType
  timeScope: AnalyticsTimeScope
  period: ReportPeriodSummary | null
  schoolYear: string | null
  comparePeriod: ReportPeriodSummary | null
  compareSchoolYear: string | null
  statusGroup: AnalyticsStatusGroup
  status: string | null
  includedPeriodCodes: string[]
}

export interface AnalyticsComparisonDelta {
  previousLabel: string
  previousUniqueStudents: number
  deltaStudents: number
  deltaPercentage: number | null
  previousReleased: number
  previousReleaseRate: number
}

export interface AnalyticsSummary {
  uniqueStudents: number
  totalTramites: number
  socialServiceStudents: number
  internshipStudents: number
  inProgressCount: number
  activeCount: number
  releasedCount: number
  pncCount: number
  cancelledCount: number
  releaseRatePercentage: number
  distinctCompanies: number
  unassignedCompanyCount: number
  unclassifiedActivityCount: number
  byGender: { male: number; female: number; unspecified: number }
  comparison: AnalyticsComparisonDelta | null
}

export interface CareerAnalyticsRow {
  careerId: number
  careerCode: string
  careerName: string
  isActive: boolean
  total: number
  male: number
  female: number
  unspecified: number
  inProgress: number
  released: number
  pnc: number
  socialServiceCount: number
  internshipCount: number
}

export interface SemesterAnalyticsRow {
  semester: number
  total: number
  male: number
  female: number
  unspecified: number
  socialServiceCount: number
  internshipCount: number
}

export interface ActivityTypeAnalyticsRow {
  activityTypeId: number | null
  code: string
  name: string
  total: number
  male: number
  female: number
  unspecified: number
  socialServiceCount: number
  internshipCount: number
  sharePercentage: number
}

export interface CompanyAnalyticsRow {
  companyId: number | null
  companyName: string
  companyType: string | null
  sector: string | null
  isTecnmInternal: boolean
  total: number
  activeCount: number
  releasedCount: number
  projects: number
  male: number
  female: number
  unspecified: number
  topActivityName: string | null
}

export interface StatusCountRow {
  status: string
  count: number
  socialServiceCount: number
  internshipCount: number
}

export interface TrendPointRow {
  key: string
  label: string
  subLabel: string
  total: number
  socialServiceCount: number
  internshipCount: number
  inProgressCount: number
  releasedCount: number
}

export interface AnalyticsReportResponse {
  scope: AnalyticsScopeSummary
  summary: AnalyticsSummary
  byStatus: StatusCountRow[]
  byCareer: CareerAnalyticsRow[]
  bySemester: SemesterAnalyticsRow[]
  byActivityType: ActivityTypeAnalyticsRow[]
  topCompanies: CompanyAnalyticsRow[]
  trend: TrendPointRow[]
}

export type PagedCompanyAnalytics = PagedResponse<CompanyAnalyticsRow>
