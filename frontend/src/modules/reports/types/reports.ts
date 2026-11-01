export interface DashboardMetrics {
  period: {
    id: number
    code: string
    name: string
  }
  socialService: Record<string, number>
  internships: Record<string, number>
  pendingDocumentReviews: number
  agreementsExpiringSoon: number
  openNonConformances: number
}

export interface TrackingItem {
  id: number
  tramiteId: number
  controlNumber: string
  studentFullName: string
  careerCode: string
  careerName: string
  semesterEnrolled: number
  companyName?: string
  programName?: string
  startDate?: string
  endDate?: string
  status: string
  progressPercentage: number
  presentationLetterFolio?: string
  releaseCertificateFolio?: string
  isPnc: boolean
}

export interface TrackingResponse {
  period: { id: number; code: string; name: string }
  tramiteType: string
  totalRecords: number
  completedExpedientsCount: number
  items: TrackingItem[]
}

