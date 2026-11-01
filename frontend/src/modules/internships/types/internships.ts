export interface InternshipListItem {
  id: number
  studentId: number
  studentControlNumber: string
  studentFullName: string
  careerCode: string
  periodCode: string
  companyName?: string
  status: string
  documentProgressPercentage: number
  isExpedientComplete: boolean
  presentationLetterFolio?: string
  startDate?: string
  endDate?: string
  pendingReviewsCount: number
  updatedAt: string
}

export interface InternshipDetail {
  id: number
  student: {
    id: number
    controlNumber: string
    fullName: string
    careerName: string
    currentSemester: number
    shift: string
    phoneNumber?: string
    institutionalEmail?: string
    hasValidImss: boolean
  }
  company?: {
    id: number
    name: string
    sector: string
  }
  status: string
  documentProgressPercentage: number
  requiredHours: number
  completedHours: number
  presentationLetterFolio?: string
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

export interface CreateInternshipPayload {
  studentId?: number
  periodId: number
  currentSemester: number
  shift: string
  phoneNumber: string
  institutionalEmail: string
}

