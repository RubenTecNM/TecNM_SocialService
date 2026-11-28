import type { RouteRecordRaw } from 'vue-router'

export const reportsRoutes: RouteRecordRaw[] = [
  {
    path: 'reports/dashboard',
    name: 'reports-dashboard',
    redirect: { name: 'reports-stats' },
  },
  {
    path: 'reports/tracking',
    name: 'reports-tracking',
    component: () => import('./views/TrackingSheetView.vue'),
    meta: { requiresAuth: true, roles: ['department_head', 'social_service_officer', 'internship_coordinator'] },
  },
  {
    path: 'reports/stats',
    name: 'reports-stats',
    component: () => import('./views/StatisticsView.vue'),
    meta: { requiresAuth: true, roles: ['department_head', 'social_service_officer', 'internship_coordinator'] },
  },
]

