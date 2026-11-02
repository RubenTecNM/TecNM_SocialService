import type { RouteLocationRaw } from 'vue-router'

export type NavIconName =
  | 'dashboard'
  | 'social'
  | 'internship'
  | 'review'
  | 'students'
  | 'reports'
  | 'tracking'
  | 'stats'
  | 'pnc'
  | 'careers'
  | 'profile'

export interface NavigationLink {
  label: string
  to: RouteLocationRaw
  icon?: NavIconName
  badge?: string | number
  badgeTone?: 'emerald' | 'navy' | 'gold' | 'slate'
}

export interface NavigationGroup {
  label: string
  open?: boolean
  children: NavigationLink[]
}

export type NavigationItem = NavigationLink | NavigationGroup

export interface BrandOptions {
  title?: string
  subtitle?: string
  showTitle?: boolean
  showLogo?: boolean
  logo?: string
}
