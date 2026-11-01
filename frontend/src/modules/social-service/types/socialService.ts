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

export interface SocialServiceDetail {
  id: number
  student: {
    id: number
    controlNumber: string
    fullName: string
    careerName: string
    hasValidImss: boolean
  }
  status: string
  modality: 'internal' | 'external'
  documentProgressPercentage: number
  accumulatedHours: number
  requiredHours: number
  startDate: string
  endDate: string
  presentationLetterFolio?: string
  presentationLetterDate?: string
  acceptanceLetterDate?: string
  terminationLetterDate?: string
  releaseCertificateFolio?: string
  checklist: Array<{
    documentCode: string
    name: string
    isMandatory: boolean
    isUploaded: boolean
    status: string
    rejectionReason?: string
  }>
  allowedActions: string[]
}

export interface CreateSocialServicePayload {
  studentId?: number
  periodId: number
  programId: number
  companyId: number
  supervisorId: number
  agreementId?: number
  modality: 'internal' | 'external'
  startDate: string
  endDate: string
  targetCommunity?: string
  application: {
    studentAddress: string
    studentPhone: string
    periodType: string
    dependencyName: string
    dependencyHeadName: string
    dependencyHeadPosition: string
    activitiesOutline: string
  }
}

export interface AcceptanceLetterData {
  studentName: string
  controlNumber: string
  careerName: string
  programName: string
  startDate: string
  endDate: string
  hoursPerDay: number
  schedule: string
  responsibleName: string
  responsiblePosition: string
  departmentName: string
}

