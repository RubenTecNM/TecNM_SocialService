import type { DocumentRequirement } from '../../expedients/types/expedients'
import type { TramiteChecklistItem } from '../../social-service/types/socialService'

export interface InternshipListItem {
  id: number
  studentId: number
  studentControlNumber: string
  studentFullName: string
  careerCode: string
  periodCode: string
  companyName?: string | null
  status: string
  documentProgressPercentage: number
  isExpedientComplete: boolean
  presentationLetterFolio?: string | null
  startDate?: string | null
  endDate?: string | null
  pendingReviewsCount: number
  updatedAt: string
}

export interface InternshipEvaluation {
  id: number
  activityName: string
  evaluationStartDate: string
  evaluationEndDate: string
  criteria: number[]
  numericalAverage: number
  performanceLevel: string
  isCompetencyAchieved: boolean
  evaluatorSupervisorId: number | null
  evaluatorSignatureDate: string
  observations: string | null
}

export interface InternshipDetail {
  evaluationFormatStartDate: string | null
  evaluationFormatEndDate: string | null
  documentWorkflowVersion: number
  documentRequirements: DocumentRequirement[]
  id: number
  student: {
    id: number
    controlNumber: string
    fullName: string
    careerName: string
    currentSemester: number
    shift: string
    phoneNumber?: string | null
    institutionalEmail?: string | null
    hasValidImss: boolean
    hasImssFile: boolean
  }
  period: { id: number; code: string }
  activityType?: { id: number; code: string; name: string; appliesTo: string } | null
  company?: {
    id: number
    name: string
    sector: string
  } | null
  supervisor?: {
    id: number
    fullName: string
    position: string
  } | null
  agreement?: {
    id: number
    agreementNumber: string
    validity: string
  } | null
  semesterEnrolled: number
  startDate?: string | null
  endDate?: string | null
  scheduleDescription?: string | null
  status: string
  documentProgressPercentage: number
  requiredHours: number
  completedHours: number
  creditsAwarded: number
  presentationLetterFolio?: string | null
  presentationLetterDate?: string | null
  acceptanceLetterDate?: string | null
  terminationLetterDate?: string | null
  originalTerminationLetterReceived: boolean
  releaseCertificateFolio?: string | null
  releaseDate?: string | null
  releasedAt?: string | null
  isExpedientComplete: boolean
  schoolServicesActSent: boolean
  schoolServicesActSentAt?: string | null
  schoolServicesActFolio?: string | null
  observations?: string | null
  cancellationReason?: string | null
  evaluation: InternshipEvaluation | null
  checklist: TramiteChecklistItem[]
  allowedActions: string[]
  createdAt: string
  updatedAt: string
}

export interface CreateInternshipPayload {
  studentId?: number | null
  periodId: number
  currentSemester: number
  shift: string
  phoneNumber: string
  institutionalEmail: string
  companyId?: number | null
  supervisorId?: number | null
  agreementId?: number | null
  startDate?: string | null
  endDate?: string | null
  scheduleDescription?: string | null
  requiredHours?: number | null
}

export interface RegisterInternshipAcceptanceRequest {
  companyId: number
  supervisorId: number
  agreementId?: number | null
  activityTypeId?: number | null
  startDate: string
  endDate: string
  scheduleDescription?: string | null
  requiredHours?: number | null
  acceptanceLetterDate: string
}

export interface InternshipEvaluationRequest {
  evaluationStartDate: string
  evaluationEndDate: string
  criteria: number[]
  evaluatorSupervisorId?: number | null
  evaluatorSignatureDate: string
  observations?: string | null
}
