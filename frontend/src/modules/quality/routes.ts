import type { RouteRecordRaw } from 'vue-router'

export const qualityRoutes: RouteRecordRaw[] = [
  {
    path: 'quality/pnc',
    name: 'quality-pnc',
    component: () => import('./views/NonConformancesView.vue'),
    meta: { requiresAuth: true },
  },
]

