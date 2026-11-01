import type { RouteRecordRaw } from 'vue-router'

export const reportsRoutes: RouteRecordRaw[] = [
  {
    path: 'reports/dashboard',
    name: 'reports-dashboard',
    component: () => import('./views/DashboardView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: 'reports/tracking',
    name: 'reports-tracking',
    component: () => import('./views/TrackingSheetView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: 'reports/stats',
    name: 'reports-stats',
    component: () => import('./views/StatisticsView.vue'),
    meta: { requiresAuth: true },
  },
]

