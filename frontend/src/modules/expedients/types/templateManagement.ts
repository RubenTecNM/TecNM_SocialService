export interface TextRow { x: number; width: number; baseline: number }

export interface Placement {
  x: number
  y: number
  width: number
  height: number
  page: number
  alignment: 'left' | 'center' | 'justify'
  fontSize: number | null
  minFontSize: number | null
  rows: TextRow[] | null
}

export interface TemplateSlot extends Placement {
  key: string
  matchValue?: string
  format?: string
  followingKey?: string
}

export interface TemplateSummary {
  key: string
  area: 'social_service' | 'internship'
  mode: 'prefill' | 'static'
  activeVersionId: number | null
  draftId: number | null
  revision: number
}

export interface TemplateVersion {
  id: number
  revision: number
  publishedAt: string | null
  logos: LogoPlacement[]
  canAdoptAuthorityBase: boolean
  layout: {
    version: string
    font: string
    fontSize: number
    minFontSize: number
    pageSizes: { width: number; height: number }[]
    fields: (Placement | null)[]
    logoZones?: LogoZone[] | null
    authorityFields?: { role: string; part: string }[] | null
  }
  slots: TemplateSlot[]
}

export interface LogoZone { name: string; page: number; x: number; y: number; width: number; height: number }
export interface LogoPlacement { slot: number; logoId: number; x: number; y: number; width: number; height: number }

export interface VersionSummary {
  id: number
  createdAt: string
  publishedAt: string | null
  sha256: string
}

export interface TemplateActivation {
  previousVersionId: number | null
  versionId: number
  action: 'import' | 'publish' | 'restore'
  actorId: number | null
  reason: string
  createdAt: string
}
