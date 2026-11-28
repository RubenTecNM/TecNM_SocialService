import type { DocumentRequirement } from '../../expedients/types/expedients'
export interface SocialServiceListItem {
  id: number
  studentId: number
  studentControlNumber: string
  studentFullName: string
  careerCode: string
  periodCode: string
  programName: string
  companyName: string
  modality: 'internal' | 'external'
  status: string
  documentProgressPercentage: number
  accumulatedHours: number
  requiredHours: number
  startDate: string
  endDate: string
  pendingReviewsCount: number
  updatedAt: string
}

export interface TramiteChecklistItem {
  key: string
  label: string
  done: boolean
  detail?: string | null
}

export interface SocialServiceApplication {
  studentAddress: string
  studentPhone: string
  periodType: string
  currentSemester: number
  dependencyName: string
  dependencyHeadName: string
  dependencyHeadPosition: string
  activitiesOutline: string
  isAcceptedByOffice: boolean | null
  rejectionReason: string | null
  officeObservations: string | null
  reviewedByName: string | null
  reviewedAt: string | null
}

export interface SocialServiceCommitment {
  studentControlNumber: string
  studentAddress: string
  studentPhone: string
  dependencyAddress: string
  commitmentDate: string
  city: string
  isSignedByStudent: boolean
  signedAt: string
}

export interface BiMonthlyReport {
  id: number
  reportNumber: number
  periodStartDate: string
  periodEndDate: string
  activitiesSummary: string
  reportedHours: number
  accumulatedHours: number
  evaluatorSupervisorId: number | null
  status: 'submitted' | 'approved' | 'rejected'
  officeObservations: string | null
  approvedByName: string | null
  approvedAt: string | null
}

export interface SocialServiceEvaluation {
  id: number
  evaluatorType: 'external_supervisor' | 'student_self'
  bimonthlyPeriod: string
  criteria: string[]
  evaluatorSupervisorId: number | null
  observations: string | null
}

export interface ActivityAssessment {
  id: number
  questions: string[]
  observations: string | null
  createdAt: string
}

export interface SocialServiceDetail {
  documentWorkflowVersion: number
  documentRequirements: DocumentRequirement[]
  id: number
  student: {
    id: number
    controlNumber: string
    fullName: string
    careerName: string
    hasValidImss: boolean
    hasImssFile: boolean
  }
  period: { id: number; code: string }
  program: { id: number; code: string; name: string; category: string }
  activityType?: { id: number; code: string; name: string; appliesTo: string } | null
  company: { id: number; name: string; isTecnmInternal: boolean }
  supervisor: { id: number; fullName: string; position: string }
  agreement: { id: number; agreementNumber: string; validity: string } | null
  semesterEnrolled: number
  status: string
  modality: 'internal' | 'external'
  documentProgressPercentage: number
  accumulatedHours: number
  requiredHours: number
  startDate: string
  endDate: string
  presentationLetterFolio?: string | null
  presentationLetterDate?: string | null
  acceptanceLetterDate?: string | null
  terminationLetterDate?: string | null
  originalTerminationLetterReceived: boolean
  releaseCertificateFolio?: string | null
  releasedAt?: string | null
  finalGradeLevel?: string | null
  finalScore?: number | null
  schoolServicesActSent: boolean
  schoolServicesActSentAt?: string | null
  schoolServicesActFolio?: string | null
  beneficiariesCount: number
  hasInclusionActivities: boolean
  targetCommunity?: string | null
  observations?: string | null
  cancellationReason?: string | null
  application: SocialServiceApplication
  commitment: SocialServiceCommitment | null
  reports: BiMonthlyReport[]
  evaluations: SocialServiceEvaluation[]
  activityAssessment: ActivityAssessment | null
  checklist: TramiteChecklistItem[]
  allowedActions: string[]
  createdAt: string
  updatedAt: string
}

export interface CreateSocialServicePayload {
  studentId?: number | null
  periodId: number
  programId: number
  activityTypeId?: number | null
  companyId: number
  supervisorId: number
  agreementId?: number | null
  modality: 'internal' | 'external'
  startDate: string
  endDate: string
  targetCommunity?: string | null
  application: {
    studentAddress: string
    studentPhone: string
    periodType: 'august_january' | 'february_july'
    dependencyName: string
    dependencyHeadName: string
    dependencyHeadPosition: string
    activitiesOutline: string
  }
}

export interface CommitmentRequest {
  studentAddress: string
  studentPhone: string
  dependencyAddress: string
  commitmentDate: string
  city: string
}

export interface ReportRequest {
  periodStartDate: string
  periodEndDate: string
  activitiesSummary: string
  reportedHours: number
  evaluatorSupervisorId?: number | null
}

export interface EvaluationRequest {
  criteria: string[]
  evaluatorSupervisorId?: number | null
  observations?: string | null
}

export interface ActivityAssessmentRequest {
  questions: string[]
  observations?: string | null
}

export type AcceptanceLetterData = Record<string, any>
