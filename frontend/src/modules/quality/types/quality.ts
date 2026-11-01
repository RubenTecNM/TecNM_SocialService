export interface NonConformance {
  id: number
  incidentCode: string
  tramiteType: 'social_service' | 'internship'
  tramiteId: number
  studentId: number
  studentControlNumber: string
  studentFullName: string
  periodId: number
  periodCode: string
  category: string
  description: string
  status: 'open' | 'in_progress' | 'closed'
  actionPlan?: string
  resolvedAt?: string
  resolvedByName?: string
  createdAt: string
  updatedAt: string
}

export interface CreateNonConformanceRequest {
  tramiteType: 'social_service' | 'internship'
  tramiteId: number
  category: string
  description: string
  actionPlan?: string
}

export interface CloseNonConformanceRequest {
  resolutionNotes: string
}

